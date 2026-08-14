# AnimeGress

AnimeGress 是面向 Unity URP 的确定性风格化草系统。它提供手工铺设、单株编辑、GPU Instancing、LOD 点状渐隐、全局风场、风场颜色变化、阴影和视锥剔除。

当前包版本为 `1.0.0`，Git 发布标签为 `v1.0`。

## 效果预览

![AnimeGress 风格化草场效果](Documentation~/animegress-scene-preview.png)

![AnimeGress 草场铺设工具与配置界面](Documentation~/animegress-editor-tools.png)

## 环境要求

- Unity 6.0 或更高版本
- Universal Render Pipeline 17 或兼容版本
- 当前版本使用 URP Compatibility Mode 的 `ScriptableRenderPass.Execute`
- 草材质需要启用 GPU Instancing

## 安装

本项目已经以嵌入式包方式安装在 `Packages/com.ming.animegress`。

发布到远程 Git 仓库后，可以在 Package Manager 中使用 Git URL 安装，并在 URL 末尾指定 `#v1.0`。也可以选择 **Add package from disk**，然后选择本目录的 `package.json`。

## 快速开始

1. 在 URP Renderer Data 的 Renderer Features 中添加 `AnimeGrassRendererFeature`。
2. 使用 `GameObject > AnimeGress > 二次元草场` 创建草场。
3. 使用 `Assets > Create > AnimeGress > 草类型配置` 创建草类型。
4. 为每个 LOD 指定 Mesh、Material、显示距离和渐隐距离。
5. 使用 `Window > AnimeGress > 草场铺设工具` 铺设、删除或编辑单株草。
6. 在场景中添加 `AnimeGrassWindZone`，控制全局风向、风力和风色。

`游戏 LOD 参考摄像机` 可以留空。留空时每个摄像机使用自身位置计算 LOD；指定后只影响游戏摄像机的 LOD 距离，不限制 Scene 视图或其他摄像机显示。

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

更完整的配置说明见 `Documentation~/index.md`。
