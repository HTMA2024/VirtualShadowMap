# ExtractedVirtualShadowMap — 虚拟阴影贴图模块

从 Recreate.Effect 框架中提取的独立虚拟阴影贴图模块。包含 VirtualShadowMap（阴影贴图核心）和 VirtualTexture（虚拟纹理基础设施）两个紧密耦合的子系统。通过虚拟纹理技术实现大范围高精度阴影，运行于 Unity Built-In 渲染管线。

## 功能概述

- 基于虚拟纹理的大范围阴影贴图
- 页表请求调度和 LRU 缓存管理
- 多画质等级支持（Ultra/High/Medium/Low/XBox 系列/Off）
- Light CommandBuffer 注入（BeforeScreenspaceMask / AfterScreenspaceMask）
- Camera CommandBuffer 注入（BeforeLighting / BeforeForwardOpaque / BeforeForwardAlpha / AfterForwardAlpha）
- Addressables 异步资源加载
- 阴影体积（Volume）和投射器（Caster）管理
- 运行时阴影深度图烘焙

## 目录结构

```
ExtractedVirtualShadowMap/
├── ExtractedVirtualShadowMap.asmdef       # Runtime Assembly Definition
├── Editor/
│   ├── ExtractedVirtualShadowMap.Editor.asmdef  # Editor Assembly Definition
│   └── Scripts/Editor/
│       ├── VirtualShadowMapsEditor.cs     # VirtualShadowMaps Inspector
│       ├── VirtualShadowCameraEditor.cs   # VirtualShadowCamera Inspector
│       └── VirtualShadowVolumeEditor.cs   # VirtualShadowVolume Inspector
├── Scripts/
│   ├── VirtualShadowMaps.cs              # 主组件（挂载于方向光）
│   ├── VirtualShadowCamera.cs            # 相机组件（页表请求 + CommandBuffer）
│   ├── VirtualShadowManager.cs           # 管理器单例（注册/注销）
│   ├── VirtualShadowData.cs              # 阴影数据 ScriptableObject
│   ├── VirtualShadowMapBaker.cs          # 运行时阴影烘焙器
│   ├── VirtualShadowVolume.cs            # 阴影体积组件
│   ├── VirtualShadowCaster.cs            # 阴影投射标记组件
│   ├── VirtualShadowMapsUtilities.cs     # 工具类
│   ├── VSMQualityLevel.cs                # 画质等级枚举
│   ├── EmptyBufferHelper.cs              # 空 GraphicsBuffer 辅助（替代 DX12Trick）
│   ├── BoundsExtension.cs                # 包围盒扩展方法
│   ├── MinMaxDepthPass.cs                # 深度 Pass
│   ├── ScaleFactor.cs                    # 缩放因子
│   ├── ShadowResolution.cs               # 阴影分辨率
│   ├── ShadowTexturePool.cs              # 阴影纹理池
│   ├── TextureExtension.cs               # 纹理扩展方法
│   └── VirtualTexture/                   # 虚拟纹理子系统
│       ├── VirtualTexture2D.cs           # 虚拟纹理核心类
│       ├── TiledTexture.cs               # 平铺纹理管理
│       ├── LookupTexture.cs              # 查找纹理
│       ├── Page.cs                       # 页表项
│       ├── PageTable.cs                  # 页表管理器
│       ├── PageLevelTable.cs             # 页表层级
│       ├── PagePayload.cs                # 页面负载
│       ├── LruCache.cs                   # LRU 缓存
│       ├── RequestPageData.cs            # 页表请求数据
│       ├── RequestPageDataJob.cs         # 页表请求任务队列
│       ├── SerializableDictionary.cs     # 可序列化字典
│       ├── VirtualTextureFormat.cs       # 纹理格式
│       └── VirtualTextureUtilities.cs    # 工具类
├── Shaders/
│   ├── VirtualShadowMaps.hlsl            # 阴影采样 Shader 库
│   ├── MinMaxDepth.compute               # 深度计算 Compute Shader
│   ├── StaticShadowCaster.shader         # 静态阴影投射 Shader
│   └── VirtualTexture/                   # 虚拟纹理 Shader
└── Materials/
    ├── StaticShadowCaster.mat            # 静态阴影投射材质
    ├── VirtualShadowMaps PCF3x3.asset    # PCF 3x3 滤波配置
    └── VirtualShadowMaps PCF5x5.asset    # PCF 5x5 滤波配置
```

## 命名空间

`VirtualTexture`

## 依赖

| 依赖 | 用途 |
|------|------|
| Unity.Addressables | 异步资源加载（阴影纹理） |
| Unity.ResourceManager | Addressables 资源管理 |

## 使用说明

### 1. 导入模块

将 `ExtractedVirtualShadowMap/` 目录放置于 Unity 项目根目录（与 `Assets/` 同级）。确保项目中已安装 Addressables 包。

### 2. 配置方向光

在场景的方向光（Directional Light）上添加 `VirtualShadowMaps` 组件：

```
1. 选择场景中的 Directional Light
2. 添加 VirtualShadowMaps 组件
3. 配置 VirtualShadowData（阴影数据资产）
4. 设置画质等级
```

### 3. 配置相机

在渲染相机上添加 `VirtualShadowCamera` 组件：

```
1. 选择渲染相机
2. 添加 VirtualShadowCamera 组件
3. 组件在 OnEnable 时自动注入 CommandBuffer 到 4 个 CameraEvent
```

### 4. 画质等级控制

```csharp
// 运行时切换画质等级
virtualShadowMaps.SetQualityLevel(VSMQualityLevel.High);

// 可用等级：Ultra, High, Medium, Low,
//          XBoxSeriesPerformance, XBoxSeriesQuality, XBoxOne, Off
```

### 5. 阴影体积

在需要投射阴影的区域添加 `VirtualShadowVolume` 组件，在需要投射阴影的物体上添加 `VirtualShadowCaster` 组件。

### 6. EmptyBufferHelper

模块使用 `EmptyBufferHelper` 替代原 `DX12Trick`，在禁用阴影时提供空的 GraphicsBuffer：

```csharp
// 自动管理，无需手动调用
// 如需手动释放：
EmptyBufferHelper.Release();
```

## 已移除的依赖

- `QualitySwitchEffect`（→ MonoBehaviour + 独立 SetQualityLevel）
- `Recreate.Utilities`（DX12Trick → EmptyBufferHelper）
- `VirtualShadowFeature`（URP ScriptableRendererFeature → Built-In CommandBuffer）
- `VirtualShadowMapsQualitySwitcher.cs`（partial class → 合并到 VirtualShadowMaps.cs）
