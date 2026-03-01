# Virtual Shadow Maps 烘焙指南

## 什么是 VSM 烘焙？

VSM 烘焙是将静态场景的阴影预先渲染并保存为资源的过程。这样可以：
- 提高运行时性能（不需要实时计算静态阴影）
- 减少内存使用
- 获得更稳定的阴影质量

## 烘焙流程

### 1. 准备场景

**设置 VirtualShadowVolume：**
1. 在场景中创建一个 GameObject
2. 添加 `VirtualShadowVolume` 组件
3. 在 Inspector 中点击 "Collect Scene Objects" 按钮
   - 这会扫描场景中所有静态对象和 LOD 组
   - 收集的渲染器会被包含在烘焙中

**调整阴影体积边界：**
- 使用 "Fit to Scene Bounds" 自动适应场景大小
- 或手动调整 Bounds 属性
- 在 Scene 视图中可以看到绿色线框表示阴影覆盖区域

### 2. 配置 VirtualShadowMaps

在 Directional Light 上的 `VirtualShadowMaps` 组件中设置：
- **Max Mip Level**: Mipmap 级别（影响细节层次）
- **Max Resolution**: 单个阴影贴图分辨率
- **Region Size**: 阴影覆盖区域大小
- **Region Center**: 阴影覆盖区域中心

### 3. 打开烘焙窗口

有三种方式打开烘焙窗口：

**方式 1: 从 VirtualShadowVolume Inspector**
1. 选择带有 VirtualShadowVolume 组件的 GameObject
2. 在 Inspector 中点击 "Generate VSM (Bake Shadows)" 按钮

**方式 2: 从 VirtualShadowMaps Inspector**
1. 选择带有 VirtualShadowMaps 组件的 Light
2. 展开 "Debug Tools" 部分
3. 点击 "Bake Shadows" 按钮

**方式 3: 从菜单**
- 选择 `Window > Virtual Shadow Maps > Bake Shadows`

### 4. 配置烘焙设置

在烘焙窗口中：

**Components（组件）：**
- **VSM Light**: 选择带有 VirtualShadowMaps 的 Directional Light
- **VSM Volume**: 选择 VirtualShadowVolume 组件
- 点击 "Auto-detect Components" 自动查找

**Output Settings（输出设置）：**
- **Output Folder**: 选择保存位置（必须在 Assets 文件夹内）
- **Asset Name**: 资源文件名称

**Bake Information（烘焙信息）：**
- 显示当前配置的页面大小、级别、分辨率等信息
- 显示将要烘焙的渲染器数量

### 5. 开始烘焙

1. 确认所有设置正确
2. 点击 "Bake Virtual Shadow Maps" 按钮
3. 等待烘焙完成
4. 烘焙完成后会自动：
   - 创建 VirtualShadowData 资源
   - 将资源分配给 VirtualShadowMaps 组件
   - 在 Project 窗口中选中新创建的资源

### 6. 使用烘焙数据

烘焙完成后：
- VirtualShadowMaps 组件的 `Shadow Data` 字段会自动填充
- 运行时会使用预烘焙的阴影数据而不是实时计算
- 可以在 Inspector 中看到 "Shadow Data" 已设置

## 烘焙窗口详解

### 组件验证
- ✅ 绿色勾号：组件已正确设置
- ⚠️ 警告：缺少必需组件或配置不完整

### 自动检测
- 点击 "Auto-detect Components" 会自动查找场景中的：
  - VirtualShadowMaps（在 Directional Light 上）
  - VirtualShadowVolume

### 输出路径
- 必须选择 Assets 文件夹内的路径
- 点击 "Browse" 浏览文件夹
- 如果文件夹不存在，会自动创建

### 烘焙信息
显示当前配置的关键参数：
- **Page Size**: 页表大小（从 Page Level 计算）
- **Page Level**: Mipmap 级别
- **Shadow Resolution**: 阴影贴图分辨率
- **Region Size**: 覆盖区域大小
- **Renderers**: 将要烘焙的渲染器数量

## 常见问题

### Q: 烘焙按钮是灰色的？
A: 检查以下条件：
- VirtualShadowMaps 组件是否存在
- VirtualShadowVolume 组件是否存在
- 输出路径和资源名称是否已填写

### Q: 没有收集到渲染器？
A: 在 VirtualShadowVolume Inspector 中：
1. 点击 "Collect Scene Objects" 按钮
2. 确保场景中有标记为 Static 的对象
3. 或者对象上有 VirtualShadowCaster 组件且 castShadow = true

### Q: 烘焙后阴影没有变化？
A: 检查：
1. VirtualShadowMaps 的 Shadow Data 字段是否已填充
2. shadowOn 是否为 true
3. 相机上是否有 VirtualShadowCamera 组件

### Q: 如何更新烘焙数据？
A: 
1. 修改场景后重新点击 "Collect Scene Objects"
2. 再次运行烘焙流程
3. 可以覆盖现有资源或创建新资源

### Q: 烘焙数据占用多少空间？
A: 取决于：
- Page Level（级别越高，页面越多）
- Shadow Resolution（分辨率越高，单个贴图越大）
- 场景复杂度（需要的页面数量）

## 性能优化建议

### 烘焙设置优化
- **移动平台**: Page Level = 3-4, Resolution = 512-1024
- **PC 中等配置**: Page Level = 4-5, Resolution = 1024
- **PC 高端配置**: Page Level = 5-6, Resolution = 1024-2048

### 场景优化
- 只将需要投射阴影的对象标记为 Static
- 使用 LOD 组减少远处物体的阴影细节
- 合理设置 Region Size，不要过大

### 运行时优化
- 使用烘焙数据处理静态阴影
- 动态对象使用 Unity 标准阴影
- 调整 Max Tile Pool 控制内存使用

## 技术说明

### 当前实现状态
当前烘焙窗口创建基础的 VirtualShadowData 资源，包含：
- 场景配置（区域大小、中心、边界）
- 光源变换矩阵
- 页面配置（大小、级别、分辨率）

### 完整烘焙实现
完整的烘焙功能需要：
1. 使用 VirtualShadowMapBaker 运行时类渲染阴影贴图
2. 将渲染结果保存为纹理资源
3. 在 VirtualShadowData 中存储纹理引用和投影矩阵
4. 设置纹理导入器参数（R16 格式，无压缩）

这需要将运行时的 VirtualShadowMapBaker 类集成到编辑器工作流中。

## 下一步

烘焙完成后，你可以：
1. 在 Project 窗口中查看创建的 VirtualShadowData 资源
2. 在 VirtualShadowMaps Inspector 中验证 Shadow Data 已分配
3. 运行场景测试阴影效果
4. 使用 Control Panel 调整运行时参数
5. 使用 Texture Viewer 查看阴影贴图

## 相关工具

- **Control Panel**: 运行时调整和监控
- **Texture Viewer**: 查看阴影贴图
- **Setup Wizard**: 快速配置 VSM 系统
- **Scene GUI**: 场景视图中的可视化工具
