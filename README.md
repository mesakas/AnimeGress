# AnimeGress

AnimeGress 是面向 Unity URP 的风格化草系统。它提供手工铺设、单株编辑、GPU Instancing、LOD 点状渐隐、全局风场、风场颜色变化、阴影和视锥剔除，并包含地表属性缓存、三维排除/推动 Volume 与远景颜色及伪阴影覆盖。

当前包版本为 `1.0.0`，Git 发布标签为 `v1.0`。

## 效果预览

![AnimeGress 风格化草场效果](Documentation~/animegress-scene-preview.png)

![AnimeGress 草场铺设工具与配置界面](Documentation~/animegress-editor-tools.png)

## 系统组成

- `AnimeGrassField`：保存确定性的草实例数据，负责分块、LOD、视锥剔除和实例化绘制。
- `AnimeGrassPrototype`：定义草类型、模型校正、风权重和各级 LOD。
- `AnimeGrassRendererFeature`：在每个 URP 摄像机渲染阶段提交草绘制。
- `AnimeGrassFarField`：在最后一级 LOD 之外用地表覆盖保留草场颜色、风色和伪阴影变化。
- `AnimeGrassWindZone`：设置全局风向、风力、阵风和风场颜色。
- `AnimeGrassPainterWindow`：提供普通鼠标、铺设、删除和单株编辑工作流。
- `AnimeGrassInstanced.shader`：提供风格化颜色、风动、点状渐隐和阴影处理。
- `AnimeSurfaceCache`：捕获固定世界范围内的地表颜色、世界法线、高度和排除草遮罩。
- `AnimeSurfaceCacheSource`：覆盖特殊材质的捕获贴图、颜色和排除草遮罩。
- `AnimeSurfaceCacheStamp`：地表遮罩 Volume 的兼容类型名，负责排除草和实时推动草叶。

## 环境要求

- Unity 6.0 或更高版本
- Universal Render Pipeline 17 或兼容版本
- 当前版本使用 URP Compatibility Mode 的 `ScriptableRenderPass.Execute`
- 草材质需要启用 GPU Instancing

## 安装

本项目已经以嵌入式包方式安装在 `Packages/com.ming.animegress`。

可以在 Package Manager 中使用 Git URL 安装，并在 URL 末尾指定 `#v1.0`。也可以选择 **Add package from disk**，然后选择本目录的 `package.json`。

## URP 配置

打开当前使用的 URP Renderer Data，在 Renderer Features 中添加 `AnimeGrassRendererFeature`。所有需要显示草的 Renderer Data 都应添加该 Feature。

当前 `v1.0` 使用 Compatibility Mode。请在 `Project Settings > Graphics > URP` 中启用 Compatibility Mode。后续版本可以增加 Render Graph 路径。

## 快速开始

1. 在 URP Renderer Data 的 Renderer Features 中添加 `AnimeGrassRendererFeature`。
2. 使用 `GameObject > AnimeGress > 二次元草场` 创建草场。
3. 使用 `Assets > Create > AnimeGress > 草类型配置` 创建草类型。
4. 为每个 LOD 指定 Mesh、Material、显示距离和渐隐距离。
5. 使用 `Window > AnimeGress > 草场铺设工具` 铺设、删除或编辑单株草。
6. 在场景中添加 `AnimeGrassWindZone`，控制全局风向、风力和风色。

`游戏 LOD 参考摄像机` 可以留空。留空时每个摄像机使用自身位置计算 LOD；指定后只影响游戏摄像机的 LOD 距离，不限制 Scene 视图或其他摄像机显示。

## LOD 配置

每级 LOD 包含：

- Mesh 与 Material
- Sub Mesh Index
- 开始距离和结束距离
- 点状渐隐距离
- 投射阴影与接收阴影开关

最后一级 LOD 的结束距离就是草的最大显示距离。`结束距离 = 0` 表示没有上限，不建议对大量实例使用。

## 远景草覆盖

`AnimeGrassFarField` 用于在真实草被最后一级 LOD 剔除后，继续保留草场的整体颜色、风场颜色和动态明暗。它根据草场内已经序列化的实例生成颜色/覆盖率和草根高度缓存，再通过摄像机深度把结果叠加到原来的地表上。该方案不绘制远距离草 Mesh，也不生成真实草阴影。

### 启用方法

1. 选中带有 `AnimeGrassField` 的草场对象。
2. 在草场 Inspector 的“远景覆盖（可选）”中点击“添加远景草覆盖”。
3. 点击“匹配最后一级 LOD 渐隐距离”，或手动让覆盖过渡与最后一级 LOD 的渐隐距离重合。
4. 根据铺草间距调整“单株覆盖半径”，然后点击“立即重建覆盖缓存”。
5. 确认当前 URP Renderer Data 已添加 `AnimeGrassRendererFeature`。Feature 会自动请求相机深度纹理。

推荐先使用以下范围：

- 覆盖开始距离：`最后一级 LOD 结束距离 - 渐隐距离`。
- 覆盖完全显示距离：最后一级 LOD 的结束距离。
- 单株覆盖半径：铺草间距的 `0.5-0.8` 倍。
- 覆盖颜色强度：`0.5-0.8`。
- 伪阴影强度：`0.1-0.25`。
- 伪阴影扰动：`0.4-0.7`，扰动斑块尺寸通常使用 `4-10` 米。
- 缓存分辨率：普通草场使用 `256`，大范围或需要清晰边界时使用 `512`。

覆盖距离必须与最后一级 LOD 的渐隐区间重叠，否则两者之间会出现没有真实草、也没有远景覆盖的空带。例如最后一级 LOD 在 `30` 米结束、渐隐距离为 `3` 米时，建议将覆盖开始/完全显示距离设为 `27 / 30`，不要设为 `57 / 60`。可直接点击“匹配最后一级 LOD 渐隐距离”自动填写。

“地表高度容差”用于区分上下楼层和悬空遮挡物；只会覆盖与草根缓存高度接近的可见表面。“最低向上法线”用于排除墙面和陡峭侧面。修改草实例、草类型材质或远景覆盖参数后，缓存会标记为待重建，并在下一次渲染时更新，也可以手动立即重建。

远景覆盖颜色从草类型第一个有效 LOD 材质的根部/顶部颜色、实例颜色和颜色倍率计算。它只表达远处的草场体块，不保留单株轮廓、草叶实时投影或草叶遮挡关系。

伪阴影扰动由多方向的世界空间波形生成，不需要额外噪声纹理采样。“扰动斑块尺寸”控制明暗区域大小，“扰动移动速度”控制图案沿全局风向漂移的速度。扰动只作用于伪阴影，不会改变草场覆盖边界。

## 铺设规则

铺设工具只在当前选中的 `AnimeGrassField` 上工作。目标表面被锁定后，射线只接受该 Collider，因此上层遮挡物不会抢占下层表面的铺设结果。

场景数据直接序列化在草场组件中，铺设结果是确定性的。单株编辑模式可以使用 Unity 默认移动、旋转和缩放工具修改实例。

`随机缩放比例 XYZ` 分别控制三个局部轴的尺寸变化。数值 `0.15` 表示对应轴在 `85%` 到 `115%` 之间随机，`0` 表示保持原始比例。`随机旋转比例 XYZ` 分别控制三个局部轴的旋转范围，数值 `1` 表示 `-180°` 到 `180°`，`0.5` 表示 `-90°` 到 `90°`。默认只启用 Y 轴随机旋转。随机结果会在铺设时写入实例并随场景保存，不会在运行时继续变化。

## 模型规范

- 推荐模型根部位于局部坐标原点。
- 推荐局部 Y 轴向上、Z 轴向前。
- Blender 导出前应用 Rotation 和 Scale。
- 模型轴向或单位不一致时，在草类型配置中使用位置、旋转和缩放校正。
- 面片草与完整模型草都可以作为任意 LOD Mesh。

## 用户资源

草模型、材质、草类型配置和场景属于项目内容，不属于插件代码。它们可以放在任意 `Assets` 子目录。当前项目的演示内容保留在 `Assets/Enlyn/Gress/Demo`。

## API 兼容性

为保持已有项目兼容，公开类型继续使用 `Enlyn.Grass` 命名空间。包程序集名称为：

- `Ming.AnimeGress.Runtime`
- `Ming.AnimeGress.Editor`

## 地表属性缓存

地表属性缓存是完整 RVT 之前的轻量方案。它使用固定大小的世界空间正交缓存，不依赖某一台游戏摄像机，也不需要额外的 Renderer Feature。缓存由三张纹理组成：

- 颜色纹理：保存地表基础颜色与有效区域。
- 数据纹理：RGB 保存编码后的世界法线，A 保存捕获高度范围内的归一化高度。
- 遮罩纹理：A 保存排除草强度，RGB 保留为零。

### 创建和配置

1. 使用 `GameObject > AnimeGress > 地表属性缓存` 创建缓存对象。
2. 将对象放到需要覆盖区域的中心，设置 `世界范围 XZ`、`垂直捕获高度` 和 `缓存分辨率`。
3. 设置 `地表 Layer`。缓存只捕获这些 Layer 上的 `MeshRenderer`、`SkinnedMeshRenderer` 和 Unity `Terrain`。
4. 点击 `立即重建缓存`，在 Inspector 的三张缓存预览中检查结果。
5. 在草材质中设置地表颜色、地表法线和排除遮罩影响。地表颜色会在草根处直接向缓存颜色混合。

缓存从上向下捕获，并使用深度保留同一 XZ 位置上最高的表面。Unity Terrain 支持高度、法线以及最多 8 个 Terrain Layer 的颜色混合。Mesh 材质会自动读取 `_BaseMap` 或 `_MainTex`，以及 `_BaseColor` 或 `_Color`；使用自定义属性名时，在目标物体或父物体上添加 `AnimeSurfaceCacheSource` 并覆盖基础贴图和颜色。

### 缓存范围与质量

缓存范围是以 `AnimeSurfaceCache` 对象 Transform 为中心的三维捕获盒。“世界范围 XZ”决定地面覆盖宽度和长度，“垂直捕获高度”以组件 Y 坐标为中心向上、向下各覆盖一半。比如 `128 x 128` 米、垂直高度 `80` 米会覆盖中心左右/前后各 `64` 米、上下各 `40` 米。

缓存纹理是正方形，因此每个轴的世界纹素尺寸分别为 `世界范围 X / 分辨率` 和 `世界范围 Z / 分辨率`。`128 x 128` 米配合 `1024` 分辨率约为 `0.125` 米/像素。扩大范围而不提高分辨率会降低地表颜色、法线和排除边界的空间精度；分辨率翻倍时，纹理显存和重建像素成本约增加到四倍。

开启“显示缓存范围”后，选中缓存对象会在 Scene 视图中显示可穿透地形和草叶的三维捕获盒、中心 XZ 覆盖面，以及尺寸、Y 高度范围和 X/Z 米每像素标签。开启“未选中时也显示”可让取消选择后的范围继续显示。跟随目标只改变缓存中心的 XZ 位置，Y 高度中心仍由缓存对象自身控制。

### 更新模式

- `变化时更新`：缓存对象、`AnimeSurfaceCacheSource`、`AnimeSurfaceCacheStamp` 或编辑器层级/资源发生变化时更新，适合静态场景。
- `定时更新`：按指定秒数更新，也会立即响应已登记的变化，适合少量动态地表。
- `每帧更新`：每帧完整重建，成本最高，只适合较小缓存或必须实时变化的场景。
- `仅手动更新`：只通过 Inspector 按钮或 `RefreshNow()` 更新，适合完全静态的确定性场景。

普通动态 Renderer 的移动不会自动触发 `变化时更新`。这类表面应添加 `AnimeSurfaceCacheSource`，或者使用定时/每帧更新。运行时代码修改 Terrain、材质或纹理后，也可以调用 `MarkDirty()`、`MarkDirty(Bounds)` 或静态方法 `RequestAllRefresh()`。

### 草场 Volume 交互

使用 `GameObject > AnimeGress > 地表遮罩 Volume` 创建球形或盒形三维区域。通过“水平尺寸 XZ”和“Volume 高度”控制范围；盒形 Volume 支持绕 Y 轴旋转。

- `排除草强度`：写入地表缓存，`0` 不移除，`1` 完全移除 Volume 内的草。缓存使用“仅手动更新”时需要手动重建。
- `推动草叶上部`：实时 GPU 顶点效果，不需要重建缓存。通过推动距离、边缘过渡和受影响起始高度控制形变，草根始终保持固定。
- 最多同时处理 16 个启用推动的 Volume。静态道路可以只使用排除草；角色或移动物体建议将排除草设为 `0`，只启用实时推动。

选中 Volume 时，Scene 视图始终显示其范围；开启“未选中时也显示”后，取消选择仍会保留轮廓。外轮廓表示完整作用边界，红色内轮廓表示排除草完全生效区，青色内轮廓表示草叶推动达到完整强度的区域。轮廓由 Scene Handles 覆盖绘制并穿透草叶显示；选中时的淡色体积填充仍依赖 Scene 视图的 Gizmos 开关。

只有缓存地表点实际位于 Volume 内时才写入排除遮罩，因此悬在上方或位于下方的 Volume 不会误伤当前缓存地表。多个 Volume 和来源遮罩使用最大值合并；移除或禁用 Volume 后会从其余来源重新生成缓存。

草 Shader 中相关参数包括：

- `地表颜色影响` 与 `仅影响草根`。
- `地表法线影响`。
- `排除遮罩影响` 与 `高度匹配容差`。

`高度匹配容差` 用于多层场景。只有缓存中的最高表面高度与草根高度足够接近时，草才会读取该表面的颜色、法线和遮罩，因此上层平台不会错误影响下层草。需要更严格地区分楼层时减小该值。

其他自定义 Shader 可以包含 `Packages/com.ming.animegress/Shaders/AnimeSurfaceCache.hlsl`，使用 `AnimeSurfaceCacheWorldToUV`、`AnimeSurfaceCacheContainsUV` 和 `SampleAnimeSurfaceCache` 读取同一份数据。

1024 分辨率缓存的显存占用约为 25 MiB，包含颜色、半精度法线/高度、遮罩、深度和 Mipmap。实际值会随平台支持的 RenderTexture 格式变化。固定缓存不会进行虚拟分页，超大世界应等待后续 Clipmap 或完整 RVT。

## 未来工作：完整 RVT

计划为大型场景增加完整的 Runtime Virtual Texture（RVT）地表缓存系统。目标是在相对固定的显存预算下覆盖更大的世界，并让近处地表保持较高纹素密度。

计划能力包括：

- 虚拟页表与固定大小的物理页面缓存。
- 基于摄像机反馈的页面请求和 GPU 按需生成。
- 页面 LRU 淘汰、缺页回退和缓存占用诊断。
- 局部脏区域失效，支持道路、脚印、湿度、积雪和烧焦等动态变化。
- 页面边界扩展与 Mipmap 生成，降低页面接缝和远距离闪烁。
- 缓存地表颜色、法线、高度、粗糙度、地表类型和交互遮罩。
- 让地形、草、岩石、道路和其他场景物体共享世界空间地表数据。
- 支持草继承地表颜色、草根融合、道路避让、水边变色和动态压草。

完整 RVT 主要面向数公里级开放世界、复杂地形材质和大量动态地表效果。对于中小型场景，将优先保留成本更低的固定地表属性缓存或多级 Clipmap 方案。

RVT 只负责地表材质数据的生成、缓存和共享，不替代现有草实例的 LOD、视锥剔除、距离剔除与 GPU Instancing。
