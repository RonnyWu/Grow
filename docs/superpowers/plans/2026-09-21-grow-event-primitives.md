# GrowEvent 事件原语实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 `Grow.Core.Event` 落地框架第一个运行时通信原语——`InvocationList<TDelegate>` 派发内核、`EventFaults` 异常上报缝、`IGrowEventRaiser` 接口、`GrowEvent` 与 `GrowEvent<T0>` 叶子——实现「逐监听异常隔离 + 派发期增删安全 + 稳态零分配」。

**Architecture:** 存储与快照两侧机制**不复制**：`InvocationList<TDelegate>` 仅组合已完成的 `Grow.Core.Collections.SnapshotSet<TDelegate>`（其内部又组合 `OrderedSet<T>`），只补 `null` 忽略、`where TDelegate : Delegate` 身份语义与 `BeginDispatch/EndDispatch` 命名。叶子 `GrowEvent`/`GrowEvent<T0>` 持有 `InvocationList`，负责「版本检查 → 空早退 → 快照顺序遍历 → 逐监听隔离 → 边界释放」，异常隔离与上报由内部 `EventFaults` 承担。全部为实例态、零静态字段、仅主线程、无锁。

**Tech Stack:** Unity 2021.3.45f2、C# 9、.NET Standard 2.1、NUnit（com.unity.test-framework 1.1.33）。

**Spec:**
- `docs/grow-event-design.md`（叶子 API、异常契约、§8 测试设计与文件名）
- `docs/grow-event-invocationlist.md`（I-1…I-7 不变量、§7 语义边界与测试锚点）
- `docs/grow-container-catalog.md` §5.7 / §7（原语回填与 G-5 硬约束）
- `docs/grow-architecture-rationale.md` §8（落点与命名）、`docs/architecture/ADR-0002-domain-reload-disabled.md`

## Global Constraints

- Unity **2021.3.45f2**、C# 9、API 兼容级别 **.NET Standard 2.1**；不得使用晚于该基线的 API。
- 落点与命名空间：`Packages/com.ronny.grow/Runtime/Core/Event/`，命名空间 `Grow.Core.Event`。
- 每个 `.cs`（含测试）以 3 行 MIT 头开始：`// Copyright (c) 2026 Ronny Wu` / `// Licensed under the MIT License.` / `// See LICENSE file in the project root for full license information.`
- 不添加任何代码注释。
- 仅主线程、无锁、无同步原语。
- 默认零静态；不得引入静态字段/缓存（ADR-0002 无需复位）。
- 无第三方依赖；仅 BCL 与 `UnityEngine`（`Debug`、`ExitGUIException`）。
- 测试程序集沿用 `Tests/Editor/Grow.Tests.Editor.asmdef`，测试命名空间 `Grow.Tests`；`Packages/manifest.json` 的 `testables` 已配置，不再改动。
- `Grow.Tests.Editor` 已通过 `Runtime/AssemblyInfo.cs` 的 `InternalsVisibleTo` 访问 `internal` 类型，无需再改 asmdef。
- Conventional Commits；用户可见变更更新 `Packages/com.ronny.grow/CHANGELOG.md`。
- **提交权限**：本计划的 Commit 步骤为约定节奏；按本仓库规则，`git commit` 仅在用户明确授权后执行。
- 验证方式：Unity 2021.3.45f2 `Window > General > Test Runner > EditMode > Run All`（或当前会话的 MCP `run_tests`）。

### 与 spec 的一处显式对齐

`docs/grow-event-invocationlist.md` §6 给出的 `InvocationList` 是**自包含状态机**，而同一文档 §9 与 `docs/grow-container-catalog.md` §7.1/§7.2 已**修订该结论**：存储机制回填 `OrderedSet<T>`、快照机制回填 `SnapshotSet<T>`，`InvocationList` 改为**组合**（G-5 硬约束：「机制在框架内只允许一份实现」，评审以「是否复制了状态机」为硬性否决项）。

本计划按修订后的结论执行：`InvocationList<TDelegate>` **只组合** `SnapshotSet<TDelegate>`，不持有 `List`/`Dictionary`/快照数组。因此本计划**不实现** `grow-event-invocationlist.md` §6 的代码块；该节的 I-1…I-7 不变量作为验收基线，由 `SnapshotSet<T>` 既有测试 + 本计划 `InvocationListTests` 共同覆盖。

---

### 文件结构

| 文件 | 职责 |
|---|---|
| `Packages/com.ronny.grow/Runtime/Core/Event/EventFaults.cs` | fatal 判定 + 单条可见上报（内部，新增） |
| `Packages/com.ronny.grow/Runtime/Core/Event/InvocationList.cs` | 组合 `SnapshotSet<TDelegate>` 的委托派发内核（内部，新增） |
| `Packages/com.ronny.grow/Runtime/Core/Event/IGrowEventRaiser.cs` | `IGrowEventRaiser` / `IGrowEventRaiser<in T0>`（公开，新增） |
| `Packages/com.ronny.grow/Runtime/Core/Event/GrowEvent.cs` | 无参叶子（公开，**替换现有空壳**） |
| `Packages/com.ronny.grow/Runtime/Core/Event/GrowEventT0.cs` | 单参叶子 `GrowEvent<T0>`（公开，新增） |
| `Packages/com.ronny.grow/Tests/Editor/EventFaultsTests.cs` | fatal 判定与上报（新增） |
| `Packages/com.ronny.grow/Tests/Editor/InvocationListTests.cs` | 派发内核容器侧契约（新增） |
| `Packages/com.ronny.grow/Tests/Editor/GrowEventOrderingTests.cs` | 注册序 / 去重 / 移除 / null（新增） |
| `Packages/com.ronny.grow/Tests/Editor/GrowEventSnapshotTests.cs` | 派发期增删清空、空事件、稳态复用（新增） |
| `Packages/com.ronny.grow/Tests/Editor/GrowEventReentrancyTests.cs` | 嵌套派发、深度归零、嵌套中变更（新增） |
| `Packages/com.ronny.grow/Tests/Editor/GrowEventExceptionTests.cs` | 隔离、上报、fatal 穿透、异常不退订（新增） |
| `Packages/com.ronny.grow/Tests/Editor/GrowEventGenericTests.cs` | `GrowEvent<T0>` 参数传递、顺序、隔离、逆变（新增） |
| `Packages/com.ronny.grow/Tests/Editor/GrowEventAllocationTests.cs` | 稳态零分配 / 变更帧有界（新增，`Category("Grow.Events.GC")`） |
| `Packages/com.ronny.grow/CHANGELOG.md` | 记录新增公开类型（修改） |

---

### Task 1: `EventFaults`（fatal 判定 + 上报缝）

**Files:**
- Create: `Packages/com.ronny.grow/Runtime/Core/Event/EventFaults.cs`
- Test: `Packages/com.ronny.grow/Tests/Editor/EventFaultsTests.cs`

**Interfaces:**
- Consumes: 无。
- Produces:
  - `internal static class Grow.Core.Event.EventFaults`
  - `internal static bool IsFatal(Exception exception)`
  - `internal static void Report(Delegate handler, Exception exception)`

- [ ] **Step 1: 写失败测试**

创建 `Packages/com.ronny.grow/Tests/Editor/EventFaultsTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Text.RegularExpressions;
using Grow.Core.Event;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Grow.Tests {
    [TestFixture]
    public sealed class EventFaultsTests {
        [Test]
        public void IsFatal_OrdinaryExceptions_ReturnsFalse() {
            Assert.IsFalse(EventFaults.IsFatal(new InvalidOperationException("x")));
            Assert.IsFalse(EventFaults.IsFatal(new ArgumentException("x")));
            Assert.IsFalse(EventFaults.IsFatal(new Exception("x")));
        }

        [Test]
        public void IsFatal_FatalExceptions_ReturnsTrue() {
            Assert.IsTrue(EventFaults.IsFatal(new OutOfMemoryException()));
            Assert.IsTrue(EventFaults.IsFatal(new StackOverflowException()));
            Assert.IsTrue(EventFaults.IsFatal(new AccessViolationException()));
            Assert.IsTrue(EventFaults.IsFatal(new AppDomainUnloadedException()));
            Assert.IsTrue(EventFaults.IsFatal(new BadImageFormatException()));
            Assert.IsTrue(EventFaults.IsFatal(new InvalidProgramException()));
            Assert.IsTrue(EventFaults.IsFatal(new ExitGUIException()));
        }

        [Test]
        public void Report_LogsSingleErrorWithListenerIdentity() {
            Action handler = OnEvent;
            LogAssert.Expect(LogType.Error, new Regex("EventFaultsTests\\.OnEvent"));
            EventFaults.Report(handler, new InvalidOperationException("boom"));
        }

        [Test]
        public void Report_NullHandler_DoesNotThrow() {
            LogAssert.Expect(LogType.Error, new Regex("<unknown>"));
            Assert.DoesNotThrow(() => EventFaults.Report(null, new Exception("boom")));
        }

        private static void OnEvent() { }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: Unity Test Runner → EditMode → Run All。
Expected: 编译失败（`EventFaults` 不存在）。

- [ ] **Step 3: 写最小实现**

创建 `Packages/com.ronny.grow/Runtime/Core/Event/EventFaults.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using UnityEngine;

namespace Grow.Core.Event {
    internal static class EventFaults {
        internal static bool IsFatal(Exception exception) {
            if (exception is OutOfMemoryException) return true;
            if (exception is StackOverflowException) return true;
            if (exception is AccessViolationException) return true;
            if (exception is AppDomainUnloadedException) return true;
            if (exception is BadImageFormatException) return true;
            if (exception is InvalidProgramException) return true;
            if (exception is ExitGUIException) return true;
#if NET_4_6 || NET_UNITY_4_8
            if (exception is System.Threading.ThreadAbortException) return true;
#endif
            return false;
        }

        internal static void Report(Delegate handler, Exception exception) {
            Debug.LogError(
                $"[Grow] Event listener '{Describe(handler)}' threw. The listener stays subscribed.\n{exception}");
        }

        private static string Describe(Delegate handler) {
            if (handler == null) return "<unknown>";
            var method = handler.Method;
            var type = method.DeclaringType;
            return type == null ? method.Name : type.FullName + "." + method.Name;
        }
    }
}
```

说明：`ThreadAbortException` 在 .NET Standard 2.1 下不可用（基线即此级别），故以 `NET_4_6`/`NET_UNITY_4_8` 条件编译包裹；当前基线两符号均未定义，该分支被排除，符合 `grow-event-design.md` §9「缺类型则跳过」。

- [ ] **Step 4: 运行测试确认通过**

Run: Unity Test Runner → EditMode → Run All。
Expected: PASS，`EventFaultsTests` 全部通过。

- [ ] **Step 5: Commit**

```bash
git add Packages/com.ronny.grow/Runtime/Core/Event/EventFaults.cs Packages/com.ronny.grow/Tests/Editor/EventFaultsTests.cs
git commit -m "feat(event): add EventFaults fatal classification and reporting seam"
```

---

### Task 2: `InvocationList<TDelegate>` 派发内核（组合 `SnapshotSet`）

**Files:**
- Create: `Packages/com.ronny.grow/Runtime/Core/Event/InvocationList.cs`
- Test: `Packages/com.ronny.grow/Tests/Editor/InvocationListTests.cs`

**Interfaces:**
- Consumes: `Grow.Core.Collections.SnapshotSet<T>`（`Add`/`Remove`/`Count`/`Clear`/`BeginRead`/`EndRead`）。
- Produces:
  - `internal sealed class Grow.Core.Event.InvocationList<TDelegate> where TDelegate : Delegate`
  - `internal InvocationList()`, `internal InvocationList(int capacity)`
  - `internal int Count { get; }`
  - `internal void Add(TDelegate handler)`, `internal void Remove(TDelegate handler)`, `internal void Clear()`
  - `internal bool BeginDispatch(out TDelegate[] snapshot, out int count)`, `internal void EndDispatch()`

- [ ] **Step 1: 写失败测试**

创建 `Packages/com.ronny.grow/Tests/Editor/InvocationListTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Grow.Core.Event;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class InvocationListTests {
        [Test]
        public void Add_Duplicate_IsDeduplicated() {
            var list = new InvocationList<Action>();
            Action handler = Handle;
            list.Add(handler);
            list.Add(handler);
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void Add_Null_IsIgnored() {
            var list = new InvocationList<Action>();
            list.Add(null);
            Assert.AreEqual(0, list.Count);
            Assert.IsFalse(list.BeginDispatch(out _, out _));
        }

        [Test]
        public void Remove_Null_IsIgnored() {
            var list = new InvocationList<Action>();
            list.Add(Handle);
            list.Remove(null);
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void Remove_Unknown_LeavesStateUnchanged() {
            var list = new InvocationList<Action>();
            list.Add(Handle);
            list.Remove(Other);
            Assert.AreEqual(1, list.Count);
        }

        [Test]
        public void RemoveThenAdd_AppendsToTail() {
            var list = new InvocationList<Action>();
            var order = new List<string>();
            Action a = () => order.Add("a");
            Action b = () => order.Add("b");
            list.Add(a);
            list.Add(b);
            list.Remove(a);
            list.Add(a);

            Dispatch(list);

            CollectionAssert.AreEqual(new[] { "b", "a" }, order);
        }

        [Test]
        public void BeginDispatch_Empty_ReturnsFalseAndStaysReusable() {
            var list = new InvocationList<Action>();
            Assert.IsFalse(list.BeginDispatch(out _, out var count));
            Assert.AreEqual(0, count);
            list.EndDispatch();

            list.Add(Handle);
            Assert.IsTrue(list.BeginDispatch(out var snapshot, out var nonEmpty));
            Assert.AreEqual(1, nonEmpty);
            Assert.IsNotNull(snapshot[0]);
            list.EndDispatch();
        }

        [Test]
        public void BeginDispatch_SteadyState_ReusesSameBuffer() {
            var list = new InvocationList<Action>();
            list.Add(Handle);
            Assert.IsTrue(list.BeginDispatch(out var first, out _));
            list.EndDispatch();
            Assert.IsTrue(list.BeginDispatch(out var second, out _));
            Assert.AreSame(first, second);
            list.EndDispatch();
        }

        [Test]
        public void Dispatch_MutationDuringRound_AppliesNextRound() {
            var list = new InvocationList<Action>();
            var seen = new List<string>();
            Action added = () => seen.Add("added");
            Action first = null;
            first = () => {
                seen.Add("first");
                list.Add(added);
                list.Remove(first);
            };
            list.Add(first);

            Dispatch(list);
            CollectionAssert.AreEqual(new[] { "first" }, seen);
            Assert.AreEqual(1, list.Count);

            seen.Clear();
            Dispatch(list);
            CollectionAssert.AreEqual(new[] { "added" }, seen);
        }

        [Test]
        public void ClearDuringDispatch_RunsCurrentRoundThenEmpty() {
            var list = new InvocationList<Action>();
            var seen = new List<string>();
            Action first = () => {
                seen.Add("first");
                list.Clear();
            };
            Action second = () => seen.Add("second");
            list.Add(first);
            list.Add(second);

            Dispatch(list);

            CollectionAssert.AreEqual(new[] { "first", "second" }, seen);
            Assert.AreEqual(0, list.Count);
            Assert.IsFalse(list.BeginDispatch(out _, out _));
        }

        [Test]
        public void NestedDispatch_ReusesSnapshotAndRestartsFromHead() {
            var list = new InvocationList<Action>();
            var order = new List<string>();
            var nested = false;
            Action second = () => order.Add("second");
            Action first = null;
            first = () => {
                order.Add("first");
                if (nested) return;
                nested = true;
                Dispatch(list);
            };
            list.Add(first);
            list.Add(second);

            Dispatch(list);

            CollectionAssert.AreEqual(new[] { "first", "first", "second", "second" }, order);
        }

        private static void Dispatch(InvocationList<Action> list) {
            if (!list.BeginDispatch(out var snapshot, out var count)) return;
            try {
                for (var i = 0; i < count; i++) snapshot[i]();
            } finally {
                list.EndDispatch();
            }
        }

        private static void Handle() { }

        private static void Other() { }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: Unity Test Runner → EditMode → Run All。
Expected: 编译失败（`InvocationList` 不存在）。

- [ ] **Step 3: 写最小实现**

创建 `Packages/com.ronny.grow/Runtime/Core/Event/InvocationList.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using Grow.Core.Collections;

namespace Grow.Core.Event {
    internal sealed class InvocationList<TDelegate> where TDelegate : Delegate {
        private const int DefaultCapacity = 8;

        private readonly SnapshotSet<TDelegate> _set;

        internal InvocationList() : this(DefaultCapacity) { }

        internal InvocationList(int capacity) {
            if (capacity < 1) capacity = DefaultCapacity;
            _set = new SnapshotSet<TDelegate>(capacity);
        }

        internal int Count => _set.Count;

        internal void Add(TDelegate handler) {
            if (handler == null) return;
            _set.Add(handler);
        }

        internal void Remove(TDelegate handler) {
            if (handler == null) return;
            _set.Remove(handler);
        }

        internal void Clear() => _set.Clear();

        internal bool BeginDispatch(out TDelegate[] snapshot, out int count) =>
            _set.BeginRead(out snapshot, out count);

        internal void EndDispatch() => _set.EndRead();
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: Unity Test Runner → EditMode → Run All。
Expected: PASS，`InvocationListTests` 全部通过（I-1…I-7 容器侧契约由 `SnapshotSet<T>` 既有实现承担）。

- [ ] **Step 5: Commit**

```bash
git add Packages/com.ronny.grow/Runtime/Core/Event/InvocationList.cs Packages/com.ronny.grow/Tests/Editor/InvocationListTests.cs
git commit -m "feat(event): add InvocationList dispatch kernel composing SnapshotSet"
```

---

### Task 3: `IGrowEventRaiser` 接口与 `GrowEvent` 核心

**Files:**
- Create: `Packages/com.ronny.grow/Runtime/Core/Event/IGrowEventRaiser.cs`
- Modify: `Packages/com.ronny.grow/Runtime/Core/Event/GrowEvent.cs`（替换空壳）
- Test: `Packages/com.ronny.grow/Tests/Editor/GrowEventOrderingTests.cs`

**Interfaces:**
- Consumes: `InvocationList<TDelegate>`（Task 2）。
- Produces:
  - `public interface Grow.Core.Event.IGrowEventRaiser { void Invoke(); void Clear(); }`
  - `public interface Grow.Core.Event.IGrowEventRaiser<in T0> { void Invoke(T0 arg0); void Clear(); }`
  - `public sealed class Grow.Core.Event.GrowEvent : IGrowEventRaiser`
  - `public GrowEvent()`, `public GrowEvent(int capacity)`, `public int Count { get; }`
  - `public void Add(Action handler)`, `public void Remove(Action handler)`
  - `void IGrowEventRaiser.Invoke()`、`void IGrowEventRaiser.Clear()`（显式实现）

注：本步 `Invoke` **不含**逐监听隔离（异常会中断本轮并上抛），由 Task 5 补齐——这是刻意的 TDD 失败基线。

- [ ] **Step 1: 写失败测试**

创建 `Packages/com.ronny.grow/Tests/Editor/GrowEventOrderingTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Grow.Core.Event;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class GrowEventOrderingTests {
        [Test]
        public void Invoke_RunsListenersInRegistrationOrder() {
            var e = new GrowEvent();
            var order = new List<string>();
            Action a = () => order.Add("a");
            Action b = () => order.Add("b");
            Action c = () => order.Add("c");
            e.Add(a);
            e.Add(b);
            e.Add(c);

            Raise(e);

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, order);
        }

        [Test]
        public void Remove_FirstMiddleLast_KeepsSurvivorOrder() {
            var e = new GrowEvent();
            var order = new List<string>();
            Action a = () => order.Add("a");
            Action b = () => order.Add("b");
            Action c = () => order.Add("c");
            Action d = () => order.Add("d");
            e.Add(a);
            e.Add(b);
            e.Add(c);
            e.Add(d);

            e.Remove(a);
            e.Remove(c);

            Raise(e);

            CollectionAssert.AreEqual(new[] { "b", "d" }, order);
            Assert.AreEqual(2, e.Count);
        }

        [Test]
        public void RemoveThenAdd_MovesHandlerToTail() {
            var e = new GrowEvent();
            var order = new List<string>();
            Action a = () => order.Add("a");
            Action b = () => order.Add("b");
            e.Add(a);
            e.Add(b);
            e.Remove(a);
            e.Add(a);

            Raise(e);

            CollectionAssert.AreEqual(new[] { "b", "a" }, order);
        }

        [Test]
        public void Add_Duplicate_RunsOnceAndKeepFirstPosition() {
            var e = new GrowEvent();
            var order = new List<string>();
            Action a = () => order.Add("a");
            Action b = () => order.Add("b");
            e.Add(a);
            e.Add(b);
            e.Add(a);

            Raise(e);

            Assert.AreEqual(2, e.Count);
            CollectionAssert.AreEqual(new[] { "a", "b" }, order);
        }

        [Test]
        public void Add_Null_IsIgnored() {
            var e = new GrowEvent();
            Assert.DoesNotThrow(() => e.Add(null));
            Assert.AreEqual(0, e.Count);
        }

        [Test]
        public void Remove_Unknown_IsIgnored() {
            var e = new GrowEvent();
            e.Add(OnB);
            Assert.DoesNotThrow(() => e.Remove(OnA));
            Assert.AreEqual(1, e.Count);
        }

        [Test]
        public void InterleavedAddRemove_KeepsInsertionOrderOfSurvivors() {
            var e = new GrowEvent();
            var order = new List<string>();
            Action a = () => order.Add("a");
            Action b = () => order.Add("b");
            Action c = () => order.Add("c");
            Action d = () => order.Add("d");
            e.Add(a);
            e.Add(b);
            e.Remove(a);
            e.Add(c);
            e.Remove(b);
            e.Add(d);

            Raise(e);

            CollectionAssert.AreEqual(new[] { "c", "d" }, order);
        }

        internal static void Raise(GrowEvent e) => ((IGrowEventRaiser)e).Invoke();

        private static void OnA() { }

        private static void OnB() { }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: Unity Test Runner → EditMode → Run All。
Expected: 编译失败（`IGrowEventRaiser` 不存在；`GrowEvent` 无 `Add`/`Remove`/`Count`）。

- [ ] **Step 3: 写最小实现**

创建 `Packages/com.ronny.grow/Runtime/Core/Event/IGrowEventRaiser.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

namespace Grow.Core.Event {
    public interface IGrowEventRaiser {
        void Invoke();
        void Clear();
    }

    public interface IGrowEventRaiser<in T0> {
        void Invoke(T0 arg0);
        void Clear();
    }
}
```

替换 `Packages/com.ronny.grow/Runtime/Core/Event/GrowEvent.cs` 全文为：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;

namespace Grow.Core.Event {
    public sealed class GrowEvent : IGrowEventRaiser {
        private const int DefaultCapacity = 8;

        private readonly InvocationList<Action> _list;

        public GrowEvent() : this(DefaultCapacity) { }

        public GrowEvent(int capacity) {
            _list = new InvocationList<Action>(capacity);
        }

        public int Count => _list.Count;

        public void Add(Action handler) => _list.Add(handler);

        public void Remove(Action handler) => _list.Remove(handler);

        void IGrowEventRaiser.Invoke() {
            if (!_list.BeginDispatch(out var snapshot, out var count)) return;
            try {
                for (var i = 0; i < count; i++) snapshot[i]();
            } finally {
                _list.EndDispatch();
            }
        }

        void IGrowEventRaiser.Clear() => _list.Clear();
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: Unity Test Runner → EditMode → Run All。
Expected: PASS，`GrowEventOrderingTests` 全部通过。

- [ ] **Step 5: Commit**

```bash
git add Packages/com.ronny.grow/Runtime/Core/Event/IGrowEventRaiser.cs Packages/com.ronny.grow/Runtime/Core/Event/GrowEvent.cs Packages/com.ronny.grow/Tests/Editor/GrowEventOrderingTests.cs
git commit -m "feat(event): add IGrowEventRaiser and GrowEvent core with ordered dispatch"
```

---

### Task 4: 快照语义与重入验证

**Files:**
- Test: `Packages/com.ronny.grow/Tests/Editor/GrowEventSnapshotTests.cs`
- Test: `Packages/com.ronny.grow/Tests/Editor/GrowEventReentrancyTests.cs`
- Modify（仅当测试暴露缺陷时）: `Packages/com.ronny.grow/Runtime/Core/Event/GrowEvent.cs`

**Interfaces:**
- Consumes: `GrowEvent`（Task 3）、`InvocationList<TDelegate>`（Task 2）。
- Produces: 无新公开 API；固化「变更下一次派发生效」「嵌套复用同一快照」「最外层释放」行为。

- [ ] **Step 1: 写快照与重入测试**

创建 `Packages/com.ronny.grow/Tests/Editor/GrowEventSnapshotTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Grow.Core.Event;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class GrowEventSnapshotTests {
        [Test]
        public void AddDuringInvoke_TakesEffectNextRound() {
            var e = new GrowEvent();
            var seen = new List<string>();
            Action added = () => seen.Add("added");
            Action existing = () => {
                seen.Add("existing");
                e.Add(added);
            };
            e.Add(existing);

            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "existing" }, seen);

            seen.Clear();
            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "existing", "added" }, seen);
        }

        [Test]
        public void RemoveDuringInvoke_StillRunsThisRoundThenStops() {
            var e = new GrowEvent();
            var seen = new List<string>();
            Action second = () => seen.Add("second");
            Action first = null;
            first = () => {
                seen.Add("first");
                e.Remove(second);
            };
            e.Add(first);
            e.Add(second);

            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "first", "second" }, seen);
            Assert.AreEqual(1, e.Count);

            seen.Clear();
            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "first" }, seen);
        }

        [Test]
        public void ClearDuringInvoke_RunsRemainingThisRoundThenEmpty() {
            var e = new GrowEvent();
            var seen = new List<string>();
            Action first = () => {
                seen.Add("first");
                ((IGrowEventRaiser)e).Clear();
            };
            Action second = () => seen.Add("second");
            e.Add(first);
            e.Add(second);

            GrowEventOrderingTests.Raise(e);

            CollectionAssert.AreEqual(new[] { "first", "second" }, seen);
            Assert.AreEqual(0, e.Count);

            seen.Clear();
            GrowEventOrderingTests.Raise(e);
            Assert.AreEqual(0, seen.Count);
        }

        [Test]
        public void Invoke_EmptyEvent_IsNoOp() {
            var e = new GrowEvent();
            Assert.DoesNotThrow(() => GrowEventOrderingTests.Raise(e));
        }

        [Test]
        public void Invoke_SteadyState_ReusesSnapshotBuffer() {
            var list = new InvocationList<Action>();
            list.Add(() => { });
            Assert.IsTrue(list.BeginDispatch(out var first, out _));
            list.EndDispatch();
            Assert.IsTrue(list.BeginDispatch(out var second, out _));
            Assert.AreSame(first, second);
            list.EndDispatch();
        }

        [Test]
        public void Invoke_AfterMutationOutsideRound_RebuildsSnapshot() {
            var e = new GrowEvent();
            var count = 0;
            e.Add(() => count++);

            GrowEventOrderingTests.Raise(e);
            e.Add(() => count++);
            GrowEventOrderingTests.Raise(e);

            Assert.AreEqual(3, count);
        }
    }
}
```

创建 `Packages/com.ronny.grow/Tests/Editor/GrowEventReentrancyTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Grow.Core.Event;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class GrowEventReentrancyTests {
        [Test]
        public void NestedInvoke_RestartsFromHeadWithinSameSnapshot() {
            var e = new GrowEvent();
            var order = new List<string>();
            var nested = false;
            Action second = () => order.Add("second");
            Action first = null;
            first = () => {
                order.Add("first");
                if (nested) return;
                nested = true;
                GrowEventOrderingTests.Raise(e);
            };
            e.Add(first);
            e.Add(second);

            GrowEventOrderingTests.Raise(e);

            CollectionAssert.AreEqual(new[] { "first", "first", "second", "second" }, order);
        }

        [Test]
        public void NestedInvoke_MutationAppliesNextOutermostRound() {
            var e = new GrowEvent();
            var seen = new List<string>();
            var nested = false;
            Action added = () => seen.Add("added");
            Action first = null;
            first = () => {
                seen.Add("first");
                e.Add(added);
                if (nested) return;
                nested = true;
                GrowEventOrderingTests.Raise(e);
            };
            e.Add(first);

            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "first", "first" }, seen);

            seen.Clear();
            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "first", "added" }, seen);
        }

        [Test]
        public void ClearAfterNestedInvoke_NextRoundIsEmpty() {
            var e = new GrowEvent();
            var nested = false;
            var calls = 0;
            Action first = null;
            first = () => {
                calls++;
                if (nested) return;
                nested = true;
                GrowEventOrderingTests.Raise(e);
            };
            e.Add(first);

            GrowEventOrderingTests.Raise(e);
            Assert.AreEqual(2, calls);

            ((IGrowEventRaiser)e).Clear();
            calls = 0;
            GrowEventOrderingTests.Raise(e);
            Assert.AreEqual(0, calls);
        }
    }
}
```

- [ ] **Step 2: 运行测试**

Run: Unity Test Runner → EditMode → Run All。
Expected: PASS（Task 3 的 `try/finally` 已保证深度归零；`SnapshotSet<T>` 既有实现已保证快照语义）。若任一用例失败，即为实现缺陷，在 Step 3 修复后重跑。

- [ ] **Step 3: 仅在失败时修复实现**

若 Step 2 失败，按失败断言定位到 `GrowEvent.cs` 的 `Invoke`（`try/finally` 缺失会表现为 `ClearAfterNestedInvoke_NextRoundIsEmpty` 失败），修好后重跑至 PASS。全部通过则跳过本步。

- [ ] **Step 4: Commit**

```bash
git add Packages/com.ronny.grow/Tests/Editor/GrowEventSnapshotTests.cs Packages/com.ronny.grow/Tests/Editor/GrowEventReentrancyTests.cs
git commit -m "test(event): lock snapshot and reentrancy semantics for GrowEvent"
```

---

### Task 5: 逐监听异常隔离与 fatal 穿透

**Files:**
- Modify: `Packages/com.ronny.grow/Runtime/Core/Event/GrowEvent.cs`（`Invoke` 循环加隔离）
- Test: `Packages/com.ronny.grow/Tests/Editor/GrowEventExceptionTests.cs`

**Interfaces:**
- Consumes: `EventFaults`（Task 1）、`GrowEvent`（Task 3）。
- Produces: 固化异常契约——非致命隔离并上报、监听不退订；fatal 重抛但深度归零。

- [ ] **Step 1: 写失败测试**

创建 `Packages/com.ronny.grow/Tests/Editor/GrowEventExceptionTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Grow.Core.Event;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Grow.Tests {
    [TestFixture]
    public sealed class GrowEventExceptionTests {
        [Test]
        public void FirstListenerThrows_RestStillRun() {
            var e = new GrowEvent();
            var seen = new List<string>();
            e.Add(() => throw new InvalidOperationException("first"));
            e.Add(() => seen.Add("second"));

            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);

            CollectionAssert.AreEqual(new[] { "second" }, seen);
        }

        [Test]
        public void MiddleListenerThrows_RestStillRun() {
            var e = new GrowEvent();
            var seen = new List<string>();
            e.Add(() => seen.Add("first"));
            e.Add(() => throw new InvalidOperationException("middle"));
            e.Add(() => seen.Add("third"));

            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);

            CollectionAssert.AreEqual(new[] { "first", "third" }, seen);
        }

        [Test]
        public void LastListenerThrows_DoesNotAffectEarlierListeners() {
            var e = new GrowEvent();
            var seen = new List<string>();
            e.Add(() => seen.Add("first"));
            e.Add(() => throw new InvalidOperationException("last"));

            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            Assert.DoesNotThrow(() => GrowEventOrderingTests.Raise(e));

            CollectionAssert.AreEqual(new[] { "first" }, seen);
        }

        [Test]
        public void ThrowingListener_StaysSubscribedAndRunsNextRound() {
            var e = new GrowEvent();
            var calls = 0;
            e.Add(() => {
                calls++;
                throw new InvalidOperationException("always");
            });

            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);
            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);

            Assert.AreEqual(2, calls);
            Assert.AreEqual(1, e.Count);
        }

        [Test]
        public void RepeatedThrows_EachReportedAndNeighborsSurvive() {
            var e = new GrowEvent();
            var seen = new List<string>();
            e.Add(() => throw new InvalidOperationException("one"));
            e.Add(() => throw new InvalidOperationException("two"));
            e.Add(() => seen.Add("survivor"));

            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);

            CollectionAssert.AreEqual(new[] { "survivor" }, seen);
        }

        [Test]
        public void FatalException_PropagatesAndSkipsRemainingListeners() {
            var e = new GrowEvent();
            var ran = false;
            e.Add(() => throw new StackOverflowException());
            e.Add(() => ran = true);

            Assert.Throws<StackOverflowException>(() => GrowEventOrderingTests.Raise(e));
            Assert.IsFalse(ran);
        }

        [Test]
        public void AfterFatalException_StateStaysUsable() {
            var e = new GrowEvent();
            var ran = false;
            e.Add(() => throw new OutOfMemoryException());
            Assert.Throws<OutOfMemoryException>(() => GrowEventOrderingTests.Raise(e));

            ((IGrowEventRaiser)e).Clear();
            e.Add(() => ran = true);
            GrowEventOrderingTests.Raise(e);
            Assert.IsTrue(ran);
        }

        [Test]
        public void ThrowingListenerWithReentrancy_StateStaysConsistent() {
            var e = new GrowEvent();
            var seen = new List<string>();
            var nested = false;
            Action second = () => seen.Add("second");
            Action first = null;
            first = () => {
                seen.Add("first");
                if (nested) throw new InvalidOperationException("nested");
                nested = true;
                GrowEventOrderingTests.Raise(e);
            };
            e.Add(first);
            e.Add(second);

            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);

            Assert.AreEqual(2, e.Count);
            seen.Clear();
            LogAssert.Expect(LogType.Error, new Regex("GrowEventExceptionTests"));
            GrowEventOrderingTests.Raise(e);
            CollectionAssert.AreEqual(new[] { "first", "second" }, seen);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: Unity Test Runner → EditMode → Run All。
Expected: 多数用例 FAIL（首个异常中断本轮，后续监听不执行；且无日志导致 `LogAssert` 报错）。

- [ ] **Step 3: 写最小实现**

修改 `Packages/com.ronny.grow/Runtime/Core/Event/GrowEvent.cs`，把 `void IGrowEventRaiser.Invoke()` 替换为：

```csharp
        void IGrowEventRaiser.Invoke() {
            if (!_list.BeginDispatch(out var snapshot, out var count)) return;
            try {
                for (var i = 0; i < count; i++) {
                    var action = snapshot[i];
                    try {
                        action();
                    } catch (Exception ex) when (!EventFaults.IsFatal(ex)) {
                        try { EventFaults.Report(action, ex); } catch { }
                    }
                }
            } finally {
                _list.EndDispatch();
            }
        }
```

说明：`catch (...) when (...) ` 过滤非致命异常；fatal 不被捕获、原样上抛，`finally` 仍保证 `EndDispatch` 归零。内层 `try { Report } catch { }` 保证上报自身异常不打断派发链。

- [ ] **Step 4: 运行测试确认通过**

Run: Unity Test Runner → EditMode → Run All。
Expected: PASS，`GrowEventExceptionTests` 全部通过（fatal 用例靠 `Assert.Throws` 捕获，不视为日志失败）。

- [ ] **Step 5: Commit**

```bash
git add Packages/com.ronny.grow/Runtime/Core/Event/GrowEvent.cs Packages/com.ronny.grow/Tests/Editor/GrowEventExceptionTests.cs
git commit -m "feat(event): isolate non-fatal listener exceptions in GrowEvent"
```

---

### Task 6: `GrowEvent<T0>` 单参叶子

**Files:**
- Create: `Packages/com.ronny.grow/Runtime/Core/Event/GrowEventT0.cs`
- Test: `Packages/com.ronny.grow/Tests/Editor/GrowEventGenericTests.cs`

**Interfaces:**
- Consumes: `InvocationList<TDelegate>`、`EventFaults`、`IGrowEventRaiser<in T0>`。
- Produces: `public sealed class Grow.Core.Event.GrowEvent<T0> : IGrowEventRaiser<T0>`，含 `GrowEvent()`、`GrowEvent(int capacity)`、`Count`、`Add(Action<T0>)`、`Remove(Action<T0>)`、显式 `Invoke(T0)` / `Clear()`。

- [ ] **Step 1: 写失败测试**

创建 `Packages/com.ronny.grow/Tests/Editor/GrowEventGenericTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Grow.Core.Event;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Grow.Tests {
    [TestFixture]
    public sealed class GrowEventGenericTests {
        [Test]
        public void Invoke_PassesArgumentToEveryListener() {
            var e = new GrowEvent<string>();
            var seen = new List<string>();
            e.Add(v => seen.Add("a:" + v));
            e.Add(v => seen.Add("b:" + v));

            ((IGrowEventRaiser<string>)e).Invoke("x");

            CollectionAssert.AreEqual(new[] { "a:x", "b:x" }, seen);
        }

        [Test]
        public void Invoke_RunsListenersInRegistrationOrder() {
            var e = new GrowEvent<int>();
            var order = new List<int>();
            e.Add(v => order.Add(v + 1));
            e.Add(v => order.Add(v + 2));
            e.Add(v => order.Add(v + 3));

            ((IGrowEventRaiser<int>)e).Invoke(10);

            CollectionAssert.AreEqual(new[] { 11, 12, 13 }, order);
        }

        [Test]
        public void Invoke_MutationDuringRound_AppliesNextRound() {
            var e = new GrowEvent<int>();
            var seen = new List<int>();
            Action<int> added = v => seen.Add(v * 100);
            Action<int> first = v => {
                seen.Add(v);
                e.Add(added);
            };
            e.Add(first);

            ((IGrowEventRaiser<int>)e).Invoke(1);
            CollectionAssert.AreEqual(new[] { 1 }, seen);

            seen.Clear();
            ((IGrowEventRaiser<int>)e).Invoke(2);
            CollectionAssert.AreEqual(new[] { 2, 200 }, seen);
        }

        [Test]
        public void ListenerThrows_OthersStillRunAndErrorReported() {
            var e = new GrowEvent<int>();
            var seen = new List<int>();
            e.Add(v => throw new InvalidOperationException("bad"));
            e.Add(v => seen.Add(v));

            LogAssert.Expect(LogType.Error, new Regex("GrowEventGenericTests"));
            ((IGrowEventRaiser<int>)e).Invoke(7);

            CollectionAssert.AreEqual(new[] { 7 }, seen);
        }

        [Test]
        public void Clear_EmptiesEventAndLeavesItReusable() {
            var e = new GrowEvent<int>();
            e.Add(v => { });
            ((IGrowEventRaiser<int>)e).Clear();

            Assert.AreEqual(0, e.Count);
            var ran = false;
            e.Add(v => ran = true);
            ((IGrowEventRaiser<int>)e).Invoke(1);
            Assert.IsTrue(ran);
        }

        [Test]
        public void Raiser_IsContravariant() {
            var e = new GrowEvent<string>();
            IGrowEventRaiser<object> raiser = e;
            var seen = new List<object>();
            e.Add(v => seen.Add(v));

            raiser.Invoke("contravariant");

            CollectionAssert.AreEqual(new object[] { "contravariant" }, seen);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: Unity Test Runner → EditMode → Run All。
Expected: 编译失败（`GrowEvent<T0>` 不存在）。

- [ ] **Step 3: 写最小实现**

创建 `Packages/com.ronny.grow/Runtime/Core/Event/GrowEventT0.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;

namespace Grow.Core.Event {
    public sealed class GrowEvent<T0> : IGrowEventRaiser<T0> {
        private const int DefaultCapacity = 8;

        private readonly InvocationList<Action<T0>> _list;

        public GrowEvent() : this(DefaultCapacity) { }

        public GrowEvent(int capacity) {
            _list = new InvocationList<Action<T0>>(capacity);
        }

        public int Count => _list.Count;

        public void Add(Action<T0> handler) => _list.Add(handler);

        public void Remove(Action<T0> handler) => _list.Remove(handler);

        void IGrowEventRaiser<T0>.Invoke(T0 arg0) {
            if (!_list.BeginDispatch(out var snapshot, out var count)) return;
            try {
                for (var i = 0; i < count; i++) {
                    var action = snapshot[i];
                    try {
                        action(arg0);
                    } catch (Exception ex) when (!EventFaults.IsFatal(ex)) {
                        try { EventFaults.Report(action, ex); } catch { }
                    }
                }
            } finally {
                _list.EndDispatch();
            }
        }

        void IGrowEventRaiser<T0>.Clear() => _list.Clear();
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: Unity Test Runner → EditMode → Run All。
Expected: PASS，`GrowEventGenericTests` 全部通过。

- [ ] **Step 5: Commit**

```bash
git add Packages/com.ronny.grow/Runtime/Core/Event/GrowEventT0.cs Packages/com.ronny.grow/Tests/Editor/GrowEventGenericTests.cs
git commit -m "feat(event): add GrowEvent<T0> single-argument leaf"
```

---

### Task 7: 稳态零分配回归

**Files:**
- Test: `Packages/com.ronny.grow/Tests/Editor/GrowEventAllocationTests.cs`

**Interfaces:**
- Consumes: `GrowEvent`（Task 5）。
- Produces: 可过滤类别 `Grow.Events.GC` 的分配回归测试。

- [ ] **Step 1: 写分配测试**

创建 `Packages/com.ronny.grow/Tests/Editor/GrowEventAllocationTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using Grow.Core.Event;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    [Category("Grow.Events.GC")]
    public sealed class GrowEventAllocationTests {
        [Test]
        public void SteadyState_Invoke_AllocatesLittle() {
            var e = new GrowEvent(64);
            var sink = 0;
            for (var i = 0; i < 64; i++) {
                var value = i;
                e.Add(() => sink += value);
            }
            for (var warmup = 0; warmup < 64; warmup++) GrowEventOrderingTests.Raise(e);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalMemory(false);
            for (var round = 0; round < 4096; round++) GrowEventOrderingTests.Raise(e);
            var after = GC.GetTotalMemory(false);

            Assert.Greater(sink, 0);
            Assert.Less(after - before, 4096, "steady-state invoke must allocate ~0 bytes");
        }

        [Test]
        public void MutationFrame_Invoke_AllocatesBounded() {
            var e = new GrowEvent(64);
            var sink = 0;
            Action stable = () => sink++;
            for (var warmup = 0; warmup < 64; warmup++) {
                GrowEventOrderingTests.Raise(e);
                e.Add(stable);
                e.Remove(stable);
            }
            e.Add(stable);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalMemory(false);
            for (var round = 0; round < 4096; round++) {
                e.Add(stable);
                e.Remove(stable);
                e.Add(stable);
                GrowEventOrderingTests.Raise(e);
            }
            var after = GC.GetTotalMemory(false);

            Assert.Greater(sink, 0);
            Assert.Less(after - before, 32768, "mutation-frame invoke must allocate bounded bytes");
        }
    }
}
```

- [ ] **Step 2: 运行测试**

Run: Unity Test Runner → EditMode → Run All（或过滤 `Grow.Events.GC`）。
Expected: PASS。编辑器噪声下若偶发 FAIL，重复运行确认稳定性；仅在稳定复现时才调整阈值，量级保持「稳态每轮 < 1 字节」。

- [ ] **Step 3: Commit**

```bash
git add Packages/com.ronny.grow/Tests/Editor/GrowEventAllocationTests.cs
git commit -m "test(event): add steady-state allocation regression for GrowEvent"
```

---

### Task 8: CHANGELOG 与设计文档状态

**Files:**
- Modify: `Packages/com.ronny.grow/CHANGELOG.md`
- Modify: `docs/grow-event-design.md:3`（状态行）
- Modify: `docs/grow-event-invocationlist.md:3`（状态行）

**Interfaces:**
- Consumes: 全部前述任务。
- Produces: 用户可见变更记录与设计文档状态推进（Draft → 已实现）。

- [ ] **Step 1: 更新 CHANGELOG**

在 `Packages/com.ronny.grow/CHANGELOG.md` 的 `## [Unreleased]` → `### Added` 下追加：

```markdown
- Add `Grow.Core.Event.IGrowEventRaiser` / `IGrowEventRaiser<in T0>`, `GrowEvent` and `GrowEvent<T0>` (typed event primitives: per-listener exception isolation, mutation-safe dispatch, zero-allocation steady state).
```

- [ ] **Step 2: 推进设计文档状态**

把 `docs/grow-event-design.md` 第 3 行与 `docs/grow-event-invocationlist.md` 第 3 行由：

```
- 状态：阶段性结论（Draft，待实现验证）
```

改为：

```
- 状态：已实现（`Runtime/Core/Event/`；`InvocationList` 按 container-catalog §7 组合 `SnapshotSet<T>`）
```

- [ ] **Step 3: 全量测试确认无回归**

Run: Unity Test Runner → EditMode → Run All。
Expected: PASS，`Grow.Tests` 全部用例通过（含既有 `Collections*` 与新增 `Event*`）。

- [ ] **Step 4: Commit**

```bash
git add Packages/com.ronny.grow/CHANGELOG.md docs/grow-event-design.md docs/grow-event-invocationlist.md
git commit -m "docs(event): record GrowEvent primitives and mark design as implemented"
```

---

## 备注（不在本计划范围）

- Kernel/Loop 的 PlayerLoop 阶段驱动、Kernel/Lifecycle 的 `MonoManager` 属主（`grow-event-design.md` §10.1）另立计划。
- `Core/Log` 与上报节流（§10.2）、多参泛型 `GrowEvent<T0,T1…>`（§10.3）、Tooling 订阅泄漏诊断（§10.4）、Kernel/Scheduling 延迟派发队列（§10.5）均不在本计划。
- `GrowEvent.Add/Remove` 的委托身份去重依赖 `Delegate` 默认相等语义；「方法组二次转换」「等价闭包」的去重边界由 `InvocationListTests` 的 `Add_Duplicate_IsDeduplicated` 覆盖，若后续需要更宽容的等价判定，另立讨论。
- `docs/grow-event-design.md` §9 提到「定稿后可转 ADR（编号顺延 ADR-0003）」；本计划只推进状态，不创建 ADR。
