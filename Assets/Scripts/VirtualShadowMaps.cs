using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.AddressableAssets;
using System.Runtime.InteropServices;
using UnityEngine.ResourceManagement.AsyncOperations;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AdaptiveRendering
{
#if UNITY_2018_3_OR_NEWER
    [ExecuteAlways]
#else
    [ExecuteInEditMode]
#endif
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/Virtual Shadow Maps", 1000)]
    [RequireComponent(typeof(Light))]
    public class VirtualShadowMaps : MonoBehaviour
    {
        // --- �?QualitySwitcher 中的字段 ---

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

        // --- 独立画质管理 ---
        private OcclusionQualityPreset m_CurrentLevel = OcclusionQualityPreset.Ultra;
        public OcclusionQualityPreset CurrentLevel => m_CurrentLevel;

        // --- �?VirtualShadowMaps.cs 中的字段 ---

        /// <summary>
        /// 光源组件
        /// </summary>
        private Light m_Light;

        /// <summary>
        /// 光源的变换矩�?        /// </summary>
        private Transform m_LightTransform;

        /// <summary>
        /// 用于检查光源变�?        /// </summary>
        private Vector3 m_LightDirection;

        /// <summary>
        /// 用于检查光源变�?        /// </summary>
        private Matrix4x4 m_LocalToWorldMatrix;

        /// <summary>
        /// 用于检查光源变�?        /// </summary>
        private Matrix4x4 m_WorldToLocalMatrix;

        /// <summary>
        /// 当前场景所有联级的投影矩阵（CPU�?        /// </summary>
		private Matrix4x4[] m_LightProjecionMatrixs;

        /// <summary>
        /// 当前场景所有联级的投影矩阵（GPU�?        /// </summary>
        private GraphicsBuffer m_LightProjecionMatrixBuffer;

        /// <summary>
        /// 绘制Tile用的CommandBuffer
        /// </summary>
        private CommandBuffer m_CmdBufferBaker;

        /// <summary>
        /// 绘制Tile用的CommandBuffer
        /// </summary>
        private CommandBuffer m_CmdBufferBeforeLighting;

        /// <summary>
        /// 绘制Tile用的CommandBuffer
        /// </summary>
        private CommandBuffer m_CmdBufferAfterLighting;
        
        /// <summary>
        /// 平铺贴图对象
        /// </summary>
        private StreamingTileMap2D m_VirtualTexture;

        /// <summary>
        /// 用于流式加载的数�?        /// </summary>
        private VirtualShadowData m_CachedShadowData;

        /// <summary>
        /// 用于检查光源变�?        /// </summary>
        private Dictionary<TileRequestData, KeyValuePair<RenderTexture, Matrix4x4>> m_CachedShadowMaps = new Dictionary<TileRequestData, KeyValuePair<RenderTexture, Matrix4x4>>();

        /// <summary>
        /// 用于检查光源变�?        /// </summary>
        private TileRequestJob m_RequestPageJob = new TileRequestJob();

        /// <summary>
        /// 运行时烘�?        /// </summary>
        private VirtualShadowMapBaker m_Baker;

        /// <summary>
        /// 灯光空间场景包围�?        /// </summary>
        private Bounds m_BoundsInLightSpace;

        /// <summary>
        /// 灯光空间场景包围�?Mips)
        /// </summary>
        private Bounds[] m_BoundsInLightSpaces;

        /// <summary>
        /// 统计每帧的加载数�?        /// </summary>
        private int m_FrameRequestCount;

        /// <summary>
        /// 统计每帧的相机数�?        /// </summary>
        private int m_FrameCameraCount;

        /// <summary>
        /// Shadow Caster 材质
        /// </summary>
        public Material castMaterial;

        /// <summary>
        /// Tile纹理生成材质
        /// </summary>
        public Material drawTileMaterial;

        /// <summary>
        /// Lookup纹理生成材质
        /// </summary>
        public Material drawLookupMaterial;

        /// <summary>
        /// 计算最小最大的深度信息
        /// </summary>
        public ComputeShader minMaxDepthCompute;

        /// <summary>
        /// 覆盖区域中心.
        /// </summary>
        [Space(10)]
        public Vector3 regionCenter = Vector3.zero;

        /// <summary>
        /// 覆盖区域大小.
        /// </summary>
        public int regionSize = 1024;

        /// <summary>
        /// 最大mipmap等级
        /// </summary>
        [Space(10)]
        [Range(1, 8)]
        [SerializeField]
        private int maxMipLevel = 4;

        /// <summary>
        /// 单个Tile的尺�?
        /// </summary>
        [SerializeField]
        private TileResolution maxResolution = TileResolution._1024;

        /// <summary>
        /// Depth Bias
        /// </summary>
        [Space(10)]
        public bool shadowOn = true;

        /// <summary>
        /// padding
        /// </summary>
        [Range(0, 10)]
        public int padding = 5;

        /// <summary>
        /// Depth Bias
        /// </summary>
        [Range(0, 2)]
        public float bias = 0.4f;

        /// <summary>
        /// Normal Bias
        /// </summary>
        [Range(0, 3)]
        public float normalBias = 0.05f;

        /// <summary>
        /// PCSS enable
        /// </summary>
        [Space(10)]
        public bool pcssFilter = true;

        /// <summary>
        /// PCSS filter radius
        /// </summary>
        [Range(0, 5)]
        public float softnesss = 0.5f;

        /// <summary>
        /// PCSS filter near
        /// </summary>
        [Range(0, 5)]
        public float softnessNear = 0.2f;

        /// <summary>
        /// PCSS filter far
        /// </summary>
        [Range(0, 5)]
        public float softnessFar = 1.0f;

        /// <summary>
        /// Tile�?
        /// </summary>
        [Space(10)]
        [Min(1)]
        public int maxTilePool = 64;

        /// <summary>
        /// 一帧最多处理几�?        /// </summary>
        [Range(0, 10)]
        public int maxPageRequestLimit = 1;

        /// <summary>
        /// 更新间隔
        /// </summary>
        [Range(0, 10)]
        public int updateInterval = 5;

        /// <summary>
        /// 约束Page的加�?        /// </summary>
        [Min(0)]
        [HideInInspector]
        public int minPageRequestLevel = 0;

        /// <summary>
        /// 约束Page的加�?        /// </summary>
        [Min(0)]
        [HideInInspector]
        public int maxPageRequestLevel = short.MaxValue;

        /// <summary>
        /// 用于流式加载的数�?        /// </summary>
        [Space(10)]
        public VirtualShadowData shadowData;

        /// <summary>
        /// 灯光空间场景包围�?        /// </summary>
        public Bounds boundsInLightSpace { get => m_BoundsInLightSpace; }

        /// <summary>
        /// 灯光空间场景包围�?Mips)
        /// </summary>
        public Bounds[] boundsInLightSpaces { get => m_BoundsInLightSpaces; }

        /// <summary>
        /// 最大mipmap等级
        /// </summary>
        public int pageLevel { get => shadowData ? shadowData.maxMipLevel : maxMipLevel; }

        /// <summary>
        /// 页表尺寸.
        /// </summary>
        public int pageSize { get => 1 << (pageLevel - 1); }

        /// <summary>
        /// 当前激活的Page数量
        /// </summary>
        public int activePageCount { get { return m_VirtualTexture != null ? m_VirtualTexture.activeNodeCount : 0; } }

        /// <summary>
        /// 最大mipmap等级
        /// </summary>
        public TileResolution shadowSize { get => shadowData ? shadowData.maxResolution : maxResolution; }

        /// <summary>
        /// 用于检查光源变�?        /// </summary>
        public Vector3 lightDirection { get => shadowData ? shadowData.direction : m_LightDirection; }

        /// <summary>
        /// 用于检查光源变�?        /// </summary>
        public Matrix4x4 localToWorldMatrix { get => shadowData ? shadowData.localToWorldMatrix : m_LocalToWorldMatrix; }

        /// <summary>
        /// 用于检查光源变�?        /// </summary>
        public Matrix4x4 worldToLocalMatrix { get => shadowData ? shadowData.worldToLocalMatrix : m_WorldToLocalMatrix; }

        /// <summary>
        /// 页表对应的世界区�?
        /// </summary>
        public Rect regionRange
        {
            get
            {
                return new Rect(-regionSize / 2, -regionSize / 2, regionSize, regionSize);
            }
        }

        /// <summary>
        /// 页表对应的世界区�?
        /// </summary>
        public Matrix4x4[] lightProjecionMatrixs
        {
            get
            {
                return m_LightProjecionMatrixs;
            }
        }
        
        public GraphicsBuffer lightProjecionMatrixBuffer
        {
            get
            {
                return m_LightProjecionMatrixBuffer;
            }
        }

        public static bool useStructuredBuffer
        {
            get
            {
                GraphicsDeviceType deviceType = SystemInfo.graphicsDeviceType;
                return !Application.isMobilePlatform &&
                    (deviceType == GraphicsDeviceType.Direct3D11 ||
                     deviceType == GraphicsDeviceType.Direct3D12 ||
                     deviceType == GraphicsDeviceType.PlayStation4 ||
                     deviceType == GraphicsDeviceType.PlayStation5 ||
                     deviceType == GraphicsDeviceType.XboxOne ||
                     deviceType == GraphicsDeviceType.GameCoreXboxOne ||
                     deviceType == GraphicsDeviceType.GameCoreXboxSeries);
            }
        }

        public static int maxUniformBufferSize { get => 64; }

        // =====================================================================
        // 生命周期方法（合并自 VirtualShadowMapsQualitySwitcher.cs�?        // =====================================================================

        private void Awake()
        {
            m_Light = GetComponent<Light>();
            m_LightTransform = m_Light.transform;
        }

        private void OnEnable()
        {
            this.shadowOn = true;
            this.pcssFilter = true;
            this.minPageRequestLevel = 0;
            this.maxPageRequestLevel = this.pageLevel;

            this.m_VirtualShadowMapsKeywordFeature = GlobalKeyword.Create(m_VirtualShadowMapsKeyword);
            this.m_VirtualShadowMapsPcssKeywordFeature = GlobalKeyword.Create(m_VirtualShadowMapsPcssKeyword);

            if (m_CmdBufferBaker == null)
                this.m_CmdBufferBaker = new CommandBuffer { name = "CascadedOcclusion.Baker" };

            if (m_CmdBufferBeforeLighting == null)
            {
                this.m_CmdBufferBeforeLighting = new CommandBuffer { name = "CascadedOcclusion.BeforeLighting" };
                this.m_Light.AddCommandBuffer(LightEvent.BeforeScreenspaceMask, m_CmdBufferBeforeLighting);
            }

            // 不在 AfterScreenspaceMask 禁用关键字，因为�?deferred 管线�?            // 该事件在 Internal-DeferredShading 之前触发，会导致阴影消失�?            // 关键字清理由 VirtualShadowCamera �?CameraEvent.AfterForwardAlpha 处理�?
            this.m_FrameCameraCount = VirtualShadowManager.instance.cameras.Count;
            this.m_LightDirection = this.shadowData ? this.shadowData.direction : m_LightTransform.forward;
            this.m_LocalToWorldMatrix = this.shadowData ? this.shadowData.localToWorldMatrix : m_LightTransform.localToWorldMatrix;
            this.m_WorldToLocalMatrix = this.shadowData ? this.shadowData.worldToLocalMatrix : m_LightTransform.worldToLocalMatrix;

            SetQualityLevel(m_CurrentLevel);

            // Register first so cameras are available when Update() runs
            VirtualShadowManager.instance.Register(this);
        }

        private void OnDisable()
        {
            VirtualShadowManager.instance.Unregister(this);

            m_RequestPageJob.PurgeAll();
            m_CachedShadowData = null;
            shadowOn = false;

            if (m_CmdBufferBaker != null)
            {
                m_CmdBufferBaker.Release();
                m_CmdBufferBaker = null;
            }

            if (m_CmdBufferBeforeLighting != null)
            {
                m_Light.RemoveCommandBuffer(LightEvent.BeforeScreenspaceMask, m_CmdBufferBeforeLighting);
                m_CmdBufferBeforeLighting.Release();
                m_CmdBufferBeforeLighting = null;
            }
            
            if (m_CmdBufferAfterLighting != null)
            {
                try { m_Light.RemoveCommandBuffer(LightEvent.AfterScreenspaceMask, m_CmdBufferAfterLighting); } catch { }
                m_CmdBufferAfterLighting.Release();
                m_CmdBufferAfterLighting = null;
            }

            if (m_CachedShadowMaps.Count > 0)
            {
                foreach (var it in m_CachedShadowMaps)
                    RenderTexture.ReleaseTemporary(it.Value.Key);

                m_CachedShadowMaps.Clear();
            }

            if (m_VirtualTexture != null)
            {
                m_VirtualTexture.Dispose();
                m_VirtualTexture = null;
            }

            if (m_LightProjecionMatrixBuffer != null)
            {
                m_LightProjecionMatrixBuffer.Release();
                m_LightProjecionMatrixBuffer = null;
            }

            if (m_Baker != null)
            {
                m_Baker.Dispose();
                m_Baker = null;
            }

            Shader.DisableKeyword(m_VirtualShadowMapsKeywordFeature);
            Shader.DisableKeyword(m_VirtualShadowMapsPcssKeywordFeature);
            Shader.SetGlobalBuffer(ShaderConstants._VirtualShadowMatrixs_SSBO, NullBufferProvider.EmptyBuffer);
        }

        private void OnDestroy()
        {
            VirtualShadowManager.instance.Unregister(this);
        }

        /// <summary>
        /// 独立的画质等级设置方法，替代 OnTransformQuality 回调�?        /// 保留所有画质等级（Ultra/High/Medium/Low/XBox 系列/Off）的阴影参数配置逻辑�?        /// </summary>
        public void SetQualityLevel(OcclusionQualityPreset level)
        {
            m_CurrentLevel = level;

#if UNITY_GAMECORE_XBOXONE
            if (level != OcclusionQualityPreset.Off)
            {
                this.shadowOn = true;
                this.pcssFilter = false;
                this.minPageRequestLevel = Mathf.Min(2, Mathf.Max(this.pageLevel - 1, 0));
                this.maxPageRequestLevel = Mathf.Max(1, this.pageLevel);
                this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                this.maxTilePool += Math.Max(0, VirtualShadowManager.instance.cameras.Count - 1) * Mathf.Min(15, this.maxPageRequestLevel * 3);
                if (this.pageLevel == 1)
                    this.maxTilePool = 1;
                this.m_FrameCameraCount = VirtualShadowManager.instance.cameras.Count;
                VirtualShadowManager.instance.SetCameraDirty();
            }
            else
            {
                this.shadowOn = false;
                this.pcssFilter = false;
            }
#else
            switch (level)
            {
                case OcclusionQualityPreset.Ultra:
                    this.shadowOn = true;
                    this.pcssFilter = true;
                    this.minPageRequestLevel = 0;
                    this.maxPageRequestLevel = this.pageLevel;
                    this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                    this.maxTilePool += Math.Max(0, VirtualShadowManager.instance.cameras.Count - 1) * Mathf.Min(15, this.maxPageRequestLevel * 3);
                    if (this.pageLevel == 1)
                        this.maxTilePool = 1;
                    this.m_FrameCameraCount = VirtualShadowManager.instance.cameras.Count;
                    VirtualShadowManager.instance.SetCameraDirty();
                    break;
                case OcclusionQualityPreset.High:
                    this.shadowOn = true;
                    this.pcssFilter = false;
                    this.minPageRequestLevel = 0;
                    this.maxPageRequestLevel = this.pageLevel;
                    this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                    this.maxTilePool += Math.Max(0, VirtualShadowManager.instance.cameras.Count - 1) * Mathf.Min(15, this.maxPageRequestLevel * 3);
                    if (this.pageLevel == 1)
                        this.maxTilePool = 1;
                    this.m_FrameCameraCount = VirtualShadowManager.instance.cameras.Count;
                    VirtualShadowManager.instance.SetCameraDirty();
                    break;
                case OcclusionQualityPreset.Medium:
                    this.shadowOn = true;
                    this.pcssFilter = false;
                    this.minPageRequestLevel = Mathf.Min(1, Mathf.Max(this.pageLevel - 1, 0));
                    this.maxPageRequestLevel = Mathf.Max(1, this.pageLevel);
                    this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                    this.maxTilePool += Math.Max(0, VirtualShadowManager.instance.cameras.Count - 1) * Mathf.Min(15, this.maxPageRequestLevel * 3);
                    if (this.pageLevel == 1)
                        this.maxTilePool = 1;
                    this.m_FrameCameraCount = VirtualShadowManager.instance.cameras.Count;
                    VirtualShadowManager.instance.SetCameraDirty();
                    break;
                case OcclusionQualityPreset.Low:
                    this.shadowOn = true;
                    this.pcssFilter = false;
                    this.minPageRequestLevel = Mathf.Min(2, Mathf.Max(this.pageLevel - 1, 0));
                    this.maxPageRequestLevel = Mathf.Max(1, this.pageLevel);
                    this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                    this.maxTilePool += Math.Max(0, VirtualShadowManager.instance.cameras.Count - 1) * Mathf.Min(15, this.maxPageRequestLevel * 3);
                    if (this.pageLevel == 1)
                        this.maxTilePool = 1;
                    this.m_FrameCameraCount = VirtualShadowManager.instance.cameras.Count;
                    VirtualShadowManager.instance.SetCameraDirty();
                    break;
                case OcclusionQualityPreset.XBoxSeriesPerformance:
                case OcclusionQualityPreset.XBoxSeriesQuality:
                case OcclusionQualityPreset.XBoxOne:
                    this.shadowOn = true;
                    this.pcssFilter = false;
                    this.minPageRequestLevel = Mathf.Min(2, Mathf.Max(this.pageLevel - 1, 0));
                    this.maxPageRequestLevel = Mathf.Max(1, this.pageLevel);
                    this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                    this.maxTilePool += Math.Max(0, VirtualShadowManager.instance.cameras.Count - 1) * Mathf.Min(15, this.maxPageRequestLevel * 3);
                    if (this.pageLevel == 1)
                        this.maxTilePool = 1;
                    this.m_FrameCameraCount = VirtualShadowManager.instance.cameras.Count;
                    VirtualShadowManager.instance.SetCameraDirty();
                    break;
                case OcclusionQualityPreset.Off:
                    this.shadowOn = false;
                    this.pcssFilter = false;
                    this.minPageRequestLevel = 0;
                    this.maxPageRequestLevel = Mathf.Max(1, this.pageLevel);
                    this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                    if (this.pageLevel == 1)
                        this.maxTilePool = 1;
                    this.m_FrameCameraCount = VirtualShadowManager.instance.cameras.Count;
                    break;
                default:
                    this.shadowOn = false;
                    this.pcssFilter = false;
                    break;
            }
#endif
        }

        // =====================================================================
        // 核心更新方法（从�?VirtualShadowMaps.cs 复制，移除框架依赖）
        // =====================================================================

        public void Update()
        {
            if (this.shadowOn)
            {
                var shouldUpdate = ValidataCamera();
                var tilingCount = Mathf.CeilToInt(Mathf.Sqrt(this.maxTilePool));

                shouldUpdate |= ValidateVirtualTexture(tilingCount);
                shouldUpdate |= ValidateProjecionMatrixs(tilingCount);
                shouldUpdate |= ValidateProjecionMatrixBuffer(tilingCount);
                shouldUpdate |= ValidateLightSpaceBounds(shouldUpdate);

                if (shouldUpdate)
                {
                    foreach (var it in VirtualShadowManager.instance.cameras)
                    {
                        if (it.Value != null)
                            it.Value.SetDirty();
                    }
                }

                var cameraCount = VirtualShadowManager.instance.cameras.Count;
                if (cameraCount == 0)
                {
                    Debug.LogWarning("[VirtualShadowMaps] No cameras registered in VirtualShadowManager");
                }

                foreach (var it in VirtualShadowManager.instance.cameras)
                {
                    if (it.Value != null)
                        it.Value.UpdateSetting(this);
                }

                this.UpdateBaker();
                this.UpdateJob(maxPageRequestLimit);
                
                this.BuildCommandBuffers();
            }
        }

        private bool ValidataCamera()
        {
            var count = VirtualShadowManager.instance.cameras.Count;
            if (count != m_FrameCameraCount)
            {
                if (this.pageLevel == 1)
                {
                    this.maxTilePool = 1;
                    this.m_FrameCameraCount = count;
                    return true;
                }

#if UNITY_GAMECORE_XBOXONE
                if (m_CurrentLevel != OcclusionQualityPreset.Off)
                {
                    this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                    this.maxTilePool += Math.Max(0, VirtualShadowManager.instance.cameras.Count - 1) * Mathf.Min(15, this.maxPageRequestLevel * 3);
                }
#else
                switch (m_CurrentLevel)
                {
                    case OcclusionQualityPreset.Ultra:
                        this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                        this.maxTilePool += Math.Max(0, VirtualShadowManager.instance.cameras.Count - 1) * Mathf.Min(15, this.maxPageRequestLevel * 3);
                        break;
                    case OcclusionQualityPreset.High:
                        this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                        this.maxTilePool += Math.Max(0, VirtualShadowManager.instance.cameras.Count - 1) * Mathf.Min(15, this.maxPageRequestLevel * 3);
                        break;
                    case OcclusionQualityPreset.Medium:
                        this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                        this.maxTilePool += Math.Max(0, VirtualShadowManager.instance.cameras.Count - 1) * Mathf.Min(15, this.maxPageRequestLevel * 3);
                        break;
                    case OcclusionQualityPreset.Low:
                        this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                        this.maxTilePool += Math.Max(0, VirtualShadowManager.instance.cameras.Count - 1) * Mathf.Min(15, this.maxPageRequestLevel * 3);
                        break;
                    case OcclusionQualityPreset.XBoxSeriesPerformance:
                    case OcclusionQualityPreset.XBoxSeriesQuality:
                    case OcclusionQualityPreset.XBoxOne:
                        this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                        this.maxTilePool += Math.Max(0, VirtualShadowManager.instance.cameras.Count - 1) * Mathf.Min(15, this.maxPageRequestLevel * 3);
                        break;
                    case OcclusionQualityPreset.Off:
                        this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));
                        break;
                }
#endif
                m_FrameCameraCount = count;

                return true;
            }

            return false;
        }

        private bool ValidateVirtualTexture(int tilingCount)
        {
            if (m_VirtualTexture == null || tilingCount != m_VirtualTexture.layoutExtent)
            {
                if (m_VirtualTexture != null)
                    m_VirtualTexture.Dispose();

                var tileResolution = this.shadowSize;
                var pageSize = this.pageSize;
                var maxPageLevel = this.pageLevel;
                var textureFormat = new StreamingTileFormat[] { new StreamingTileFormat(RenderTextureFormat.Shadowmap, FilterMode.Bilinear) };

                m_VirtualTexture = new StreamingTileMap2D(tileResolution.AsPixelCount(), tilingCount, textureFormat, pageSize, maxPageLevel);

                return true;
            }

            return false;
        }

        private bool ValidateProjecionMatrixs(int tilingCount)
        {
            if (m_LightProjecionMatrixs == null || m_LightProjecionMatrixs != null && (useStructuredBuffer ? (tilingCount * tilingCount) != m_LightProjecionMatrixs.Length : m_LightProjecionMatrixs.Length != maxUniformBufferSize))
            {
                var lightProjecionMatrixs = useStructuredBuffer ? new Matrix4x4[tilingCount * tilingCount] : new Matrix4x4[maxUniformBufferSize];

                for (int i = 0; i < lightProjecionMatrixs.Length; i++)
                    lightProjecionMatrixs[i] = Matrix4x4.identity;

                if (m_LightProjecionMatrixs != null)
                {
                    var size = Mathf.Min(m_LightProjecionMatrixs.Length, lightProjecionMatrixs.Length);
                    for (int i = 0; i < size; i++)
                        lightProjecionMatrixs[i] = m_LightProjecionMatrixs[i];
                }

                m_LightProjecionMatrixs = lightProjecionMatrixs;

                return true;
            }

            return false;
        }

        private bool ValidateProjecionMatrixBuffer(int tilingCount)
        {
            if (useStructuredBuffer && (m_LightProjecionMatrixBuffer == null || (tilingCount * tilingCount) != m_LightProjecionMatrixBuffer.count))
            {
                if (m_LightProjecionMatrixBuffer != null)
                    m_LightProjecionMatrixBuffer.Dispose();

                m_LightProjecionMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_LightProjecionMatrixs.Length, Marshal.SizeOf<Matrix4x4>());
                m_LightProjecionMatrixBuffer.SetData(m_LightProjecionMatrixs);

                return true;
            }

            return false;
        }

        private bool ValidateLightSpaceBounds(bool force)
        {
            if (m_CachedShadowData != this.shadowData || force)
            {
                var maxMipLevel = this.pageLevel;
                var maxPageLevel = (int)Mathf.Log(this.pageSize, 2) + 1;
                var worldToLocalMatrix = this.worldToLocalMatrix;

                var bounds = this.shadowData != null ? this.shadowData.bounds : this.CalculateBoundingBox();

                m_BoundsInLightSpace = bounds.ProjectToLocalSpace(worldToLocalMatrix);
                m_BoundsInLightSpaces = new Bounds[maxMipLevel];

                for (var level = 0; level < maxPageLevel; level++)
                {
                    var boundsInLightSpace = m_BoundsInLightSpace;
                    var perSize = 1 << (maxPageLevel - level);

                    var size = boundsInLightSpace.size;
                    size.x /= perSize;
                    size.y /= perSize;

                    m_BoundsInLightSpaces[level] = new Bounds(boundsInLightSpace.center, size);
                }

                m_CachedShadowData = this.shadowData;

                return true;
            }

            return false;
        }

        public void BuildCommandBuffers()
        {
            if (m_CmdBufferBeforeLighting == null)
                return;

            var hasLookup = this.drawLookupMaterial != null;
            var hasVT = m_VirtualTexture != null;
            var camCount = VirtualShadowManager.instance.cameras.Count;
            var activeCount = hasVT ? m_VirtualTexture.activeNodeCount : 0;

            // 直接通过 Shader.SetGlobal* 设置全局变量�?            // 避免编辑器模式下 LightEvent CommandBuffer 的时序问�?            m_CmdBufferBeforeLighting.Clear();

            if (hasLookup && this.shadowOn && hasVT && camCount > 0 && activeCount > 0)
            {
                var tileTexture = this.FetchMosaicSurface();
                var tileSize = tileTexture.tileDimension;

                var pageSize = this.pageSize;
                var maxMipLevel = this.pageLevel;
                var localToWorldMatrix = this.localToWorldMatrix;
                var worldToLocalMatrix = this.worldToLocalMatrix;

                var lightSpaceBounds = this.boundsInLightSpace;
                var orthographicSize = Mathf.Max(this.boundsInLightSpaces[0].extents.x, this.boundsInLightSpaces[0].extents.y);
                var biasScale = VirtualShadowMapsUtilities.CalculateBiasScale(orthographicSize, tileSize);
                var regionRange = new Rect(lightSpaceBounds.min.x, lightSpaceBounds.min.y, lightSpaceBounds.size.x, lightSpaceBounds.size.y);
                var softness = this.softnesss * (1 << (maxMipLevel - 1));
                var softnessNear = this.softnessNear * softness;
                var softnessFar = this.softnessFar * softness;
                var lightDirection = localToWorldMatrix.MultiplyVector(Vector3.forward);

                // Set globals directly �?no CommandBuffer timing dependency
                Shader.EnableKeyword(m_VirtualShadowMapsKeywordFeature);
                Shader.SetKeyword(m_VirtualShadowMapsPcssKeywordFeature, this.pcssFilter);

                Shader.SetGlobalMatrix(ShaderConstants._VirtualShadowLightMatrix, worldToLocalMatrix);
                Shader.SetGlobalVector(ShaderConstants._VirtualShadowLightDirection, lightDirection);
                Shader.SetGlobalVector(ShaderConstants._VirtualShadowBiasParams, new Vector4(this.bias * biasScale, this.normalBias * biasScale, 0, 0));
                Shader.SetGlobalVector(ShaderConstants._VirtualShadowRegionParams, new Vector4(regionRange.x, regionRange.y, 1.0f / regionRange.width, 1.0f / regionRange.height));
                Shader.SetGlobalVector(ShaderConstants._VirtualShadowPageParams, new Vector4(pageSize, 1.0f / pageSize, maxMipLevel, 0));
                Shader.SetGlobalVector(ShaderConstants._VirtualShadowTileParams, new Vector4(tileTexture.tileDimension, tileTexture.layoutExtent, tileTexture.atlasResolution, 0));
                Shader.SetGlobalVector(ShaderConstants._VirtualShadowPcssParams, new Vector4(softness, softnessNear, softnessFar, 0));

                Shader.SetGlobalTexture(ShaderConstants._VirtualShadowTileTexture, this.FetchSurface());

                if (VirtualShadowMaps.useStructuredBuffer)
                    Shader.SetGlobalBuffer(ShaderConstants._VirtualShadowMatrixs_SSBO, this.lightProjecionMatrixBuffer);
                else
                    Shader.SetGlobalMatrixArray(ShaderConstants._VirtualShadowMatrixs, this.lightProjecionMatrixs);

                // Also populate the CommandBuffer for LightEvent as redundant path
                m_CmdBufferBeforeLighting.BeginSample("VirtualShadowMap.SetupLight");
                m_CmdBufferBeforeLighting.EnableKeyword(m_VirtualShadowMapsKeywordFeature);
                m_CmdBufferBeforeLighting.SetKeyword(m_VirtualShadowMapsPcssKeywordFeature, this.pcssFilter);
                m_CmdBufferBeforeLighting.SetGlobalMatrix(ShaderConstants._VirtualShadowLightMatrix, worldToLocalMatrix);
                m_CmdBufferBeforeLighting.SetGlobalVector(ShaderConstants._VirtualShadowLightDirection, lightDirection);
                m_CmdBufferBeforeLighting.SetGlobalVector(ShaderConstants._VirtualShadowBiasParams, new Vector4(this.bias * biasScale, this.normalBias * biasScale, 0, 0));
                m_CmdBufferBeforeLighting.SetGlobalVector(ShaderConstants._VirtualShadowRegionParams, new Vector4(regionRange.x, regionRange.y, 1.0f / regionRange.width, 1.0f / regionRange.height));
                m_CmdBufferBeforeLighting.SetGlobalVector(ShaderConstants._VirtualShadowPageParams, new Vector4(pageSize, 1.0f / pageSize, maxMipLevel, 0));
                m_CmdBufferBeforeLighting.SetGlobalVector(ShaderConstants._VirtualShadowTileParams, new Vector4(tileTexture.tileDimension, tileTexture.layoutExtent, tileTexture.atlasResolution, 0));
                m_CmdBufferBeforeLighting.SetGlobalVector(ShaderConstants._VirtualShadowPcssParams, new Vector4(softness, softnessNear, softnessFar, 0));
                m_CmdBufferBeforeLighting.SetGlobalTexture(ShaderConstants._VirtualShadowTileTexture, this.FetchSurface());
                if (VirtualShadowMaps.useStructuredBuffer)
                    m_CmdBufferBeforeLighting.SetGlobalBuffer(ShaderConstants._VirtualShadowMatrixs_SSBO, this.lightProjecionMatrixBuffer);
                else
                    m_CmdBufferBeforeLighting.SetGlobalMatrixArray(ShaderConstants._VirtualShadowMatrixs, this.lightProjecionMatrixs);
                m_CmdBufferBeforeLighting.EndSample("VirtualShadowMap.SetupLight");
            }
            else
            {
                Shader.DisableKeyword(m_VirtualShadowMapsKeywordFeature);
                m_CmdBufferBeforeLighting.DisableKeyword(m_VirtualShadowMapsKeywordFeature);
            }
        }

        public RenderTexture FetchSurface()
        {
            return m_VirtualTexture?.FetchSurface(0);
        }

        public int GetLightBufferSize()
        {
            return m_CmdBufferBeforeLighting != null ? m_CmdBufferBeforeLighting.sizeInBytes : -1;
        }

        public MosaicTexture FetchMosaicSurface()
        {
            return m_VirtualTexture?.FetchMosaicSurface();
        }

        /// <summary>
        /// 请求页表加载，如果没有找到页表返回null
        /// </summary>
        public TileNode SubmitRequest(int x, int y, int mip)
        {
            if (m_VirtualTexture != null)
                return m_VirtualTexture.SubmitRequest(x, y, mip);
            return null;
        }

        /// <summary>
        /// 移除一个页表加载请�?        /// </summary>
        public void CancelRequest(int x, int y, int mip)
        {
            if (m_VirtualTexture != null)
                m_VirtualTexture.CancelRequest(x, y, mip);
        }

        /// <summary>
        /// 获取加载请求数量
        /// </summary>
        public int PendingRequestCount()
        {
            return m_VirtualTexture != null ? m_VirtualTexture.PendingRequestCount() : 0;
        }

        public int FrameRequestCount()
        {
            return m_FrameRequestCount;
        }

        public void UpdateBaker()
        {
            if (!shadowOn || m_VirtualTexture == null)
                return;

            if (m_Baker == null)
            {
                if (this.shadowData == null)
                {
                    m_Baker = new VirtualShadowMapBaker(this);
                }
                else
                {
                    return;
                }
            }

            if (m_RequestPageJob.queuedCount > 0)
            {
                var it = m_RequestPageJob.PeekRequest();
                if (it != null)
                {
                    var req = it.Value;
                    var shadowMap = OcclusionTexturePool.Acquire(shadowSize);

                    m_Baker.Render(shadowMap, req.gridColumn, req.gridRow, req.lodTier);

                    m_CachedShadowMaps.Add(req, new KeyValuePair<RenderTexture, Matrix4x4>(shadowMap, m_Baker.lightProjecionMatrix));

                    m_RequestPageJob.CancelRequest(it.Value);
                }
            }
            else if (m_CachedShadowMaps.Count > 0)
            {
                m_CmdBufferBaker.Clear();
                m_CmdBufferBaker.SetRenderTarget(this.FetchSurface(), RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);

                foreach (var it in m_CachedShadowMaps)
                {
                    var req = it.Key;
                    var page = m_VirtualTexture.QueryTileNode(req.gridColumn, req.gridRow, req.lodTier);
                    if (page != null && page.isResident)
                    {
                        var shadowMap = it.Value.Key;
                        var lightProjecionMatrix = it.Value.Value;

                        m_LightProjecionMatrixs[page.tileSlot] = lightProjecionMatrix;

                        m_CmdBufferBaker.SetGlobalTexture(ShaderConstants._MainTex, shadowMap);
                        m_CmdBufferBaker.DrawMesh(StreamingTileUtilities.fullscreenMesh, m_VirtualTexture.ComputeTileTransform(page.tileSlot), this.drawTileMaterial, 0);
                    }
                }

                if (VirtualShadowMaps.useStructuredBuffer)
                    m_CmdBufferBaker.SetBufferData(m_LightProjecionMatrixBuffer, m_LightProjecionMatrixs);
                else
                    m_CmdBufferBaker.SetGlobalMatrixArray(ShaderConstants._VirtualShadowMatrixs, m_LightProjecionMatrixs);

                Graphics.ExecuteCommandBuffer(m_CmdBufferBaker);

                foreach (var it in m_CachedShadowMaps)
                    RenderTexture.ReleaseTemporary(it.Value.Key);

                m_CachedShadowMaps.Clear();

                m_LightDirection = m_Baker.lightDirection;
                m_LocalToWorldMatrix = m_Baker.localToWorldMatrix;
                m_WorldToLocalMatrix = m_Baker.worldToLocalMatrix;

                ValidateLightSpaceBounds(true);
            }
            else
            {
                var diffAngle = Mathf.Acos(Vector3.Dot(m_Baker.lightDirection, m_LightTransform.forward));
                if (diffAngle > updateInterval * Mathf.Deg2Rad)
                {
                    m_Baker.Dispose();
                    m_Baker = new VirtualShadowMapBaker(this);

                    m_RequestPageJob.PurgeAll();

                    foreach (var it in m_VirtualTexture.activeNodes)
                    {
                        var page = it.Value;
                        m_RequestPageJob.SubmitRequest(page.column, page.rank, page.mipLevel);
                    }

                    m_RequestPageJob.OrderRequests();
                }
            }
        }

        public void UpdateJob(int num, bool async = true)
        {
            if (!shadowOn || m_VirtualTexture == null)
            {
                m_FrameRequestCount = 0;
                return;
            }

            m_FrameRequestCount = Mathf.Min(num, m_VirtualTexture.PendingRequestCount());

            if (m_FrameRequestCount == 0 && m_VirtualTexture.activeNodeCount == 0)
            {
                Debug.LogWarning("[VirtualShadowMaps] No requests and no active pages");
            }

            if (m_FrameRequestCount > 0)
            {
                m_VirtualTexture.OrderRequests();

                for (int i = 0; i < m_FrameRequestCount; i++)
                {
                    var req = m_VirtualTexture.PeekRequest();
                    if (req != null)
                    {
                        try
                        {
                            this.ProcessRequest(req.Value, async);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[VirtualShadowMaps] ProcessRequest failed for page ({req.Value.gridColumn},{req.Value.gridRow},mip{req.Value.lodTier}): {e}");
                            this.CancelRequest(req.Value.gridColumn, req.Value.gridRow, req.Value.lodTier);
                        }
                    }
                }
            }
        }

        private void ProcessRequest(TileRequestData req, bool async = true)
        {
            if (this.shadowData != null)
            {
                var key = this.shadowData.GetTexAsset(req);
                if (key == null || key.Length <= 0)
                {
                    this.CancelRequest(req.gridColumn, req.gridRow, req.lodTier);
                    return;
                }

#if UNITY_EDITOR
                // In editor, load directly via AssetDatabase to avoid Addressables dependency
                var assetPath = AssetDatabase.GUIDToAssetPath(key);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (texture != null)
                    {
                        if (m_VirtualTexture != null)
                        {
                            var page = m_VirtualTexture.QueryTileNode(req.gridColumn, req.gridRow, req.lodTier);
                            if (page != null && page.payload.queuedRequest.Equals(req))
                            {
                                var tile = m_VirtualTexture.AllocateTile();
                                if (this.OnBeginTileLoading(tile, texture, this.shadowData.GetMatrix(req)))
                                {
                                    m_VirtualTexture.EnableTileNode(tile, page);
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[VirtualShadowMaps] Failed to load texture at {assetPath}");
                        this.CancelRequest(req.gridColumn, req.gridRow, req.lodTier);
                    }
                }
                else
                {
                    Debug.LogWarning($"[VirtualShadowMaps] No asset found for GUID={key}");
                    this.CancelRequest(req.gridColumn, req.gridRow, req.lodTier);
                }
#else
                // In builds, use Addressables
                var handle = Addressables.LoadAssetAsync<Texture2D>(key);
                if (!handle.IsValid())
                {
                    Addressables.Release(handle);
                    this.CancelRequest(req.gridColumn, req.gridRow, req.lodTier);
                    return;
                }

                if (async)
                {
                    handle.Completed += (AsyncOperationHandle<Texture2D> handleOp) =>
                    {
                        if (handleOp.Status == AsyncOperationStatus.Succeeded && handleOp.Result != null)
                        {
                            if (m_VirtualTexture != null)
                            {
                                var page = m_VirtualTexture.QueryTileNode(req.gridColumn, req.gridRow, req.lodTier);
                                if (page != null && page.payload.queuedRequest.Equals(req))
                                {
                                    var tile = m_VirtualTexture.AllocateTile();
                                    if (this.OnBeginTileLoading(tile, handleOp.Result, this.shadowData.GetMatrix(req)))
                                    {
                                        m_VirtualTexture.EnableTileNode(tile, page);
                                    }
                                }
                            }

                            Resources.UnloadAsset(handleOp.Result);
                        }
                        else
                        {
                            this.CancelRequest(req.gridColumn, req.gridRow, req.lodTier);
                        }

                        Addressables.Release(handleOp);
                    };
                }
                else
                {
                    var texture = handle.WaitForCompletion();
                    if (texture != null)
                    {
                        if (m_VirtualTexture != null)
                        {
                            var page = m_VirtualTexture.QueryTileNode(req.gridColumn, req.gridRow, req.lodTier);
                            if (page != null && page.payload.queuedRequest.Equals(req))
                            {
                                var tile = m_VirtualTexture.AllocateTile();
                                if (this.OnBeginTileLoading(tile, texture, this.shadowData.GetMatrix(req)))
                                {
                                    m_VirtualTexture.EnableTileNode(tile, page);
                                }
                            }
                        }

                        Resources.UnloadAsset(texture);
                    }
                    else
                    {
                        this.CancelRequest(req.gridColumn, req.gridRow, req.lodTier);
                    }

                    Addressables.Release(handle);
                }
#endif
            }
            else
            {
                var page = m_VirtualTexture.QueryTileNode(req.gridColumn, req.gridRow, req.lodTier);
                if (page != null && page.payload.queuedRequest.Equals(req))
                {
                    var shadowMap = OcclusionTexturePool.Acquire(shadowSize);

                    if (m_Baker == null)
                        m_Baker = new VirtualShadowMapBaker(this);

                    if (m_Baker.Render(shadowMap, req.gridColumn, req.gridRow, req.lodTier) != null)
                    {
                        var tile = m_VirtualTexture.AllocateTile();

                        if (this.OnBeginTileLoading(tile, shadowMap, m_Baker.lightProjecionMatrix))
                        {
                            m_VirtualTexture.EnableTileNode(tile, page);
                        }
                    }

                    OcclusionTexturePool.Reclaim(shadowMap);
                }
            }
        }

        private bool OnBeginTileLoading(int tile, Texture texture, Matrix4x4 lightProjection)
        {
            m_LightProjecionMatrixs[tile] = lightProjection;

            m_CmdBufferBaker.Clear();

            if (VirtualShadowMaps.useStructuredBuffer)
                m_CmdBufferBaker.SetBufferData(m_LightProjecionMatrixBuffer, m_LightProjecionMatrixs);
            else
                m_CmdBufferBaker.SetGlobalMatrixArray(ShaderConstants._VirtualShadowMatrixs, m_LightProjecionMatrixs);

            m_CmdBufferBaker.SetGlobalTexture(ShaderConstants._MainTex, texture);
            m_CmdBufferBaker.SetRenderTarget(this.FetchSurface(), RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            m_CmdBufferBaker.DrawMesh(StreamingTileUtilities.fullscreenMesh, m_VirtualTexture.ComputeTileTransform(tile), this.drawTileMaterial, 0);

            Graphics.ExecuteCommandBuffer(m_CmdBufferBaker);

            return true;
        }

        public Transform GetLightTransform()
        {
            return m_LightTransform;
        }

        public void CalculateRegionBox()
        {
            var bounds = VirtualShadowManager.instance.GetBounds();
            this.regionCenter = bounds.center;
            this.regionSize = Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Max(bounds.size.x, bounds.size.z)));
        }

        public Bounds CalculateBoundingBox()
        {
            var bounds = VirtualShadowManager.instance.GetBounds();
            var worldSize = new Vector3(this.regionRange.size.x, bounds.size.y, this.regionRange.size.y);

            return new Bounds(regionCenter, worldSize);
        }

        static class ShaderConstants
        {
            public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
            public static readonly int _TiledIndex = Shader.PropertyToID("_TiledIndex");

            public static readonly int _VirtualShadowMatrixs = Shader.PropertyToID("_CascadedOcclusionMatrices");
            public static readonly int _VirtualShadowMatrixs_SSBO = Shader.PropertyToID("_CascadedOcclusionMatrices_SSBO");
            public static readonly int _VirtualShadowLightMatrix = Shader.PropertyToID("_CascadedOcclusionLightMatrix");
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

#if UNITY_EDITOR
        public void Reset()
        {
            this.CalculateRegionBox();

            this.m_CachedShadowData = null;
            this.m_Light = GetComponent<Light>();
            this.m_LightTransform = m_Light.transform;
            this.m_FrameRequestCount = 0;
            this.m_FrameCameraCount = VirtualShadowManager.instance.cameras.Count;

            this.maxResolution = TileResolution._1024;
            this.maxMipLevel = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(regionSize, 2f) - 3f), 1, 8);

            this.bias = 0.05f;
            this.normalBias = 0.4f;
            this.minPageRequestLevel = 0;
            this.maxPageRequestLevel = maxMipLevel;
            this.shadowData = null;
            this.maxTilePool = Mathf.Max(1, Mathf.FloorToInt(Mathf.Pow(this.maxPageRequestLevel - this.minPageRequestLevel, 2.0f)));

            var castMaterialPath = AssetDatabase.GUIDToAssetPath("d61a0d0a851619f4591c84f677daffd2");
            var drawTileMaterialPath = AssetDatabase.GUIDToAssetPath("59c6e3303ee192e44887a15d4d616a6b");
            var drawLookupMaterialPath = AssetDatabase.GUIDToAssetPath("cecbabc9a0f16f7499aff705988df6e7");
            var minMaxDepthPath = AssetDatabase.GUIDToAssetPath("fb868c4339761cb41ba176a2fd7bf8dd");

            this.castMaterial = AssetDatabase.LoadAssetAtPath<Material>(castMaterialPath);
            this.drawTileMaterial = AssetDatabase.LoadAssetAtPath<Material>(drawTileMaterialPath);
            this.drawLookupMaterial = AssetDatabase.LoadAssetAtPath<Material>(drawLookupMaterialPath);
            this.minMaxDepthCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(minMaxDepthPath);

            EditorUtility.SetDirty(this);
        }

        public void OnValidate()
        {
            if (m_Light == null || m_LightTransform == null)
            {
                m_Light = GetComponent<Light>();
                if (m_Light != null)
                    m_LightTransform = m_Light.transform;
            }

            if (this.castMaterial == null)
            {
                var castMaterialPath = AssetDatabase.GUIDToAssetPath("d61a0d0a851619f4591c84f677daffd2");
                this.castMaterial = AssetDatabase.LoadAssetAtPath<Material>(castMaterialPath);
            }

            if (this.drawTileMaterial == null)
            {
                var drawTileMaterialPath = AssetDatabase.GUIDToAssetPath("59c6e3303ee192e44887a15d4d616a6b");
                this.drawTileMaterial = AssetDatabase.LoadAssetAtPath<Material>(drawTileMaterialPath);
            }

            if (this.drawLookupMaterial == null)
            {
                var drawLookupMaterialPath = AssetDatabase.GUIDToAssetPath("cecbabc9a0f16f7499aff705988df6e7");
                this.drawLookupMaterial = AssetDatabase.LoadAssetAtPath<Material>(drawLookupMaterialPath);
            }

            if (this.minMaxDepthCompute == null)
            {
                var minMaxDepthPath = AssetDatabase.GUIDToAssetPath("fb868c4339761cb41ba176a2fd7bf8dd");
                this.minMaxDepthCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(minMaxDepthPath);
            }
        }

        public void OnDrawGizmos()
        {
            if (Selection.activeGameObject == this.gameObject)
            {
                if (shadowData != null)
                {
                    Gizmos.matrix = Matrix4x4.identity;
                    Gizmos.color = new Color(0.0f, 0.5f, 1.0f, 0.2f);
                    Gizmos.DrawCube(shadowData.bounds.center, shadowData.bounds.size);
                    Gizmos.color = new Color(0.0f, 0.5f, 1.0f, 0.5f);
                    Gizmos.DrawWireCube(shadowData.bounds.center, shadowData.bounds.size);
                }
                else
                {
                    var bounds = CalculateBoundingBox();
                    Gizmos.matrix = Matrix4x4.identity;
                    Gizmos.color = new Color(0.0f, 0.5f, 1.0f, 0.2f);
                    Gizmos.DrawCube(bounds.center, bounds.size);
                    Gizmos.color = new Color(0.0f, 0.5f, 1.0f, 0.5f);
                    Gizmos.DrawWireCube(bounds.center, bounds.size);
                }
            }
        }
#endif
    }
}
