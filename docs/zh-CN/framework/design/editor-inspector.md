<!-- source: docs/en/framework/design/editor-inspector.md @ blob:80ef7d24e4a66ab688242429c65f4f9adebca2c4 -->
<!-- 中文译文；以英文 docs/en/framework/design/editor-inspector.md 为准，译文可能滞后于原文。blob 是英文文件的内容 sha，可用 git hash-object docs/en/framework/design/editor-inspector.md 核验。 -->

# 编辑器 Inspector

**其他语言：** [English](../../../en/framework/design/editor-inspector.md) | [简体中文](editor-inspector.md)

*设计记录*——`com.ronny.grow` 自带的注解驱动 Inspector。

本设计覆盖 `Grow.Core.Attributes` 中的创作注解，以及 `Grow.Editor.Inspector` 中的编辑器侧实现。可选的 `com.ronny.grow.lens` 工具包另有[设计记录](editor-lens.md)。

## 目标

- 让序列化字段在字段旁一次性声明标签、分组、顺序、条件、只读状态与值变更回调。
- 让方法直接变成带类型参数的按钮，不必为每个类型手写 Editor。
- 保持默认 Inspector 可用：没有 Grow Drawer 或 PropertyDrawer 处理时，所有注解都退化为无操作。
- 把绘制流程（`GrowEditorBase`）与注解模型（`GrowEditor`）分离：完全自定义面板可以只复用生命周期，不继承模型。
- 包内不引入 Store、第三方库或全局枚举 Drawer。

## 非目标

- 替换 Unity Inspector，或实现通用序列化/编辑器框架。
- 完整表达式语言：条件只支持字段路径、方法调用、取反和单次等值比较。
- 后台自动保存；保存是显式的 `SaveAll()` 动作。
- 项目级 `[Flags]` 枚举 Drawer；`GrowEnumFlags` 按字段显式启用。
- 为所有 Unity 类型提供自定义绘制；模型只针对根级序列化字段。

## 两档注解

| 档位 | 注解 | 默认 Inspector 是否生效 |
| --- | --- | --- |
| Property / Decorator Drawer | `GrowLabel`、`GrowReadOnly`、`GrowHelpBox`、`GrowSeparator`、`GrowEnumFlags` | 是 |
| 仅驱动 | `GrowPropertyOrder`、`GrowGroup`、`GrowShowIf` / `GrowHideIf` / `GrowEnableIf` / `GrowDisableIf`、`GrowOnValueChanged`、`GrowButton` | 否；需要 `GrowEditor` |

仅驱动的注解是普通 `Attribute`，默认 Inspector 会忽略且不报错。Drawer 注解派生自 `PropertyAttribute`，由 `PropertyDrawer` 或 `DecoratorDrawer` 处理。属性 Drawer 按固定顺序链式绘制——`GrowLabel`（`-1000`）→ `GrowReadOnly`（`-500`）→ `GrowEnumFlags`（默认 order）——因此一个字段可以同时带标签、只读状态与位标志；每个 Drawer 都经 `PropertyField` 转发给下一个。`GrowHelpBox` 与 `GrowSeparator` 是 Decorator Drawer，绘制在字段上方，不改变字段本身的绘制方式。

## 工作流

`GrowEditorBase : Editor` 负责绘制流程：

1. 首次 `Layout` 调用一次 `OnSetup`，并订阅 `beforeAssemblyReload` 以触发一次 `OnEditorDestroy`。
2. 每次 `Layout` 调用 `OnCalc`。
3. 每帧绘制只读脚本头，在变更检测中调用 `OnDraw`，应用未提交的 `SerializedObject` 改动，并在本帧确有变更时触发 `OnChanged`。本帧内变脏的目标——立即 Apply 或调用 `[GrowButton]`——同样算作变更。
4. `OnDisable` 调用 `OnDisabled` 并应用未提交改动；它不写盘。
5. `SaveAll()` 应用未提交改动，写入脏的持久化目标资产，在启用 `ApplyPrefabInstanceToAsset` 时写回预制体实例，在启用 `SaveScene` 时保存已加载且为脏的场景。与目标无关的脏资产不会被保存。

基类刻意不包含调度器和自动写盘路径。延迟保存调度器需要实例级状态与经过测试的写盘路径；显式动作更可预测、也更容易测试。

## 注解模型

`InspectorTypeModel` 按目标类型构建模型：

- 除 `m_Script` 之外的根级可见属性变成 `FieldModel`；模型保留序列化顺序作为并列时的排序依据；
- 带 `GrowButton` 的实例 `void` 方法变成 `ButtonModel`。静态方法、泛型方法与非 `void` 方法会被跳过；参数类型不受支持的方法会被跳过并记录一条警告；
- 字段与按钮共享同一套排序空间：先按显式 `GrowPropertyOrder`，再字段优先于按钮，最后字段按序列化顺序、按钮按方法名排序。未标顺序的成员使用 `int.MaxValue`，因此未标注的按钮排在字段之后；
- `GrowGroup` 路径按 `/` 拆分、去除首尾空白并丢弃空段，合并成组树，再输出有序令牌流（`FieldToken`、`ButtonToken`、`GroupToken`）。每个组是单一连续区块，由排序最靠前的成员锚定；
- 叶子组只要有直接成员要求 `Box` 就是 `Box`；含子组的容器组一律 `Foldout`。

模型按类型缓存，由 `ClearCache()`（测试）或域重载清除。折叠状态属于视图状态：它保存在 Editor 实例上，不在模型里。

## 条件

`GrowShowIf` / `GrowHideIf` 控制可见性；`GrowEnableIf` / `GrowDisableIf` 控制可编辑性。表达式语法：

| 写法 | 含义 |
| --- | --- |
| `Field` | 为真：`true`、非零数字或枚举、非空字符串、非 null 对象 |
| `!Field` | 取反 |
| `Field == value` | 与数字、`true` / `false`、枚举成员名或数值、字符串或 `null` 相等 |
| `Method()` | 调用无参方法并判断其返回值；`Method() == value` 改为比较返回值 |

字段路径可用点号分隔（`Stats.Enabled`）。枚举成员名区分大小写。每个注解只支持一次比较，不支持 `!=`、`&&`、`||`、范围或算术。

组合规则：

- `ShowIf` 与 `EnableIf`：该类型的每个注解都必须成立。
- `HideIf` 与 `DisableIf`：只要有一个注解成立，字段就隐藏或禁用。
- 无法解析的子句会被忽略，因此拼写错误不会隐藏或禁用字段。未解析子句不会记录警告；同一个表达式同时出现在 `ShowIf` 与 `HideIf` 时，模型构建期会记录一条警告。

多目标时可见性按目标聚合：全部可见正常绘制，部分可见绘制但禁用（混合态），全部不可见则隐藏。可编辑性要求所有目标都可编辑。条件子句通过反射在每个目标上求值，因此不依赖 `SerializedObject`，并由 EditMode 测试覆盖。

## 按钮

`GrowButton` 标在实例方法上，绘制带标题和 `Invoke` 按钮的卡片。支持的参数类型为 `int`、`long`、`float`、`double`、`bool`、`string` 与枚举。标题取自注解标签，为空时回退方法名。

- 带参数的方法默认折叠；展开后每个参数一个对应类型的输入控件。
- 参数值保存在渲染器中，并以目标类型全名、方法签名与参数下标为键持久化到 `EditorPrefs`，因此重载方法不会互相覆盖。只有点击 `Invoke` 才写入；在那之前使用类型默认值。
- `[Flags]` 枚举参数复用字段同一套层级多选；非 Flags 枚举参数使用 Unity 原生下拉。
- 方法会在每个选中目标上按顺序调用一次，并对每个目标调用 `Undo.RecordObject`。异常会被捕获并记录，其余目标继续执行。调用后由所有者更新 `SerializedObject`、标脏所有目标、报告变更并重绘。

## 枚举位标志

`GrowEnumFlags` 把 `[Flags]` 枚举绘制为带搜索、三态分组行和 `Nothing` / `Everything` 项的层级多选。搜索匹配完整的 `value : Name : Label` 文本，保留每个命中项的父分组，并隐藏 `Nothing`、`Everything` 与不相关分组。只有正的单比特成员参与勾选；零值、负值与多比特组合值不作为可勾选叶子。所有位都已呈现的多比特成员，在 `showCombos` 为 `true`（默认）时可以作为整体切换行出现。`[Obsolete]` 成员保持可见但禁用，避免位丢失。枚举定义了 `None` / `Nothing` / `All` / `Everything` 成员时，`Nothing` 与 `Everything` 的文案取自这些成员，否则回退为 `Nothing` / `Everything`。

没有全局枚举 Drawer。未标注的枚举使用 Unity 原生绘制，包括原生 `EnumFlagsField`。Drawer 作用于直接的 `[Flags]` 枚举字段；数组与 `List<T>` 字段回退为原生绘制，非 `[Flags]` 枚举会记录一条警告。

## 公开 API 与命名

所有注解都带 `Grow` 前缀（`GrowButton`、`GrowLabel`、`GrowGroup` 等），因此可以在同一文件里与 Odin Inspector、NaughtyAttributes 等同名注解共存。`GrowLabel` 作用于序列化字段，包括枚举成员；按钮标题使用 `GrowButton(label)`。

`GrowEditor` 与 `GrowEditorBase` 是预期的扩展点：

- `GrowEditor` 增加注解模型、用于在令牌顺序之外单独绘制字段的 `DrawAnnotatedField(path)`，以及可先调用 `base.OnDraw()` 再追加内容的 `OnDraw` 重写。
- `GrowEditorBase` 提供生命周期（`OnSetup`、`OnCalc`、`OnDraw`、`OnChanged`、`OnDisabled`、`OnEditorDestroy`）、`ForceWideMode`、`SaveScene`、`ApplyPrefabInstanceToAsset`、`MarkChanged()` 与 `SaveAll()`。

模型、条件求值器、按钮渲染器、Drawer 与枚举辅助类属于编辑器程序集的实现细节。

## 限制

- 只对根级字段做令牌化；嵌套可序列化类中的字段不会被驱动注解标注。Drawer 注解是普通 Property Drawer，Unity 绘制该字段时仍会生效。
- 条件表达式不支持布尔运算符、范围、算术或 `!=`。
- 工作流会在 `OnDraw` 之前绘制脚本头；需要不同脚本头的完全自定义面板必须自行处理。
- `GrowLabel` 标在枚举成员上时，只有 `GrowEnumFlags` 多选树会读取；非 Flags 按钮参数使用的 Unity 原生枚举下拉不显示该标签。
- 按钮参数缓冲使用 `EditorPrefs`。它是编辑器 UI 状态，不是游戏数据；键是用户全局的（Windows 上跨工程共享），值只在点击按钮时写入。
- Inspector 文案为英文。包的本地化目录只覆盖设置页。

## 测试

EditMode 测试覆盖无 IMGUI 的部分：类型模型（排序、分组、Tooltip、按钮收集）、条件解析与求值、多目标聚合、按钮参数编解码与签名，以及枚举位标志的展示数据。Drawer 绘制、保存、Undo 与点击需在编辑器中人工检查。运行方式：`pwsh ci/run-tests.ps1 -Mode EditMode`。

## 另见

- [编辑器 Lens](editor-lens.md)——可选的编辑器工具包。
- [架构](../architecture/README.md)——编辑器程序集在 Grow 中的位置。
- [术语表](../reference/glossary.md)——这些页面共用的术语。
