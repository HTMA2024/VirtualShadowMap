using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AdaptiveRendering
{
#if UNITY_2018_3_OR_NEWER
    [ExecuteAlways]
#else
    [ExecuteInEditMode]
#endif
    [DisallowMultipleComponent, ImageEffectAllowedInSceneView]
    [AddComponentMenu("Rendering/Virtual Shadow Camera", 1000)]
    [RequireComponent(typeof(Camera))]
    public class VirtualShadowCamera : MonoBehaviour
	{
        /// <summary>
        /// 用于开启VSM功能的关键字
        /// </summary>
        private readonly string m_VirtualShadowMapsKeyword = "_CASCADED_OCCLUSION_MAPS";

        /// <summary>
        /// 用于开启VSM功能的关键字
        /// </summary>
        private readonly string m_VirtualShadowMapsPcssKeyword = "_CASCADED_OCCLUSION_MAPS_PCSS";

        /// <summary>
        /// 用于开启VSM功能的关键字
        /// </summary>
        private GlobalKeyword m_VirtualShadowMapsKeywordFeature;

        /// <summary>
        /// 用于开启VSM功能的关键字
        /// </summary>
        private GlobalKeyword m_VirtualShadowMapsPcssKeywordFeature;

        /// <summary>
        /// 渲染相机
        /// </summary>
        private Camera m_Camera;

        /// <summary>
        /// 绘制Tile用的CommandBuffer
        /// </summary>
        private CommandBuffer m_CmdBufferTileRendering;

        /// <summary>
        /// 绘制Tile用的CommandBuffer
        /// </summary>
        private CommandBuffer m_CmdBufferEnable;
        
        /// <summary>
        /// 绘制Tile用的CommandBuffer
        /// </summary>
        private CommandBuffer m_CmdBufferDisable;
        
        /// <summary>
        /// 初始化当前帧需要渲染的数据
        /// </summary>
        private CommandBuffer m_CmdBufferBeforeRendering;

        /// <summary>
        /// 渲染相机
        /// </summary>
        private Transform m_CameraTransform;

        /// <summary>
        /// 记录上一帧的位置用于判断是否需要更新Page
        /// </summary>
        private Vector3 m_LastCameraPosition;

        /// <summary>
        /// 当前场景的烘焙数�?        /// </summary>
        private VirtualShadowMaps m_VirtualShadowMaps;

        /// <summary>
        /// 当前相机的纹理查找表
        /// </summary>
        private IndirectionTexture m_LookupTexture;

        /// <summary>
        /// 用于重新构建查找�?        /// </summary>
        private bool m_Dirty;

        /// <summary>
        /// 跟踪上一帧阴影是否激活，避免每帧清空CommandBuffer导致编辑器闪�?        /// </summary>
        private bool m_LastShadowActive;

        /// <summary>
        /// 细分等级(数值越大加载的页表越多)
        /// </summary>
        [Space(10)]
        [Range(0, 10)]
        public float levelOfDetail = 1.0f;

        /// <summary>
        /// 世界空间相机位置
        /// </summary>
        public Vector3 worldSpaceCameraPosition { get => m_CameraTransform.position; }

        /// <summary>
        /// 世界空间相机朝向
        /// </summary>
        public Vector3 worldSpaceCameraDirection { get => m_CameraTransform.forward; }

        public void Awake()
        {
            m_Camera = GetComponent<Camera>();
            m_CameraTransform = m_Camera.transform;
        }

        public void OnEnable()
        {
            m_Dirty = true;

            m_VirtualShadowMapsKeywordFeature = GlobalKeyword.Create(m_VirtualShadowMapsKeyword);
            m_VirtualShadowMapsPcssKeywordFeature = GlobalKeyword.Create(m_VirtualShadowMapsPcssKeyword);

            m_CmdBufferTileRendering = new CommandBuffer { name = "CascadedOcclusion.Mosaic" };
            m_CmdBufferBeforeRendering = new CommandBuffer { name = "CascadedOcclusion.Init" };
            m_CmdBufferEnable = new CommandBuffer { name = "CascadedOcclusion.Enable" };
            m_CmdBufferDisable = new CommandBuffer { name = "CascadedOcclusion.Disable" };

            m_CmdBufferDisable.BeginSample("CascadedOcclusion.Disable");
            m_CmdBufferDisable.DisableKeyword(m_VirtualShadowMapsKeywordFeature);
            m_CmdBufferDisable.EndSample("CascadedOcclusion.Disable");

            m_Camera.AddCommandBuffer(CameraEvent.BeforeLighting, m_CmdBufferBeforeRendering);
            m_Camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, m_CmdBufferEnable);
            m_Camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, m_CmdBufferEnable);
            m_Camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, m_CmdBufferDisable);

            VirtualShadowManager.instance.Register(this);
        }

        public void OnDisable()
        {
            if (m_Camera != null)
            {
                if (m_CmdBufferDisable != null)
                    m_Camera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, m_CmdBufferDisable);
                if (m_CmdBufferBeforeRendering != null)
                    m_Camera.RemoveCommandBuffer(CameraEvent.BeforeLighting, m_CmdBufferBeforeRendering);
                if (m_CmdBufferEnable != null)
                {
                    m_Camera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, m_CmdBufferEnable);
                    m_Camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, m_CmdBufferEnable);
                }
            }

            if (m_LookupTexture != null)
            {
                m_LookupTexture.Dispose();
                m_LookupTexture = null;
            }

            VirtualShadowManager.instance.Unregister(this);
        }

        public void OnDestroy()
        {
            m_CmdBufferTileRendering?.Release();
            m_CmdBufferBeforeRendering?.Release();
            m_CmdBufferEnable?.Release();
            m_CmdBufferDisable?.Release();
        }

        public void Reset()
        {
            this.levelOfDetail = 1;
            this.SetDirty();
            this.m_Camera = this.GetComponent<Camera>();
            this.m_CameraTransform = m_Camera.transform;
            this.m_VirtualShadowMaps = null;
            this.m_LookupTexture?.Dispose();
            this.m_LookupTexture = null;
        }

        public void OnValidate()
        {
            if (m_Camera == null || m_CameraTransform == null)
            {
                this.m_Camera = this.GetComponent<Camera>();
                if (m_Camera != null)
                    this.m_CameraTransform = m_Camera.transform;
            }
        }

        public void SetDirty()
        {
            m_Dirty = true;
        }

        public bool GetDirty()
        {
            return m_Dirty;
        }

        public Camera GetCamera()
        {
            return m_Camera;
        }

        public Texture GetLookupTexture()
        {
            return m_LookupTexture?.FetchSurface();
        }

        public void UpdateSetting(VirtualShadowMaps virtualShadowMaps)
        {
            try
            {
                bool sameRef = (m_VirtualShadowMaps == virtualShadowMaps);
                if (m_VirtualShadowMaps != virtualShadowMaps || m_Dirty)
                {
                    if (virtualShadowMaps != null && virtualShadowMaps.isActiveAndEnabled && virtualShadowMaps.shadowOn)
                    {
                        if (m_LookupTexture != null)
                            m_LookupTexture.Dispose();

                        m_LastCameraPosition = Vector3.positiveInfinity;
                        m_VirtualShadowMaps = virtualShadowMaps;
                        m_LookupTexture = new IndirectionTexture(virtualShadowMaps.pageSize, virtualShadowMaps.maxTilePool);

                        this.UpdateRequest();

                    #if UNITY_EDITOR
                        m_VirtualShadowMaps.UpdateJob(int.MaxValue, false);
                    #else
                        m_VirtualShadowMaps.UpdateJob(int.MaxValue);
                    #endif

                    }
                    else
                    {
                        m_VirtualShadowMaps = null;
                    }

                    m_Dirty = false;
                }
                else
                {
                    // Not dirty and same reference - skip
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[VirtualShadowCamera] UpdateSetting failed: {e}");
                m_VirtualShadowMaps = null;
            }
        }

        private void UpdateRequest()
        {
            if (!m_VirtualShadowMaps.shadowOn)
            {
                return;
            }

            var maxMipLevel = m_VirtualShadowMaps.pageLevel;
            var worldToLocalMatrix = m_VirtualShadowMaps.worldToLocalMatrix;
            var pageSize = m_VirtualShadowMaps.pageSize;

            var lightSpaceBounds = m_VirtualShadowMaps.boundsInLightSpace;

            if (lightSpaceBounds.size.sqrMagnitude < 1e-6f)
                return;

            var lightSpaceMin = new Vector3(lightSpaceBounds.min.x, lightSpaceBounds.min.y, lightSpaceBounds.min.z);
            var lightSpaceRight = new Vector3(lightSpaceBounds.max.x, lightSpaceBounds.min.y, lightSpaceBounds.min.z);
            var lightSpaceBottom = new Vector3(lightSpaceBounds.min.x, lightSpaceBounds.max.y, lightSpaceBounds.min.z);
            var lightSpaceAxisX = Vector3.Normalize(lightSpaceRight - lightSpaceMin);
            var lightSpaceAxisY = Vector3.Normalize(lightSpaceBottom - lightSpaceMin);
            var lightSpaceWidth = (lightSpaceRight - lightSpaceMin).magnitude;
            var lightSpaceHeight = (lightSpaceBottom - lightSpaceMin).magnitude;

            var lightSpaceCameraPos = worldToLocalMatrix.MultiplyPoint(m_CameraTransform.position);
            lightSpaceCameraPos.z = lightSpaceBounds.min.z;

            var lightSpaceCameraVector = worldToLocalMatrix.MultiplyVector(m_CameraTransform.forward);
            lightSpaceCameraVector.z = 0;

            var halfSize = Mathf.Max(lightSpaceWidth, lightSpaceHeight) / (pageSize * 2);
            var estimateUpdate = (m_LastCameraPosition - lightSpaceCameraPos).sqrMagnitude / (halfSize * halfSize);

            if (estimateUpdate < levelOfDetail && m_VirtualShadowMaps.FrameRequestCount() == 0)
                return;

            m_LookupTexture.ResetAll();
            m_LastCameraPosition = lightSpaceCameraPos;

            var minPageLevel = Math.Max(m_VirtualShadowMaps.minPageRequestLevel, 0);
            var maxPageLevel = Math.Min(m_VirtualShadowMaps.maxPageRequestLevel, (int)maxMipLevel);

            for (int level = minPageLevel; level < maxPageLevel; level++)
            {
                var mipScale = 1 << level;
                var mipSize = pageSize / mipScale;

                var cellWidth = lightSpaceWidth / mipSize;
                var cellHeight = lightSpaceHeight / mipSize;
                var cellSize = Mathf.Max(cellWidth, cellHeight);
                var cellSize2 = 1.0f / (cellSize * cellSize);

                var lightSpaceCameraRect = new Rect(lightSpaceCameraPos.x - cellWidth * levelOfDetail * 0.5f, lightSpaceCameraPos.y - cellHeight * levelOfDetail * 0.5f,
                    cellWidth * levelOfDetail, cellHeight * levelOfDetail);

                // 最粗糙级别无条件加载，保证远距离基础阴影覆盖
                bool isCoarsestLevel = (level >= maxPageLevel - 1);

                for (int y = 0; y < mipSize; y++)
                {
                    var posY = lightSpaceMin + lightSpaceAxisY * ((y + 0.5f) * cellHeight);

                    for (int x = 0; x < mipSize; x++)
                    {
                        var thisPos = lightSpaceAxisX * ((x + 0.5f) * cellWidth) + posY;
                        var estimate = Vector3.SqrMagnitude(thisPos - lightSpaceCameraPos) * cellSize2;
                        if (isCoarsestLevel || estimate < levelOfDetail)
                        {
                            if (isCoarsestLevel)
                            {
                                var page = m_VirtualShadowMaps.SubmitRequest(x, y, level);
                                if (page != null)
                                    m_LookupTexture.Enqueue(page);
                            }
                            else
                            {
                                var rect = new Rect(thisPos.x, thisPos.y, cellSize, cellSize);
                                if (rect.Overlaps(lightSpaceCameraRect))
                                {
                                    var page = m_VirtualShadowMaps.SubmitRequest(x, y, level);
                                    if (page != null)
                                        m_LookupTexture.Enqueue(page);
                                }
                                else
                                {
                                    var angle = Vector3.Dot(lightSpaceCameraVector, (thisPos - lightSpaceCameraPos).normalized);
                                    if (angle > 0.0f)
                                    {
                                        var page = m_VirtualShadowMaps.SubmitRequest(x, y, level);
                                        if (page != null)
                                            m_LookupTexture.Enqueue(page);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void LateUpdate()
        {
            if (m_VirtualShadowMaps != null && m_VirtualShadowMaps.shadowOn)
            {
                if (m_VirtualShadowMaps.PendingRequestCount() == 0)
                    this.UpdateRequest();

                // 编辑器非播放模式下手动触�?light 侧的 CommandBuffer 重建
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                    m_VirtualShadowMaps.BuildCommandBuffers();
                #endif

                var activeCount = m_VirtualShadowMaps.activePageCount;
                var hasLookup = m_LookupTexture != null;
                var hasDrawMat = m_VirtualShadowMaps.drawLookupMaterial != null;

                if (hasLookup && hasDrawMat && activeCount > 0)
                {
                    m_LookupTexture.RefreshActiveTiles(m_VirtualShadowMaps.FetchMosaicSurface());

                    m_CmdBufferEnable.Clear();
                    m_CmdBufferEnable.BeginSample("CascadedOcclusion.Enable");
                    m_CmdBufferEnable.EnableKeyword(m_VirtualShadowMapsKeywordFeature);
                    m_CmdBufferEnable.EndSample("CascadedOcclusion.Enable");

                    var lookupCmd = new CommandBuffer { name = "CascadedOcclusion.IndirectionRender" };
                    lookupCmd.BeginSample("CascadedOcclusion.IndirectionRender");
                    m_LookupTexture.RebuildIndirectionSurface(lookupCmd, m_VirtualShadowMaps.drawLookupMaterial);
                    lookupCmd.EndSample("CascadedOcclusion.IndirectionRender");
                    Graphics.ExecuteCommandBuffer(lookupCmd);
                    lookupCmd.Release();

                    m_CmdBufferBeforeRendering.Clear();
                    m_CmdBufferBeforeRendering.BeginSample("VirtualShadowMap.Lookup");
                    m_CmdBufferBeforeRendering.EnableKeyword(m_VirtualShadowMapsKeywordFeature);

                    var lightShadowData = new Vector4(0, 0, 5.0f / QualitySettings.shadowDistance, -1.0f * (2.0f + m_Camera.fieldOfView / 180.0f * 2.0f));

                    m_CmdBufferBeforeRendering.SetGlobalVector(ShaderConstants._VirtualShadowData, lightShadowData);
                    m_CmdBufferBeforeRendering.SetGlobalTexture(ShaderConstants._VirtualShadowLookupTexture, m_LookupTexture.FetchSurface());

                    m_CmdBufferBeforeRendering.EndSample("VirtualShadowMap.Lookup");

                    Shader.SetGlobalVector(ShaderConstants._VirtualShadowData, lightShadowData);
                    Shader.SetGlobalTexture(ShaderConstants._VirtualShadowLookupTexture, m_LookupTexture.FetchSurface());

                    m_LastShadowActive = true;
                }
                else if (m_LastShadowActive)
                {
                    m_CmdBufferEnable.Clear();
                    m_CmdBufferEnable.DisableKeyword(m_VirtualShadowMapsKeywordFeature);
                    m_CmdBufferBeforeRendering.Clear();
                    Shader.DisableKeyword(m_VirtualShadowMapsKeywordFeature);
                    m_LastShadowActive = false;
                }
            }
            else if (m_LastShadowActive)
            {
                m_CmdBufferEnable.Clear();
                m_CmdBufferEnable.DisableKeyword(m_VirtualShadowMapsKeywordFeature);
                m_CmdBufferBeforeRendering.Clear();
                Shader.DisableKeyword(m_VirtualShadowMapsKeywordFeature);
                m_LastShadowActive = false;
            }

        }



        static class ShaderConstants
        {
            public static readonly int _VirtualShadowMatrixs = Shader.PropertyToID("_CascadedOcclusionMatrices");
            public static readonly int _VirtualShadowMatrixs_SSBO = Shader.PropertyToID("_CascadedOcclusionMatrices_SSBO");
            public static readonly int _VirtualShadowLightMatrix = Shader.PropertyToID("_CascadedOcclusionLightMatrix");
            public static readonly int _VirtualShadowData = Shader.PropertyToID("_CascadedOcclusionData");
            public static readonly int _VirtualShadowLightDirection = Shader.PropertyToID("_CascadedOcclusionLightDirection");
            public static readonly int _VirtualShadowBiasParams = Shader.PropertyToID("_CascadedOcclusionBiasParams");
            public static readonly int _VirtualShadowPcssParams = Shader.PropertyToID("_CascadedOcclusionPcssParams");
            public static readonly int _VirtualShadowRegionParams = Shader.PropertyToID("_CascadedOcclusionRegionParams");
            public static readonly int _VirtualShadowPageParams = Shader.PropertyToID("_CascadedOcclusionPageParams");
            public static readonly int _VirtualShadowTileParams = Shader.PropertyToID("_CascadedOcclusionTileParams");
            public static readonly int _VirtualShadowFeedbackParams = Shader.PropertyToID("_CascadedOcclusionFeedbackParams");
            public static readonly int _VirtualShadowTileTexture = Shader.PropertyToID("_CascadedOcclusionTileTexture");
            public static readonly int _VirtualShadowLookupTexture = Shader.PropertyToID("_CascadedOcclusionLookupTexture");
        }
    }
}
