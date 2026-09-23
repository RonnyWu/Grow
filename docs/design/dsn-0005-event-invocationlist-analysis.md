# InvocationList 引擎设计（GrowEvent 派发内核）

- 状态：已实现（`Runtime/Core/Event/`；`InvocationList` 按 dsn-0002-container-catalog §7 组合 `SnapshotSet<T>`）
- 日期：2026-09-19
- 范围：`Packages/com.ronny.grow/Runtime/Core/Event/InvocationList.cs`
- 关联：`docs/design/dsn-0004-event-primitives.md`（事件原语设计）、`docs/explanation/exp-0001-architecture-rationale.md` §8

## 1. 定位与不变量

引擎只做一件事：**维护「注册意图」与「派发视图」两个世界，并在边界同步**。它不感知委托签名（`where TDelegate : Delegate`），类型化派发由叶子（`GrowEvent`、`GrowEvent<T0>`）承担。

七条不变量（实现与测试均以此为准）：

| # | 不变量 |
|---|---|
| I-1 | `_registration` 物理序 == 注册时间序；`Add` 只 append，绝不复用墓碑槽 |
| I-2 | `_index` 仅含活跃项（key → 槽位）；`_index.Count == 活跃数` |
| I-3 | 派发只读 `_snapshot`；增删只改注册集；`_version` 单调表征意图变化 |
| I-4 | 仅 `_invokeDepth == 0 && _version != _builtVersion` 时于派发边界重建 |
| I-5 | 嵌套派发复用同一快照、从头独立派发；最外层退出才是唯一重建/释放边界 |
| I-6 | 惰性压缩：`tombstone >= active` 时边界就地前移收缩；注册集 ≤ 2×active |
| I-7 | 无变更轮次零分配：仅版本比较 + 数组顺序遍历 |

## 2. 状态与数据结构

> 以下字段为参考形态；实际落地为组合 `Core/Collections` 的 `OrderedSet<T>`/`SnapshotSet<T>`，字段与流程分散在两者中（见 §9 与 `dsn-0002-container-catalog.md` §7.2）。

| 字段 | 角色 | 增长/回收 |
|---|---|---|
| `_registration : List<TDelegate>` | 顺序权威（含墓碑） | 容量只增；`RemoveRange` 不缩容 |
| `_index : Dictionary<TDelegate,int>` | 去重 + O(1) 定位 | 容量只增 |
| `_version : int` | 变更计数（脏判定） | 单调；溢出实际不可达 |
| `_tombstoneCount : int` | 压缩触发依据 | Remove++；压缩/Clear 归零 |
| `_snapshot : TDelegate[]` | 派发视图（紧凑、不可变） | 倍增增长，永不缩容 |
| `_snapshotCount : int` | 快照有效长度 | 随重建更新 |
| `_builtVersion : int` | 快照版本，初值 -1 保证首轮必建 | 随重建/清空更新 |
| `_invokeDepth : int` | 重入深度 | Enter/End 配对 |

## 3. 关键流程

- **Add**：`_index` 命中即忽略（去重）；否则 `_index[h] = _registration.Count`、`_registration.Add(h)`、`_version++`。一律追加队尾。
- **Remove**：`_index` 未命中即忽略；否则槽位置 null（墓碑）、`_index.Remove(h)`、`_tombstoneCount++`、`_version++`。O(1)。
- **Clear**：清注册集/索引、`_tombstoneCount = 0`、`_version++`；深度 0 → 立即置空快照引用并同步 `_builtVersion`；深度 >0 → 仅使 `_builtVersion = -1`（本轮跑完）。
- **BeginDispatch**：深度 0 且版本已变 → 重建快照并同步版本；读快照引用与长度；`count == 0` 返回 false（不进入深度）；否则 `_invokeDepth++` 返回 true。
- **EndDispatch**：`_invokeDepth--`；若回到 0 且版本已变 → 置空快照引用并标脏（`_builtVersion = -1`），**不重建、不分配**；重建推迟到下次 `BeginDispatch`。
- **RebuildSnapshot**：按需扩容快照；扫描注册集跳过墓碑填充快照；仅当 `tombstone >= active` 时同步前移并重写 `_index`（压缩），随后 `RemoveRange` 收缩并复位墓碑计数；清空快照尾部残留引用。

## 4. 复杂度与分配

| 操作 | 复杂度 | 分配 |
|---|---|---|
| Add / Remove | 平均 O(1) | 仅容器扩容，摊还 O(1) |
| Clear | O(active) | 无 |
| 稳态 BeginDispatch + 遍历 | O(1) 准备 + O(active) 遍历 | 0 |
| 变更帧 BeginDispatch（重建） | O(regCount)，压缩帧同阶 | 仅快照倍增，摊还 O(1) |
| EndDispatch | O(1)（稳态）/ O(active)（有在途变更时置空） | 0 |

**压缩摊还证明**：触发条件 `tombstone >= active`，即活跃占比 ≤ 50%。一次压缩把长度 `n` 降到 `active ≤ n/2`，至少淘汰 `active` 个墓碑；每个墓碑平均只被搬运常数次 → 摊还 O(1)/Remove。刻意不采用「每次重建都压缩」，否则高频增删轮次每次都要 O(n) 前移与索引重写。

**不缩容的取舍**：快照与 `_index` 容量只增不缩，换取复用与零抖动；事件通常长期存在、监听规模有界，可接受。

## 5. 相对参考实现的改动

**改动 1 —— 接口封装为 `BeginDispatch/EndDispatch`**
参考暴露 `PrepareInvoke` + `Snapshot/SnapshotCount` + `EnterInvoke/ExitInvoke`，叶子需自行遵守协议。现收成两个方法，把「重建 → 空早退 → 进入深度 → 退出 → 边界释放」全部关进引擎，叶子不可能漏调或错序。

**改动 2 —— 派发边界释放（I3）**
参考在派发期 `Clear` 后只置 `_builtVersion = -1`，快照引用要留到下次 Invoke 重建才释放。现改为 `EndDispatch` 回到深度 0 且版本已变时：置空快照引用 + 标脏，**不重建、不分配、不可抛**；重建仍留到下次 `BeginDispatch`。
理由：若在 `finally` 内直接重建（O(n)、可能分配），一旦此时正有 fatal 异常传播，会掩盖原异常；「置空 + 标脏」等价地达到了尽快释放引用的目的，且天然异常安全。

**改动 3 —— 风格**
K&R 大括号、极少注释，对齐仓库既有代码（`GrowBoot.cs`）；语义不变量由本文与 `dsn-0004-event-primitives.md` 承载。

## 6. 引擎代码形态

> 以下为参考形态，**非**最终落地代码；实现为组合 `SnapshotSet<TDelegate>`（见 §9）。

```csharp
// Copyright (c) 2026 Ronny Wu
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Grow.Core.Event {
    internal sealed class InvocationList<TDelegate> where TDelegate : Delegate {
        private const int DefaultCapacity = 8;

        private readonly List<TDelegate> _registration;
        private readonly Dictionary<TDelegate, int> _index;
        private TDelegate[] _snapshot;
        private int _snapshotCount;
        private int _version;
        private int _tombstoneCount;
        private int _builtVersion = -1;
        private int _invokeDepth;

        internal InvocationList(int capacity = DefaultCapacity) {
            if (capacity < 1) capacity = DefaultCapacity;
            _registration = new List<TDelegate>(capacity);
            _index = new Dictionary<TDelegate, int>(capacity);
            _snapshot = new TDelegate[capacity];
        }

        internal int Count => _index.Count;

        internal void Add(TDelegate handler) {
            if (handler == null) return;
            if (_index.ContainsKey(handler)) return;
            _index[handler] = _registration.Count;
            _registration.Add(handler);
            _version++;
        }

        internal void Remove(TDelegate handler) {
            if (handler == null) return;
            if (!_index.TryGetValue(handler, out var slot)) return;
            _registration[slot] = null;
            _index.Remove(handler);
            _tombstoneCount++;
            _version++;
        }

        internal void Clear() {
            _registration.Clear();
            _index.Clear();
            _tombstoneCount = 0;
            _version++;
            if (_invokeDepth == 0) {
                ClearSnapshot();
                _builtVersion = _version;
            } else {
                _builtVersion = -1;
            }
        }

        internal bool BeginDispatch(out TDelegate[] snapshot, out int count) {
            if (_invokeDepth == 0 && _version != _builtVersion) {
                RebuildSnapshot();
                _builtVersion = _version;
            }
            snapshot = _snapshot;
            count = _snapshotCount;
            if (count == 0) return false;
            _invokeDepth++;
            return true;
        }

        internal void EndDispatch() {
            if (_invokeDepth > 0) _invokeDepth--;
            if (_invokeDepth == 0 && _version != _builtVersion) {
                ClearSnapshot();
                _builtVersion = -1;
            }
        }

        private void ClearSnapshot() {
            for (var i = 0; i < _snapshotCount; i++) _snapshot[i] = null;
            _snapshotCount = 0;
        }

        private void RebuildSnapshot() {
            var active = _index.Count;
            if (_snapshot.Length < active) {
                var newSize = _snapshot.Length < DefaultCapacity ? DefaultCapacity : _snapshot.Length * 2;
                if (newSize < active) newSize = active;
                Array.Resize(ref _snapshot, newSize);
            }

            var regCount = _registration.Count;
            var compact = _tombstoneCount > 0 && _tombstoneCount >= active;
            var w = 0;
            for (var r = 0; r < regCount; r++) {
                var handler = _registration[r];
                if (handler == null) continue;
                if (compact && w != r) {
                    _registration[w] = handler;
                    _index[handler] = w;
                }
                _snapshot[w++] = handler;
            }

            if (compact) {
                if (w < regCount) _registration.RemoveRange(w, regCount - w);
                _tombstoneCount = 0;
            }

            for (var i = active; i < _snapshotCount; i++) _snapshot[i] = null;
            _snapshotCount = active;
        }
    }
}
```

## 7. 语义边界与测试锚点

- `Add/Remove(null)`、移除未知委托 → 无副作用
- 重复 Add（同委托、方法组二次转换、等价闭包）→ 去重
- `Remove→Add` → 排队尾（严格时间序）
- 派发中：Remove 后续监听/自己 → 本轮仍执行；Add → 下轮队尾；`Clear` → 本轮跑完、下轮空
- 嵌套派发；`Clear` 后嵌套派发 → 仍跑旧快照（同一最外层轮）
- 压缩边界：`tombstone == active - 1` 不压缩、`== active` 压缩；压缩后 `_index` 槽位全部有效且顺序不变
- 大量增删后注册集 ≤ 2×active
- fatal 穿透时 `finally` 使深度归零
- `EndDispatch` 在途变更后：快照引用被置空、下次派发重建为最新注册集

## 8. 与替代引擎对比

| 引擎形态 | 顺序保证 | Remove | 变更帧分配 | 备注 |
|---|---|---|---|---|
| **快照 + 墓碑 + 字典（本方案）** | 严格时间序 | O(1) | 仅倍增（摊还） | 迭代连续、语义确定 |
| `List` + `IndexOf` | 严格时间序 | O(n) | 无 | 大列表退化 |
| COW / ImmutableArray | 严格时间序 | O(n) | 每次变更分配 | R3 已明确批判 |
| 链表 + 节点字典 | 严格时间序 | O(1) | 快照仍需 | 迭代指针跳转、节点分配 |
| 双缓冲 + 命令缓冲（方案 B） | last-op-wins | — | 节点泄漏 | 语义与架构均不符 |
| 自由槽复用（方案 A 变体） | 需额外版本号 | O(1) | 重建临时数组 | 复杂度高、丢序风险 |

## 9. 定位边界：专用引擎与可复用路径

**定位：事件派发内核 = 通用容器原语 + 事件策略。** 契约层（`BeginDispatch/EndDispatch`、变更下一次派发生效、异常隔离归叶子）是事件语义，保持 `internal`；其**存储机制**（插入序注册集 + 哈希索引 + 墓碑惰性压缩，I-1/I-2/I-6/I-7）是通用容器原语，回填 `Core/Collections` 的 `OrderedSet<T>`；其**快照迭代协议**（I-3/I-4/I-5）独立为 `SnapshotSet<T>` 或先私有于 `Core/Event`（见下）。

**类型参数**：保持 `InvocationList<TDelegate> where TDelegate : Delegate`。

| 形态 | 判断 |
|---|---|
| `InvocationList<Action>` | 否：零参/单参/多参需各写一套引擎，且丢失委托签名 |
| `InvocationList<T>`（无约束） | 否：需引入 `IEqualityComparer<T>`；语义弱化；诊断丢失委托身份 |
| `InvocationList<TDelegate> : Delegate` | 是：一套引擎覆盖任意元数；`Delegate` 的相等语义（target+method）正是 Add/Remove 去重所需 |

**可迁移的结构**（落点 `Core/Collections`，已立项为 `OrderedSet<T>` / `OrderedDictionary<K,V>`）：

| 目标结构 | 机制吻合度 | 说明 |
|---|---|---|
| 有序字典 / 有序集合 | 高 | 顺序 + O(1) 更新/删除 + 稳定迭代完全复用；快照层可选 |
| FIFO / 插入序缓存 | 高 | 按插入序淘汰即本结构的序 |
| LRU 缓存 | 低 | LRU 需访问即重排（move-to-front），与 append-only 序冲突，强行实现会造成墓碑风暴；应用「字典 + 侵入式双向链表」经典实现 |

**抽取：触发条件已满足，不再推迟。** `docs/design/dsn-0002-container-catalog.md` §7 规划的 `OrderedSet<T>` / `OrderedDictionary<K,V>` 即第二个真实消费方，故按该文 §2.1「原语优先」原则处理：

- **存储机制**（插入序注册集 + 哈希索引 + 墓碑惰性压缩，I-1/I-2/I-6/I-7）回填为 `Core/Collections` 的 `OrderedSet<T>`，作为框架内的**唯一实现**；**快照迭代协议**（I-3/I-4/I-5）独立为 `SnapshotSet<T>` 或先私有于 `Core/Event`（见 `dsn-0003-container-necessity-review.md` §5.6）；
- `InvocationList<TDelegate>` 降为「原语 + 事件策略」的薄组合，只保留 `where TDelegate : Delegate` 的身份去重与异常隔离；
- 本文原「等第二个消费方再抽」的结论作废，以 `dsn-0002-container-catalog.md` §2.1 / §7 为准。

## 10. 结论

采用「注册集（append + 墓碑）+ 哈希索引 + 版本驱动紧凑快照 + 惰性压缩」作为 `InvocationList` 的最终形态；对叶子以 `BeginDispatch/EndDispatch` 暴露，派发期变更本轮不变、最外层边界释放引用；语义不变量 I-1…I-7 作为实现与测试的验收基线。
