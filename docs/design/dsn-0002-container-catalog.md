# Grow 自定义容器目录（Core/Collections · Core/Pool 需求 Spec）

- 状态：草稿（需求目录，待逐项立项）
- 日期：2026-09-19
- 范围：未来 `Packages/com.ronny.grow/Runtime/Core/Collections/*` 与 `Runtime/Core/Pool/*`
- 关联：`docs/design/dsn-0003-container-necessity-review.md`（实现前的必要性验证流程与登记表）、
  `docs/design/dsn-0005-event-invocationlist-analysis.md`（机制复用与 LRU 纠正）、`docs/design/dsn-0004-event-primitives.md`、
  `docs/explanation/exp-0001-architecture-rationale.md` §8（目录与归属）、`docs/architecture/adr-0002-domain-reload-disabled.md`
- 产物边界：本目录只回答「Unity 常用但 C#/Unity 未直接提供、需自研的容器有哪些、各自缺口与契约是什么」。
  每个容器的逐步 TDD 实现步骤另行以 `writing-plans` 产出，本文不含 checkbox 任务。

---

## Global Constraints（全目录适用）

- Unity **2021.3.45f2**，C# 9，API 兼容级别 **.NET Standard 2.1**；不得使用晚于该基线的 API。
- 落点与命名空间：集合 → `Runtime/Core/Collections`、命名空间 `Grow.Core.Collections`；
  泛型对象池 → `Runtime/Core/Pool`、命名空间 `Grow.Core.Pool`。二者均属 Core（纯数据结构、无时机）。
- 每个 `.cs` 文件以 3 行 MIT 头开始（`Copyright (c) 2026 Ronny Wu` / MIT / LICENSE）。
- 不添加任何代码注释；语义契约由本文与各容器专题文档承载。
- 仅主线程、无锁、无同步原语。
- 无第三方依赖；仅 BCL +（必要时）`UnityEngine` 纯值类型；绝不依赖 Kernel / Services / Integrations / Tooling。
- **ADR-0002**：关闭域重载。静态字段/缓存/singleton 必须在 `GrowBoot.OnReset` 可复位；本目录默认
  **零静态**，凡引入静态（类型 id 缓存、全局池注册表）者必须显式登记复位点。
- 稳态零分配、枚举器为 `struct`（禁止装箱）、容量增长/收缩策略明确写入专题文档。
- 禁止 L1 名 `Utils / Common / Misc / Helpers / Managers`。

---

## 1. 背景与目标

Unity 运行时长期缺少一批「高频、通用、但 BCL 不提供或 Unity 不提供」的容器。团队通常在各项目里
重复手写（对象池、优先队列、LRU、双端队列……），既重复又易错。Grow 把这类容器沉淀为框架特色能力，
统一契约（零分配、确定性顺序、可测边界），并以 `Core` 被动原语的形态供 Kernel/Services/玩法复用。

目标：

1. 圈定一份**有界**的自研容器清单（每项必须有真实消费方，见 §2 门槛），避免「造库无消费方」。
2. 对每项给出「缺口 → API 草案 → 复杂度/分配契约 → 优先级 → 依赖」。
3. 明确**不做**的容器与**边界外**容器（归属其他 L1），防止范围蔓延。

---

## 2. 入选门槛与判据

一个候选进入本目录，必须同时满足：

| # | 门槛 |
|---|---|
| G-1 | C# BCL（.NET Standard 2.1）或 Unity 官方包**没有等价物**，或等价物存在关键缺陷（装箱、分配、无 O(1) 删除、无容量控制）。 |
| G-2 | 被动数据结构、无时间概念；删掉 Kernel 后仍可编译/单测/复用（架构 §8.6）。 |
| G-3 | 至少一个**具名真实消费方**（框架模块或明确玩法场景）；无消费方 → 仅登记不立项。 |
| G-4 | 能给出确定性语义（顺序、去重、失效规则）与可测边界。 |

优先级：`P0` 消费方明确且高频；`P1` 明确但可用现成结构临时顶替；`P2` 有价值、消费方待定，先不实现。

**实现前门禁**：通过 G-1…G-4 后，还须通过 `docs/design/dsn-0003-container-necessity-review.md` 的 Necessity Gate
（N-1…N-6，官方盘点 + 最近似对照 + 社区先例 + 通用性判定），产出经证据的结论写入其登记表；未通过不得开工。

### 2.1 原语优先原则（anti-proliferation）

**专用容器是症状，不是终点。** 每出现一个专用容器或私有数据结构，先判它属于哪一类：

| 差异维度 | 判定 | 处置 |
|---|---|---|
| 不变量 / 顺序 / 内存布局（插入序、O(1) 删除、字级位运算、堆序） | **缺失的容器原语** | 补进 `Core/Collections`，专用类型改为**组合**它 |
| 语义 / 策略 / 生命周期（委托身份、GameObject 生命周期、帧时机） | **非容器** | 落 Kernel / Services，内部组合原语 |
| 仅同一原语的特化（如 `T : Delegate`、`T : struct`） | 泛化原语 + 薄策略层 | 原语不加约束，特化只保留策略 |

判定口诀：**「拿掉领域语义后，这个数据结构本身还成立吗？」** 成立 → 它是原语；不成立 → 它是领域类型。

**强制不重复机制（G-5）**：任何专用类型**不得**复制原语的状态机（注册集 / 墓碑 / 版本 / sift / 位字 / 环形游标）。
框架内出现第二份同机制实现，即视为架构缺陷，必须回填原语并让原持有者改为组合。

> 推论：`InvocationList` 不是独立容器，而是「插入序集合原语 + 事件策略」的组合（见 §5.7、§7）。
> 本目录不得把它当作特例豁免；凡与原语机制重合的私有实现一律纳入原语范畴。

---

## 3. 现状盘点：BCL / Unity 已提供什么

### 3.1 已有、无需自研

| 能力 | 现成类型 | 说明 |
|---|---|---|
| 动态数组 | `List<T>` | — |
| 哈希映射/集合 | `Dictionary<K,V>` / `HashSet<T>` | — |
| 队列/栈 | `Queue<T>` / `Stack<T>` | 各仅单端 |
| 双向链表 | `LinkedList<T>` | 节点独立分配、缓存不友好 |
| 排序结构 | `SortedSet<T>` / `SortedDictionary<K,V>` | 红黑树、O(log n)、无索引访问 |
| 数组池 | `System.Buffers.ArrayPool<T>`（.NET Standard 2.1） | 不重复提供 |
| Unity 泛型池 | `UnityEngine.Pool.ObjectPool<T>` / `ListPool<T>` / `HashSetPool<T>` / `DictionaryPool<K,V>` | Unity 2021.1+，有但对多场景缺口明显 |
| 值/内存切片 | `Span<T>` / `Memory<T>` / `ArraySegment<T>` | .NET Standard 2.1 可用 |
| 可观察集合 | `System.Collections.ObjectModel.ObservableCollection<T>` | 存在，但逐次变更分配重 |

### 3.2 关键缺口（本目录的立项依据）

| 缺口类型 | 具体缺失 | 后果 |
|---|---|---|
| 优先队列 | `System.Collections.Generic.PriorityQueue<T,P>` 是 **.NET 6+**，Unity 2021.3 基线不可用 | A* open set、定时器、调度器无可用堆 |
| 有序泛型字典 | 基线内最接近者为 `KeyedCollection<K,I>`（List 序但 Remove O(n)、需派生）与**非泛型** `Specialized.OrderedDictionary`（装箱、Remove O(n)）；泛型 `Generic.OrderedDictionary<K,V>` 是 **.NET 9+**，基线不可用且为 List-like | 需插入序 + O(1) 增删的场景只能手写 List+IndexOf（O(n) 删除）；详见必要性验证 §5 |
| LRU | 无任何现成 | 缓存只能自写，且易写错重排/淘汰 |
| 双端队列 | `Queue`/`Stack` 各单端 | 输入缓冲、undo/redo、命令队列需 O(1) 双端 |
| 固定容量覆盖缓冲 | 无 | 帧历史、replay、固定日志需手写取模 |
| 一对多映射 | `ToLookup` 的 `ILookup` 不可变、每次重建分配 | tag→对象、依赖表、事件分组需可变一对多 |
| 稀疏集 | 无 | 组件/标签存储、脏标记缺缓存友好的 O(1) 增删 |
| 字级位集 | `BitArray` 是 class、逐位索引慢、无 popcount/ctz；`System.Numerics.BitOperations` 为 .NET Core 3.0+，基线不可用 | 可见性/mask/脏位需自实现字级位运算 |
| 强类型枚举位集 | 无 | `[Flags]` 的动态集合只能手写 |
| 代际句柄/槽位 | 无 | 安全引用（防悬垂）需自研 SlotMap |
| 泛型池的容量控制 | Unity 2021.3 的 `ObjectPool<T>` 无 `maxSize`/Trim/Prewarm/`IPoolable` 契约/统计 | 池无上限、无预热、无统一复位契约 |
| Inspector 可序列化字典 | Unity 不序列化 `Dictionary<K,V>` | 需 `SerializableDictionary`（落 `Core/Serialization`） |

---

## 4. 缺口矩阵（一页速览）

| 容器 | 最近现成物 | 核心缺口 | 落点 / 命名空间 | 优先级 |
|---|---|---|---|---|
| **`OrderedSet<T>` / `OrderedDictionary<K,V>`（原语）** | `KeyedCollection` / 非泛型 OrderedDictionary / Generic.OrderedDictionary(.NET9) | 泛型 + 插入序 + O(1) 增删 + 无装箱 + 稳定枚举 | `Core/Collections` | **P0** |
| `SnapshotSet<T>`（原语，已验证） | 无（官方类型均不提供枚举期安全） | 枚举期变更安全、变更下一次生效、边界释放引用 | `Core/Collections` | P0 |
| `InvocationList<TDelegate>`（现状） | 无 | **不是独立容器**：应降为 `OrderedSet<T>` + 事件策略；其机制须回填原语 | `Core/Event` **组合** `Core/Collections` | **重构** |
| `Pool<T>` + `IPoolable` | `UnityEngine.Pool.ObjectPool<T>` | 无 maxSize/Trim/Prewarm、无统一复位契约、无统计 | `Core/Pool` · `Grow.Core.Pool` | P0 |
| `MinHeap<T>` / `PriorityQueue<TItem,TPriority>` | 无 | 基线无优先队列 | `Core/Collections` | P0 |
| `IndexedMinHeap<TKey,TItem>` | 无 | 无 decrease-key | `Core/Collections` | P0 |
| `LruCache<TKey,TValue>` | 无 | 完全没有 | `Core/Collections` | P0 |
| `Deque<T>` | `Queue`/`Stack` | 无双端 O(1) | `Core/Collections` | P0 |
| `RingBuffer<T>` | 无 | 无固定容量覆盖式 | `Core/Collections` | P0 |
| `MultiValueDictionary<K,V>` | `ILookup`（不可变） | 无可变一对多、零分配遍历 | `Core/Collections` | P1 |
| `SparseSet<T>` | 无 | 无缓存友好 O(1) 稀疏集合 | `Core/Collections` | P1 |
| `BitSet` / `EnumSet<TEnum>` | `BitArray`（慢/装箱） | 无字级位运算/popcount/ctz | `Core/Collections` | P1 |
| `SerializableDictionary<K,V>` | 无 | Unity 不序列化字典 | `Core/Serialization` | P1 |
| `SlotMap<T>` + `SlotHandle` | 无 | 无代际句柄 | `Core/Collections` | P2 |
| `TypeMap<TValue>` | 无 | 无类型→索引注册 | `Core/Collections` | P2 |
| `ObservableList<T>` | `ObservableCollection<T>` | 逐次分配、无成簇通知 | `Core/Collections` | P2 |
| GameObject 池 | `UnityEngine.Pool.ObjectPool<GameObject>` | 预制体/场景生命周期 | **边界外**：`Services/Pooling` | 延后 |
| 时间轮 / 延时队列 | 无 | 掌时 | **边界外**：`Kernel/Scheduling` | 延后 |
| 空间哈希 / 均匀网格 | 无 | 领域能力 | **边界外**：`Services`（待立） | 延后 |

---

## 5. 容器清单

### 5.1 `Pool<T>` 与 `IPoolable`（P0）

- 用途：子弹/特效/UI 列表项/集合临时对象的复用；一次获取即自动复位。
- 缺口：Unity 2021.3 的 `ObjectPool<T>` 无最大容量、无 Trim、无 Prewarm、无统一复位接口、无命中统计。
- 与 `UnityEngine.Pool` 的关系：**不包装**（其 2021.3 版无法外部 Trim），自持以控制容量/统计/契约；
  `UnityEngine.Pool` 保留给不需这些能力的临时用法。数组池一律用 `System.Buffers.ArrayPool<T>`，不重复。

```csharp
namespace Grow.Core.Pool {
    public interface IPoolable {
        void OnGet();
        void OnRelease();
        void OnDestroy();
    }

    public sealed class Pool<T> where T : class {
        public Pool(Func<T> create, Action<T> onGet = null, Action<T> onRelease = null,
                    Action<T> onDestroy = null, int defaultCapacity = 0, int maxSize = 1024);
        public int CountInactive { get; }
        public int CountAll { get; }
        public int CountActive { get; }
        public T Get();
        public PooledObject<T> Get(out T value);
        public void Release(T value);
        public void Release(PooledObject<T> pooled);
        public void Prewarm(int count);
        public int Trim(int targetInactive);
        public void Clear();
    }

    public struct PooledObject<T> : IDisposable, IEquatable<PooledObject<T>> where T : class {
        public T Value { get; }
        public void Dispose();
    }
}
```

| 操作 | 复杂度 | 分配 |
|---|---|---|
| Get / Release | 摊还 O(1) | 仅首次创建；释放入栈无分配 |
| Prewarm(n) | O(n) | n 次创建 |
| Trim(k) | O(超出量) | 触发 `onDestroy` |
| Clear | O(inactive) | 0 |

- 契约：`maxSize` 满时 `Release` 直接销毁（调 `onDestroy`）；`T : IPoolable` 时自动调用生命周期钩子，
  显式传入的 `onGet/onRelease/onDestroy` 在其后追加；从 `Get(out …)` 取得的句柄 `Dispose` 等价 `Release`。
- 依赖：无。消费方：Services/Pooling（作为底层泛型池）、任何高频 spawn 玩法。
- 静态：默认实例持有；若提供 `PoolRegistry`（类型→池），必须登记 `GrowBoot.OnReset` 复位。

### 5.2 `MinHeap<T>` / `PriorityQueue<TItem,TPriority>`（P0）

- 用途：A* open set、定时器到期、行为树/调度优先级、Dijkstra。
- 缺口：基线无优先队列（`PriorityQueue<T,P>` 是 .NET 6+）。
- 形态：数组二叉堆；`MinHeap<T>` 以 `IComparer<T>` 排序；`PriorityQueue` 显式携带优先级，并以
  **入队序号**做稳定性 tie-break，保证同优先级出队顺序确定。

```csharp
namespace Grow.Core.Collections {
    public sealed class MinHeap<T> {
        public MinHeap(int capacity = 16);
        public MinHeap(IComparer<T> comparer, int capacity = 16);
        public int Count { get; }
        public bool IsEmpty { get; }
        public T Peek();
        public void Push(T item);
        public T Pop();
        public bool TryPop(out T item);
        public bool TryPeek(out T item);
        public void Clear();
    }

    public sealed class PriorityQueue<TItem, TPriority> {
        public PriorityQueue(int capacity = 16);
        public PriorityQueue(IComparer<TPriority> comparer, int capacity = 16);
        public int Count { get; }
        public void Enqueue(TItem item, TPriority priority);   // 同优先级按入队序
        public bool TryDequeue(out TItem item);
        public bool TryPeek(out TItem item);
        public void Clear();
    }
}
```

| 操作 | 复杂度 | 分配 |
|---|---|---|
| Push / Enqueue | O(log n) | 仅扩容，摊还 O(1) |
| Pop / Dequeue / Peek | O(log n) / O(1) | 0 |
| Clear | O(1) | 0 |

- 消费方：A* 寻路（Services 或玩法）、Kernel/Time 的到期堆。

### 5.3 `IndexedMinHeap<TKey,TItem>`（P0）

- 用途：A* 中已有节点**降低键值（decrease-key）**，避免重复入堆膨胀。
- 缺口：`PriorityQueue` 不支持按 key 更新；这是 A* 性能关键。

```csharp
namespace Grow.Core.Collections {
    public sealed class IndexedMinHeap<TKey, TItem> {
        public IndexedMinHeap(IComparer<TItem> comparer, int capacity = 16);
        public int Count { get; }
        public bool ContainsKey(TKey key);
        public void PushOrUpdate(TKey key, TItem item);   // 不存在则入堆，存在则 replace + sift
        public bool TryPop(out TKey key, out TItem item);
        public bool Remove(TKey key);
        public void Clear();
    }
}
```

- 机制：`Dictionary<TKey,int>` key→堆下标 + 堆内数组；`PushOrUpdate` 命中即原地替换并按需上浮/下沉。
- 复杂度：全部 O(log n)，除 `ContainsKey` O(1)；稳态零分配。
- 依赖：`MinHeap` 的 sift 逻辑（内部共享）。

### 5.4 `LruCache<TKey,TValue>`（P0）

- 用途：资源缓存、寻路结果缓存、UI 页缓存、局部化字符串缓存。
- 缺口：BCL/Unity 均无。
- 机制（**关键决策**）：`dsn-0005-event-invocationlist-analysis.md` §9 明确指出「LRU 需访问即 move-to-front，
  与 append-only 插入序冲突，强行复用墓碑方案会造成墓碑风暴」；故采用经典
  **`Dictionary<TKey,int>` + 侵入式双向链表（并行数组实现）**，全程 O(1) 且稳态零分配。

```csharp
namespace Grow.Core.Collections {
    public sealed class LruCache<TKey, TValue> {
        public LruCache(int capacity = 64, Action<TKey, TValue> onEvict = null);
        public LruCache(int capacity, IEqualityComparer<TKey> comparer,
                        Action<TKey, TValue> onEvict = null);
        public int Count { get; }
        public int Capacity { get; set; }
        public long Hits { get; }
        public long Misses { get; }
        public bool TryGet(TKey key, out TValue value);    // 命中即 move-to-front
        public bool TryPeek(TKey key, out TValue value);   // 不改变顺序
        public void Set(TKey key, TValue value);           // 已存在则更新并置 MRU
        public bool Remove(TKey key);
        public bool TryRemove(TKey key, out TValue value);
        public int Trim(int targetCount);                  // 返回淘汰数，触发 onEvict
        public void Clear();
    }
}
```

| 操作 | 复杂度 | 分配 |
|---|---|---|
| TryGet / TryPeek / Set / Remove | O(1) | 0（可能触发 onEvict，但不分配） |
| Trim(k) / Clear | O(淘汰数) | 0 |

- 契约：`capacity <= 0` 视为非法（构造抛）；`Capacity` 调小立即淘汰至新容量；`TryPeek` 不刷新热度。
- 依赖：无。消费方：Services/Assets、寻路、UI。

### 5.5 `Deque<T>`（P0）

- 用途：输入缓冲、undo/redo、命令队列、滑动窗口。
- 缺口：`Queue`/`Stack` 各仅单端 O(1)。

```csharp
namespace Grow.Core.Collections {
    public sealed class Deque<T> {
        public Deque(int capacity = 16);
        public int Count { get; }
        public T this[int index] { get; set; }   // 0 = 前端（最旧）
        public void PushFront(T item);
        public void PushBack(T item);
        public T PopFront();
        public T PopBack();
        public bool TryPopFront(out T item);
        public bool TryPopBack(out T item);
        public T PeekFront();
        public T PeekBack();
        public void Clear();
    }
}
```

- 机制：环形数组，头尾游标；扩容时按逻辑序重排为线性（一次性摊还 O(1)）。
- 复杂度：两端增删 O(1) 摊还；索引随机访问 O(1)；Clear O(1)（置空引用）。

### 5.6 `RingBuffer<T>`（P0）

- 用途：帧历史、replay/回放、固定长度日志、最近 N 事件。
- 缺口：无固定容量覆盖式缓冲（`Deque` 满会扩容，语义不同）。

```csharp
namespace Grow.Core.Collections {
    public sealed class RingBuffer<T> {
        public RingBuffer(int capacity);   // 固定容量，满则覆盖最旧
        public int Capacity { get; }
        public int Count { get; }
        public bool IsFull { get; }
        public T Oldest { get; }
        public T Newest { get; }
        public void Push(T item);
        public T this[int index] { get; }  // 0 = 最旧
        public void Clear();
    }
}
```

- 与 `Deque` 的分工：需要「满则覆盖」用 `RingBuffer`；需要「满则增长」用 `Deque`。
- 契约：容量固定、只增不改；`Push` 恒 O(1)、零分配（容量构造时定死）。

### 5.7 `OrderedSet<T>` / `OrderedDictionary<K,V>` / `SnapshotSet<T>`（P0，**原语对**）

- 定位：本目录的**第一个通用原语**，也是 §2.1 判定与 §7 的落点。凡「插入序 + O(1) 增删 + 稳定迭代」
  的需求都收敛到它，专用类型只能组合它，不得复制其状态机。
- 现有消费者：`InvocationList<TDelegate>`（事件派发内核）——即它**已经是第二个消费方**，
  满足 G-3，故直接 `P0`，不再「等出现第二消费方」。
- 用途：插入序注册表、FIFO 插入序缓存、确定性迭代的字典/集合、事件派发。
- 缺口（见 `dsn-0003-container-necessity-review.md` §5）：`SortedDictionary` 按键排序而非插入序；
  `KeyedCollection` / 非泛型 `Specialized.OrderedDictionary` 的删除是 O(n) 或装箱；泛型
  `Generic.OrderedDictionary<K,V>` 是 .NET 9+（基线不可用）且为 List-like，删除非 O(1)。
- 机制：`dsn-0005-event-invocationlist-analysis.md` §9 判定为「高机制吻合度」的
  **插入序注册集（append + 墓碑 + 惰性压缩）+ 哈希索引 + 稳定枚举**；这是该机制在框架内的**唯一实现**。
- 快照迭代的归属（已定）：必要性验证 §5.6/§6 将「枚举期变更安全」拆为独立原语 `SnapshotSet<T>`，
  与 `OrderedSet<T>` **同步首发**；`SnapshotSet<T>` 内部组合存储、自维护版本快照，不把迭代协议塞进
  通用容器。

```csharp
namespace Grow.Core.Collections {
    public sealed class OrderedDictionary<TKey, TValue> {
        public OrderedDictionary(int capacity = 8);
        public OrderedDictionary(IEqualityComparer<TKey> comparer, int capacity = 8);
        public int Count { get; }
        public bool ContainsKey(TKey key);
        public bool TryGetValue(TKey key, out TValue value);
        public TValue this[TKey key] { get; set; }
        public void Add(TKey key, TValue value);   // 已存在则抛
        public void Set(TKey key, TValue value);   // 存在则原位覆盖，不改变插入序
        public bool Remove(TKey key);
        public void Clear();
        public Enumerator GetEnumerator();         // 插入序，struct
        public KeyEnumerator Keys { get; }
        public ValueEnumerator Values { get; }
    }

    public sealed class OrderedSet<T> {
        public OrderedSet(int capacity = 8);
        public OrderedSet(IEqualityComparer<T> comparer, int capacity = 8);
        public int Count { get; }
        public bool Add(T item);     // 重复返回 false 且不变序
        public bool Remove(T item);
        public bool Contains(T item);
        public void Clear();
        public int Version { get; }          // 每次结构变更自增，供上层按需重建快照
        public Enumerator GetEnumerator();   // 插入序，struct，自动跳过墓碑
    }
}
```

- 契约（对齐 I-1、I-2、I-6、I-7 的纯容器部分）：物理序 == 插入时间序；`Set`/索引器不改变位置；
  删除 O(1) 墓碑；`tombstone >= active` 时于操作边界惰性压缩；注册集 ≤ 2×active；无变更遍历 0 分配。
- 载重语义：`T : Delegate` 的去重身份、异常隔离、`Clear` 时机等**一律不进原语**；原语只暴露
  `IEqualityComparer<T>`，事件语义留在 `Core/Event`。
- 抽取义务：`InvocationList` 的**存储机制**（注册集/索引/墓碑）回填到 `OrderedSet<T>`、
  **快照机制**回填到 `SnapshotSet<T>`（下节），`Core/Event` 只保留事件策略（G-5 硬约束）。

#### `SnapshotSet<T>`（迭代协议原语，P0）

- 用途：任何「遍历中可能增删」的集合（事件派发、观察者广播、实体更新列表）。
- 缺口：官方无「枚举边界快照 + 变更下一次生效 + 零分配」策略；COW/Immutable 每次分配已被否决（§9）。
- 机制：内部持有存储（组合 `OrderedSet<T>` 的注册集/索引/墓碑，不复制其状态机）；自维护
  `_snapshot : T[]`、`_snapshotCount`、`_builtVersion`、`_depth`。读取边界重建、嵌套复用、最外层释放。

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

- 契约（对齐 I-3 / I-4 / I-5）：仅 `_depth == 0 && _version != _builtVersion` 时于读取边界重建快照；
  嵌套读取复用同一快照、各自从头独立迭代；最外层退出才置空引用并标脏（不重建、不分配、不抛）；
  无变更轮零分配。
- 消费方：`InvocationList<TDelegate>`（`SnapshotSet<TDelegate>` + 委托身份/异常策略）。

### 5.8 `MultiValueDictionary<TKey,TValue>`（P1）

- 用途：tag→对象、实体→组件、依赖表、事件分组。
- 缺口：`ILookup` 不可变且每次重建分配；`Dictionary<K,List<V>>` 样板代码 + 空列表残留。

```csharp
namespace Grow.Core.Collections {
    public sealed class MultiValueDictionary<TKey, TValue> {
        public MultiValueDictionary(int capacity = 8);
        public MultiValueDictionary(IEqualityComparer<TKey> comparer, int capacity = 8);
        public int KeyCount { get; }
        public int ValueCount { get; }
        public bool Add(TKey key, TValue value);
        public bool Remove(TKey key, TValue value);
        public bool RemoveAll(TKey key);
        public bool Contains(TKey key, TValue value);
        public bool ContainsKey(TKey key);
        public bool TryGetValues(TKey key, out IReadOnlyList<TValue> values);
        public ValuesEnumerator GetValues(TKey key);   // struct，零分配
        public void Clear();
    }
}
```

- 机制：`Dictionary<TKey,List<TValue>>`；`Remove` 清空后移除键，杜绝空列表残留。
- 契约：值重复默认不去重（如需去重另立 `MultiValueSet`）；`GetValues` 的 struct 枚举器不装箱。

### 5.9 `SparseSet<T>`（P1）

- 用途：组件/标签存储、脏标记集合、需要「O(1) 增删 + 连续密集遍历」的场景。
- 缺口：无。`HashSet<T>` 遍历缓存不友好，`List<T>` 删除 O(n)。

```csharp
namespace Grow.Core.Collections {
    public sealed class SparseSet<T> where T : IEquatable<T> {
        public SparseSet(int capacity = 16);
        public int Count { get; }
        public bool Contains(T item);
        public bool Add(T item);
        public bool Remove(T item);
        public void Clear();
        public Enumerator GetEnumerator();   // 密集数组序，struct
    }
}
```

- 机制：稀疏→密集下标映射 + 密集数组；`Remove` 用末尾元素回填被删槽（swap-remove），O(1)。
- 契约：遍历顺序**不稳定**（swap-remove 会改变顺序），需稳定序时用 `OrderedSet`。
- 可选特化：键为 `int` 且范围有限时，用数组稀疏表替代字典（`SparseSetInt`），后续按需再立。

### 5.10 `BitSet` / `EnumSet<TEnum>`（P1）

- 用途：可见性 mask、脏位、权限/标签集合、宽相碰撞候选。
- 缺口：`BitArray` 是 class、逐位索引与装箱开销、无 popcount/ctz；`System.Numerics.BitOperations`
  为 .NET Core 3.0+，Unity 2021.3 基线不可用 → 字级位运算需自实现。

```csharp
namespace Grow.Core.Collections {
    public sealed class BitSet {
        public BitSet(int capacity = 64);
        public int Capacity { get; }
        public int Count { get; }              // == PopCount()
        public void Set(int index);
        public void Reset(int index);
        public void Set(int index, bool value);
        public bool Get(int index);
        public void SetAll();
        public void ResetAll();
        public void UnionWith(BitSet other);
        public void IntersectWith(BitSet other);
        public void ExceptWith(BitSet other);
        public bool IsSubsetOf(BitSet other);
        public int PopCount();
        public int FirstSetBit();              // 无则 -1
        public int NextSetBit(int from);       // 无则 -1
    }

    public struct EnumSet<TEnum> where TEnum : struct, Enum {
        public int Count { get; }
        public void Add(TEnum value);
        public void Remove(TEnum value);
        public bool Contains(TEnum value);
        public void UnionWith(EnumSet<TEnum> other);
        public void ExceptWith(EnumSet<TEnum> other);
        public void Clear();
        public Enumerator GetEnumerator();     // 已置位枚举，struct
    }
}
```

- 机制：`ulong` 字数组；popcount/ctz 用 De Bruijn 或字节查找表（不用 `BitOperations`）。
- 契约：越界索引按需扩容（`BitSet`）或抛（`EnumSet` 越界即抛）；`EnumSet` 的枚举上界在静态构造
  缓存一次，该静态缓存须登记 `GrowBoot.OnReset`（ADR-0002）或改为按 `Convert.ToInt32` 即时计算。

### 5.11 `SerializableDictionary<TKey,TValue>`（P1）

- 落点：`Runtime/Core/Serialization`、命名空间 `Grow.Core.Serialization`（架构 §8.7 已规划，非 Collections）。
- 用途：Inspector 可编辑的字典（Unity 不序列化 `Dictionary<K,V>`）。
- 缺口：Unity 原生序列化仅支持 `List<K>` + `List<V>` 平行结构；无现成泛型字典。

```csharp
namespace Grow.Core.Serialization {
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver {
        public void OnBeforeSerialize();
        public void OnAfterDeserialize();
    }
}
```

- 机制：内部 `List<TKey>`/`List<TValue>` 存放序列化副本；回调中与字典同步；反序列化时跳过重复键并上报。
- 契约：需支持 `[Serializable]` 子类以便 Inspector 绘制；`OnAfterDeserialize` 不得抛（Inspector 数据可能脏）。

### 5.12 `SlotMap<T>` + `SlotHandle`（P2）

- 用途：安全引用/句柄（防悬垂），实体/组件句柄、订阅令牌。
- 缺口：无代际句柄容器。
- 立项前需具名消费方；否则仅登记。

```csharp
namespace Grow.Core.Collections {
    public readonly struct SlotHandle : IEquatable<SlotHandle> {
        public readonly int Index;
        public readonly int Generation;
    }

    public sealed class SlotMap<T> {
        public SlotHandle Add(T value);
        public bool TryGet(SlotHandle handle, out T value);
        public bool Contains(SlotHandle handle);
        public bool TryRemove(SlotHandle handle, out T value);
        public int Count { get; }
        public void Clear();
    }
}
```

- 机制：槽数组 + 空闲链表 + 每槽 gen 计数；`Add` 复用空闲槽并自增 gen，旧句柄 gen 不匹配即失效。
- 复杂度：全部 O(1)；稳态零分配。

### 5.13 `TypeMap<TValue>`（P2）

- 用途：类型→索引/值的 O(1) 注册（组件类型表、序列化类型的 handler 表）。
- 缺口：无。

```csharp
namespace Grow.Core.Collections {
    public sealed class TypeMap<TValue> {
        public int Count { get; }
        public bool TryGet<T>(out TValue value);
        public void Set<T>(TValue value);
        public bool Remove<T>();
        public bool Contains<T>();
        public void Clear();
    }
}
```

- 机制：`Dictionary<Type,int>` + 值数组；**不使用**静态 `TypeId<T>` 缓存，以规避 ADR-0002 复位义务；
  若后续确认需静态缓存，必须登记复位点。

### 5.14 `ObservableList<T>`（P2）

- 用途：UI 数据绑定中「成簇通知、零/低分配」的列表变更。
- 缺口：`ObservableCollection<T>` 每次变更构造 `NotifyCollectionChangedEventArgs`，且无批量操作单通知。

```csharp
namespace Grow.Core.Collections {
    public enum ListChangeKind { Add, Remove, Replace, Move, Reset }

    public readonly struct ListChangedEvent {
        public readonly ListChangeKind Kind;
        public readonly int Index;
        public readonly int Count;
    }

    public sealed class ObservableList<T> : IReadOnlyList<T> {
        public event Action<ListChangedEvent> Changed;
        public int Count { get; }
        public T this[int index] { get; }
        public void Add(T item);
        public bool Remove(T item);
        public void Replace(int index, T item);
        public void Move(int from, int to);
        public void AddRange(IReadOnlyList<T> items);   // 单次 Changed 通知
        public void Clear();
    }
}
```

- 立项前需具名消费方（UI 数据绑定方案未定则缓办）。

---

## 6. 跨容器契约

| 维度 | 契约 |
|---|---|
| 命名空间 | 集合 `Grow.Core.Collections`；泛型池 `Grow.Core.Pool`；序列化容器 `Grow.Core.Serialization` |
| 可见性 | 公开类型；实现细节走私有字段/嵌套 struct 枚举器；不暴露 `InvocationList` 式内部引擎 |
| 分配 | 稳态操作零分配；扩容摊还 O(1)；不缩容（换来零抖动），Trim/Clear 显式提供 |
| 枚举器 | 一律嵌套 `struct`（`Enumerator`/`KeyEnumerator`/`ValuesEnumerator` 等定义在各自容器内部），`MoveNext/Current` 不装箱；修改中遍历的失效行为在专题文档固定 |
| 顺序 | 每个容器必须书面写明顺序语义：插入序（Ordered*）、密集序（SparseSet）、优先级序 + 入队 tie-break（堆） |
| 线程 | 仅主线程、无锁 |
| 静态 | 默认零静态；确需静态缓存的，登记 `GrowBoot.OnReset` 复位点（ADR-0002） |
| 依赖 | 仅 BCL + `UnityEngine` 纯值；禁止依赖 Kernel/Services |
| 测试锚点 | 空/单元素、边界索引、去重、扩容临界、墓碑压缩阈值、枚举器失效、分配趋近 0（可过滤 Category） |

---

## 7. `InvocationList` 的重新定位：原语 + 策略

原先的目录遗漏了 `InvocationList`：把它当成「事件引擎」，未识别其容器本质。按 §2.1 纠正如下。

### 7.1 分解

`InvocationList` = **一个通用容器原语** + **一层事件策略**：

| 层 | 内容 | 归属 |
|---|---|---|
| 存储原语 | 插入序注册集 + 哈希索引 + 墓碑惰性压缩（I-1、I-2、I-6、I-7） | `Core/Collections` → `OrderedSet<T>`/`OrderedDictionary<K,V>` |
| 迭代协议 | 版本化快照（I-3、I-4、I-5）；建议独立为 `SnapshotSet<T>` 或先私有于 `InvocationList` | `Core/Collections` / `Core/Event`（待确认） |
| 策略 | `where TDelegate : Delegate` 身份去重、逐监听异常隔离、`Clear` 语义、派发边界释放 | `Core/Event`（保留） |

拿掉「委托/异常」策略，剩下的结构对任意 `T` 都成立 → 它是原语（§2.1 口诀）。

### 7.2 与 `dsn-0005-event-invocationlist-analysis.md` §9 的对齐与修订

§9 结论「等出现第二个真实消费方再抽」在当时成立；但**本目录规划的 `OrderedSet`/`OrderedDictionary`
本身就是第二个消费方**，触发条件已满足。故：

1. **修订**：不再「等」，立即将**存储机制**回填为 `OrderedSet<T>`/`OrderedDictionary<K,V>`，
   `InvocationList` 改为**组合**（G-5 硬约束）；**快照机制**按必要性验证 §5.6 独立为 `SnapshotSet<T>`
   或先私有。`dsn-0005-event-invocationlist-analysis.md` §9 的推迟结论由本文取代。
2. **保留**：§9 对「`InvocationList<T>` 不加约束」的否决依然有效——原语泛化、`T : Delegate`
   的强类型叶子仍留在 `Core/Event`，身份语义由 `IEqualityComparer<T>`（默认委托相等）承载。
3. **沿用**：LRU「低吻合、不复用墓碑」、否决 COW/ImmutableArray 两条判据不变（见 §5.4、§9）。

---

## 8. 边界外容器（归属其他 L1，不在 Core）

| 容器 | 归属 | 理由（架构 §8.7） |
|---|---|---|
| GameObject / Component 池 | `Services/Pooling` | 与预制体、场景生命周期绑定，属领域能力；泛型 `Pool<T>` 是其底层依赖 |
| 时间轮 / 延时队列 / 定时器堆封装 | `Kernel/Scheduling`（或 `Kernel/Time`） | 掌时；`MinHeap` 是其底层数据结构 |
| 空间哈希 / 均匀网格 / 四叉树 | `Services`（待立模块） | 领域能力（碰撞宽相/邻域查询），非纯容器 |
| 异步日志队列（数据结构） | `Core`（日志模块内） | 纯容器，但归 `Core/Log` 而非 `Collections` |
| FSM `Fsm<T>` | `Kernel/Fsm` | 与「运行」绑定的通用机制 |

---

## 9. 明确不做

- 并发/无锁容器（无跨线程消费方；将来的跨线程投递归 `Kernel/Scheduling`）。
- 通用树/图/跳表/B 树/LSM 等（YAGNI，无消费方）。
- Immutable / COW 集合（`InvocationList` §8 已否决）。
- LINQ/查询引擎替代。
- 自研 `ArrayPool<T>`（用 `System.Buffers.ArrayPool<T>`）。
- 重写 `UnityEngine.Pool.ObjectPool<T>` 的完整等价面（只补其容量/契约/统计缺口，不重复造轮子）。

---

## 10. 实施路线图（批次分组，非逐步计划）

> 每个批次另以 `writing-plans` 产出独立 TDD 计划；本文只定义批次与依赖。

**首发**：B1 的第一件为原语对 `OrderedSet<T>` + `SnapshotSet<T>`（§5.7）。理由：唯一具名消费方
`InvocationList` 已存在，两者必要性验证均已通过（`dsn-0003-container-necessity-review.md` §5/§6），
契约与测试锚点最完整，且是把「存储」与「迭代」两侧机制一次性收敛为唯一实现的第一道闸。其实现
计划需同时建立包内 EditMode 测试程序集（`Tests/Editor/` + `Packages/manifest.json` 的 `testables`）。

| 批次 | 内容 | 依赖 | 立项前置 |
|---|---|---|---|
| B1（P0） | **`OrderedSet<T>`/`OrderedDictionary<K,V>`（原语，机制回填）→ 重构 `InvocationList` 为组合**；`MinHeap` → `PriorityQueue`/`IndexedMinHeap`；`Deque`；`RingBuffer`；`Pool<T>` + `IPoolable`；`LruCache` | 无 | 各容器均有具名消费方；`InvocationList` 已是原语的具名消费方 |
| B2（P1） | `MultiValueDictionary`；`SparseSet`；`BitSet`/`EnumSet`；`SerializableDictionary` | B1 | 具名消费方 |
| B3（P2） | `SlotMap`；`TypeMap`；`ObservableList` | B1 | 具名消费方出现后再启动 |
| B4（边界） | `Services/Pooling`（GameObject 池，依赖 `Pool<T>`）；`Kernel/Scheduling`（时间轮，依赖 `PriorityQueue`） | B1/B2 | 对应 L1 模块立项 |

依赖图（简）：

```
OrderedSet/OrderedDictionary ──► (重构) InvocationList ──► GrowEvent 叶子
MinHeap ──► PriorityQueue
        └─► IndexedMinHeap
        └─► (Kernel/Scheduling 时间轮)
Pool<T> ─► Services/Pooling
```

---

## 11. 风险与开放问题

| 项 | 处理 |
|---|---|
| 提前泛化 / 造库无消费方 | G-3 门槛：每项必须具名消费方；P2 仅登记不实现；P0 逐项在 B1 计划中写明消费方 |
| 与 `UnityEngine.Pool` / BCL 重复 | §9 明确边界：只补缺口，不提供等价并行 API |
| 基线 API 误用 | 禁用 `PriorityQueue<T,P>`、`BitOperations`、泛型 `OrderedDictionary`；专题文档列「基线可用性」核对表 |
| 静态状态违反 ADR-0002 | 默认零静态；`EnumSet` 枚举上界缓存、`PoolRegistry` 等必须登记 `GrowBoot.OnReset` |
| GC 断言在编辑器抖动 | 同 `dsn-0004-event-primitives.md` §9：预热 + 宽松阈值 + 可过滤 Category |
| 枚举器失效语义不统一 | 每个容器专题文档固定「修改中遍历」的行为（抛 / 快照 / 未定义），并纳入测试锚点 |
| 专用容器继续增生 / 原语被绕过 | G-5：机制在框架内只允许一份实现；`InvocationList` 必须回填至 `OrderedSet`；评审以「是否复制了状态机」为硬性否决项（§2.1） |
| `dsn-0005-event-invocationlist-analysis.md` §9 与本文的取向 | 已同步修订该文 §9：机制回填原语、`InvocationList` 改组合；实现落地后记入 `CHANGELOG.md` |
| 是否公开 `Core/Collections` | 一旦公开即成 API 契约；B1 前确认公开面最小化与向后兼容策略 |

---

## 12. 结论

Unity 2021.3 基线在优先级队列、LRU、双端/覆盖缓冲、插入序有序字典、可变一对多、稀疏集、
字级位集、代际句柄、可控泛型池、可序列化字典上均存在确定性缺口。

统一原则是**原语优先**（§2.1）：专用容器不得自带一套机制，只能组合原语或在原语上叠领域策略。
据此，本目录的**第一原语对**是 `OrderedSet<T>`/`OrderedDictionary<K,V>` + `SnapshotSet<T>`
（存储：泛型、插入序、O(1) 增删、稳定枚举；迭代：枚举边界快照、变更下一次生效；必要性验证见
`dsn-0003-container-necessity-review.md` §5/§6）——`InvocationList` 的存储机制回填到前者、快照机制回填到
后者，降为「原语 + 事件策略」的薄组合。这正是对「InvocationList 未被当作容器」的纠正，也是防止
专用容器增生的硬约束。

B1（P0）第一批：**上述原语 + `InvocationList` 重构**、`MinHeap`/`PriorityQueue`/`IndexedMinHeap`、
`Deque`、`RingBuffer`、`Pool<T>`、`LruCache`；B2/B3 按具名消费方逐项立项；边界容器回归各自 L1。
所有容器服从 §6 契约与 §2 门槛；LRU 采用 `dsn-0005-event-invocationlist-analysis.md` §9 的纠正路线
（字典 + 侵入式链表），不复用墓碑方案。
