# Unity Virtual Shadow Maps

基于 Unity Built-in Render Pipeline 的虚拟阴影贴图系统，通过虚拟纹理技术实现高效的动态阴影流式加载。

## 效果展示

CSM | VSM
![compare](Images/compare.gif)

## 系统架构

### 核心组件

- **VirtualShadowMaps** — 挂载到平行光，管理虚拟纹理页表、Shadow Atlas 和阴影渲染
- **VirtualShadowCamera** — 挂载到相机，基于视锥体进行页面请求和 LOD 管理
- **VirtualShadowVolume** — 定义阴影覆盖区域，收集场景中的阴影投射物体
- **VirtualShadowDebug** — 提供分屏对比和调试可视化功能

### 技术特性

- 虚拟纹理页表系统，支持 8 级 Mipmap LOD
- 基于距离和视锥体的动态页面请求算法
- LRU 缓存管理，最大化 tile 复用率
- 支持预烘焙静态阴影和运行时动态渲染两种模式
- PCSS 软阴影过滤（26 点 Poisson 采样）
- 分帧异步加载，避免单帧卡顿

## 渲染管线

```
页面请求阶段:
  VirtualShadowCamera.UpdateRequest → 距离估算 → 视锥体剔除 → 视线角度过滤
  → 生成请求队列 → 更新 IndirectionTexture

阴影渲染阶段:
  预烘焙模式: 从磁盘加载 EXR → 复制到 Shadow Atlas
  运行时模式: VirtualShadowMapBaker → 渲染深度 → 复制到 Shadow Atlas

阴影采样阶段:
  世界坐标 → IndirectionTexture 查询 → 获取 Tile 信息
  → 变换到 Shadow Atlas 坐标 → PCF/PCSS 采样 → 阴影值
```

## 关键技术实现

### 虚拟纹理系统

- **IndirectionTexture (页表)**：256×256 ARGBHalf 纹理，存储 (TileX, TileY, MipLevel, MipScale)
- **Shadow Atlas**：动态 tile 池，通过 LRU 算法管理有限的内存槽位
- **TileIndexTable**：多级页表结构，支持快速查询和最近邻回退

### 页面选择算法

三重过滤机制：
1. **距离估算** — `estimate = distance² × cellSize²`，近处加载高精度页面
2. **视锥体重叠** — 只加载与相机视野重叠的区域
3. **视线角度** — 预加载相机前方即将看到的区域

最粗糙级别（Level 7-8）无条件加载，确保远距离基础阴影覆盖。

### 性能优化

- GPU Instancing 绘制 IndirectionTexture，单次 DrawCall 处理所有页面
- 每帧限制最大请求数（`maxPageRequestLimit`），平滑加载曲线
- 相机移动检测，静止时跳过不必要的更新
- CommandBuffer 分离渲染，避免与 Unity 内置阴影冲突

### 编辑器工具

- 现代化 Inspector UI，支持 Low/Medium/High/Ultra 四档质量预设
- Setup Wizard 一键配置场景组件
- 实时统计面板显示活跃页面数、请求数、内存占用
- 纹理预览和调试可视化

## 目录结构

```
Assets/
├── Scripts/
│   ├── VirtualShadowMaps.cs          核心管理类，处理页表和渲染
│   ├── VirtualShadowCamera.cs        相机端页面请求逻辑
│   ├── VirtualShadowMapBaker.cs      运行时阴影烘焙器
│   ├── StreamingTile/                虚拟纹理基础设施
│   │   ├── TileIndexTable.cs         多级页表数据结构
│   │   ├── IndirectionTexture.cs     页表纹理管理
│   │   ├── MosaicTexture.cs          Shadow Atlas 管理
│   │   └── EvictionCache.cs          LRU 缓存实现
│   └── ...
│
├── Shaders/
│   ├── CascadedOcclusionMaps.hlsl    阴影采样核心函数
│   ├── AdaptiveRendering/
│   │   ├── DrawIndirection.shader    IndirectionTexture 绘制
│   │   └── DrawMosaic.shader         Shadow Atlas 更新
│   └── Internal-DeferredShading.shader  延迟渲染集成
│
├── Editor/
│   ├── VirtualShadowMapsInspector.cs      主 Inspector + 烘焙工具
│   ├── VirtualShadowCameraInspector.cs    相机 Inspector
│   ├── VirtualShadowMapSetupWizard.cs     配置向导
│   └── ...
│
└── Bistro/                           Amazon Lumberyard Bistro 测试场景
```

## 环境要求与配置

- Unity 2022.3+，Windows/Mac/Linux
- Built-in Render Pipeline（不支持 URP/HDRP）

### 快速开始

1. 打开菜单 `Window > Virtual Shadow Maps > Setup Wizard`
2. 选择场景中的平行光（Directional Light）和主相机
3. 选择质量预设（推荐从 Medium 开始）
4. 点击 "Setup Virtual Shadow Maps" 完成配置
5. 在 VirtualShadowMaps Inspector 中调整参数：
   - `Max Tile Pool`: 控制内存占用（64-256）
   - `Bias / Normal Bias`: 调整阴影偏移，消除 acne
   - `PCSS Filter`: 启用软阴影
6. 在 VirtualShadowCamera Inspector 中调整 `Level of Detail`（默认 5.0）
7. 可选：使用 Baking Tools 预烘焙静态阴影

## 注意事项

- 项目修改了 `Internal-DeferredShading.shader`，升级 Unity 版本时需手动合并
- 运行时模式性能开销较大，建议静态场景使用预烘焙
- `VirtualShadowVolume.Collect()` 会扫描场景中所有 Renderer，大场景可能耗时较长
- 最大支持 256×256 页表（65536 个虚拟页面），超大场景需调整 `pageSize` 参数

## 技术文档

- `Assets/Editor/LOOKUP_TEXTURE_DESIGN.md` — IndirectionTexture 设计原理
- `Assets/Editor/BAKING_GUIDE_CN.md` — 阴影烘焙完整指南

## 许可

本项目包含的 Bistro 场景资源受以下许可约束：

Bistro 场景：© 2017 Amazon Lumberyard
场景资源版权信息见 `Assets/Bistro/Bistro_v5_2/LICENSE.txt`。
