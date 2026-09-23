# Grow 容器必要性验证（Necessity Review）

- Status: Draft（流程 + 首个实例结论）
- Date: 2026-09-19
- Scope: `Core/Collections`、`Core/Pool` 每个容器**实现前**的必要性核验
- Related: `docs/design/dsn-0002-container-catalog.md`（§2.1 原语优先、§5 清单、§10 路线图）、
  `docs/design/dsn-0005-event-invocationlist-analysis.md`（首个消费方）
- 目的：在动手写代码前证明每个自研容器满足「**真正有需求 + 通用 + 官方在目标基线缺失**」，
  避免自造轮子与造库无消费方。

---

## 1. 为什么需要这个流程

自研容器最容易犯两类错：**重复官方已有物**、**因单个场景过拟合而不通用**。本流程把「要不要做、
做成什么形态」变成**有证据的判定**，而不是直觉。每个容器通过后方可进入 `writing-plans` 的实现计划。

---

## 2. Necessity Gate（可复用流程）

每个候选容器实现前，逐条给出证据：

| # | 检查项 | 产出 |
|---|---|---|
| N-1 | **需求陈述** | 列出必须满足的能力（顺序语义、增删查复杂度、枚举与失效、分配、线程、装箱），逐条可测 |
| N-2 | **官方盘点** | 在**目标基线**（.NET Standard 2.1 / Unity 2021.3）逐一核对 BCL 与 Unity 类型；给出版本证据（文档 moniker / 程序集） |
| N-3 | **最近似对照** | 指出最接近的 1–3 个官方类型，**逐项对照需求**说明为何不满足（不得只说「没有」） |
| N-4 | **社区先例** | 查既有实现/范式，确认是公认缺口且设计稳定；记录其命名与 API 惯例以供对齐；标注核验状态 |
| N-5 | **通用性判定** | 确认不含领域语义（§2.1 口诀）；若含，把领域部分拆出 |
| N-6 | **结论** | `立项 / 调整后立项 / 已有等价物（放弃）/ 仅登记`，写入 §4 登记表 |

### 2.1 官方盘点的查法（基线证据）

- Microsoft Learn API 页 URL 追加 `?view=netstandard-2.1`，读取页面 `monikers:` 列表：
  含 `netstandard-2.1` 才在 Unity 2021.3 基线可用；仅有 `net-9.0+` 即**基线不可用**。
- Unity 2021.3 兼容级别为 **.NET Standard 2.1、C# 9**；`UnityEngine.Pool` 为 Unity 自带，需单独看其
  Unity 版本引入时间与能力（见 N-3）。
- 记录核验日期与证据链接，写入 §4 登记表。

---

## 3. 判定口径（避免自欺）

- **「官方没有」不等于「缺失」**：必须给出最近似物并逐项对照（N-3）。
- **「基线不可用」是硬缺口**：即使最新 .NET 已有同类，只要基线拿不到，仍属缺口；但要在文档写明
  「官方已在新版本补齐」，并说明其语义/复杂度是否本就满足（若满足，我们的价值仅在基线兼容）。
- **「通用」指无领域语义**：顺序、去重、复杂度、分配是通用契约；委托身份、GameObject 生命周期、
  帧时机是领域语义，必须剥离。

---

## 4. 登记表

| 容器 | 官方最近似 | 基线可用 | 决定性缺口 | 结论 | 核验日期 |
|---|---|---|---|---|---|
| `OrderedSet<T>` | `KeyedCollection` / `Specialized.OrderedDictionary` / `Generic.OrderedDictionary<,>`(.NET 9) | 是 / 是(非泛型) / 否 | 泛型 + O(1) 删 + 稳定插入序 + 无装箱 + struct 枚举 | **立项**（拆分快照，见 §5.6） | 2026-09-19 |
| `SnapshotSet<T>` | 无（官方均无「枚举边界快照」策略） | — | 枚举期变更安全 + 变更下一次生效 + 零分配 | **立项**（随 `OrderedSet` 首发） | 2026-09-19 |
| `Pool<T>` | `UnityEngine.Pool.ObjectPool<T>` | 是 | 无 maxSize/Trim/Prewarm/统一复位契约/统计 | 待验证 | — |
| `MinHeap` / `PriorityQueue` | `PriorityQueue<,>`(.NET 6+) | 否 | 基线无优先队列 | 待验证 | — |
| 其余（§4 目录） | — | — | — | 待验证 | — |

---

## 5. 实例：`OrderedSet<T>` 必要性验证

### 5.1 需求陈述（N-1）

消费方：`InvocationList<TDelegate>`（事件派发内核）及其泛化形态（插入序注册表 / FIFO 插入序缓存）。

| # | 能力 | 可测判据 |
|---|---|---|
| R-1 | 稳定**插入序** | 枚举顺序 == 注册时间序；`Remove→Add` 排队尾 |
| R-2 | 去重 | 重复元素返回 false/忽略，且不改变位置 |
| R-3 | 增删查期望 O(1) | `Add/Remove/Contains` 在大集合下不退化为 O(n)/O(log n) |
| R-4 | 迭代连续、稳态零分配 | 连续数组遍历；`struct` 枚举器不装箱；无变更轮次 0 GC |
| R-5 | 支持引用类型 + 自定义相等 | 可传 `IEqualityComparer<T>`（委托相等语义靠它承载） |
| R-6 | 主线程、无锁 | 无同步原语 |
| R-7 | 容量有界 | 删除产生的墓碑不无限增长（≤ 2×active） |

> R-8「枚举期变更安全（快照、变更下一次生效）」**初判为该容器能力**，复核后建议分离（见 §5.6）。

### 5.2 官方候选逐一对照（N-3）

| 官方类型 | 插入序 | Add/Remove/Contains | 枚举 | 装箱/分配 | 对照结论 |
|---|---|---|---|---|---|
| `Dictionary<K,V>` / `HashSet<T>` | 否（无稳定序） | O(1) | 无稳定序 | 无 | 不满足 R-1 |
| `SortedSet<T>` / `SortedDictionary<K,V>` | 否（**键序**） | O(log n) | 键序 | 节点分配 | 序语义与 R-1 不符、R-3 不符 |
| `List<T>` | 是 | Add O(1) / Remove **O(n)** / Contains O(n) | 索引连续 | 无 | R-3 退化 |
| `LinkedList<T>` | 是 | Add/Remove O(1)（需节点引用）/ Contains O(n) | 指针跳转 | 每节点分配 | 需节点引用、R-4 差 |
| `KeyedCollection<K,I>` | 是（List 序） | 键查≈O(1) / Remove **O(n)** | 索引连续 | 无（但需派生） | R-3 不符；抽象需子类化；无快照 |
| `Specialized.OrderedDictionary` | 是 | 键查 O(1) / Remove **O(n)** | `DictionaryEntry` | **装箱** | 非泛型、R-3/R-4/R-5 不符 |
| `Generic.OrderedDictionary<K,V>`（.NET 9） | 是 | 键查 O(1) / **List-like**（官方 Remarks：复杂度类似 `List<T>`） | 索引 | 无 | **基线不可用**；且非 O(1) 删 |
| `ImmutableArray` / COW | 是 | 变更 O(n)+每次分配 | — | 每次分配 | 违反 R-4（R3 已批判） |
| `Span<T>` / `ArraySegment<T>` | 是 | 只读视图，非容器 | — | 无 | 不适用 |

### 5.3 基线可用性证据（N-2）

| 类型 | 结论 | 证据 |
|---|---|---|
| `System.Collections.Generic.OrderedDictionary<TKey,TValue>` | **基线不可用** | Learn 页 monikers 仅 `net-9.0 / net-10.0 / net-11.0`；`?view=netstandard-2.1` 无此类型 |
| `System.Collections.Specialized.OrderedDictionary` | 基线**可用但非泛型** | Learn 页 monikers 含 `netstandard-2.0 / netstandard-2.1`；实现 `IDictionary`，元素为 `DictionaryEntry` |
| `System.Collections.ObjectModel.KeyedCollection<TKey,TItem>` | 基线可用 | Learn 页 monikers 含 `netstandard-2.1`；List+Dictionary 混合，Remove 为 List 式 O(n) |

> 证据链接见文末附录。**这条修正了目录 §3.2 早前的表述**（原文误称非泛型
> `OrderedDictionary` 不在 .NET Standard 2.1 面内）。

### 5.4 社区既有实现与范式（N-4）

| 来源 | 形态 | 与需求关系 | 核验 |
|---|---|---|---|
| Rust `indexmap` | 哈希表 + 条目数组；`swap_remove` O(1) 但乱序、`shift_remove` O(n) 保序 | 印证「保序 + O(1) 删」需权衡；本方案用**批量惰性压缩**取得保序 + 摊还 O(1) | 待核验 |
| 游戏物理库的 Quick 系容器（如 Bepu `QuickDictionary`/`QuickSet`） | 字典 + 数组，删除用 swap-remove | 乱序，不满足 R-1 | 待核验 |
| 社区 `OrderedSet<T>` 实现（如 Towel） | 字典 + 链表族 | 满足 R-1/R-3，但节点分配与指针遍历（R-4 次优） | 待核验 |
| .NET 泛型 `OrderedDictionary` 长期提案 → .NET 9 落地 | 官方诉求存在 | 印证官方缺口真实；但其选择 List-like 语义，非 O(1) 删 | 已核验（§5.3） |
| `UniTask PlayerLoopRunner` / `R3` 派发前取快照数组 | 快照派发 | 印证「枚举期安全」是**独立关切**，不属有序集合语义 | 已核验（见 `dsn-0004-event-primitives.md` §5） |

### 5.5 缺口判定

- **R-1 + R-3 + R-4 的组合在基线内无官方解**：能保序的（`List`/`KeyedCollection`/`Specialized`）
  删除皆为 O(n) 或非泛型/装箱；能 O(1) 增删的（`Dictionary`/`HashSet`）无稳定序。
- **`Generic.OrderedDictionary<,>` 即使能拿到也不满足 R-3**：官方 Remarks 明示其复杂度类似 `List<T>`
  （保序靠数组位移），删除非 O(1)。故我们与官方最新实现是**不同取舍**，不是重复。
- 结论：`OrderedSet<T>`（保序 + O(1) 增删查 + 连续零分配枚举 + 自定义相等）**属真实、通用的官方缺口**，
  应予补充。

### 5.6 设计校正：快照是否属于该容器

复核 R-8 后**建议拆分**：

- 有序集合的**标准语义**只到「插入序 + 去重 + 复杂度 + 枚举」；「枚举期变更安全」是一层**迭代协议**，
  官方 `OrderedDictionary`、`KeyedCollection` 均不提供，社区由派发器自行取快照。
- 把快照塞进 `OrderedSet<T>` 会让每个不需要该协议的消费者也承担版本数组与读深度；且强制
  `BeginRead/EndRead` 使用协议，违背单一职责。
- 因此：`OrderedSet<T>` 保持**通用纯容器**（暴露 `Version` 与跳过墓碑的枚举，供上层重建快照）；
  快照层作为**独立原语** `SnapshotSet<T>`（或先私有于 `InvocationList`，待第二消费方再提升）。
  这样既不重复机制（唯一实现），也不把领域/协议语义污染通用容器。

### 5.7 结论（N-6）

- **结论：调整后立项。** 实现 `OrderedSet<T>`（纯容器，§5.6）；快照层 `SnapshotSet<T>` 作为独立原语，
  单独过 Necessity Gate（见 §6）。
- 对 `InvocationList` 的意义：它组合「`OrderedSet<T>` + `SnapshotSet<T>` + 事件策略」，机制无第二份实现。

---

## 6. 实例：`SnapshotSet<T>` 必要性验证

> 决策更新：用户已确认快照层作为**独立原语同步交付**（不再「先私有」），故本节补齐其 Gate。

### 6.1 需求陈述（N-1）

消费方：`InvocationList<TDelegate>`（事件派发）——以及任何「遍历中可能增删」的可变序列。

| # | 能力 | 可测判据 |
|---|---|---|
| S-1 | 枚举期变更安全 | 迭代期间对源集合增删，本轮迭代结果不变 |
| S-2 | 变更下一次生效 | 变更后开始的下一轮读取反映最新集合 |
| S-3 | 嵌套/重入 | 嵌套读取复用同一快照、各自从头独立迭代；最外层退出才释放 |
| S-4 | 边界释放引用 | 最外层退出且已变更时置空快照引用（不重建、不分配、不抛） |
| S-5 | 稳态零分配 | 无变更轮：仅版本比较 + 数组遍历，0 GC |
| S-6 | 主线程、无锁 | 无同步原语 |
| S-7 | 通用 | 对任意 `T` 成立，不含事件/委托语义 |

### 6.2 官方候选逐一对照（N-3）

| 官方类型 | 枚举期变更 | 变更生效时机 | 分配 | 对照结论 |
|---|---|---|---|---|
| `ReadOnlyCollection<T>` / `IReadOnlyList<T>` | 否（活视图，受下层修改影响） | 立即 | 无 | 不满足 S-1 |
| `ImmutableArray<T>` / COW | 是（不可变） | 每次变更新建 | 每次变更分配 | 违反 S-5（R3 已批判，见 InvocationList §8） |
| `List<T>` 等 BCL 枚举器 | 否（`InvalidOperationException`） | — | 无 | 不满足 S-1 |
| `Collection<T>` / `KeyedCollection<K,I>` | 否（枚举器失效） | — | — | 不满足 S-1 |
| `Span<T>` / `Memory<T>` | 只读窗口，非策略 | — | — | 不适用 |

**无一官方类型提供「枚举边界快照 + 变更下一次生效 + 零分配」这一策略。**

### 6.3 基线可用性（N-2）

不适用：目标能力在 .NET Standard 2.1 与最新 .NET 中**均无官方对应类型**（§6.2 已证）。

### 6.4 社区先例（N-4）

| 来源 | 形态 | 关系 | 核验 |
|---|---|---|---|
| `UniTask` `PlayerLoopRunner` | 遍历前取数组快照 | 印证策略；但快照内联于 runner，非可复用类型 | 已核验（`dsn-0004-event-primitives.md` §5） |
| `R3` | 派发前物化、异常不回灌当前轮 | 印证「变更/异常下一次生效」 | 已核验（同上） |
| 常见事件系统 `ToArray()`/复制列表 | 每轮分配 | 反例：正是要消除的分配 | 待核验 |

### 6.5 通用性判定（N-5）

`SnapshotSet<T>` 只依赖「源集合的 `Version` + 活跃元素 + 计数」，与事件/委托无关 → 通用。
协议（`BeginRead/EndRead`、变更下一次生效、边界释放）是**迭代策略**，非领域语义。

### 6.6 结论（N-6）

- **结论：立项。** 与 `OrderedSet<T>` 同步交付；`SnapshotSet<T>` 内部持有 `OrderedSet<T>`（组合），
  不复制其存储状态机（满足 G-5）。
- 接口草案：

  ```csharp
  namespace Grow.Core.Collections {
      public sealed class SnapshotSet<T> {
          public SnapshotSet(int capacity = 8);
          public SnapshotSet(IEqualityComparer<T> comparer, int capacity = 8);
          public int Count { get; }
          public bool Add(T item);
          public bool Remove(T item);
          public bool Contains(T item);
          public void Clear();
          public bool BeginRead(out T[] items, out int count);   // 空集返回 false 且不进入深度
          public void EndRead();
      }
  }
  ```

- 不变量：复用 InvocationList 的 I-3/I-4/I-5（边界重建、嵌套复用、最外层释放）；条目顺序 = 插入序。

---

## 7. 下一步

1. 已确认：拆分，且 `SnapshotSet<T>` 同步交付。
2. 已修正 `docs/design/dsn-0002-container-catalog.md` §5.7 与首发描述。
3. 对 `OrderedSet<T>` + `SnapshotSet<T>` 启动 `writing-plans` 实现计划（含 EditMode 测试程序集搭建）。

---

## 附录：核验证据链接

- `Generic.OrderedDictionary<TKey,TValue>`（仅 .NET 9+）：
  https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ordereddictionary-2
- 其 `Remove`（页内说明复杂度与 `List<T>` 类似，见类 Remarks）：
  https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ordereddictionary-2.remove
- `Specialized.OrderedDictionary`（含 netstandard-2.0/2.1）：
  https://learn.microsoft.com/en-us/dotnet/api/system.collections.specialized.ordereddictionary
- `KeyedCollection<TKey,TItem>`（含 netstandard-2.1）：
  https://learn.microsoft.com/en-us/dotnet/api/system.collections.objectmodel.keyedcollection-2
