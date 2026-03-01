# ExtractedVirtualShadowMap — 技术要点深度解析

> 面试准备文档：涵盖虚拟阴影贴图系统的所有核心知识点

## 1. 虚拟纹理（Virtual Texture）核心概念

### 1.1 什么是虚拟纹理

虚拟纹理是一种按需加载的纹理管理技术，将超大纹理分割为固定大小的 Tile（瓦片），只加载当前可见区域的 Tile。

**面试要点：**
- 类比操作系统的虚拟内存：页表（Page Table）映射虚拟地址到物理地址
- 在阴影贴图中的应用：大范围场景的阴影不可能用单张高分辨率纹理覆盖，虚拟纹理按需加载可见区域的阴影 Tile
- 核心组件：
  - `VirtualTexture2D`：虚拟纹理核心类，管理页表和 Tile 缓存
  - `PageTable`：页表管理器，维护虚拟页到物理 Tile 的映射
  - `TiledTexture`：物理 Tile 纹理池
  - `LookupTexture`：查找纹理，GPU 用于快速定位 Tile
  - `LruCache`：LRU 缓存，管理 Tile 的淘汰策略

### 1.2 页表系统

```
虚拟纹理坐标 (x, y, mip)
    ↓ PageTable 查找
物理 Tile 位置 (tileX, tileY)
    ↓ TiledTexture 采样
阴影深度值
```

**面试要点：**
- `Page`：页表项，包含虚拟坐标 (x, y, mip) 和物理 Tile 位置
- `PageLevelTable`：按 mip 级别组织的页表层级
- `RequestPageData`：页表请求数据，当相机需要某个区域的阴影时发起请求
- `RequestPageDataJob`：请求任务队列，管理异步加载的优先级和去重
- mip 级别：类似 Mipmap，远处使用低分辨率 Tile，近处使用高分辨率 Tile

### 1.3 LRU 缓存

```csharp
public class LruCache
{
    // Least Recently Used 缓存
    // 当 Tile 池满时，淘汰最久未使用的 Tile
}
```

**面试要点：**
- LRU 算法：每次访问 Tile 时将其移到队列头部，淘汰时从队列尾部移除
- 时间复杂度：查找 O(1)（HashMap），插入/删除 O(1)（双向链表）
- `maxTilePool`：Tile 池大小上限，决定同时缓存的 Tile 数量
- 缓存命中率：影响阴影加载的流畅度，池太小会导致频繁加载/卸载（thrashing）

## 2. Light CommandBuffer 注入

### 2.1 方向光 CommandBuffer

```csharp
private CommandBuffer m_CmdBufferBaker;           // 烘焙阴影深度
private CommandBuffer m_CmdBufferBeforeLighting;   // 光照前设置
private CommandBuffer m_CmdBufferAfterLighting;    // 光照后清理
```

注入点：
- `LightEvent.BeforeScreenspaceMask`：在屏幕空间阴影遮罩计算之前
- `LightEvent.AfterScreenspaceMask`：在屏幕空间阴影遮罩计算之后

**面试要点：**
- `Light.AddCommandBuffer(LightEvent, CommandBuffer)`：向光源的渲染管线注入命令
- `BeforeScreenspaceMask`：在此注入虚拟阴影贴图的采样逻辑，替代 Unity 默认的阴影计算
- `AfterScreenspaceMask`：清理临时资源，恢复渲染状态
- 与 Camera CommandBuffer 的区别：Light CB 在光照计算阶段执行，Camera CB 在相机渲染阶段执行

## 3. Camera CommandBuffer 注入

### 3.1 VirtualShadowCamera 的 4 个注入点

```csharp
// OnEnable 中注入
camera.AddCommandBuffer(CameraEvent.BeforeLighting, ...);
camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, ...);
camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, ...);
camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, ...);
```

**面试要点：**
- `BeforeLighting`：设置虚拟阴影贴图的全局纹理和参数
- `BeforeForwardOpaque`：不透明物体渲染前更新阴影状态
- `BeforeForwardAlpha`：透明物体渲染前更新阴影状态
- `AfterForwardAlpha`：所有前向渲染完成后清理
- 4 个注入点的必要性：不同渲染阶段需要不同的阴影参数配置

### 3.2 GlobalKeyword 管理

```csharp
private GlobalKeyword m_VirtualShadowMapsKeywordFeature;
private GlobalKeyword m_VirtualShadowMapsPcssKeywordFeature;

// 启用/禁用全局 Shader 关键字
Shader.EnableKeyword(m_VirtualShadowMapsKeywordFeature);
Shader.DisableKeyword(m_VirtualShadowMapsPcssKeywordFeature);
```

**面试要点：**
- `GlobalKeyword`：Unity 2021+ 的新 API，替代字符串形式的 `Shader.EnableKeyword("_KEYWORD")`
- 性能优势：GlobalKeyword 是预编译的，避免每次调用时的字符串哈希计算
- `_VIRTUAL_SHADOW_MAPS`：主开关关键字
- `_VIRTUAL_SHADOW_MAPS_PCSS`：PCSS 软阴影开关关键字
- Shader 变体：每个关键字组合生成一个 Shader 变体，关键字过多会导致变体爆炸

## 4. GraphicsBuffer 使用

### 4.1 StructuredBuffer vs UniformBuffer

```csharp
private GraphicsBuffer m_LightProjecionMatrixBuffer;
// GraphicsBuffer.Target.Structured → StructuredBuffer<float4x4>
```

**面试要点：**
- `GraphicsBuffer`（Unity 2020+）：替代 `ComputeBuffer`，支持更多 Target 类型
- `Target.Structured`：对应 Shader 中的 `StructuredBuffer<T>`，支持任意大小和随机访问
- `Target.Constant`：对应 Shader 中的 `cbuffer`，大小限制 64KB，适合少量全局参数
- 投影矩阵缓冲：存储所有级联的光源投影矩阵，Shader 中按索引读取
- 内存管理：`GraphicsBuffer` 是非托管资源，必须手动 `Release()`

### 4.2 EmptyBufferHelper

```csharp
public static class EmptyBufferHelper
{
    private static GraphicsBuffer s_EmptyBuffer;
    
    public static GraphicsBuffer EmptyBuffer
    {
        get
        {
            if (s_EmptyBuffer == null || !s_EmptyBuffer.IsValid())
                s_EmptyBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(float));
            return s_EmptyBuffer;
        }
    }
    
    public static void Release()
    {
        if (s_EmptyBuffer != null)
        {
            s_EmptyBuffer.Release();
            s_EmptyBuffer = null;
        }
    }
}
```

**面试要点：**
- 懒加载单例模式：首次访问时创建，之后复用
- `IsValid()` 检查：GraphicsBuffer 可能在设备丢失（Device Lost）后变为无效
- 自动重建：`null` 或 `!IsValid()` 时自动创建新 Buffer
- 用途：当虚拟阴影关闭时，Shader 仍需要绑定一个有效的 Buffer（否则报错），EmptyBuffer 提供占位
- 替代原 `DX12Trick.EmptyBuffer`：解耦对 Recreate.Utilities 的依赖
- 属性测试验证：访问-释放-再访问序列始终返回有效 Buffer

## 5. 多画质等级系统

### 5.1 VSMQualityLevel 枚举

```csharp
public enum VSMQualityLevel
{
    Ultra, High, Medium, Low,
    XBoxSeriesPerformance, XBoxSeriesQuality, XBoxOne,
    Off
}
```

**面试要点：**
- 平台特定等级：Xbox Series 有 Performance/Quality 两档，Xbox One 单独一档
- `Off`：完全关闭虚拟阴影，使用 EmptyBuffer 占位
- `SetQualityLevel`：运行时切换，需要重建虚拟纹理和 CommandBuffer

### 5.2 画质参数映射

不同画质等级影响：
- `maxMipLevel`：最大 mip 级别（Ultra=4, Low=2）
- `maxResolution`：单个 Tile 分辨率（Ultra=1024, Low=256）
- `maxTilePool`：Tile 池大小（Ultra=64, Low=16）
- `pcssFilter`：是否启用 PCSS 软阴影（Low 以下关闭）

**面试要点：**
- 画质等级的设计原则：高画质增加 Tile 数量和分辨率，低画质减少以换取性能
- 运行时切换的开销：需要释放旧的虚拟纹理并重新创建，有一帧的阴影闪烁

## 6. PCSS 软阴影

### 6.1 Percentage Closer Soft Shadows

```csharp
public bool pcssFilter = true;
public float softnesss = 0.5f;      // 柔软度
public float softnessNear = 0.2f;   // 近处柔软度
public float softnessFar = 1.0f;    // 远处柔软度
```

**面试要点：**
- PCSS 原理：根据遮挡物到接收面的距离动态调整阴影模糊半径
- 近处阴影锐利、远处阴影柔和：模拟真实世界的半影效果
- 与 PCF 的区别：PCF（Percentage Closer Filtering）使用固定核大小，PCSS 根据距离自适应
- 性能开销：PCSS 需要两次采样（一次搜索遮挡物，一次滤波），比 PCF 贵约 2 倍
- `_VIRTUAL_SHADOW_MAPS_PCSS` 关键字：通过 Shader 变体控制是否启用

## 7. Addressables 异步加载

### 7.1 阴影纹理的流式加载

```csharp
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

private void ProcessRequest(RequestPageData req, bool async = true)
{
    // 通过 Addressables 异步加载阴影深度纹理
    // 加载完成后写入 TiledTexture
}
```

**面试要点：**
- Addressables 系统：Unity 的资源管理框架，支持按需加载、远程加载、引用计数
- 异步加载：不阻塞主线程，加载完成后回调处理
- `AsyncOperationHandle<T>`：异步操作句柄，可查询状态、注册回调
- 引用计数：每次 `LoadAssetAsync` 增加引用，`Release` 减少引用，归零时卸载
- 与 `Resources.Load` 的区别：Addressables 支持远程加载、依赖管理、内存管理

### 7.2 VirtualShadowData

```csharp
private VirtualShadowData m_CachedShadowData;
// ScriptableObject，存储预烘焙的阴影数据
// 包含 Tile 的 Addressable 地址映射
```

**面试要点：**
- `ScriptableObject`：Unity 的数据资产类型，不依赖场景中的 GameObject
- 预烘焙数据：离线烘焙阴影深度图，运行时按需加载
- 地址映射：虚拟坐标 (x, y, mip) → Addressable 地址 → 阴影纹理

## 8. 运行时阴影烘焙

### 8.1 VirtualShadowMapBaker

```csharp
private VirtualShadowMapBaker m_Baker;

public void UpdateBaker()
{
    // 对动态物体实时烘焙阴影深度图
    // 使用 CommandBuffer 渲染到 RenderTexture
}
```

**面试要点：**
- 静态阴影 vs 动态阴影：静态物体使用预烘焙纹理（Addressables 加载），动态物体实时烘焙
- `castMaterial`：阴影投射材质（`StaticShadowCaster.shader`），只输出深度
- 烘焙流程：设置光源视角的 VP 矩阵 → 渲染场景到深度 RT → 写入 TiledTexture
- 每帧预算：`maxRequestPerFrame` 限制每帧处理的请求数，避免帧率波动

## 9. 投影矩阵与级联

### 9.1 级联阴影（Cascaded Shadow Maps）

```csharp
private Matrix4x4[] m_LightProjecionMatrixs;
// 每个级联一个投影矩阵
```

**面试要点：**
- 级联阴影原理：将相机视锥体分割为多个区域（级联），每个区域使用不同分辨率的阴影贴图
- 近处高分辨率、远处低分辨率：与虚拟纹理的 mip 级别对应
- `ValidateProjecionMatrixs`：验证投影矩阵是否需要更新（光源方向变化时）
- `ValidateLightSpaceBounds`：计算光源空间的场景包围盒，确定阴影覆盖范围

### 9.2 光源空间变换

```csharp
private Matrix4x4 m_LocalToWorldMatrix;
private Matrix4x4 m_WorldToLocalMatrix;
```

**面试要点：**
- 光源空间：以方向光的方向为 Z 轴的坐标系
- 世界坐标 → 光源空间：用于计算阴影深度
- 光源空间 → 世界坐标：用于将阴影投影回场景

## 10. 深度计算

### 10.1 MinMaxDepth Compute Shader

```csharp
public ComputeShader minMaxDepthCompute;
```

**面试要点：**
- Compute Shader：在 GPU 上运行的通用计算程序，不参与渲染管线
- Min/Max Depth：计算场景的最小/最大深度值，用于优化阴影范围
- 并行归约（Parallel Reduction）：GPU 上高效计算最值的标准算法
- 用途：收紧阴影的近/远平面，提高深度精度

## 11. 组件关系

```
VirtualShadowMaps（挂载于 Directional Light）
  ├── 管理 VirtualTexture2D（虚拟纹理）
  ├── 管理 VirtualShadowMapBaker（运行时烘焙）
  ├── 注入 Light CommandBuffer
  └── 注册到 VirtualShadowManager（全局管理器）

VirtualShadowCamera（挂载于 Camera）
  ├── 发起页表请求（UpdateRequest）
  ├── 注入 Camera CommandBuffer（4 个注入点）
  └── 设置全局 Shader 参数

VirtualShadowVolume（挂载于场景物体）
  └── 定义阴影覆盖区域

VirtualShadowCaster（挂载于场景物体）
  └── 标记为阴影投射者
```

## 12. 关键面试问答

### Q: 虚拟阴影贴图 vs 传统级联阴影贴图？
A: 传统 CSM 使用固定数量的级联（通常 4 个），每个级联一张完整纹理。虚拟阴影贴图将整个阴影范围视为一张超大纹理，按需加载可见区域的 Tile。优势：更高的有效分辨率、更灵活的内存使用。劣势：实现复杂、有加载延迟。

### Q: LRU 缓存的替代方案？
A: LFU（Least Frequently Used）按访问频率淘汰，适合热点数据稳定的场景。ARC（Adaptive Replacement Cache）自适应调整 LRU 和 LFU 的比例。对于阴影贴图，LRU 最合适，因为相机移动时访问模式是局部性的。

### Q: GraphicsBuffer vs ComputeBuffer？
A: `GraphicsBuffer`（Unity 2020+）是 `ComputeBuffer` 的超集，支持更多 Target 类型（Vertex、Index、Constant 等）。新项目应使用 `GraphicsBuffer`。两者在 Shader 中的使用方式相同。

### Q: 为什么需要 EmptyBuffer？
A: Shader 中声明了 `StructuredBuffer<T>` 后，即使不使用也必须绑定一个有效的 Buffer，否则在某些平台（DX12、Vulkan）上会崩溃。EmptyBuffer 提供一个最小的有效 Buffer 作为占位。

### Q: Addressables 加载失败如何处理？
A: 通过 `AsyncOperationHandle.Status` 检查加载状态。失败时使用低 mip 级别的 Tile 作为 fallback，或显示默认阴影。重试策略：指数退避重试，避免频繁请求失败的资源。

### Q: PCSS 的采样数如何确定？
A: 搜索阶段通常 16-32 个采样点（泊松盘分布），滤波阶段根据半影大小动态调整（16-64 个）。移动端可减少到 8-16 个，配合时间抗锯齿（TAA）分摊到多帧。
