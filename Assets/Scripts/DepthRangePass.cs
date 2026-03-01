using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdaptiveRendering
{
	sealed public class DepthRangePass : IDisposable
    {
        private ComputeShader m_RangeCompute;

        private ComputeBuffer m_RangeBuffer;

		private CommandBuffer m_CmdList;

        public DepthRangePass(ComputeShader shader)
        {
            m_CmdList = new CommandBuffer();
            m_RangeCompute = shader;
            m_RangeBuffer = new ComputeBuffer(2, sizeof(int));
            m_RangeBuffer.SetData(new int[2] { short.MaxValue, short.MinValue });
        }

        ~DepthRangePass()
        {
            this.Dispose();
            GC.SuppressFinalize(this);
        }

        public Vector2Int Execute(RenderTexture texture, Material material, int pass, Camera camera)
        {
            Debug.Assert(material != null);

            var minMaxDepth = new int[2];
            minMaxDepth[0] = Mathf.CeilToInt(short.MaxValue);
            minMaxDepth[1] = Mathf.CeilToInt(short.MinValue);

            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var renderers = VirtualShadowManager.instance.GetRenderers();
            if (renderers != null)
            {
                m_CmdList.Clear();
                m_CmdList.SetRenderTarget(texture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                m_CmdList.ClearRenderTarget(true, true, Color.black);
                m_CmdList.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);

                foreach (var it in renderers)
                {
                    if (!GeometryUtility.TestPlanesAABB(planes, it.bounds))
                        continue;

                    if (it.TryGetComponent<MeshFilter>(out var meshFilter))
                    {
                        for (int i = 0; i < meshFilter.sharedMesh.subMeshCount; i++)
                            m_CmdList.DrawMesh(meshFilter.sharedMesh, it.localToWorldMatrix, material, i, pass);
                    }
                }

                m_CmdList.SetBufferData(m_RangeBuffer, minMaxDepth);

                m_CmdList.SetComputeIntParam(m_RangeCompute, "viewportWidth", texture.width);
                m_CmdList.SetComputeIntParam(m_RangeCompute, "viewportHeight", texture.height);
                m_CmdList.SetComputeTextureParam(m_RangeCompute, 0, "sourceDepthMap", texture);
                m_CmdList.SetComputeBufferParam(m_RangeCompute, 0, "depthRangeBuffer", m_RangeBuffer);
                m_CmdList.DispatchCompute(m_RangeCompute, 0, Mathf.CeilToInt(texture.width / 8.0f), Mathf.CeilToInt(texture.height / 8.0f), 1);

                Graphics.ExecuteCommandBuffer(m_CmdList);

                m_RangeBuffer.GetData(minMaxDepth);
            }

            return new Vector2Int(minMaxDepth[0], minMaxDepth[1]);
        }

        public Vector2Int ExecuteImmediate(RenderTexture renderTexture, Material material, int pass, Camera camera, float minDepth = short.MinValue, float maxDepth = short.MaxValue)
        {
            Debug.Assert(material != null);

            var minMaxDepth = new int[2];
            minMaxDepth[0] = Mathf.CeilToInt(maxDepth);
            minMaxDepth[1] = Mathf.CeilToInt(minDepth);

            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var renderers = VirtualShadowManager.instance.GetRenderers();
            if (renderers != null)
            {
                RenderTexture savedRT = RenderTexture.active;

                Graphics.SetRenderTarget(renderTexture);
                GL.Clear(true, true, Color.black);
                GL.LoadIdentity();
                GL.modelview = camera.worldToCameraMatrix;
                GL.LoadProjectionMatrix(camera.projectionMatrix);

                foreach (var it in renderers)
                {
                    if (!GeometryUtility.TestPlanesAABB(planes, it.bounds))
                        continue;

                    if (it.TryGetComponent<MeshFilter>(out var meshFilter))
                    {
                        material.SetPass(pass);
                        Graphics.DrawMeshNow(meshFilter.sharedMesh, it.localToWorldMatrix);
                    }
                }

                Graphics.SetRenderTarget(savedRT);

                m_RangeBuffer.SetData(minMaxDepth);

                m_RangeCompute.SetInt("viewportWidth", renderTexture.width);
                m_RangeCompute.SetInt("viewportHeight", renderTexture.height);
                m_RangeCompute.SetTexture(0, "sourceDepthMap", renderTexture);
                m_RangeCompute.SetBuffer(0, "depthRangeBuffer", m_RangeBuffer);
                m_RangeCompute.Dispatch(0, Mathf.CeilToInt(renderTexture.width / 8.0f), Mathf.CeilToInt(renderTexture.height / 8.0f), 1);

                m_RangeBuffer.GetData(minMaxDepth);
            }

            return new Vector2Int(minMaxDepth[0], minMaxDepth[1]);
        }

        public void Dispose()
        {
            if (m_RangeBuffer != null)
            {
                m_RangeBuffer.Dispose();
                m_RangeBuffer = null;
            }
        }
    }
}
