# Grow 架构依据与研究综述

> 状态：设计依据文档（非代码、非最终规范）。
> 目的：汇总软件架构、游戏引擎、主流公司、技术会议、各语言生态的演进脉络，
> 提炼当代共识，作为 Grow 一级目录（L1）与模块化规范的论证基础。
> 结论先行见 §8「推荐的 L1 结果」。

---

## 0. 为什么需要这份文档

Grow 的目标是「模块化、清晰、优雅」的 Unity 游戏框架。为避免仅凭个人直觉划分目录，
本文横向调研五条演进线，回答两个问题：

1. 行业在不同领域反复验证了哪些架构原则？
2. 这些原则如何映射到 Grow 的 L1 与模块内部结构？

调研范围：

- 软件架构大师的理论演进
- Unity / 游戏引擎领域的技术演进
- 主流工程组织的架构演进
- 技术会议的主题演进
- 各语言生态在「模块概念」上的演进

---

## 1. 软件架构大师的演进（1968 → 2021）

| 年代 | 人物 / 文献 | 关键转变 |
|---|---|---|
| 1972 | Parnas《On the Criteria To Be Used in Decomposing Systems into Modules》 | 从「按处理步骤」分解 → **按信息隐藏**分解（模块化原点） |
| 1988 | Johnson & Foote《Designing Reusable Classes》 | 提出 **Inversion of Control**；「framework = extensible skeleton」（框架的定义） |
| 2003 | Fowler《Patterns of Enterprise Application Architecture》 | Separated Interface / Plugin / Gateway / Service Layer——接口与实现分离 |
| 2005 | Cockburn《Hexagonal Architecture》 | 从「左右分层」→ **内外对称**；端口按用途、适配器按技术 |
| 2003–08 | Evans《Domain-Driven Design》 | 从技术分层 → **Bounded Context / Context Map**（领域边界 + 关系模式） |
| 2011 | Martin《Screaming Architecture》 | 架构应喊出**用例**，而非框架；「frameworks are tools, not ways of life」 |
| 2012 | Martin《The Clean Architecture》 | 同心圆 + **Dependency Rule**（依赖只能向内）；「内圈是策略，外圈是机制」 |
| 2012 | Bernhardt《Functional Core, Imperative Shell》 | 纯核心 + 命令式外壳 |
| 2014–15 | Lewis/Fowler 微服务 → Fowler《Monolith First》 | 微服务代价（Microservice Premium）→ **先单体、可模块化，再拆** |
| 2015 | Fowler《Presentation Domain Data Layering》 | **关键**：「层不应是顶层模块；顶层应是领域模块，内部再分层」 |
| 2018 | Bogard《Vertical Slice Architecture》 | 反全局分层：**按变化轴切片**，「slice 内高内聚、slice 间低耦合」 |
| 2018 | Ousterhout《A Philosophy of Software Design》 | **deep modules**、信息隐藏（Parnas 当代版） |
| 2019 | Skelton & Pais《Team Topologies》 | 架构=组织；**认知负荷**；X-as-a-Service；Thinnest Viable Platform |
| 2020 | Richards & Ford《Fundamentals of Software Architecture》 | 架构**风格目录**；**Microkernel** 正式名；architecture quantum |
| 2020+ | Drotbohm, Spring Modulith / jMolecules | 模块 = **provided interface + internal + required interface**；**架构可验证**（ArchUnit） |
| 2021 | Ford/Richards 等《Software Architecture: The Hard Parts》 | 静态/动态耦合分类、architecture quantum、分布式数据 |

**主线**：分层 → 端口/适配器 → 领域边界 → 按变化轴切片 → 模块内规范 + 关系模式 + 可验证性。

---

## 2. Unity / 游戏领域的演进

| 阶段 | 代表 | 转变 |
|---|---|---|
| 2014 | Nystrom《Game Programming Patterns》 | 解耦三件套：**Component / Event Queue / Service Locator** |
| 2015–17 | Ryan Hipple（Unite）：ScriptableObject 架构 | 用 **SO 作数据/事件通道**，解耦 MonoBehaviour |
| 2018+ | Mike Acton / Unity DOTS | **Data-Oriented Design → ECS**：Component=数据（名词）、System=行为（谓词）；Burst/Jobs |
| 2019+ | Unity 自身 | **UPM 包化 + asmdef 模块化**；Boss Room / Megacity / V Rising 等生产验证 |
| — | Jason Gregory《Game Engine Architecture》 | 引擎标准分层：Platform → Core Systems → Resources → Subsystems → Gameplay Foundation → Game-specific → Tools |

**主线**：对象/继承 → 组合 + SO 解耦 → **数据导向 ECS**。方向是「从对象到数据、从逐帧到批量」。

**对 Grow 的意义**：ECS / 数据导向属于**玩法域内部**的实现选择，不应上升为框架的 L1；
但「数据（名词）与行为（谓词）分离」这一思想可用于模块内部划分（`Contracts` vs `Systems`）。

---

## 3. 主流公司的演进

| 公司 | 演进 | 关键概念 |
|---|---|---|
| Google | monorepo + Bazel + 《Software Engineering at Google》(2020) | 依赖治理、可扩展构建、Live at Head、Hyrum's Law |
| Meta | monorepo + Buck | 大规模代码复用 |
| Amazon | two-pizza teams + API mandate + single-threaded ownership | 服务化与所有权 |
| Netflix | 微服务 + chaos engineering | 韧性与平台化 |
| Uber | 微服务(2012-13) → 2200 服务复杂度 → **DOMA(2020)** | domains / layer design / gateways / extensions |
| Spotify | squad model | 组织与架构对齐 |
| Epic (Unreal) | Modules + Game Features / Modular Game Features | 模块化插件、按需装配 |
| Apple / Java | SwiftPM / JPMS (Jigsaw) | 语言级模块与可见性 |

**Uber DOMA 四条原则**（与 Grow 最贴近，可直接借鉴）：

1. **Domain**：按「功能集合」组织，而非单个服务；
2. **Layer design**：谁能调用谁，按爆炸半径分层；
3. **Gateway**：每个域**单一入口**，屏蔽内部实现（对应 Services 契约）；
4. **Extensions**：逻辑扩展 / 数据扩展，不改核心即可扩展（对应插件/适配器）。

**主线**：单体 → 微服务 → **复杂度反噬** → 域导向 + 网关 + 扩展点 → **模块化单体回归**。

---

## 4. 技术会议的演进主题

- **QCon / GOTO / NDC / DDD Europe / Devoxx**：Simon Brown《Modular Monoliths》、
  Bogard《Vertical Slice》、DDD Context Mapping、Architecture as Code。
- **Unity Unite / GDC**：DOTS/ECS 实践、ScriptableObject 架构、模块化与 Addressables。
- **Strange Loop**：Bernhardt《Functional Core, Imperative Shell》。
- **.NET Conf / Google I/O / Build**：模块化、依赖治理、平台化。

**会议层面的共识**：从「画分层图」转向「讨论边界、耦合与可验证规则」。

---

## 5. 语言领域大佬在「模块概念」上的演进

| 语言 | 人物/文献 | 转变 |
|---|---|---|
| C#/.NET | Cwalina & Abrams《Framework Design Guidelines》 | 库/框架 API 设计规范：Do/Consider/Avoid、可扩展性、一致性 |
| Java | Rod Johnson(Spring) → Mark Reinhold (JPMS/Jigsaw) | 从 DI 容器 → **语言级模块系统**：`module-info`、exports/requires、强封装 |
| Go | Ben Johnson《Standard Package Layout》 | 反框架、**按依赖分组**：root=domain types，子包=适配器，main 负责装配 |
| Rust | Rust API Guidelines | 命名/灵活性/未来兼容/零成本抽象、crate 可见性 |
| Swift/C++/TS | SwiftPM / C++20 modules / Nx monorepo | 语言与工具层的模块化 + 依赖边界 |

**主线**：包/命名空间 → **语言级模块 + 明确导出/可见性 + 依赖方向约束** → **可验证架构**。

---

## 6. 五条线收敛出的当代共识

1. **层不是顶层，领域才是**；层在模块内部（Fowler 2015）。
2. **依赖单向向内**（Clean / Dependency Rule）。
3. **端口按用途，适配器按技术**（Cockburn）；契约与实现分离（Separated Interface / Plugin）。
4. **模块 = provided interface + internal + required interface**（Modulith），且**可验证**（ArchUnit 式结构测试）。
5. **内核 + 插件 = Microkernel**（Richards/Ford）；扩展点用接口/插件（Uber Extensions）。
6. **边界要匹配认知负荷与团队**（Team Topologies）；平台保持 thin（X-as-a-Service）。

---

## 7. 术语映射（大师术语 ↔ Grow）

| Grow 概念 | 对应术语 | 出处 |
|---|---|---|
| Core | Functional Core / Shared Kernel | Bernhardt / Evans |
| Kernel | IoC Container / Composition Root / extensible skeleton | Fowler / Johnson & Foote |
| Services | Service Layer / 应用边界 / ports by purpose | Fowler / Cockburn |
| Integrations | Driven (secondary) adapters / Frameworks & Drivers | Cockburn / Martin |
| Tooling | Driving (primary) adapters | Cockburn |
| 模块内 `Contracts` vs `Systems` | Provided interface vs internal implementation / Separated Interface | Spring Modulith / Fowler |
| 整体形态 | **Microkernel** | Richards & Ford |

**两句话可作总纲**：

> 内为策略（policy），外为机制（mechanism）；端口按用途（purpose），适配器按技术（technology）。
>
> 依赖永远向内（Dependency Rule）；外部的一切皆「细节」（details）。

---

## 8. 推荐的 L1 结果

### 8.1 前提：UPM 的 facet 轴与 concept 轴正交

UPM 天然切出 `Runtime/` `Editor/` `Tests/` `Documentation~/` `Samples~/`，这是**编译/分发面（facet）**，
**不是概念层，不进入命名空间**。L1 概念位于 facet 之下：

```
com.grow.<pkg>/
├─ Runtime/              ← facet（不进命名空间）
│   └─ <Concept>/        ← L1 概念
│       └─ <Module>/     ← L2 模块
│           └─ <Sub>/    ← L3 模块内目录（Contracts / Systems / Internal …）
├─ Editor/               ← facet（镜像概念轴，叶子以 .Editor 标记）
├─ Tests/                ← facet
└─ Samples~/
```

命名空间 = `Grow.<Concept>.<Module>[.<Sub>]`（facet 透明，root + ≤3 级）。

### 8.2 推荐：3 级脊柱 + 2 个横切分区

```
Core            }  脊柱：依赖阶梯，固定 3 级（policy，依赖向内）
Kernel          }
Services        }
Integrations    }  横切分区：适配器环，只进不出（mechanism / details）
Tooling         }
```

| L1 | 定义 | 判据 | 依赖 |
|---|---|---|---|
| **Core** | 纯原语：集合/扩展/流/日志门面/数学/诊断 | 无引擎、无第三方、无生命周期、被动 | 无 |
| **Kernel** | 原子机制：Module/Service/System 的定义、DI/容器、启动管线、PlayerLoop 调度 | 有生命周期、负责引导与中介 | `Core` |
| **Services** | 域服务与系统：事件/资源/存档/数据/本地化/音频/UI/补间/场景/输入 | 对外提供能力（端口）+ 内部运行系统 | `Core,Kernel` |
| **Integrations** | 三方与平台后端：Addressables / LitMotion / Newtonsoft … | Driven / secondary adapter，实现 Services 契约 | `Services` |
| **Tooling** | 开发与诊断：Dev 控制台 / Profiler / CodeGen（非出货） | Driving / primary adapter，只进不出 | `Services` |

**结构关系**：

- `Core → Kernel → Services` 是**依赖阶梯**（每级只能引用其下）。
- `Integrations`、`Tooling` 是**适配器环**，依赖 `Services`，无任何东西依赖它们。
- 适配器再按**谓词轴**（控制流方向）二分：`Integrations` = 被系统驱动，`Tooling` = 驱动系统。

### 8.3 模块内部规范（L2/L3）

采用 Spring Modulith 三件套：

```
Services/<Module>/
├─ Contracts/   对外 API / 端口（Separated Interface）   ← 名词
├─ Systems/     运行行为与状态（内部实现）                ← 谓词
└─ Internal/    纯内部辅助（不对外可见）
```

- 跨模块**只允许经 `Contracts` 交互**；模块对外视为**单一 Gateway**。
- 模块间解耦优先用**事件**，而非直接互调（Modulith / Uber DOMA）。
- 第 4 级目录**仅两种合法理由**：① 独立编译单元（`Backends/<Name>/`）；② `Internal`/`Editor` 可见性隔离。其余第 4 级 = 该模块应上提为 L2。

### 8.4 12 个方面 → L1 的映射

| # | 方面 | L1 归属 |
|---|---|---|
| ① | 基础库 | `Core` |
| ② | 启动 / 生命周期 / DI | `Kernel` |
| ③ | 时间 / 调度 / 异步 | `Kernel` |
| ④ | FSM / 流程编排 | `Kernel`（FSM）→ `Services`（SceneFlow） |
| ⑤ | 事件 / 消息 | `Kernel` |
| ⑥ | 资源 | `Services`（契约）+ `Integrations`（后端） |
| ⑦ | 数据 / 配置 / 存档 | `Services`（+ `Integrations` 序列化后端） |
| ⑧ | 输入 | `Services` |
| ⑨ | 表现层（UI/本地化/音频/补间） | `Services`（+ `Integrations` 补间后端） |
| ⑩ | 玩法支撑（对象池/场景/AI 接入） | `Services`（AI/物理仅给约定） |
| ⑪ | 编辑器 / 工具 | `Tooling` + Editor facet |
| ⑫ | 工程化（CI/构建/测试/文档） | 仓库根（非包内） |

### 8.5 命名空间与依赖规则（落地约束）

1. **facet 透明**：`Runtime`/`Tests` 不进命名空间；编辑器代码以 `.Editor` 作 L3 叶子
   （如 `Grow.Services.Assets.Editor`），不设全局 `Grow.Editor` 概念根。
2. **单向无环**：`Core → Kernel → Services → {Integrations, Tooling}`；`Integrations` 与 `Tooling` 互不依赖。
3. **门禁式第三方**：契约常驻、零第三方；后端以
   `Version Defines → Define Constraints` 门禁，装则编译、缺则零影响、运行时可切（Plugin）。
4. **命名纪律**：禁止 `Utils / Common / Misc / Helpers / Managers` 作为 L1；
   通用能力进 `Core/<域>`，模块私有辅助进 `<Module>/Internal`。
5. **可验证**：以结构测试（ArchUnit 式）校验模块 API/internal 边界与依赖方向，纳入 CI。
6. **组织形式**：模块边界按认知负荷切；框架作为 thin platform，以 X-as-a-Service 对外。

### 8.6 Core 与 Kernel 的本质区别

一句话：

> **Core 无「何时」，Kernel 定义「何时」。**
> Core 是「被调用的纯原语」，Kernel 是「负责调用的运行时」。

| 维度 | Core | Kernel |
|---|---|---|
| 角色 | 被调用者（callee） | 调用者 / 编排者（caller） |
| 时间 | 无时间概念 | 拥有时间（启动 / 每帧 / 调度 / 停止） |
| 控制流 | 纯 call/return，从不回调别人 | 掌 IoC、回调、广播、装配 |
| 运行知识 | 不知道「游戏正在运行」 | 定义「游戏如何运行」 |
| 生命周期 | 无 | Initialize / Shutdown / Tick |
| 依赖 | 无（至多 UnityEngine 纯值类型） | Core |
| 可删除性测试 | 删掉 Kernel 后仍可编译、单测、复用 | 删掉 Kernel 后一切不再开始运行 |

**判别口诀**：「它会不会在某个时机**主动叫别人**？」会 → Kernel；只会被叫 → Core。

**注意**：Core 并非「不能依赖 UnityEngine」；它不能依赖的是**生命周期与运行语义**
（MonoBehaviour、协程、帧、场景、服务容器）。对 `Vector3`/`Mathf` 这类纯值做运算是允许的。

### 8.7 归属判定阶梯（从高到低，先命中先归属）

1. 只服务于**开发 / 诊断**（非出货）？→ `Tooling`
2. 是某个能力的**三方 / 平台实现**（对接外部技术）？→ `Integrations`
3. 提供某个**领域能力**（资源 / 存档 / 数据 / 本地化 / 音频 / UI / 补间 / 场景 / 输入 / 池）？→ `Services`
4. 拥有或定义**运行时机**（生命周期 / 调度 / 引导 / 中介），或与「游戏运行」这一概念绑定？→ `Kernel`
5. 其余（纯值 / 纯算法 / 纯数据 / 无时间 / 无运行概念）→ `Core`

用该阶梯检验易混淆项：

| 候选 | 命中 | 归属 | 理由 |
|---|---|---|---|
| 泛型对象池 | 5 | `Core` | 纯数据结构，无生命周期 |
| GameObject 对象池 | 3 | `Services` | 与预制体/场景生命周期绑定的能力 |
| FSM `Fsm<T>` | 4 | `Kernel` | 与「运行」绑定的通用机制（非纯语言原语） |
| EventBus | 4 | `Kernel` | 广播/回调/主线程投递——掌控制流的中介机制 |
| FSM 状态数据（枚举/DTO） | 5 | `Core` | 若被独立沉淀，属纯数据 |
| 序列化容器 `SerializedDictionary` | 5 | `Core` | 纯容器 |
| 序列化后端（Newtonsoft） | 2 | `Integrations` | 契约的第三方实现 |
| 日志门面 `LogUtil` | 5 | `Core` | 被动门面，无时机 |
| 文件/远程日志 sink | 2 | `Integrations` | 对接外部技术（也可由宿主装配） |
| 守卫/断言 `Throw`/`Assert` | 5 | `Core` | 纯诊断原语 |
| Profiler 运行时/窗口 | 1 | `Tooling` | 只服务开发诊断 |
| DI 容器 / 启动管线 / PlayerLoop | 4 | `Kernel` | 定义并拥有运行时机 |

> `Events` 放 `Kernel` 而非 `Services`：它是运行时的**通信机制**而非领域能力，
> 且被所有 Services 消费；放 Kernel 可避免「基础能力反过来依赖领域模块」的环。

### 8.8 L1 / L2 / L3 推荐目录结构

```
<Package>/
├─ Runtime/                       ← facet（不进命名空间）
│   ├─ Core/                      ← 无时间、无运行
│   │   ├─ Compiler/
│   │   ├─ Extensions/
│   │   ├─ Collections/
│   │   ├─ Pool/                  ← 泛型池
│   │   ├─ Flow/
│   │   ├─ Log/                   ← 门面
│   │   ├─ Math/
│   │   ├─ Serialization/         ← 容器与契约
│   │   ├─ Reflection/
│   │   └─ Diagnostics/           ← 守卫/断言
│   ├─ Kernel/                    ← 掌时、掌权
│   │   ├─ Entry/                 ← GrowApp / GrowInitialize / MonoExecutor
│   │   ├─ Modules/               ← IModule / ModuleStarter
│   │   ├─ IoC/                   ← IServiceContainer
│   │   ├─ Lifecycle/
│   │   ├─ Loop/                  ← PlayerLoop
│   │   ├─ Time/                  ← GameTime / Timer
│   │   ├─ Scheduling/            ← Dispatcher / Tasks / Threading
│   │   ├─ Fsm/
│   │   └─ Events/                ← IEventBus
│   ├─ Services/                  ← 领域能力
│   │   ├─ Assets/
│   │   ├─ Save/
│   │   ├─ Data/
│   │   ├─ Localization/
│   │   ├─ Audio/
│   │   ├─ Ui/
│   │   ├─ Tween/
│   │   ├─ SceneFlow/
│   │   ├─ Input/
│   │   └─ Pooling/
│   ├─ Integrations/              ← driven adapters
│   │   ├─ Addressables/
│   │   ├─ LitMotion/
│   │   ├─ Dotween/
│   │   └─ Newtonsoft/
│   └─ Tooling/                   ← driving adapters（非出货）
│       ├─ Dev/
│       ├─ Profiling/
│       └─ CodeGen/
├─ Editor/                        ← facet（镜像概念轴，叶子 .Editor）
├─ Tests/                         ← facet
└─ Samples~/
```

**L3（模块内）统一三件套**：

```
<Module>/
├─ Contracts/   对外 API / 端口（跨模块只能经此）
├─ Systems/     行为与运行态
└─ Internal/    私有实现
```

**L1 / L2 汇总表**：

| L1 | L2 模块 |
|---|---|
| **Core** | Compiler · Extensions · Collections · Pool · Flow · Log · Math · Serialization · Reflection · Diagnostics |
| **Kernel** | Entry · Modules · IoC · Lifecycle · Loop · Time · Scheduling · Fsm · Events |
| **Services** | Assets · Save · Data · Localization · Audio · Ui · Tween · SceneFlow · Input · Pooling |
| **Integrations** | Addressables · LitMotion · Dotween · Newtonsoft |
| **Tooling** | Dev · Profiling · CodeGen |

**命名空间**：`Grow.<L1>.<L2>[.<L3>]`，编辑器叶子为 `.Editor`（如 `Grow.Services.Ui.Editor`）。

**已定**：

1. **包粒度**：暂**单包不动**（不按 L1 拆 UPM 包）；模块化的具体落地方式后议。
2. **门禁后端落点**：统一集中在 `Integrations/<Tech>`，不采用各 Service 的 `Backends/<Tech>` 子目录。
   此举是为了让 L1 的「适配器环」成立且**必要**——三方实现不再散落于各 Service 内部，从而支撑
   `Integrations` 作为独立 L1 的合理性与必要性。

**仍待定稿**：无。

### 8.9 Log / Diagnostics 的边界（按本质判定）

以 §8.6 的本质（**Core 无「何时」**）与 §8.7 阶梯逐组件裁定：

| 组件 | 本质 | 归属 |
|---|---|---|
| 日志门面 `LogUtil` / `Log`（等级、格式化、入口） | 被动、无时间 | `Core` |
| 日志契约 `ILogPrinter` / `ILogSink` / `ILogMessageDecoration` | 纯契约 | `Core` |
| 默认 sink（Unity `Debug`、Console） | 被动转发到平台 | `Core` |
| 编辑器 sink（点击跳源码） | 只服务开发 | Editor facet（`Tooling`） |
| 异步日志队列（数据结构） | 被动容器 | `Core` |
| 异步日志驱动 / 刷新（调度） | **掌时** | `Kernel` |
| 第三方日志后端（远程 / 遥测 SDK） | 三方实现 | `Integrations` |
| 守卫 / 断言 `Throw` / `Assert` / `Result<T>` | 纯诊断原语 | `Core` |
| 微基准 `PerfTimer`（被动计时） | 被调用的纯工具 | `Core` |
| 运行时监控 `Monitor`（逐帧采样） | 掌时 + 仅开发 | `Tooling` |

**两条裁定要点**：

1. **Unity 不是第三方**：默认 sink 使用 Unity / System 的被动 API，不属门禁后端，故留在 `Core`；
   只有对接**第三方包 / 远端服务**的 sink 才进 `Integrations`。
2. **「被动设施」与「主动驱动」分家**：日志/诊断中的纯数据、纯算法、契约、被动 sink → `Core`；
   只要某部分**自己拥有时机**（异步刷新、逐帧采样），该部分即离开 `Core`，进入 `Kernel`（掌时机制）
   或 `Tooling`（掌时且仅开发）。这正是「Core 无何时」的直接推论。

> 由于 `Log` / `Diagnostics` 既非 Tooling、非 Integrations、非 Services、亦无时机（不属 Kernel），
> 按阶梯**只能落 `Core`**——它是「无时间的横切设施」，而非「领域能力」。

---

## 9. 结论

Grow 采用 **Microkernel（内核 + 适配器）** 形态：

```
Core → Kernel → Services        （内核/脊柱，策略，依赖向内）
                ├─ Integrations （driven adapters，对接外部技术）
                └─ Tooling      （driving adapters，对接开发者/测试）
```

该结论同时满足：分层依赖可强制、模块边界可验证、第三方可插拔、玩法层与框架层解耦，
并与 Cockburn（Ports & Adapters）、Martin（Dependency Rule）、Fowler（Service Layer/Separated Interface/Plugin）、
Drotbohm（Modulith）、Richards & Ford（Microkernel）、Uber（DOMA）等当代权威一致。

---

## 10. 参考资料

本次调研实抓来源：

- Fowler《Monolith First》— https://martinfowler.com/bliki/MonolithFirst.html
- Fowler《Presentation Domain Data Layering》— https://martinfowler.com/bliki/PresentationDomainDataLayering.html
- Fowler《Inversion of Control》— https://martinfowler.com/bliki/InversionOfControl.html
- Fowler《Service Layer / Separated Interface / Plugin》— https://martinfowler.com/eaaCatalog/
- Cockburn《Hexagonal Architecture》— https://alistair.cockburn.us/hexagonal-architecture/
- Martin《The Clean Architecture》— https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html
- Martin《Screaming Architecture》— https://blog.cleancoder.com/uncle-bob/2011/09/30/Screaming-Architecture.html
- Spring Modulith《Fundamentals》— https://docs.spring.io/spring-modulith/reference/fundamentals.html
- Team Topologies《Key Concepts》— https://teamtopologies.com/key-concepts
- Bogard《Vertical Slice Architecture》— https://www.jimmybogard.com/vertical-slice-architecture/
- Nystrom《Game Programming Patterns · Decoupling Patterns》— https://gameprogrammingpatterns.com/decoupling-patterns.html
- Uber《Domain-Oriented Microservice Architecture》— https://www.uber.com/blog/microservice-architecture/
- Microsoft《Common web application architectures》— https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures
- Microsoft《Framework Design Guidelines》— https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/
- Go, Ben Johnson《Standard Package Layout》— https://www.gobeyond.dev/standard-package-layout/
- OpenJDK《Project Jigsaw》— https://openjdk.org/projects/jigsaw/
- Unity《DOTS》— https://unity.com/dots
- Unity《Entities overview》— https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/index.html
- Google《Software Engineering at Google》— https://abseil.io/resources/swe-book
- Rust《API Guidelines》— https://rust-lang.github.io/api-guidelines/

未抓取成功、可后续补充：

- Herberto Graça《Explicit Architecture》（403）
- jMolecules 官网（传输错误）
- Unreal《Game Features and Modular Game Features》（内容为空）
- InfoQ 相关文章（405）
- Wikipedia《Game engine》《Entity component system》（传输错误）
