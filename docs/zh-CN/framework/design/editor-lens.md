<!-- source: docs/en/framework/design/editor-lens.md @ blob:ae2065cd67015ac425c56beeae454e27ab914b11 -->
<!-- 中文译文；以英文 docs/en/framework/design/editor-lens.md 为准，译文可能滞后于原文。blob 是英文文件的内容 sha，可用 git hash-object docs/en/framework/design/editor-lens.md 核验。 -->

# 编辑器 Lens

**其他语言：** [English](../../../en/framework/design/editor-lens.md) | [简体中文](editor-lens.md)

*设计记录*——可选的 `com.ronny.grow.lens` 编辑器工具包。

Lens 是一组编辑器便利功能，不是框架机制。它作为独立的 Editor-only 包发布，使内核包对所有消费者的行为保持不变。

## 目标

- 让内核包不承担全局编辑器钩子与 Unity 内部 API 风险。
- 所有功能 opt-in：开关关闭时，不注册任何 `hierarchyWindowItemOnGUI`、`projectWindowItemOnGUI`、`finishedDefaultHeaderGUI`、`hierarchyChanged` 或 `EditorApplication.update` 回调。
- 所有订阅只有一个所有者，避免启用/停用时留下部分处理器。
- Unity 内部 API 变化时安全降级。

## 非目标

- 通用编辑器框架、窗口框架或 Odin Inspector 的替代品。
- 笔记、文档链接、Transform 剪贴板等数据录入工具。
- 绘制时修改层级或资产数据。
- 项目级 Property Drawer 或枚举 Drawer。
- 注册表持久化（`EditorPrefs` / `PlayerPrefs`）。

## 包边界

```text
com.ronny.grow        内核 + Grow.Editor（编辑器程序集）
com.ronny.grow.lens   Editor-only，独立
```

Lens 不引用 Store 包，也不引用 Grow 程序集，因此包图保持无环。Lens 程序集本身只使用 Unity 编辑器 API；`package.json` 未声明对内核的包依赖。

## 设置

`UserSettings/Grow/Lens.json` 是每项目一个小 JSON 文件，只有一个布尔值（`Enabled`）。它位于 `Assets/` 之外，因此 Unity 不会导入它，也不会进入构建；本仓库同时忽略 `UserSettings/`。默认 `false`；文件缺失或不可读也解析为 `false`。

设置页在 **Preferences > Grow > Lens**。切换开关会立即应用宿主，无需域重载。开关也是修改该设置唯一受支持的方式：没有菜单或工具栏快捷入口。

## 宿主

`GrowLensHost` 是唯一订阅/退订编辑器回调的地方。各功能只暴露 `Subscribe` / `Unsubscribe`，不在自己的初始化方法里订阅。

`Apply()` 幂等：它比较期望状态与当前状态，相同时直接返回。调用时机：

- 编辑器加载、`[InitializeOnLoadMethod]` 运行时（含域重载后）；
- 设置开关变化。

`GrowLensHost.Disable(reason)` 是失败路径：记录错误、把 Lens 标记为当前编辑器会话内禁用，并退订所有功能。已保存的开关保持启用；重启编辑器或显式关闭再打开会以新的失败计数重试。

宿主也把 batchmode 视为关闭，因此无头测试运行不会安装编辑器回调。

## 功能

| 功能 | 实现 | 说明 |
| --- | --- | --- |
| Hierarchy 组件图标 | `ItemComponentIcon` | 右对齐图标，按组件顺序排列；普通左键打开聚焦 Inspector，与组件 **Properties...** 行为一致 |
| Hierarchy 分隔符 | `HierarchySeparator` | 只读渲染 `##RRGGBB[#RRGGBB]Text` 标题；通过 **GameObject > Grow > Create Separator** 创建；搜索模式跳过 |
| Hierarchy 树连接线 | `ItemIndentLine` | 读取真实 `TreeViewGUI` 几何与可见行 depth，在祖先列绘制 T/L 连接线及横向臂 |
| 资产头部动作 | `GrowAssetHead` | 复制完整路径、资产路径与 GUID；Rider 动作可选，且被排除的类型不显示该行 |
| Project / Hierarchy 菜单 | `GrowContextMenus` + `GrowMenuActions` | Ctrl+右键；直接调用 Unity API 与 Unity 菜单项 |
| 工具栏菜单 | `GrowToolbar` | 左侧一个 **Grow** 入口，点击打开菜单；不改 PlayMode 容器 |

### Hierarchy 组件图标

行回调只拿到行矩形，因此图标不依赖树内部结构。图标从行右边缘开始按组件顺序排列，可见数量受对象名结尾限制；Hierarchy 越窄，能显示的图标越少。图标集合按对象缓存，在 `hierarchyChanged` 时重建。

在可见图标上普通左键点击，会在该行的 IMGUI 事件结束后通过延迟调用打开 Unity 的聚焦 Inspector（`PropertyEditor.OpenPropertyEditor`），并消费事件，因此 Hierarchy 不会再选中该行。带修饰键的点击、右键或点击图标矩形之外，事件仍交给 Hierarchy。如果无法解析该内部入口，回退路径会选中组件并打开 Inspector 窗口，这会改变当前选择。

### Hierarchy 分隔符

分隔符是显示约定，不是特殊对象类型：名字匹配格式的 GameObject 会被绘制成带居中文本的色带。Lens 不重排子节点，也不编辑该 GameObject，因此子节点保持原位，名字始终是唯一真源。

格式为 `##` + 3 位或 6 位背景色、或 `-` 表示主题背景，随后可选 `#` + 3 位或 6 位文字色，再是显示文本。例如 `##2E7D32 Level A`、`##F00#FFF Title`、`##-#FFD54F Note`、`##383838`。颜色没有 alpha 通道。文本会去除首尾空白；不匹配格式的名字就是普通名字，包括旧的单 `#` 形式与 5 位颜色。

选中的分隔符让出原生行——Lens 只画一条细选中标记，不画色带——因此选中高亮与重命名输入框仍然可用。搜索模式完全跳过分隔符绘制。创建入口有三个：`GameObject > Grow > Create Separator`、Hierarchy 上下文菜单或工具栏菜单；新建对象名为 `##383838 Section`，作为当前场景对象的兄弟节点放在其后（没有当前场景对象时放在根级），并被选中与 ping。Prefab Stage 中该命令禁用，因为分隔符是场景内容，不应写入预制体资产；多场景同时加载时，对象创建在当前对象所属场景。

### Hierarchy 树连接线

连接线渲染器通过反射读取当前窗口的 `TreeViewGUI` 几何：基础缩进、缩进宽度、foldout 宽度、行高、首行边距，以及可见行/深度列表。它由回调内容 x 推导行 depth，并拒绝 x 不在深度列上的行，从而覆盖横向滚动与布局变化。

每个祖先列在分支继续时画竖段；父列在有后续兄弟时画整行，末子时画半行，并画横向臂：分支停到 foldout 箭头，叶子延伸到内容。当前行自己的 foldout 列永不使用。depth 为 0（场景标题）、搜索中的树以及无法解析的几何一律不画；无法解析的情况只记录一次警告。

## 资产头部

`GrowAssetHead` 为单个持久化资产在 `Editor.finishedDefaultHeaderGUI` 上追加一行。它显示完整路径、工程相对路径、GUID；Rider 集成可解析时还显示 `Meta` 与 `File` 按钮。多选、没有资产路径或 GUID 的对象，以及导入器已自带头部的类型（`GameObject`、`MonoScript`、`TextAsset`、`Shader`、`Font`、`AudioClip`、`ComputeShader` 及其子类）不显示该行。Rider 通过反射解析，因此包对 Rider 包没有硬依赖。

## 上下文菜单

`GrowContextMenus` 处理 Project 与 Hierarchy 窗口中的 Ctrl+右键（macOS 为 Command+右键）。如果点击项尚未被选中，会先选中它，使基于选择的命令作用于该项。

- **Project**：复制名称、路径或 GUID；在文件管理器中显示（`Show in Explorer`）；新建文件夹、场景、材质、预制体或预制体变体；删除。需要资产身份的操作在没有身份时置灰。
- **Hierarchy**：复制名称或根到叶路径；复制最近预制体资产的 GUID；通过 Unity 自带菜单新建空对象、子对象或父对象，并重置新对象的局部 Transform；创建分隔符；删除。

`Delete` 调用 Unity 的 `Edit/Delete`，不额外增加确认。菜单动作都在失败守卫内执行，绘制行时不会修改层级或资产数据。

## 工具栏

`GrowToolbar` 反射 `UnityEditor.Toolbar`，取到 `m_Root`，向 `ToolbarZoneLeftAlign` 追加一个 `ToolbarButton`。它每 3 秒轮询一次，且仅在按钮缺失或脱离面板时轮询，因此工具栏重建后可以重新挂载，而不会出现重复按钮。不触碰 PlayMode 容器。如果内部类型、`m_Root` 或目标区域缺失，功能只记录一次警告并保持不激活，不会每帧抛异常。

菜单汇集常用编辑器窗口与命令。截图与打开文件夹动作使用项目根目录的 `Screenshots/`，本仓库已忽略该目录。

## 失败隔离

绘制回调经过 `GrowLensGuard.Run`，用户动作经过 `GrowLensGuard.RunAction`。某个绘制功能首次失败只记录一次；同一功能连续三次失败会在当前编辑器会话内关闭 Lens 并移除所有回调。成功的绘制调用会重置该功能的计数。菜单动作失败只记录日志，不会关闭整个包。这样 Unity 内部 API 损坏时不会每次重绘都刷屏，也不会把一次用户动作异常变成持久关闭。

## 限制

- Unity 内部 toolbar、Hierarchy 与 header 形状不是契约；升级 Unity 可能使对应功能失效。
- 树连接线由当前窗口的 `TreeViewGUI` 与可见行推导 depth、兄弟与末子状态；搜索模式、场景标题与无法解析的内部布局一律不画（fail closed），不用固定像素猜。
- 组件图标绘制在 GameObject 行上，不绘制在分隔符行上；Project 窗口里的资产图标不受影响。
- 所有装饰都由 `hierarchyWindowItemOnGUI` 绘制，因此 Lens 只装饰 Hierarchy 行；它没有 Stage 视图或 Project 窗口的覆盖层。
- 组件图标点击使用普通左键，打开聚焦 Inspector 窗口且不改变当前选择；修饰键点击与右键交回 Hierarchy。回退路径会选中组件。
- 分隔符使用 `##RRGGBB`、3 位 `##RGB` 或 `##-`；不识别单 `#` 形式。选中时让出原生行；搜索模式跳过分隔符渲染。
- Hierarchy 图标只在 `hierarchyChanged` 时清除，不是每次组件编辑都清；不触发该事件的组件变化可能保留旧图标，直到下一次变化。
- 上下文菜单需要 Ctrl+右键。复制与定位作用于点击行；Delete 作用于当前选择；创建类命令调用 Unity 内置的资产/GameObject 命令。
- 分隔符是真实 GameObject，因此会存在于运行时与构建产物中。
- 包没有本地化目录；UI 文案为英文。

## 测试

包内有 EditMode 测试程序集，覆盖不依赖 IMGUI 的部分：设置 JSON 往返、分隔符解析、组件图标布局、连接线几何、针对 2022.3 内部成员的反射探测，以及 Hierarchy 路径辅助方法。绘制、点击、菜单与工具栏需在编辑器中人工检查；自动化套件无法驱动它们。

Unity 2022.3 人工冒烟清单：

1. 在 **Preferences > Grow > Lens** 启用 Lens；工具栏入口应出现，且无需重启编辑器。
2. 打开 **Grow** 工具栏菜单，检查 Preferences、Console 与 Take Screenshot。
3. 选中材质或贴图确认资产头部动作；选中 Hierarchy 对象确认组件图标与树连接线。
4. 用普通左键点击组件图标；聚焦 Inspector 打开且当前选择不变。修饰键点击与右键仍属于 Hierarchy。
5. 在 Project 项与 Hierarchy 行上 Ctrl+右键，确认菜单作用于点击项。
6. 用 **GameObject > Grow > Create Separator** 创建分隔符，重命名为 `##2E7D32#FFFFFF Section`，再选中它确认原生行恢复。
7. 搜索 Hierarchy，确认分隔符与连接线消失。
8. 关闭 Lens，确认 Grow 入口与所有 Hierarchy/header 装饰消失。
9. 进入 Play mode、切换编辑器布局，确认 Grow 入口重新挂载且不重复。
10. 运行 `pwsh ci/run-tests.ps1 -Mode EditMode`；batch mode 不得安装回调。

## 另见

- [编辑器 Inspector](editor-inspector.md)——注解驱动的 Inspector。
- [架构](../architecture/README.md)——编辑器程序集在 Grow 中的位置。
- [术语表](../reference/glossary.md)——这些页面共用的术语。
