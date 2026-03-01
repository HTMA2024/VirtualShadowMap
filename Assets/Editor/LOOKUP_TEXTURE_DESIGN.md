# IndirectionTexture 设计原理详解

## 概述

IndirectionTexture（间接寻址纹理/页表纹理）是 Virtual Shadow Maps 系统中的核心组件，它充当了**虚拟纹理页表**的角色，用于在运行时快速查找哪些阴影页面（TileNode）已经加载到物理纹理（Tile Texture）中。

## 核心概念

### 1. 虚拟纹理系统

Virtual Shadow Maps 使用虚拟纹理技术来管理大规模阴影贴图：

```
虚拟空间（Virtual Space）
├── 页表（Page Table）- 逻辑上的阴影覆盖区域
│   ├── Mip Level 0: 1x1 页面（最粗糙）
│   ├── Mip Level 1: 2x2 页面
│   ├── Mip Level 2: 4x4 页面
│   └── Mip Level N: pageSize x pageSize 页面（最精细）
│
└── 物理空间（Physical Space）
    └── Tile Texture - 实际的阴影贴图图集
        ├── Tile 0: 存储某个 Page 的阴影数据
        ├── Tile 1: 存储另一个 Page 的阴影数据
        └── ...
```

### 2. IndirectionTexture 的作用

IndirectionTexture 是一个**间接寻址表**，它记录了：
- 哪些虚拟页面（TileNode）已经被加载
- 这些页面在物理纹理（Tile Texture）中的位置
- 页面的 Mip Level 信息

## 数据结构

### TileNode（页面）
```csharp
public sealed class TileNode
{
    public int column;         // 页面在虚拟空间的 X 坐标
    public int rank;           // 页面在虚拟空间的 Y 坐标
    public int mipLevel;       // Mip 级别
    public TilePayload payload; // 页面数据
    
    // 页面大小 = 2^mipLevel
    public int nodeExtent { get => 1 << mipLevel; }
    
    // 是否已加载到物理纹理
    public bool isResident { get => payload.isResident; }
    
    // 在 Tile Texture 中的索引
    public int tileSlot { get => payload.tileIndex; }
}
```

### TilePayload（页面数据）
```csharp
public sealed class TilePayload
{
    public int tileIndex;      // 在 Tile Texture 中的索引（-1 表示未加载）
    public int lastTouchFrame; // 最后激活的帧号
    public TileRequestData? queuedRequest; // 加载请求
    
    public bool isResident { get => tileIndex != -1; }
}
```

### IndirectionTexture 核心字段
```csharp
public sealed class IndirectionTexture
{
    // 输出的间接寻址纹理（ARGBHalf 格式）
    private RenderTexture m_LookupSurface;
    
    // 当前相机请求的页面列表
    private List<TileNode> m_PendingEntries;
    
    // 当前帧激活的 Tile 列表（已加载的页面）
    private List<TileNode> m_ActiveTiles;
    
    // Tile 索引数组：(tileX, tileY, mipLevel, mipScale)
    private Vector4[] m_TiledIndex;
    
    // Tile 变换矩阵数组（用于绘制到 IndirectionTexture）
    private Matrix4x4[] m_TiledMatrixs;
    
    // 页表大小
    private int m_TableSize;
    
    // 是否需要重绘
    private bool m_IsDirty;
}
```

## 工作流程

### 1. 初始化
```csharp
public IndirectionTexture(int pageSize, int maxMosaicPool)
{
    // 计算 Tile 排列数量（例如 64 个 tile = 8x8 排列）
    var tilingCount = Mathf.CeilToInt(Mathf.Sqrt(maxMosaicPool));
    
    // 创建间接寻址纹理（pageSize x pageSize，ARGBHalf 格式）
    m_LookupSurface = RenderTexture.GetTemporary(
        pageSize, pageSize, 16, 
        RenderTextureFormat.ARGBHalf
    );
    
    // 点采样，避免插值
    m_LookupSurface.filterMode = FilterMode.Point;
    m_LookupSurface.wrapMode = TextureWrapMode.Clamp;
}
```

**为什么是 ARGBHalf 格式？**
- 需要存储浮点数据（Tile 坐标、Mip Level）
- 4 个通道可以存储：
  - R: Tile X 坐标
  - G: Tile Y 坐标
  - B: Mip Level
  - A: Mip Scale (2^mipLevel)

### 2. 添加请求的页面
```csharp
public void Add(TileNode node)
{
    m_PendingEntries.Add(node);
    
    // 如果页面已加载但不在激活列表中，标记需要重绘
    if (node.isResident && !m_ActiveTiles.Contains(node))
        m_IsDirty = true;
}
```

### 3. 更新 Tile 列表
```csharp
public bool UpdateTiles(MosaicTexture mosaicTexture)
{
    // 检查是否有变化
    bool isDirty = m_IsDirty;
    
    if (!isDirty)
    {
        foreach (var node in m_PendingEntries)
        {
            if (node.isResident)
            {
                // 如果页面在当前帧被激活，需要更新
                if (node.payload.lastTouchFrame >= m_PreviousFrameTick)
                    isDirty = true;
            }
            else
            {
                // 如果之前激活的页面现在不可用，需要更新
                if (m_ActiveTiles.Contains(node))
                    isDirty = true;
            }
        }
    }
    
    if (isDirty)
    {
        // 重建激活的 Tile 列表
        m_ActiveTiles.Clear();
        foreach (var node in m_PendingEntries)
        {
            if (node.isResident)
                m_ActiveTiles.Add(node);
        }
        
        // 按 Mip Level 降序排序（粗糙的在前，精细的在后）
        m_ActiveTiles.Sort((a, b) => -a.mipLevel.CompareTo(b.mipLevel));
        
        // 准备绘制数据
        for (int i = 0; i < m_ActiveTiles.Count; i++)
        {
            var node = m_ActiveTiles[i];
            var tilePos = mosaicTexture.IdToPos(node.payload.tileIndex);
            
            // 存储 Tile 信息
            m_TiledIndex[i] = new Vector4(
                tilePos.x,             // Tile X
                tilePos.y,             // Tile Y
                node.mipLevel,         // Mip Level
                1 << node.mipLevel     // Mip Scale
            );
            
            // 计算绘制矩阵（页面在 IndirectionTexture 中的位置和大小）
            m_TiledMatrixs[i] = node.GetMatrix(m_TableSize, Vector2.zero);
        }
        
        m_IsDirty = true;
    }
    
    return m_IsDirty;
}
```

**为什么按 Mip Level 降序排序？**
- 粗糙的 Mip Level（大页面）先绘制，作为基础覆盖
- 精细的 Mip Level（小页面）后绘制，覆盖在上面
- 这样可以确保精细区域使用高质量阴影，远处使用低质量阴影

### 4. 渲染 IndirectionTexture
```csharp
public void UpdateIndirectionTexture(CommandBuffer cmd, Material material)
{
    if (this.m_IsDirty)
    {
        // 设置 Shader 参数
        m_PropertyBlock.Clear();
        m_PropertyBlock.SetVectorArray("_TiledIndex", this.tiledIndex);
        
        // 清空纹理
        cmd.SetRenderTarget(this.GetTexture());
        cmd.ClearRenderTarget(true, true, Color.clear);
        
        // 使用 GPU Instancing 绘制所有 Tile
        cmd.DrawMeshInstanced(
            StreamingTileUtilities.fullscreenMesh,    // 全屏四边形
            0,                                        // SubMesh 索引
            material,                                 // drawIndirectionMaterial
            0,                                        // Pass 索引
            this.tiledMatrixs,                       // 实例变换矩阵
            this.tiledCount,                         // 实例数量
            m_PropertyBlock                          // 材质属性
        );
        
        this.m_IsDirty = false;
    }
}
```

**GPU Instancing 的优势：**
- 一次 Draw Call 绘制所有激活的 Tile
- 每个实例使用不同的变换矩阵（位置和大小）
- 高效利用 GPU 并行能力

## Shader 实现

### drawIndirectionMaterial Shader（推测）
```hlsl
// Vertex Shader
struct v2f
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    uint instanceID : SV_InstanceID;
};

// 从 MaterialPropertyBlock 传入
float4 _TiledIndex[MAX_TILES]; // (tileX, tileY, mipLevel, mipScale)

v2f vert(appdata v, uint instanceID : SV_InstanceID)
{
    v2f o;
    
    // 使用实例矩阵变换顶点（定位到 IndirectionTexture 中的正确位置）
    o.pos = mul(unity_ObjectToWorld, v.vertex);
    o.uv = v.uv;
    o.instanceID = instanceID;
    
    return o;
}

// Fragment Shader
float4 frag(v2f i) : SV_Target
{
    // 获取当前实例的 Tile 信息
    float4 tileInfo = _TiledIndex[i.instanceID];
    
    // 输出到 IndirectionTexture
    // R: Tile X 坐标
    // G: Tile Y 坐标
    // B: Mip Level
    // A: Mip Scale
    return tileInfo;
}
```

## 在 Shader 中使用 IndirectionTexture

### 阴影采样流程
```hlsl
// 1. 计算世界空间位置在光源空间的坐标
float3 lightSpacePos = mul(WorldToLightMatrix, worldPos);

// 2. 转换为 IndirectionTexture UV（归一化到 [0,1]）
float2 lookupUV = lightSpacePos.xy / RegionSize + 0.5;

// 3. 从 IndirectionTexture 采样，获取 Tile 信息
float4 tileInfo = tex2D(_CascadedOcclusionLookupTexture, lookupUV);
float2 tilePos = tileInfo.rg;    // Tile 在 Tile Texture 中的位置
float mipLevel = tileInfo.b;      // Mip Level
float mipScale = tileInfo.a;      // Mip Scale

// 4. 计算在 Tile 内的局部坐标
float2 localUV = frac(lookupUV * PageSize / mipScale);

// 5. 计算在 Tile Texture 中的最终 UV
float2 tileSize = TileResolution / TileTextureSize;
float2 finalUV = (tilePos + localUV) * tileSize;

// 6. 从 Tile Texture 采样阴影深度
float shadowDepth = tex2D(_CascadedOcclusionTileTexture, finalUV).r;

// 7. 比较深度，计算阴影
float shadow = (lightSpacePos.z < shadowDepth) ? 1.0 : 0.0;
```

## 关键设计决策

### 1. 为什么使用纹理而不是 Buffer？
- **跨平台兼容性**：纹理在所有平台都支持
- **硬件加速**：纹理采样器有专门的硬件优化
- **点采样**：FilterMode.Point 确保精确查找，无插值
- **空间局部性**：相邻像素对应相邻的虚拟页面，缓存友好

### 2. 为什么是 pageSize x pageSize？
- **1:1 映射**：IndirectionTexture 的每个像素对应虚拟空间的一个单元
- **Mip 层次**：
  - Mip 0: 整个纹理对应 1 个页面
  - Mip 1: 整个纹理对应 2x2 = 4 个页面
  - Mip N: 整个纹理对应 pageSize x pageSize 个页面
- **简化计算**：UV 坐标直接对应虚拟空间坐标

### 3. 为什么按 Mip Level 排序？
- **覆盖策略**：粗糙 Mip 先绘制，提供基础覆盖
- **精细覆盖**：精细 Mip 后绘制，覆盖需要高质量的区域
- **LOD 过渡**：自然实现从粗糙到精细的过渡

### 4. 为什么使用 GPU Instancing？
- **性能**：一次 Draw Call 绘制所有 Tile
- **效率**：避免多次状态切换
- **可扩展**：支持大量 Tile（受限于 maxTilePool）

## 性能特性

### 内存占用
```
IndirectionTexture 大小 = pageSize × pageSize × 8 bytes (ARGBHalf)

例如：
- pageSize = 256: 256 × 256 × 8 = 512 KB
- pageSize = 512: 512 × 512 × 8 = 2 MB
```

### 更新频率
- **每帧检查**：UpdateTiles() 检查是否需要更新
- **按需更新**：只有在 Tile 变化时才重绘
- **增量更新**：只更新变化的部分（通过 m_IsDirty 标记）

### GPU 开销
- **绘制开销**：1 次 Draw Call（GPU Instancing）
- **采样开销**：每个阴影采样需要 1 次 IndirectionTexture 采样 + 1 次 Tile Texture 采样
- **带宽**：IndirectionTexture 通常很小，缓存命中率高

## 优势

1. **动态加载**：只加载相机可见的阴影页面
2. **内存高效**：虚拟空间可以很大，物理空间有限
3. **LOD 支持**：自动支持多级细节
4. **灵活性**：可以动态调整加载策略
5. **可扩展**：支持超大规模场景

## 局限性

1. **间接寻址开销**：需要额外的纹理采样
2. **更新延迟**：Tile 加载需要时间
3. **内存碎片**：Tile 池可能产生碎片
4. **复杂性**：实现和调试较复杂

## 总结

IndirectionTexture 是 Virtual Shadow Maps 系统的核心，它通过**虚拟纹理页表**的方式，实现了：
- 大规模阴影场景的高效管理
- 动态的 LOD 和流式加载
- 内存和性能的平衡

它的设计充分利用了 GPU 的纹理采样硬件和并行能力，是现代虚拟纹理技术在阴影系统中的典型应用。
