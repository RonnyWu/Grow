# ADR-0002: 关闭 Domain Reload 作为 Grow 默认运行模式

- 状态：Accepted
- 日期：2026-09-18
- 关联：`docs/explanation/exp-0001-architecture-rationale.md`（§8.6 Kernel 掌时）、`Runtime/Kernel/Entry/GrowBoot.cs`、`Editor/Setup/GrowProjectSetup.cs`

## 背景（Context）

Unity 默认在进入 Play 模式时重载整个应用程序域（Domain Reload），静态字段、静态事件、单例因此被 CLR 清空。代价是每次进入 Play 都要付出数秒的重载时间，且随代码规模增长而恶化。

`Enter Play Mode Options` 允许关闭 Domain Reload。关闭后：

- 进入 Play 近乎瞬时，迭代速度显著提升；
- 但静态状态**跨 Play 存活**，不再自动复位。

Grow 的 Kernel 已在 `GrowBoot` 中提供最早的复位时机：

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void SubsystemRegistration() { ... OnReset?.Invoke(); }
```

该钩子在每次进入 Play 时都会被调用（无论是否重载域），因此具备承担显式复位的能力。

## 决策（Decision）

1. **Grow 官方推荐且受支持的运行模式为：关闭 Domain Reload，保留 Scene Reload。**
   即 `EditorSettings.enterPlayModeOptionsEnabled = true` 且 `enterPlayModeOptions = DisableDomainReload`。
2. **通过显式菜单应用，而非静默修改。** 入口为 `Tools/Grow/Setup/Apply Project Settings`（幂等），`Tools/Grow/Setup/Diagnose` 只读报告当前状态。Grow 不使用 `[InitializeOnLoad]` 在导入时偷偷改动全局项目设置。
3. **配套硬契约（随本 ADR 一并生效）：**
   1. 一切静态状态（静态字段、单例、缓存）必须能够由 `SubsystemRegistration` 阶段的 `OnReset` 复位；
   2. 静态事件必须在 `OnReset` 中统一退订，防止跨 Play 重复订阅；
   3. 各模块的 Reset 行为应有对应测试，验证复位后为「干净态」。
4. **运行时代码不得假设域重载存在或不存在。** 关闭 Domain Reload 是 Editor-only 的体验与正确性约束；构建产物每次都是全新进程，不受影响。
5. **v1 不关闭 Scene Reload。** 现有复位契约只覆盖静态状态，不覆盖场景对象状态；若未来要支持「域与场景都不重载」的极速模式，需先补充场景重置契约（另立 ADR）。

## 结果（Consequences）

**正面**

- 进入 Play 的速度不再随代码规模恶化；
- 静态状态泄漏在开发期即暴露，而非依赖 CLR 隐式清理；
- 编辑器启动语义与构建启动语义对齐（都从「干净态」开始）；
- 使 Kernel 的 Reset 契约从可选卫生上升为框架硬约束。

**负面**

- 每个模块都必须实现并测试 Reset，漏一处即造成跨 Play 状态泄漏；
- 静态事件若未退订会导致重复订阅、内存泄漏或回调到失效对象；
- 携带自身静态缓存的第三方 SDK 可能不兼容；
- 新成员可能因「值在两次 Play 间保留」而感到困惑。

**中性**

- 对构建产物无运行时影响。

## 备选方案（Alternatives）

- **保持 Domain Reload 开启（默认）**：失去迭代收益，且不强制状态规范；框架仍可工作，故不作为默认，但保持兼容。
- **同时关闭 Domain Reload 与 Scene Reload**：速度更极致，但需要额外的场景状态复位契约，超出 v1 范围，暂缓。
- **`[InitializeOnLoad]` 自动写入设置**：免去手动步骤，但会静默修改全局项目设置并波及用户代码与第三方；被否决。

## 参考（References）

- Unity Manual — Configurable Enter Play Mode：https://docs.unity3d.com/2021.3/Documentation/Manual/ConfigurableEnterPlayMode.html
- Unity Scripting API — `UnityEditor.EditorSettings`、`UnityEditor.EnterPlayModeOptions`
- `Packages/com.ronny.grow/Runtime/Kernel/Entry/GrowBoot.cs`
- `Packages/com.ronny.grow/Editor/Setup/GrowProjectSetup.cs`
