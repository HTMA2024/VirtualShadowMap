using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdaptiveRendering
{
    public sealed class VirtualShadowMapBaker : IDisposable
    {
        /// <summary>
        /// ��Դ��������
        /// </summary>
        private GameObject m_CameraGO;

        /// <summary>
        /// ��Դ��������
        /// </summary>
        private Camera m_Camera;

        /// <summary>
        /// ��Ӱ��Ⱦ����任����
        /// </summary>
        private Transform m_CameraTransform;

        /// <summary>
        /// �����׶�޳�
        /// </summary>
        private Plane[] m_Planes = new Plane[6];

        /// <summary>
        /// ��Ⱦ�ľ�̬����
        /// </summary>
        private List<Renderer> m_Renderers;

        /// <summary>
        /// ͶӰ��ɫ��
        /// </summary>
        private Material m_Material;

        /// <summary>
        /// ͶӰ��ɫ��
        /// </summary>
        private DepthRangePass m_MinMaxDepthPass;

        /// <summary>
        /// �����Χ��
        /// </summary>
        private Bounds m_WorldBounds;

        /// <summary>
        /// �決��Ҫ������
        /// </summary>
        private VirtualShadowMaps m_VirtualShadowMaps;

        /// <summary>
        /// ��Ⱦ����ShadowMap��Ҫ������
        /// </summary>
		private CommandBuffer m_CmdBuffer = new CommandBuffer();

        /// <summary>
        /// ��Դ����
        /// </summary>
        public Vector3 lightDirection { get; }

        /// <summary>
        /// ��Դλ��
        /// </summary>
        public Vector3 lightPosition { get; }

        /// <summary>
        /// ��Դ����
        /// </summary>
        public Quaternion lightRotation { get; }

        /// <summary>
        /// ��Ⱦ����ShadowMap��Ҫ�ı任����
        /// </summary>
        public Matrix4x4 localToWorldMatrix { get; }

        /// <summary>
        /// ��Ⱦ����ShadowMap��Ҫ�ı任����
        /// </summary>
        public Matrix4x4 worldToLocalMatrix { get; }

        /// <summary>
        /// ��Ⱦ����ShadowMap��Ҫ��ͶӰ����
        /// </summary>
        public Matrix4x4 lightProjecionMatrix { get; internal set; }

        public VirtualShadowMapBaker(VirtualShadowMaps virtualShadowMaps)
        {
            m_CameraGO = new GameObject("", typeof(Camera));
            m_CameraGO.name = "VirtualShadowCamera" + m_CameraGO.GetInstanceID().ToString();
            m_CameraGO.hideFlags = HideFlags.HideAndDontSave;

            m_Camera = m_CameraGO.GetComponent<Camera>();
            m_Camera.enabled = false;
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = Color.black;
            m_Camera.orthographic = true;
            m_Camera.renderingPath = RenderingPath.Forward;
            m_Camera.targetTexture = null;
            m_Camera.allowHDR = false;
            m_Camera.allowMSAA = false;
            m_Camera.allowDynamicResolution = false;
            m_Camera.aspect = 1.0f;
            m_Camera.useOcclusionCulling = false;
            m_Camera.SetReplacementShader(virtualShadowMaps.castMaterial.shader, "RenderType");

            m_CameraTransform = m_Camera.GetComponent<Transform>();
            //m_CameraTransform.parent = virtualShadowMaps.transform;
            m_CameraTransform.localRotation = Quaternion.identity;
            m_CameraTransform.localScale = Vector3.one;

            m_Renderers = VirtualShadowManager.instance.GetRenderers(true);
            m_WorldBounds = virtualShadowMaps.CalculateBoundingBox();
            m_Material = virtualShadowMaps.castMaterial;

            m_VirtualShadowMaps = virtualShadowMaps;
            m_MinMaxDepthPass = new DepthRangePass(virtualShadowMaps.minMaxDepthCompute);

            var transform = virtualShadowMaps.GetLightTransform();
            lightDirection = transform.forward;
            lightPosition = transform.position;
            lightRotation = transform.rotation;
            localToWorldMatrix = transform.localToWorldMatrix;
            worldToLocalMatrix = transform.worldToLocalMatrix;
        }

        ~VirtualShadowMapBaker()
        {
            this.Dispose();
        }

        public RenderTexture Render(RenderTexture renderTexture, int x, int y, int level)
        {
            var mipScale = 1 << level;
            var clipOffset = 0.05f;

            var lightSpaceBounds = m_WorldBounds.ProjectToLocalSpace(this.worldToLocalMatrix);
            var lightSpaceMin = new Vector3(lightSpaceBounds.min.x, lightSpaceBounds.min.y, lightSpaceBounds.min.z);
            var lightSpaceRight = new Vector3(lightSpaceBounds.max.x, lightSpaceBounds.min.y, lightSpaceBounds.min.z);
            var lightSpaceBottom = new Vector3(lightSpaceBounds.min.x, lightSpaceBounds.max.y, lightSpaceBounds.min.z);
            var lightSpaceAxisX = Vector3.Normalize(lightSpaceRight - lightSpaceMin);
            var lightSpaceAxisY = Vector3.Normalize(lightSpaceBottom - lightSpaceMin);
            var lightSpaceWidth = (lightSpaceRight - lightSpaceMin).magnitude;
            var lightSpaceHeight = (lightSpaceBottom - lightSpaceMin).magnitude;

            var cellWidth = lightSpaceWidth / m_VirtualShadowMaps.pageSize * mipScale;
            var cellHeight = lightSpaceHeight / m_VirtualShadowMaps.pageSize * mipScale;
            var cellCenter = lightSpaceMin + lightSpaceAxisX * cellWidth * (x + 0.5f) + lightSpaceAxisY * cellHeight * (y + 0.5f);
            var cellPos = this.localToWorldMatrix.MultiplyPoint(cellCenter);

            var boundsInLightSpaceOrthographicSize = Mathf.Max(cellWidth, cellHeight) * 0.5f + m_VirtualShadowMaps.padding;
            var boundsInLightSpaceLocalPosition = new Vector3(cellCenter.x, cellCenter.y, cellCenter.z - clipOffset);
            var boundsInLightSpaceWorldPosition = localToWorldMatrix.MultiplyPoint(boundsInLightSpaceLocalPosition);

            m_CameraTransform.SetPositionAndRotation(boundsInLightSpaceWorldPosition, lightRotation);
            m_Camera.aspect = 1.0f;
            m_Camera.orthographicSize = boundsInLightSpaceOrthographicSize;
            m_Camera.nearClipPlane = clipOffset;
            m_Camera.farClipPlane = clipOffset + lightSpaceBounds.size.z;
            m_Camera.ResetProjectionMatrix();

            var minMaxDepth = m_MinMaxDepthPass.Execute(renderTexture, m_Material, 1, m_Camera);
            if (minMaxDepth[0] == minMaxDepth[1])
            {
                var bounds = VirtualShadowMapsUtilities.CalculateBoundingBox(m_Renderers, m_Camera);
                minMaxDepth[0] = Mathf.CeilToInt(bounds.min.y);
                minMaxDepth[1] = Mathf.CeilToInt(bounds.max.y);
            }

            var obliqueHeight = minMaxDepth[1] - minMaxDepth[0];
            var obliquePosition = new Vector3(0, minMaxDepth[1] + clipOffset, 0);
            var obliqueSlope = Vector3.Dot(Vector3.up, -this.lightDirection);
            var obliqueSine = Mathf.Sqrt(1 - obliqueSlope * obliqueSlope);
            var obliqueDistance = (cellPos.y - minMaxDepth[1]) / obliqueSlope;
            var obliqueWeight = Mathf.Clamp01(boundsInLightSpaceOrthographicSize / (obliqueHeight + obliqueHeight * obliqueSine));

            m_CameraTransform.SetPositionAndRotation(boundsInLightSpaceWorldPosition + this.lightDirection * obliqueDistance, lightRotation);
            m_Camera.aspect = 1.0f;
            m_Camera.orthographicSize = boundsInLightSpaceOrthographicSize;
            m_Camera.nearClipPlane = clipOffset;
            m_Camera.farClipPlane = clipOffset + Mathf.Lerp(obliqueHeight / obliqueSlope, 1.0f, obliqueWeight);
            m_Camera.projectionMatrix = m_Camera.CalculateObliqueMatrix(VirtualShadowMapsUtilities.CameraSpacePlane(m_Camera, obliquePosition, Vector3.up, -1.0f));

            var projection = GL.GetGPUProjectionMatrix(m_Camera.projectionMatrix, false);
            lightProjecionMatrix = StreamingTileUtilities.ComputeScaleBiasMatrix() * projection * m_Camera.worldToCameraMatrix;

            return this.RenderShadowMap(renderTexture);
        }

        public RenderTexture RenderNow(RenderTexture renderTexture, int x, int y, int level)
        {
            var mipScale = 1 << level;
            var clipOffset = 0.05f;

            var lightTransform = m_VirtualShadowMaps.GetLightTransform();
            var lightSpaceBounds = m_WorldBounds.ProjectToLocalSpace(lightTransform.worldToLocalMatrix);
            var lightSpaceMin = new Vector3(lightSpaceBounds.min.x, lightSpaceBounds.min.y, lightSpaceBounds.min.z);
            var lightSpaceRight = new Vector3(lightSpaceBounds.max.x, lightSpaceBounds.min.y, lightSpaceBounds.min.z);
            var lightSpaceBottom = new Vector3(lightSpaceBounds.min.x, lightSpaceBounds.max.y, lightSpaceBounds.min.z);
            var lightSpaceAxisX = Vector3.Normalize(lightSpaceRight - lightSpaceMin);
            var lightSpaceAxisY = Vector3.Normalize(lightSpaceBottom - lightSpaceMin);
            var lightSpaceWidth = (lightSpaceRight - lightSpaceMin).magnitude;
            var lightSpaceHeight = (lightSpaceBottom - lightSpaceMin).magnitude;

            var cellWidth = lightSpaceWidth / m_VirtualShadowMaps.pageSize * mipScale;
            var cellHeight = lightSpaceHeight / m_VirtualShadowMaps.pageSize * mipScale;
            var cellCenter = lightSpaceMin + lightSpaceAxisX * cellWidth * (x + 0.5f) + lightSpaceAxisY * cellHeight * (y + 0.5f);
            var cellPos = lightTransform.localToWorldMatrix.MultiplyPoint(cellCenter);

            var boundsInLightSpaceOrthographicSize = Mathf.Max(cellWidth, cellHeight) * 0.5f + m_VirtualShadowMaps.padding;
            var boundsInLightSpaceLocalPosition = new Vector3(cellCenter.x, cellCenter.y, cellCenter.z - clipOffset);
            var boundsInLightSpaceWorldPosition = localToWorldMatrix.MultiplyPoint(boundsInLightSpaceLocalPosition);

            m_CameraTransform.SetPositionAndRotation(boundsInLightSpaceWorldPosition, lightRotation);
            m_Camera.aspect = 1.0f;
            m_Camera.orthographicSize = boundsInLightSpaceOrthographicSize;
            m_Camera.nearClipPlane = clipOffset;
            m_Camera.farClipPlane = clipOffset + lightSpaceBounds.size.z;
            m_Camera.ResetProjectionMatrix();

            var minMaxDepth = m_MinMaxDepthPass.ExecuteImmediate(renderTexture, m_Material, 1, m_Camera);
            if (minMaxDepth[0] == minMaxDepth[1])
            {
                var bounds = VirtualShadowMapsUtilities.CalculateBoundingBox(m_Renderers, m_Camera);
                minMaxDepth[0] = Mathf.CeilToInt(bounds.min.y);
                minMaxDepth[1] = Mathf.CeilToInt(bounds.max.y);
            }

            var obliqueHeight = minMaxDepth[1] - minMaxDepth[0];
            var obliquePosition = new Vector3(0, minMaxDepth[1] + clipOffset, 0);
            var obliqueSlope = Vector3.Dot(Vector3.up, -lightTransform.forward);
            var obliqueSine = Mathf.Sqrt(1 - obliqueSlope * obliqueSlope);
            var obliqueDistance = (cellPos.y - minMaxDepth[1]) / obliqueSlope;
            var obliqueWeight = Mathf.Clamp01(boundsInLightSpaceOrthographicSize / (obliqueHeight + obliqueHeight * obliqueSine));

            m_CameraTransform.SetPositionAndRotation(boundsInLightSpaceWorldPosition + this.lightDirection * obliqueDistance, lightRotation);
            m_Camera.aspect = 1.0f;
            m_Camera.orthographicSize = boundsInLightSpaceOrthographicSize;
            m_Camera.nearClipPlane = clipOffset;
            m_Camera.farClipPlane = clipOffset + Mathf.Lerp(obliqueHeight / obliqueSlope, obliqueHeight, obliqueWeight);
            m_Camera.projectionMatrix = m_Camera.CalculateObliqueMatrix(VirtualShadowMapsUtilities.CameraSpacePlane(m_Camera, obliquePosition, Vector3.up, -1.0f));

            var projection = GL.GetGPUProjectionMatrix(m_Camera.projectionMatrix, false);
            lightProjecionMatrix = StreamingTileUtilities.ComputeScaleBiasMatrix() * projection * m_Camera.worldToCameraMatrix;

            this.RenderShadowMapNow(renderTexture);

            return renderTexture;
        }

        private RenderTexture RenderShadowMap(RenderTexture renderTexture)
        {
            if (m_Renderers == null)
                return null;

            m_CmdBuffer.Clear();
            m_CmdBuffer.SetViewProjectionMatrices(m_Camera.worldToCameraMatrix, m_Camera.projectionMatrix);
            m_CmdBuffer.SetRenderTarget(renderTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            m_CmdBuffer.ClearRenderTarget(true, true, Color.black);

            GeometryUtility.CalculateFrustumPlanes(m_Camera, m_Planes);

            foreach (var it in m_Renderers)
            {
                if (!GeometryUtility.TestPlanesAABB(m_Planes, it.bounds))
                    continue;

                if (it.TryGetComponent<MeshFilter>(out var meshFilter))
                {
                    var customPass = it.sharedMaterial.FindPass("VirtualShadowCaster");
                    if (customPass >= 0)
                    {
                        for (int i = 0; i < meshFilter.sharedMesh.subMeshCount; i++)
                            m_CmdBuffer.DrawMesh(meshFilter.sharedMesh, it.localToWorldMatrix, it.sharedMaterial, i, customPass);
                    }
                    else
                    {
                        //m_Material.CopyPropertiesFromMaterial(it.sharedMaterial);

                        for (int i = 0; i < meshFilter.sharedMesh.subMeshCount; i++)
                            m_CmdBuffer.DrawMesh(meshFilter.sharedMesh, it.localToWorldMatrix, m_Material, i, 0);
                    }
                }
            }

            Graphics.ExecuteCommandBuffer(m_CmdBuffer);

            return renderTexture;
        }

        private void RenderShadowMapNow(RenderTexture renderTexture)
        {
            if (m_Renderers == null)
                return;

            RenderTexture savedRT = RenderTexture.active;

            Graphics.SetRenderTarget(renderTexture);
            GL.Clear(true, true, Color.black);
            GL.LoadIdentity();
            GL.modelview = m_Camera.worldToCameraMatrix;
            GL.LoadProjectionMatrix(m_Camera.projectionMatrix);

            var planes = GeometryUtility.CalculateFrustumPlanes(m_Camera);

            foreach (var it in m_Renderers)
            {
                if (!GeometryUtility.TestPlanesAABB(planes, it.bounds))
                    continue;

                var customPass = it.sharedMaterial.FindPass("VirtualShadowCaster");
                if (customPass >= 0)
                {
                    it.sharedMaterial.SetPass(customPass);
                }
                else
                {
                    m_Material.CopyPropertiesFromMaterial(it.sharedMaterial);
                    m_Material.SetPass(0);
                }

                if (it.TryGetComponent<MeshFilter>(out var meshFilter))
                {
                    Graphics.DrawMeshNow(meshFilter.sharedMesh, it.localToWorldMatrix);
                }
            }

            Graphics.SetRenderTarget(savedRT);
        }

        public void Dispose()
        {
            if (m_CameraGO != null)
            {
                if (Application.isEditor)
                    GameObject.DestroyImmediate(m_CameraGO);
                else
                    GameObject.Destroy(m_CameraGO);

                m_Camera = null;
                m_CameraTransform = null;
                m_CameraGO = null;
            }

            if (m_MinMaxDepthPass != null)
            {
                m_MinMaxDepthPass.Dispose();
                m_MinMaxDepthPass = null;
            }
        }
    }
}