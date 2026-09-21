# GrowEvent 技术设计（Core/Event 事件原语 v1）

- 状态：已实现（`Runtime/Core/Event/`；`InvocationList` 按 container-catalog §7 组合 `SnapshotSet<T>`）
- 日期：2026-09-19
- 范围：`Packages/com.ronny.grow/Runtime/Core/Event/*`、`Packages/com.ronny.grow/Tests/Editor/*`
- 关联：`docs/grow-event-invocationlist.md`（派发内核专题）、`docs/grow-architecture-rationale.md` §8、`docs/architecture/ADR-0002-domain-reload-disabled.md`
- 参考：`com.ronnywu.grow@3.1.0-exp.3/com.grow.core/Runtime/Events/*` 及其 `GrowEvent-ADR.md`

## 1. 背景与目标

`Runtime/Core/Event/GrowEvent.cs` 目前为空壳。本设计为框架建立第一个运行时通信原语，服务 Update 驱动等场景：`MonoManager.Update` 暴露一个事件，PlayerLoop 注册的子系统每帧调用其 Raiser，事件内部遍历监听。

必须满足：

- 同级监听互不影响：一个监听抛异常不阻断同轮其余监听；
- 派发期增删安全：执行期 Add/Remove/Clear 不破坏本轮遍历；
- 稳态零分配：无变更轮次派发不产生 GC；
- 无静态状态：满足 ADR-0002 的域重载复位契约（无需 Reset）；
- 纯主线程、无锁。

非目标（v1）：全局 EventBus、多参泛型（仅 T0）、异步与延迟派发、日志节流、PlayerLoop 驱动本体与 MonoManager（属 Kernel/Loop、Kernel/Lifecycle，另立任务）。

## 2. 定位与关键决策

| 决策 | 结论 | 依据 |
|---|---|---|
| 模块落点 | `Runtime/Core/Event`，命名空间 `Grow.Core.Event` | GrowEvent 是被动原语、不掌时机（§8.6 判据），删除 Kernel 后仍可编译/测试/复用；`Kernel/Events` 留给未来全局 EventBus（§8.7 EventBus 行） |
| 属主模型 | `Add/Remove` + 显式 `IGrowEventRaiser`；`Invoke/Clear` 不出现在叶子上 | 对齐 C# event 属主语义 |
| 状态 | 实例态、零静态字段 | ADR-0002；GrowEvent 无需 `GrowBoot.OnReset` 参与 |
| 依赖 | Core 零上向依赖；异常上报用 `UnityEngine.Debug`（被动 API） | §8.2 / §8.9；不引用不存在的 `GrowApp` / `LogChannel` |
| 线程 | 主线程订阅/触发，无锁、不做断言 | 更新驱动天然主线程；跨线程投递将来由 Kernel/Scheduling 负责 |
| 异常 | 逐监听隔离 + fatal 重抛 + 立即上报 | 见 §4 / §6.4 |
| 风格 | K&R 大括号、极少注释（对齐 `GrowBoot.cs`） | 项目现有代码风格 |

已确认的运行策略：

1. fatal 重抛是「同级互不影响」的唯一例外；
2. v1 立即记录 + 预留上报 seam，节流留待 `Core/Log`；
3. 异常监听保留订阅，不自动退订。

## 3. 改进方向

1. **按新架构重新定位**：把参考实现当作 Core 被动原语，而非连带上层的运行时服务；移除一切不存在的依赖（`GrowApp.IsMainThread`、`LogChannel`、`UnifiedClassService.PostToMain`）。
2. **异常分级化**：由「全吞 + 仅 Editor 记录」改为「非致命 → 隔离且必然可见；致命 → 重抛」，预留统一上报缝。
3. **为帧驱动优化**：空事件常数时间早退、连续数组遍历、无每帧分配、派发边界释放引用；固化「变更下一次派发生效」语义。
4. **收紧公开面**：叶子 `sealed`、保留 `Count` 与 `in T0`、内部引擎不暴露。
5. **为演进留缝、不提前泛化**：多参、异步、延迟派发、节流均为后续独立任务。

## 4. 改进内容（相对参考实现）

| # | 改进 | 做法 | 影响 |
|---|---|---|---|
| I1 | fatal 不吞 | `catch (Exception ex) when (!EventFaults.IsFatal(ex))`；致命类型 + `UnityEngine.ExitGUIException` 原样上抛 | 修复 OOM/栈溢出被吞、编辑器 IMGUI 控制流被破坏 |
| I2 | 上报缝 + 必然可见 | 内部 `EventFaults.Report(handler, ex)`：含监听标识（`DeclaringType.Method`）与原堆栈，单条日志；`Report` 自身再包一层 try/catch；不再 Editor-only | 玩家端也可见；将来只改一处即可切到 `Core/Log` |
| I3 | 派发边界释放 | `EndDispatch` 深度归零且版本已变时：置空快照引用 + 标脏（`_builtVersion = -1`），不重建、不分配；重建推迟到下次 `BeginDispatch` | 修复「Clear 后事件长期持有监听引用」；不可抛，不掩盖 fatal |
| I4 | API 收紧 | 叶子 `sealed`；保留 `Count`；接口 `IGrowEventRaiser<in T0>`；删除死代码 `LogTag` | 表面积小、语义完整 |
| I5 | 依赖与文档校正 | 代码极少注释（语义见本设计与专题文档）；删除对不存在 API 的引用 | 与新架构与仓库风格一致 |

## 5. 吸收的点

| 来源 | 吸收 | 落地 |
|---|---|---|
| UniTask `PlayerLoopRunner` | 逐项 try/catch 后回调自身再防护；出错不阻断其余项 | I2 的 `Report` 内层 try/catch |
| UniTask `PlayerLoopHelper` | 主线程 id 运行时捕获而非硬编码；关闭域重载时重初始化 | 未来 Kernel/Loop 驱动（本文仅记录约定） |
| R3 | 「OnError 不应终止管道」；异常不退订 | 采用：隔离继续 + 保留订阅 |
| R3 / MessagePipe | 订阅泄漏是主要风险（Tracker/analyzer） | 后续 Tooling 诊断（`Count` 已具备） |
| MessagePipe | 默认 fail-fast、隔离靠 filter | 否决：默认直接隔离，不做 filter 管线 |
| MediatR | 发布策略可替换（串行/并行聚合） | 否决：框架给确定默认，避免配置错位 |
| Unity 原生 | 逐接收者隔离、每帧记录日志 | 采用其行为基线；时间型节流归 Kernel/Log |
| 方案 B（帧延迟双缓冲） | 命令缓冲 / 同帧最后态 wins | 记为未来 Kernel/Scheduling 的延迟派发队列，不进 Core 原语 |
| 方案 A / B | 异常钩子概念 | 由 `EventFaults` seam 覆盖，并补齐 fatal 过滤与默认可见性 |

## 6. 详细设计

### 6.1 文件与可见性

```
Runtime/Core/Event/
├─ IGrowEventRaiser.cs     public  IGrowEventRaiser / IGrowEventRaiser<in T0>
├─ GrowEvent.cs            public sealed GrowEvent : IGrowEventRaiser
├─ GrowEventT0.cs          public sealed GrowEvent<T0> : IGrowEventRaiser<T0>
├─ InvocationList.cs       internal sealed InvocationList<TDelegate> where TDelegate : Delegate
└─ EventFaults.cs          internal static（fatal 判定 + 上报）
```

`Tests/Editor/` 为包内唯一测试程序集（纯 EditMode）；`Packages/manifest.json` 需加入 `"testables": ["com.ronny.grow"]`。

### 6.2 公开 API

```csharp
public interface IGrowEventRaiser { void Invoke(); void Clear(); }
public interface IGrowEventRaiser<in T0> { void Invoke(T0 arg0); void Clear(); }

public sealed class GrowEvent : IGrowEventRaiser
{
    public void Add(Action handler);
    public void Remove(Action handler);
    public int Count { get; }
    void IGrowEventRaiser.Invoke();   // 显式：仅属主/驱动器可用
    void IGrowEventRaiser.Clear();
}
public sealed class GrowEvent<T0> : IGrowEventRaiser<T0> { /* 同形，Action<T0> */ }
```

属主模型：属主持有事件与其 Raiser（`_raiser = Updated;`），订阅侧只见 `Add/Remove/Count`。

### 6.3 派发内核

引擎 `InvocationList<TDelegate>` 的完整契约、不变量、数据结构、复杂度与替代方案对比见 **`docs/grow-event-invocationlist.md`**。对外形态：

```csharp
internal sealed class InvocationList<TDelegate> where TDelegate : Delegate
{
    internal InvocationList();
    internal InvocationList(int capacity);
    internal int Count { get; }
    internal void Add(TDelegate handler);
    internal void Remove(TDelegate handler);
    internal void Clear();
    internal bool BeginDispatch(out TDelegate[] snapshot, out int count);
    internal void EndDispatch();
}
```

叶子派发：

```csharp
void IGrowEventRaiser.Invoke()
{
    if (!_list.BeginDispatch(out var snapshot, out var count)) return;
    try
    {
        for (var i = 0; i < count; i++)
        {
            var action = snapshot[i];
            try { action(); }
            catch (Exception ex) when (!EventFaults.IsFatal(ex))
            { try { EventFaults.Report(action, ex); } catch { } }
        }
    }
    finally { _list.EndDispatch(); }
}
```

### 6.4 异常契约

- **保证**：非致命异常 → 本轮其余监听照常、下轮照常、监听保留订阅；引擎状态（快照/版本/深度）不被破坏。
- **例外**：fatal 重抛并终止本轮。fatal 清单：`OutOfMemoryException`、`StackOverflowException`、`AccessViolationException`、`ThreadAbortException`（按 API 兼容条件编译）、`AppDomainUnloadedException`、`BadImageFormatException`、`InvalidProgramException`、`UnityEngine.ExitGUIException`。
- **可见性**：默认立即记录一条，含监听标识与原堆栈；`Report` 自身异常不得打断派发链。
- **节流**：不在 Core 实现（Core 无时机）；由将来 `Core/Log` 的 sink 承担。

### 6.5 Update 驱动契约（未来 Kernel/Loop，本次只保证原语侧）

```
PlayerLoopSystem.updateDelegate（安装一次、无每帧分配）
  → 阶段回调（ProfilerMarker 包裹）
    → IMonoExecutor.PhaseRaiser.Invoke()   （属主面）
      → GrowEvent.Invoke（版本检查 → 空早退 → 快照遍历 → 逐监听隔离）
```

- 订阅方缓存 `Action`（`_onUpdate = Update;`）以避免方法组重复分配。
- 驱动侧不再加兜底 catch：非致命已隔离，逃出的只剩 fatal 或框架 bug，都应响亮暴露。
- GrowEvent 不含时间概念、不引用 PlayerLoop；无静态状态，域重载/场景重载天然安全。

### 6.6 性能与内存契约

| 场景 | 目标 |
|---|---|
| 稳态（无增删） | 版本比较 + 连续数组遍历 + 每监听 1 委托调用；零分配 |
| 变更后首次派发 | O(n) 重建，容器倍增摊销；无临时数组 |
| 空事件 | 常数时间，不进入循环 |
| 派发期变更 | 本轮快照不变；最外层退出即置空引用并标脏 |
| 线程 | 仅主线程；无锁、无同步原语 |

## 7. 替代方案与否决理由

- **方案 A「改进的事件容器」**：变更帧 `BuildSnapshot()` 每次 `new T[Count]` 额外分配；异常钩子设在 internal 引擎上、叶子无入口，玩家端静默；丢失 `Count`/`in T0`；`Remove` 内立即压缩带来派发中途抖动。否决。
- **方案 B「帧延迟双缓冲事件系统」**：调度器无人注册导致命令永不应用，按所给代码不可用；链表节点数组只增不减导致内存泄漏、哈希墓碑无回收；默认完全静默吞异常；语义变为「下一帧末由调度器统一生效」，强依赖 MonoBehaviour 与执行顺序；含 `MonoBehaviour`/`Mathf`/静态单例，违反 Core 无时机与 ADR-0002。否决，其命令缓冲思想转为未来 Kernel/Scheduling 的延迟派发队列。

## 8. 测试设计

| 文件 | 覆盖 |
|---|---|
| `GrowEventOrderingTests` | 注册序、首中尾移除、`Remove→Add` 排队尾、交错增删、去重、null 忽略 |
| `GrowEventSnapshotTests` | 派发期增删/清空本轮不变下轮生效、空事件、连续无变更轮复用 |
| `GrowEventExceptionTests` | 首/中/尾抛→其余继续；连续抛；异常后恢复；异常不退订；异常+重入；手工 `new StackOverflowException()/OutOfMemoryException()` 验证穿透；`LogAssert` 校验上报 |
| `GrowEventReentrancyTests` | 嵌套派发从头开始、深度归零、嵌套中增删 |
| `GrowEventGenericTests` | `GrowEvent<T0>` 参数/顺序/隔离；`in T0` 逆变编译验证 |
| `GrowEventAllocationTests` | 稳态多轮 Invoke 分配趋近 0、变更帧摊还有界（`Category("Grow.Events.GC")` 可过滤） |

## 9. 风险与开放问题

| 项 | 处理 |
|---|---|
| GC 断言在编辑器可能抖动 | 预热 + 宽松阈值 + 可过滤 Category |
| fatal 清单跨 API 兼容（`ThreadAbortException`） | 条件编译，缺类型则跳过 |
| `sealed` 收紧 | 无既有消费方；如需继承扩展再放开 |
| 文档归档 | 本文为阶段性结论；定稿后可转 ADR（编号顺延 ADR-0003） |

## 10. 后续演进（本次不做）

1. Kernel/Loop：PlayerLoop 阶段安装 + 驱动 + 域重载重初始化；Kernel/Lifecycle：MonoManager 门面与属主。
2. Core/Log：被动门面接管 `EventFaults` 上报，实施节流。
3. 多参泛型叶 `GrowEvent<T0,T1…>`（引擎与参数无关，扩展成本低）。
4. Tooling：订阅泄漏诊断（基于 `Count`/订阅堆栈）。
5. Kernel/Scheduling：延迟派发队列（吸收方案 B 的命令缓冲思想，由 Kernel 掌时）。
