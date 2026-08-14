# AnimeGress 1.0 使用说明

## 组成

- `AnimeGrassField`：保存确定性的草实例数据，负责分块、LOD、视锥剔除和实例化绘制。
- `AnimeGrassPrototype`：定义草类型、模型校正、风权重和各级 LOD。
- `AnimeGrassRendererFeature`：在每个 URP 摄像机渲染阶段提交草绘制。
- `AnimeGrassWindZone`：设置全局风向、风力、阵风和风场颜色。
- `AnimeGrassPainterWindow`：提供普通鼠标、铺设、删除和单株编辑工作流。
- `AnimeGrassInstanced.shader`：提供风格化颜色、风动、点状渐隐和阴影处理。

## URP 配置

打开当前使用的 URP Renderer Data，在 Renderer Features 中添加 `AnimeGrassRendererFeature`。所有需要显示草的 Renderer Data 都应添加该 Feature。

当前 `v1.0` 使用 Compatibility Mode。请在 `Project Settings > Graphics > URP` 中启用 Compatibility Mode。后续版本可以增加 Render Graph 路径。

## LOD

每级 LOD 包含：

- Mesh 与 Material
- Sub Mesh Index
- 开始距离和结束距离
- 点状渐隐距离
- 投射阴影与接收阴影开关

最后一级 LOD 的结束距离就是草的最大显示距离。`结束距离 = 0` 表示没有上限，不建议对大量实例使用。

## 铺设规则

铺设工具只在当前选中的 `AnimeGrassField` 上工作。目标表面被锁定后，射线只接受该 Collider，因此上层遮挡物不会抢占下层表面的铺设结果。

场景数据直接序列化在草场组件中，铺设结果是确定性的。单株编辑模式可以使用 Unity 默认移动、旋转和缩放工具修改实例。

## Git 发布

包根目录本身是独立 Git 仓库。`package.json` 使用语义版本 `1.0.0`，对应发布标签 `v1.0`。
