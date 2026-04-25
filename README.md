# Cubemap 6-Side 导出与天空球 Shader 工具说明

# 功能概述

# 本工具用于将 Unity 中已经导入好的 Cubemap 资源导出为 6 张 Skybox/6 Sided 可用的面贴图，并可自动生成两种材质：

# 

# Skybox/6 Sided 材质：用于 Lighting > Environment > Skybox Material

# Bj/Skybox6SidedSphere 材质：用于场景中的反面球体/天空球模型

# 该方案适合在项目存在单张贴图尺寸限制时，将一张 Cubemap 拆分为 6 张较高分辨率贴图，以获得更清晰的天空背景效果。

# 

# 文件位置

# 工具脚本：

# 

# Assets/Editor/CubemapFaceExporter.cs

# 球体专用 Shader：

# 

# Assets/GameData/Shaders/BjShader/Skybox6SidedSphere.shader

# 使用流程

# 将原始 HDRI 或全景图导入 Unity。

# 在贴图 Import Settings 中设置为 Cubemap。

# 打开菜单：

# BjTools/贴图工具/Cubemap导出6面

# 也可以在 Project 面板中右键选中一个 Cubemap，选择：

# 

# 导出Cubemap为Skybox 6 Sided贴图

# 在工具窗口中设置：

# 

# 源 Cubemap

# 输出目录

# 输出格式

# 导出分辨率

# 是否创建材质

# 点击“导出六面贴图”。

# 

# 输出格式说明

# 支持三种输出格式：

# 

# Png8：8 位 PNG，适合普通 LDR 天空图，不适合保留 HDR 亮度。

# Exr16：16 位浮点 EXR，推荐用于 HDR 天空图，体积和效果较平衡。

# Exr32：32 位浮点 EXR，精度最高，文件体积较大。

# 如果原始资源是 HDRI，推荐使用 Exr16。

# 

# 导出分辨率

# 可选择：

# 

# 源尺寸 / 128 / 256 / 512 / 1024 / 自定义

# 源尺寸 表示使用当前 Cubemap 在 Unity 中导入后的面分辨率。

# 如果需要更高清的导出结果，需要先提高源 Cubemap 的 Import Settings 中的 Max Size。

# 

# 六面贴图对应关系

# 工具会导出以下 6 张图：

# 

# +X\_Right

# \-X\_Left

# +Y\_Up

# \-Y\_Down

# +Z\_Front

# \-Z\_Back

# 对应 Skybox/6 Sided 的材质槽位：

# 

# +X\_Right  -> Right Tex

# \-X\_Left   -> Left Tex

# +Y\_Up     -> Up Tex

# \-Y\_Down   -> Down Tex

# +Z\_Front  -> Front Tex

# \-Z\_Back   -> Back Tex

# 工具内部已处理 Cubemap 面读取到 2D 贴图时的上下翻转问题，并根据实际测试修正了 X 方向对应关系。

# 

# Skybox 材质

# 如果勾选“创建6 Sided材质”，工具会自动生成：

# 

# \*\_Skybox6Sided.mat

# 该材质使用 Unity 内置：

# 

# Skybox/6 Sided

# 适用于：

# 

# Window > Rendering > Lighting > Environment > Skybox Material

# 天空球材质

# 如果勾选“创建球体方向采样材质”，工具会自动生成：

# 

# \*\_SkySphere6Sided.mat

# 该材质使用自定义 Shader：

# 

# Bj/Skybox6SidedSphere

# 适用于场景中预制好的反面球体或天空球模型。

# 

# 为什么不能直接把 Skybox/6 Sided 材质赋给球体

# Unity 内置的 Skybox/6 Sided 是天空盒 shader，主要用于环境背景渲染。

# 它并不是普通 Mesh 材质，不适合直接应用到球体模型上。

# 

# 直接赋给球体时，效果可能会出现：

# 

# 方向错误

# 显示像受模型 UV 影响

# 旋转/移动不符合预期

# 与 Lighting Skybox 中的效果不一致

# 因此球体模型应使用：

# 

# Bj/Skybox6SidedSphere

# Bj/Skybox6SidedSphere Shader 原理

# 该 Shader 不使用模型 UV，而是使用方向向量进行 6-side 采样。

# 

# 核心流程：

# 

# 获取当前像素对应的方向。

# 判断方向最接近 X/Y/Z 哪个轴。

# 根据轴向选择对应的 6-side 面贴图。

# 将 3D 方向投影到该面的 2D UV。

# 采样对应贴图并输出颜色。

# 这相当于手动模拟 Cubemap 采样。

# 

# 采样空间

# Shader 提供 Sample Space 参数：

# 

# WorldView

# ObjectSpace

# ObjectSpace

# 默认推荐模式。

# 

# 使用模型本地坐标作为采样方向：

# 

# dir = positionOS

# 特点：

# 

# 不依赖模型 UV

# 球体旋转会影响天空方向

# 显示稳定，不容易因相机和球体偏移产生视差抖动

# 适合预制天空球

# WorldView

# 使用从相机到当前像素的世界方向：

# 

# dir = positionWS - cameraPosition

# 特点：

# 

# 更接近传统天空盒视线采样

# 如果球体没有严格以相机为中心，可能出现视差或抖动

# 适合调试对比

# Shader 参数

# Bj/Skybox6SidedSphere 主要参数：

# 

# Cull Mode

# Tint Color

# Exposure

# Sample Space

# Rotation

# Vertical Rotation

# Front / Back / Left / Right / Up / Down Tex

# 说明：

# 

# Cull Mode：剔除模式。默认 Off，适合反面球体和普通球体测试。

# Tint Color：整体颜色 tint。

# Exposure：整体曝光强度。

# Sample Space：选择方向采样空间。

# Rotation：绕 Y 轴旋转天空，用于左右调整。

# Vertical Rotation：绕 X 轴旋转天空，用于上下调整。

# 六张贴图：对应导出的 6-side 面图。

# 推荐设置

# 用于场景天空球时推荐：

# 

# Shader: Bj/Skybox6SidedSphere

# Sample Space: ObjectSpace

# Cull Mode: Off

# Exposure: 1

# Rotation: 根据场景调整

# Vertical Rotation: 根据场景调整

# 球体模型建议：

# 

# 包围相机和场景可视范围

# 不参与阴影

# 不写深度

# 如作为无限远背景，可让球体跟随相机位置

# 使用较低复杂度球体即可，因为采样不依赖模型 UV 精度

# 注意事项

# 如果使用 HDRI，推荐导出 Exr16，不要用 PNG，否则会丢失 HDR 高亮信息。

# 如果导出结果不够清晰，先检查源 Cubemap 的 Import Settings Max Size。

# 如果球体显示方向不对，优先调整材质的 Rotation 和 Vertical Rotation。

# 如果使用普通 Mesh UV 贴 6 张图，效果会错误；该方案必须使用方向采样。

# 当前工具和 Shader 是基于 URP 项目实现的。

