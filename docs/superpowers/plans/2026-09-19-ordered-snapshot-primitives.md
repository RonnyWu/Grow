# OrderedSet + SnapshotSet 原语实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 `Grow.Core.Collections` 落地首个容器原语对——通用保序集合 `OrderedSet<T>` 与迭代协议集合 `SnapshotSet<T>`——并建立包内 EditMode 测试程序集。

**Architecture:** `OrderedSet<T>` 是纯容器（append-only 注册集 + 哈希索引 + 墓碑 + 惰性压缩 + 保序枚举），零领域语义；`SnapshotSet<T>` 组合 `OrderedSet<T>` 作存储，另维护版本化快照读取（枚举期变更安全、变更下一次生效、最外层边界释放）。两者均无静态状态、主线程、稳态零分配。

**Tech Stack:** Unity 2021.3.45f2、C# 9、.NET Standard 2.1、NUnit（com.unity.test-framework 1.1.33）。

**Spec:**
- `docs/grow-container-catalog.md` §5.7（原语对标 API 与契约）
- `docs/grow-container-necessity-review.md` §5/§6（必要性验证与结论）
- `docs/grow-event-invocationlist.md`（I-1…I-7 不变量与 §7 测试锚点）
- `docs/grow-architecture-rationale.md` §8（落点与命名）、`docs/architecture/ADR-0002-domain-reload-disabled.md`

## Global Constraints

- Unity **2021.3.45f2**、C# 9、API 兼容级别 **.NET Standard 2.1**；不得使用晚于该基线的 API。
- 落点与命名空间：`Packages/com.ronny.grow/Runtime/Core/Collections/`，命名空间 `Grow.Core.Collections`。
- 每个 `.cs`（含测试）以 3 行 MIT 头开始：`// Copyright (c) 2026 Ronny Wu` / `// Licensed under the MIT License.` / `// See LICENSE file in the project root for full license information.`
- 不添加任何代码注释。
- 仅主线程、无锁、无同步原语。
- 默认零静态；不得引入静态字段/缓存（ADR-0002 无需复位）。
- 无第三方依赖；仅 BCL。
- 测试程序集：`Tests/Editor/Grow.Tests.Editor.asmdef`，测试命名空间 `Grow.Tests`；须在 `Packages/manifest.json` 加入 `"testables": ["com.ronny.grow"]`。
- Conventional Commits；用户可见变更更新 `Packages/com.ronny.grow/CHANGELOG.md`。
- **提交权限**：本计划的 Commit 步骤为约定节奏；按本仓库规则，`git commit` 仅在用户明确授权后执行。
- 验证方式：Unity 2021.3.45f2 中 `Window > General > Test Runner > EditMode > Run All`。

### 文件结构

| 文件 | 职责 |
|---|---|
| `Packages/com.ronny.grow/Tests/Editor/Grow.Tests.Editor.asmdef` | 测试程序集定义（Editor-only，引用 Grow 与 TestRunner） |
| `Packages/com.ronny.grow/Tests/Editor/SmokeTests.cs` | 测试基座自检 |
| `Packages/com.ronny.grow/Tests/Editor/OrderedSetTests.cs` | `OrderedSet<T>` 核心/删除/清空/版本 |
| `Packages/com.ronny.grow/Tests/Editor/OrderedSetCompactionTests.cs` | `OrderedSet<T>` 惰性压缩与容量界 |
| `Packages/com.ronny.grow/Tests/Editor/SnapshotSetTests.cs` | `SnapshotSet<T>` 读取协议/嵌套/释放/清空 |
| `Packages/com.ronny.grow/Tests/Editor/CollectionsAllocationTests.cs` | 稳态零分配（可类别过滤） |
| `Packages/com.ronny.grow/Runtime/Core/Collections/OrderedSet.cs` | 保序集合原语（实现） |
| `Packages/com.ronny.grow/Runtime/Core/Collections/SnapshotSet.cs` | 迭代协议原语（实现） |
| `Packages/com.ronny.grow/Runtime/AssemblyInfo.cs` | 追加 `InternalsVisibleTo("Grow.Tests.Editor")` |
| `Packages/manifest.json` | 追加 `testables` |
| `Packages/com.ronny.grow/CHANGELOG.md` | 记录新增公开类型 |

---

### Task 1: 建立 EditMode 测试程序集

**Files:**
- Create: `Packages/com.ronny.grow/Tests/Editor/Grow.Tests.Editor.asmdef`
- Create: `Packages/com.ronny.grow/Tests/Editor/SmokeTests.cs`
- Modify: `Packages/manifest.json:41`（在依赖对象之后追加 `testables`）

**Interfaces:**
- Consumes: 无。
- Produces: 程序集 `Grow.Tests.Editor`、命名空间 `Grow.Tests`，供后续所有测试使用。

- [ ] **Step 1: 创建测试程序集定义**

创建 `Packages/com.ronny.grow/Tests/Editor/Grow.Tests.Editor.asmdef`：

```json
{
    "name": "Grow.Tests.Editor",
    "rootNamespace": "Grow.Tests",
    "references": [
        "Grow",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: 创建自检测试**

创建 `Packages/com.ronny.grow/Tests/Editor/SmokeTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class SmokeTests {
        [Test]
        public void TestAssembly_IsDiscovered() {
            Assert.Pass();
        }
    }
}
```

- [ ] **Step 3: 在包清单启用 testables**

修改 `Packages/manifest.json`，在 `dependencies` 对象之后、根对象结束前追加：

```json
  "testables": [
    "com.ronny.grow"
  ]
```

修改后文件末尾形如：

```json
  "dependencies": {
    "com.unity.modules.xr": "1.0.0"
  },
  "testables": [
    "com.ronny.grow"
  ]
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: Unity 2021.3.45f2 → `Window > General > Test Runner` → `EditMode` 标签 → `Run All`。
Expected: PASS，`SmokeTests.TestAssembly_IsDiscovered` 被发现并通过，0 failed。

（可选 CLI：`& "<你的 2021.3.45f2 安装路径>\Editor\Unity.exe" -batchmode -nographics -projectPath "D:\UnityProjects\GrowFramework" -runTests -testPlatform EditMode -testResults "C:\Users\Ronny\AppData\Local\Temp\opencode\editmode.xml" -logFile "C:\Users\Ronny\AppData\Local\Temp\opencode\unity-tests.log" -quit`。）

- [ ] **Step 5: Commit**

```bash
git add Packages/com.ronny.grow/Tests/Editor/Grow.Tests.Editor.asmdef Packages/com.ronny.grow/Tests/Editor/SmokeTests.cs Packages/manifest.json
git commit -m "test(collections): add EditMode test assembly"
```

---

### Task 2: `OrderedSet<T>` 核心（新增/去重/保序枚举）

**Files:**
- Create: `Packages/com.ronny.grow/Runtime/Core/Collections/OrderedSet.cs`
- Create: `Packages/com.ronny.grow/Tests/Editor/OrderedSetTests.cs`
- Modify: `Packages/com.ronny.grow/CHANGELOG.md`

**Interfaces:**
- Consumes: `Grow.Tests.Editor`（Task 1）。
- Produces:
  - `public sealed class Grow.Core.Collections.OrderedSet<T>`
  - `public OrderedSet()`, `public OrderedSet(int capacity)`, `public OrderedSet(IEqualityComparer<T> comparer, int capacity)`
  - `public int Count { get; }`、`public int Version { get; }`
  - `public bool Add(T item)`、`public bool Contains(T item)`
  - `public Enumerator GetEnumerator()`（嵌套 `public struct Enumerator { public T Current { get; } public bool MoveNext(); }`）

- [ ] **Step 1: 写失败测试**

创建 `Packages/com.ronny.grow/Tests/Editor/OrderedSetTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Grow.Core.Collections;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class OrderedSetTests {
        [Test]
        public void Add_NewItem_ReturnsTrueAndIncrementsCount() {
            var set = new OrderedSet<int>();
            Assert.IsTrue(set.Add(1));
            Assert.IsTrue(set.Add(2));
            Assert.AreEqual(2, set.Count);
        }

        [Test]
        public void Add_Duplicate_ReturnsFalseAndKeepsCount() {
            var set = new OrderedSet<int>();
            set.Add(7);
            Assert.IsFalse(set.Add(7));
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void Contains_ReflectsAdds() {
            var set = new OrderedSet<int>();
            set.Add(5);
            Assert.IsTrue(set.Contains(5));
            Assert.IsFalse(set.Contains(6));
        }

        [Test]
        public void Enumerate_YieldsInsertionOrder() {
            var set = new OrderedSet<int>();
            set.Add(3);
            set.Add(1);
            set.Add(2);
            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, ToArray(set));
        }

        [Test]
        public void Add_WithCustomComparer_DeduplicatesByComparer() {
            var set = new OrderedSet<string>(System.StringComparer.OrdinalIgnoreCase, 4);
            Assert.IsTrue(set.Add("A"));
            Assert.IsFalse(set.Add("a"));
            Assert.AreEqual(1, set.Count);
        }

        private static T[] ToArray<T>(OrderedSet<T> set) {
            var list = new List<T>(set.Count);
            foreach (var item in set) list.Add(item);
            return list.ToArray();
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: Test Runner → EditMode → Run All。
Expected: 编译失败（`OrderedSet<T>` 不存在）。

- [ ] **Step 3: 写最小实现**

创建 `Packages/com.ronny.grow/Runtime/Core/Collections/OrderedSet.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Grow.Core.Collections {
    public sealed class OrderedSet<T> {
        private const int DefaultCapacity = 8;

        private readonly List<T> _items;
        private readonly List<bool> _alive;
        private readonly Dictionary<T, int> _index;
        private int _version;

        public OrderedSet() : this(null, DefaultCapacity) { }

        public OrderedSet(int capacity) : this(null, capacity) { }

        public OrderedSet(IEqualityComparer<T> comparer) : this(comparer, DefaultCapacity) { }

        public OrderedSet(IEqualityComparer<T> comparer, int capacity) {
            if (capacity < 1) capacity = DefaultCapacity;
            _items = new List<T>(capacity);
            _alive = new List<bool>(capacity);
            _index = comparer == null
                ? new Dictionary<T, int>(capacity)
                : new Dictionary<T, int>(capacity, comparer);
        }

        public int Count => _index.Count;

        public int Version => _version;

        public bool Add(T item) {
            if (_index.ContainsKey(item)) return false;
            _index[item] = _items.Count;
            _items.Add(item);
            _alive.Add(true);
            _version++;
            return true;
        }

        public bool Contains(T item) => _index.ContainsKey(item);

        public Enumerator GetEnumerator() => new Enumerator(this);

        public struct Enumerator {
            private readonly OrderedSet<T> _set;
            private readonly int _version;
            private int _index;
            private T _current;

            internal Enumerator(OrderedSet<T> set) {
                _set = set;
                _version = set._version;
                _index = -1;
                _current = default;
            }

            public T Current => _current;

            public bool MoveNext() {
                if (_version != _set._version) {
                    throw new System.InvalidOperationException("OrderedSet was modified during enumeration.");
                }
                var items = _set._items;
                var alive = _set._alive;
                var i = _index + 1;
                while (i < items.Count) {
                    if (alive[i]) {
                        _index = i;
                        _current = items[i];
                        return true;
                    }
                    i++;
                }
                _index = items.Count;
                _current = default;
                return false;
            }
        }
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: Test Runner → EditMode → Run All。
Expected: PASS，`OrderedSetTests` 全部通过。

- [ ] **Step 5: 更新 CHANGELOG**

修改 `Packages/com.ronny.grow/CHANGELOG.md`，在 `## [Unreleased]` 的 `### Added` 下追加：

```markdown
- Add `Grow.Core.Collections.OrderedSet<T>` (insertion-ordered set, O(1) add/remove/contains).
```

- [ ] **Step 6: Commit**

```bash
git add Packages/com.ronny.grow/Runtime/Core/Collections/OrderedSet.cs Packages/com.ronny.grow/Tests/Editor/OrderedSetTests.cs Packages/com.ronny.grow/CHANGELOG.md
git commit -m "feat(collections): add OrderedSet<T> core with insertion-order enumeration"
```

---

### Task 3: `OrderedSet<T>` 删除（O(1) 墓碑）与枚举失效

**Files:**
- Modify: `Packages/com.ronny.grow/Runtime/Core/Collections/OrderedSet.cs`
- Modify: `Packages/com.ronny.grow/Tests/Editor/OrderedSetTests.cs`

**Interfaces:**
- Consumes: Task 2 的类型与字段。
- Produces: `public bool Remove(T item)`；删除后 `_tombstoneCount` 跟踪（Task 4 消费）。

- [ ] **Step 1: 写失败测试**

在 `OrderedSetTests` 类内追加：

```csharp
        [Test]
        public void Remove_ExistingItem_ReturnsTrueAndDecrementsCount() {
            var set = new OrderedSet<int>();
            set.Add(1);
            set.Add(2);
            Assert.IsTrue(set.Remove(1));
            Assert.AreEqual(1, set.Count);
            Assert.IsFalse(set.Contains(1));
            Assert.IsTrue(set.Contains(2));
        }

        [Test]
        public void Remove_MissingItem_ReturnsFalseAndKeepsCount() {
            var set = new OrderedSet<int>();
            set.Add(1);
            Assert.IsFalse(set.Remove(9));
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void Remove_PreservesOrderOfSurvivors() {
            var set = new OrderedSet<int>();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            set.Add(4);
            set.Remove(2);
            set.Remove(4);
            CollectionAssert.AreEqual(new[] { 1, 3 }, ToArray(set));
        }

        [Test]
        public void RemoveThenAdd_AppendsToTail() {
            var set = new OrderedSet<int>();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            set.Remove(1);
            set.Add(1);
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, ToArray(set));
        }

        [Test]
        public void Enumerate_DuringMutation_Throws() {
            var set = new OrderedSet<int>();
            set.Add(1);
            set.Add(2);
            Assert.Throws<System.InvalidOperationException>(() => {
                foreach (var _ in set) set.Add(99);
            });
        }
```

- [ ] **Step 2: 运行测试确认失败**

Run: Test Runner → EditMode → Run All。
Expected: 编译失败（`OrderedSet<T>.Remove` 不存在）。

- [ ] **Step 3: 写最小实现**

在 `OrderedSet.cs` 的字段区追加 `_tombstoneCount`：

```csharp
        private int _tombstoneCount;
```

在 `Contains` 方法之后追加 `Remove`：

```csharp
        public bool Remove(T item) {
            if (!_index.TryGetValue(item, out var slot)) return false;
            _alive[slot] = false;
            _items[slot] = default;
            _index.Remove(item);
            _tombstoneCount++;
            _version++;
            return true;
        }
```

- [ ] **Step 4: 运行测试确认通过**

Run: Test Runner → EditMode → Run All。
Expected: PASS，新增删除与失效测试全部通过（`_tombstoneCount` 暂未压缩，容量未受限属预期）。

- [ ] **Step 5: Commit**

```bash
git add Packages/com.ronny.grow/Runtime/Core/Collections/OrderedSet.cs Packages/com.ronny.grow/Tests/Editor/OrderedSetTests.cs
git commit -m "feat(collections): add O(1) tombstone removal to OrderedSet<T>"
```

---

### Task 4: `OrderedSet<T>` 惰性压缩与容量界

**Files:**
- Modify: `Packages/com.ronny.grow/Runtime/Core/Collections/OrderedSet.cs`
- Create: `Packages/com.ronny.grow/Tests/Editor/OrderedSetCompactionTests.cs`
- Modify: `Packages/com.ronny.grow/Runtime/AssemblyInfo.cs`

**Interfaces:**
- Consumes: Task 3 的 `_tombstoneCount`。
- Produces: `internal int RegistrationCount { get; }`、`internal int TombstoneCount { get; }`；`CompactIfNeeded()`（Remove 内调用）。

- [ ] **Step 1: 暴露内部诊断供测试读取**

修改 `Packages/com.ronny.grow/Runtime/AssemblyInfo.cs` 为：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]
[assembly: InternalsVisibleTo("Grow.Tests.Editor")]
```

- [ ] **Step 2: 写失败测试**

创建 `Packages/com.ronny.grow/Tests/Editor/OrderedSetCompactionTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Grow.Core.Collections;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class OrderedSetCompactionTests {
        [Test]
        public void Remove_DoesNotCompactWhileTombstonesBelowActive() {
            var set = new OrderedSet<int>(8);
            set.Add(1);
            set.Add(2);
            set.Add(3);
            set.Add(4);
            set.Add(5);
            set.Remove(1);
            set.Remove(2);
            Assert.AreEqual(5, set.RegistrationCount);
            Assert.AreEqual(2, set.TombstoneCount);
        }

        [Test]
        public void Remove_CompactsWhenTombstonesReachActive() {
            var set = new OrderedSet<int>(8);
            set.Add(1);
            set.Add(2);
            set.Add(3);
            set.Add(4);
            set.Add(5);
            set.Remove(1);
            set.Remove(2);
            set.Remove(3);
            Assert.AreEqual(2, set.RegistrationCount);
            Assert.AreEqual(0, set.TombstoneCount);
            CollectionAssert.AreEqual(new[] { 4, 5 }, ToArray(set));
        }

        [Test]
        public void Churn_KeepsRegistrationWithinTwiceActive() {
            var set = new OrderedSet<int>(8);
            for (var i = 0; i < 128; i++) set.Add(i);
            for (var i = 0; i < 128; i++) {
                set.Remove(i);
                set.Add(1000 + i);
            }
            Assert.LessOrEqual(set.RegistrationCount, 2 * set.Count);
        }

        private static T[] ToArray<T>(OrderedSet<T> set) {
            var list = new List<T>(set.Count);
            foreach (var item in set) list.Add(item);
            return list.ToArray();
        }
    }
}
```

- [ ] **Step 3: 运行测试确认失败**

Run: Test Runner → EditMode → Run All。
Expected: 编译失败（`RegistrationCount`/`TombstoneCount` 不存在）。

- [ ] **Step 4: 写最小实现**

在 `OrderedSet.cs` 的 `Version` 属性之后追加内部诊断：

```csharp
        internal int RegistrationCount => _items.Count;

        internal int TombstoneCount => _tombstoneCount;
```

在 `Remove` 内、`return true;` 之前插入压缩调用：

```csharp
            CompactIfNeeded();
```

在 `GetEnumerator` 之前追加：

```csharp
        private void CompactIfNeeded() {
            var active = _index.Count;
            if (_tombstoneCount > 0 && _tombstoneCount >= active) Compact();
        }

        private void Compact() {
            var write = 0;
            var count = _items.Count;
            for (var read = 0; read < count; read++) {
                if (!_alive[read]) continue;
                if (write != read) {
                    _items[write] = _items[read];
                    _alive[write] = true;
                    _index[_items[write]] = write;
                }
                write++;
            }
            if (write < count) {
                _items.RemoveRange(write, count - write);
                _alive.RemoveRange(write, count - write);
            }
            _tombstoneCount = 0;
        }
```

- [ ] **Step 5: 运行测试确认通过**

Run: Test Runner → EditMode → Run All。
Expected: PASS，压缩阈值与容量界测试通过。

- [ ] **Step 6: Commit**

```bash
git add Packages/com.ronny.grow/Runtime/Core/Collections/OrderedSet.cs Packages/com.ronny.grow/Runtime/AssemblyInfo.cs Packages/com.ronny.grow/Tests/Editor/OrderedSetCompactionTests.cs
git commit -m "feat(collections): lazy compaction and bounded storage in OrderedSet<T>"
```

---

### Task 5: `OrderedSet<T>` 清空与版本

**Files:**
- Modify: `Packages/com.ronny.grow/Runtime/Core/Collections/OrderedSet.cs`
- Modify: `Packages/com.ronny.grow/Tests/Editor/OrderedSetTests.cs`

**Interfaces:**
- Consumes: Task 4 的字段与诊断。
- Produces: `public void Clear()`。

- [ ] **Step 1: 写失败测试**

在 `OrderedSetTests` 类内追加：

```csharp
        [Test]
        public void Clear_RemovesAllItemsAndResetsStorage() {
            var set = new OrderedSet<int>();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            set.Remove(1);
            set.Clear();
            Assert.AreEqual(0, set.Count);
            Assert.AreEqual(0, set.RegistrationCount);
            Assert.AreEqual(0, set.TombstoneCount);
        }

        [Test]
        public void Clear_AllowsReuseInInsertionOrder() {
            var set = new OrderedSet<int>();
            set.Add(1);
            set.Add(2);
            set.Clear();
            set.Add(3);
            set.Add(4);
            CollectionAssert.AreEqual(new[] { 3, 4 }, ToArray(set));
        }

        [Test]
        public void Version_IsMonotonicAndIgnoresDuplicates() {
            var set = new OrderedSet<int>();
            var v0 = set.Version;
            set.Add(1);
            var v1 = set.Version;
            set.Add(1);
            var v2 = set.Version;
            set.Remove(1);
            var v3 = set.Version;
            set.Clear();
            var v4 = set.Version;
            Assert.Greater(v1, v0);
            Assert.AreEqual(v1, v2);
            Assert.Greater(v3, v2);
            Assert.Greater(v4, v3);
        }
```

- [ ] **Step 2: 运行测试确认失败**

Run: Test Runner → EditMode → Run All。
Expected: 编译失败（`OrderedSet<T>.Clear` 不存在）。

- [ ] **Step 3: 写最小实现**

在 `OrderedSet.cs` 的 `Contains` 方法之后追加：

```csharp
        public void Clear() {
            _items.Clear();
            _alive.Clear();
            _index.Clear();
            _tombstoneCount = 0;
            _version++;
        }
```

- [ ] **Step 4: 运行测试确认通过**

Run: Test Runner → EditMode → Run All。
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add Packages/com.ronny.grow/Runtime/Core/Collections/OrderedSet.cs Packages/com.ronny.grow/Tests/Editor/OrderedSetTests.cs
git commit -m "feat(collections): add Clear and version tracking to OrderedSet<T>"
```

---

### Task 6: `SnapshotSet<T>` 核心与读取快照语义

**Files:**
- Create: `Packages/com.ronny.grow/Runtime/Core/Collections/SnapshotSet.cs`
- Create: `Packages/com.ronny.grow/Tests/Editor/SnapshotSetTests.cs`
- Modify: `Packages/com.ronny.grow/Runtime/Core/Collections/OrderedSet.cs`（追加 `internal int CopyActiveTo(T[] buffer)`）
- Modify: `Packages/com.ronny.grow/CHANGELOG.md`

**Interfaces:**
- Consumes: `OrderedSet<T>`（Task 5）。
- Produces:
  - `public sealed class Grow.Core.Collections.SnapshotSet<T>`
  - `public SnapshotSet()`, `public SnapshotSet(int capacity)`, `public SnapshotSet(IEqualityComparer<T> comparer, int capacity)`
  - `public int Count { get; }`、`public bool Add(T)`、`public bool Remove(T)`、`public bool Contains(T)`
  - `public bool BeginRead(out T[] items, out int count)`、`public void EndRead()`
  - `internal int SnapshotCount { get; }`、`internal int ReadDepth { get; }`

- [ ] **Step 1: 为 OrderedSet 增加活跃元素拷贝（内部）**

在 `OrderedSet.cs` 的 `GetEnumerator` 之前追加：

```csharp
        internal int CopyActiveTo(T[] buffer) {
            var write = 0;
            var count = _items.Count;
            for (var read = 0; read < count; read++) {
                if (_alive[read]) buffer[write++] = _items[read];
            }
            return write;
        }
```

- [ ] **Step 2: 写失败测试**

创建 `Packages/com.ronny.grow/Tests/Editor/SnapshotSetTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using Grow.Core.Collections;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    public sealed class SnapshotSetTests {
        [Test]
        public void AddRemoveContains_DelegateToStorage() {
            var set = new SnapshotSet<int>();
            Assert.IsTrue(set.Add(1));
            Assert.IsFalse(set.Add(1));
            Assert.AreEqual(1, set.Count);
            Assert.IsTrue(set.Contains(1));
            Assert.IsTrue(set.Remove(1));
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void BeginRead_ReturnsInsertionOrderSnapshot() {
            var set = new SnapshotSet<int>();
            set.Add(3);
            set.Add(1);
            set.Add(2);
            Assert.IsTrue(set.BeginRead(out var items, out var count));
            Assert.AreEqual(3, count);
            Assert.AreEqual(3, items[0]);
            Assert.AreEqual(1, items[1]);
            Assert.AreEqual(2, items[2]);
            set.EndRead();
        }

        [Test]
        public void BeginRead_Empty_ReturnsFalseWithoutEnteringDepth() {
            var set = new SnapshotSet<int>();
            Assert.IsFalse(set.BeginRead(out _, out var count));
            Assert.AreEqual(0, count);
            Assert.AreEqual(0, set.ReadDepth);
        }

        [Test]
        public void MutationDuringRead_DoesNotAffectCurrentSnapshot() {
            var set = new SnapshotSet<int>();
            set.Add(1);
            Assert.IsTrue(set.BeginRead(out var items, out var count));
            set.Add(2);
            Assert.AreEqual(1, count);
            Assert.AreEqual(1, items[0]);
            set.EndRead();
        }

        [Test]
        public void MutationAfterRead_VisibleOnNextRead() {
            var set = new SnapshotSet<int>();
            set.Add(1);
            Assert.IsTrue(set.BeginRead(out _, out _));
            set.EndRead();
            set.Add(2);
            Assert.IsTrue(set.BeginRead(out var items, out var count));
            Assert.AreEqual(2, count);
            Assert.AreEqual(2, items[1]);
            set.EndRead();
        }

        [Test]
        public void BeginRead_SteadyState_ReusesSnapshotWithoutRebuild() {
            var set = new SnapshotSet<int>();
            set.Add(1);
            Assert.IsTrue(set.BeginRead(out var first, out _));
            set.EndRead();
            Assert.IsTrue(set.BeginRead(out var second, out _));
            Assert.AreSame(first, second);
            set.EndRead();
        }
    }
}
```

- [ ] **Step 3: 运行测试确认失败**

Run: Test Runner → EditMode → Run All。
Expected: 编译失败（`SnapshotSet<T>` 不存在）。

- [ ] **Step 4: 写最小实现**

创建 `Packages/com.ronny.grow/Runtime/Core/Collections/SnapshotSet.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Grow.Core.Collections {
    public sealed class SnapshotSet<T> {
        private const int DefaultCapacity = 8;

        private readonly OrderedSet<T> _set;
        private T[] _snapshot;
        private int _snapshotCount;
        private int _builtVersion = -1;
        private int _depth;

        public SnapshotSet() : this(null, DefaultCapacity) { }

        public SnapshotSet(int capacity) : this(null, capacity) { }

        public SnapshotSet(IEqualityComparer<T> comparer) : this(comparer, DefaultCapacity) { }

        public SnapshotSet(IEqualityComparer<T> comparer, int capacity) {
            if (capacity < 1) capacity = DefaultCapacity;
            _set = new OrderedSet<T>(comparer, capacity);
            _snapshot = new T[capacity];
        }

        public int Count => _set.Count;

        internal int SnapshotCount => _snapshotCount;

        internal int ReadDepth => _depth;

        public bool Add(T item) => _set.Add(item);

        public bool Remove(T item) => _set.Remove(item);

        public bool Contains(T item) => _set.Contains(item);

        public bool BeginRead(out T[] items, out int count) {
            if (_depth == 0 && _builtVersion != _set.Version) {
                Rebuild();
                _builtVersion = _set.Version;
            }
            items = _snapshot;
            count = _snapshotCount;
            if (count == 0) return false;
            _depth++;
            return true;
        }

        public void EndRead() {
            if (_depth == 0) return;
            _depth--;
            if (_depth == 0 && _builtVersion != _set.Version) {
                ClearSnapshot();
                _builtVersion = -1;
            }
        }

        private void ClearSnapshot() {
            for (var i = 0; i < _snapshotCount; i++) _snapshot[i] = default;
            _snapshotCount = 0;
        }

        private void Rebuild() {
            var active = _set.Count;
            if (_snapshot.Length < active) {
                var newSize = _snapshot.Length < DefaultCapacity ? DefaultCapacity : _snapshot.Length * 2;
                if (newSize < active) newSize = active;
                Array.Resize(ref _snapshot, newSize);
            }
            var count = _set.CopyActiveTo(_snapshot);
            for (var i = count; i < _snapshotCount; i++) _snapshot[i] = default;
            _snapshotCount = count;
        }
    }
}
```

- [ ] **Step 5: 运行测试确认通过**

Run: Test Runner → EditMode → Run All。
Expected: PASS，`SnapshotSetTests` 全部通过。

- [ ] **Step 6: 更新 CHANGELOG**

在 `Packages/com.ronny.grow/CHANGELOG.md` 的 `### Added` 下追加：

```markdown
- Add `Grow.Core.Collections.SnapshotSet<T>` (safe iteration under mutation: snapshot at read boundary, changes take effect next round).
```

- [ ] **Step 7: Commit**

```bash
git add Packages/com.ronny.grow/Runtime/Core/Collections/SnapshotSet.cs Packages/com.ronny.grow/Runtime/Core/Collections/OrderedSet.cs Packages/com.ronny.grow/Tests/Editor/SnapshotSetTests.cs Packages/com.ronny.grow/CHANGELOG.md
git commit -m "feat(collections): add SnapshotSet<T> with versioned read snapshots"
```

---

### Task 7: `SnapshotSet<T>` 嵌套读取、边界释放与清空

**Files:**
- Modify: `Packages/com.ronny.grow/Runtime/Core/Collections/SnapshotSet.cs`
- Modify: `Packages/com.ronny.grow/Tests/Editor/SnapshotSetTests.cs`

**Interfaces:**
- Consumes: Task 6 的类型与内部诊断。
- Produces: `public void Clear()`（含读取期语义）。

- [ ] **Step 1: 写失败测试**

在 `SnapshotSetTests` 类内追加：

```csharp
        [Test]
        public void NestedRead_ReusesSameSnapshot() {
            var set = new SnapshotSet<int>();
            set.Add(1);
            Assert.IsTrue(set.BeginRead(out var outer, out var outerCount));
            set.Add(2);
            Assert.IsTrue(set.BeginRead(out var inner, out var innerCount));
            Assert.AreSame(outer, inner);
            Assert.AreEqual(1, innerCount);
            set.EndRead();
            Assert.AreEqual(1, outerCount);
            Assert.AreEqual(1, set.ReadDepth);
            set.EndRead();
            Assert.IsTrue(set.BeginRead(out var items, out var count));
            Assert.AreEqual(2, count);
            set.EndRead();
        }

        [Test]
        public void EndRead_OutermostAfterMutation_ReleasesSnapshot() {
            var set = new SnapshotSet<int>();
            set.Add(1);
            Assert.IsTrue(set.BeginRead(out _, out _));
            set.Add(2);
            set.EndRead();
            Assert.AreEqual(0, set.ReadDepth);
            Assert.AreEqual(0, set.SnapshotCount);
        }

        [Test]
        public void EndRead_WithoutBeginRead_IsNoOp() {
            var set = new SnapshotSet<int>();
            set.Add(1);
            set.EndRead();
            Assert.AreEqual(0, set.ReadDepth);
        }

        [Test]
        public void ClearDuringRead_CurrentRoundRuns_NextRoundEmpty() {
            var set = new SnapshotSet<int>();
            set.Add(1);
            set.Add(2);
            Assert.IsTrue(set.BeginRead(out var items, out var count));
            set.Clear();
            Assert.AreEqual(2, count);
            Assert.AreEqual(2, items[1]);
            set.EndRead();
            Assert.IsFalse(set.BeginRead(out _, out _));
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void ClearOutsideRead_ResetsSnapshot() {
            var set = new SnapshotSet<int>();
            set.Add(1);
            Assert.IsTrue(set.BeginRead(out _, out _));
            set.EndRead();
            set.Clear();
            Assert.AreEqual(0, set.SnapshotCount);
            Assert.IsFalse(set.BeginRead(out _, out _));
        }
```

- [ ] **Step 2: 运行测试确认失败**

Run: Test Runner → EditMode → Run All。
Expected: 编译失败（`SnapshotSet<T>.Clear` 不存在）。

- [ ] **Step 3: 写最小实现**

在 `SnapshotSet.cs` 的 `Contains` 方法之后追加：

```csharp
        public void Clear() {
            _set.Clear();
            if (_depth == 0) {
                ClearSnapshot();
                _builtVersion = _set.Version;
            } else {
                _builtVersion = -1;
            }
        }
```

- [ ] **Step 4: 运行测试确认通过**

Run: Test Runner → EditMode → Run All。
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add Packages/com.ronny.grow/Runtime/Core/Collections/SnapshotSet.cs Packages/com.ronny.grow/Tests/Editor/SnapshotSetTests.cs
git commit -m "feat(collections): add nested reads, boundary release and Clear to SnapshotSet<T>"
```

---

### Task 8: 稳态零分配回归

**Files:**
- Create: `Packages/com.ronny.grow/Tests/Editor/CollectionsAllocationTests.cs`

**Interfaces:**
- Consumes: `OrderedSet<T>`（Task 5）、`SnapshotSet<T>`（Task 7）。
- Produces: 可过滤类别 `Grow.Collections.GC` 的分配回归测试。

- [ ] **Step 1: 写失败测试**

创建 `Packages/com.ronny.grow/Tests/Editor/CollectionsAllocationTests.cs`：

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using Grow.Core.Collections;
using NUnit.Framework;

namespace Grow.Tests {
    [TestFixture]
    [Category("Grow.Collections.GC")]
    public sealed class CollectionsAllocationTests {
        [Test]
        public void OrderedSet_Enumeration_SteadyState_AllocatesLittle() {
            var set = new OrderedSet<int>(64);
            for (var i = 0; i < 64; i++) set.Add(i);
            for (var warmup = 0; warmup < 64; warmup++) {
                var ignored = 0;
                foreach (var item in set) ignored += item;
            }

            var before = GC.GetTotalMemory(false);
            var sum = 0;
            for (var round = 0; round < 4096; round++) {
                foreach (var item in set) sum += item;
            }
            var after = GC.GetTotalMemory(false);

            Assert.Greater(sum, 0);
            Assert.Less(after - before, 4096, "steady-state enumeration must allocate ~0 bytes");
        }

        [Test]
        public void SnapshotSet_Read_SteadyState_AllocatesLittle() {
            var set = new SnapshotSet<int>(64);
            for (var i = 0; i < 64; i++) set.Add(i);
            for (var warmup = 0; warmup < 64; warmup++) {
                if (!set.BeginRead(out var items, out var count)) continue;
                var ignored = 0;
                for (var i = 0; i < count; i++) ignored += items[i];
                set.EndRead();
            }

            var before = GC.GetTotalMemory(false);
            var sum = 0;
            for (var round = 0; round < 4096; round++) {
                if (!set.BeginRead(out var items, out var count)) continue;
                for (var i = 0; i < count; i++) sum += items[i];
                set.EndRead();
            }
            var after = GC.GetTotalMemory(false);

            Assert.Greater(sum, 0);
            Assert.Less(after - before, 4096, "steady-state read must allocate ~0 bytes");
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败/基线**

Run: Test Runner → EditMode → Run All（或过滤 `Grow.Collections.GC`）。
Expected: 若实现存在装箱/分配则 FAIL；正确实现下应 PASS。若因编辑器噪声偶发 FAIL，重复运行确认是否稳定，并据实调整阈值（保持「每轮迭代 < 1 字节」量级）。

- [ ] **Step 3: 运行测试确认通过**

Run: Test Runner → EditMode → 选中 `CollectionsAllocationTests` → Run Selected。
Expected: PASS，两个分配测试通过。

- [ ] **Step 4: Commit**

```bash
git add Packages/com.ronny.grow/Tests/Editor/CollectionsAllocationTests.cs
git commit -m "test(collections): add steady-state allocation regression for primitives"
```

---

## 备注（不在本计划范围）

- `OrderedDictionary<K,V>`（`docs/grow-container-catalog.md` §5.7）与 `OrderedSet<T>` 同源，按需另立
  `writing-plans` 计划；本计划只交付 `InvocationList` 所需的集合与快照两端。
- `InvocationList`/`GrowEvent` 的落地（组合本计划的两个原语 + 委托策略）为独立计划，随后进行。
