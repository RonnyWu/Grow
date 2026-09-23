# 文档分布设计

- 状态：已实现
- 日期：2026-09-24
- 关联：`docs/README.md`（导航入口）、`docs/architecture/adr-0003-diataxis-documentation-layout.md`、`CONTRIBUTING.md`、`AGENTS.md`

## 1. 目标

- **权威文档随克隆可得**：架构、设计、决策必须进入版本控制。
- **过程草稿不污染历史**：计划、spec 留在本地。
- **结构对人与 AI 都直观**：贡献者不必先读规范即可猜到落点。
- **规则成本低**：单人维护为主，不引入机器强制。

## 2. 分布总览

| 区域 | 职责 | 版本控制 |
|---|---|---|
| 仓库根 | 门面与契约入口（清单见 §5） | 跟踪 |
| `Packages/com.ronny.grow/` | 框架包本体；包内 `README.md`、`CHANGELOG.md`、`LICENSE.md`、`Third Party Notices.md` 遵循 UPM 惯例，**不受本文命名规则约束** | 跟踪 |
| `docs/` | 文档正文，两条轨（见 §3） | 跟踪 |
| `.github/` | 平台元数据：workflows、PR/issue 模板、dependabot | 跟踪 |
| `.drafts/` | 过程草稿：计划、spec | **不跟踪** |

`Assets/`（仅 `.gitkeep`）、`ci/` 等非文档目录不在本文范围内。

根 `README.md` 的仓库布局表与 `CONTRIBUTING.md` 的要点是本文的只读摘要；规则冲突时以本文为准。

## 3. `docs/` 的两条轨

| 轨 | 目录 | 面向谁 | 回答什么 |
|---|---|---|---|
| 产品文档（Diátaxis） | `tutorials/` `how-to/` `reference/` `explanation/` | 使用 Grow 的人（含贡献者） | 怎么学、怎么用、是什么、为什么 |
| 工程记录 | `architecture/`（ADR）、`design/`（设计） | 维护者 | 当初为什么这样定、打算怎么做 |

四象限的边界：

- **Tutorials** —— 教一个完全不会的人（学习导向）。
- **How-to** —— 解决一个已知目标的具体问题（任务导向）。
- **Reference** —— 工作时需要查的确定事实（信息导向）。
- **Explanation** —— 理解背景与取舍（理解导向）。

**两条轨并列、不混放**：工程记录（ADR、设计）是决策档案，不作为教学或速查材料出现在四象限。

## 4. 命名与编号规则

正文文件名统一为 `<前缀>-<nnnn>-<topic>.md`。

### 4.1 前缀

每个文档类型使用固定的类型缩写前缀（全小写），与目录一一对应：

| 目录 | 前缀 | 示例 |
|---|---|---|
| `architecture/` | `adr-` | `adr-0002-domain-reload-disabled.md` |
| `design/` | `dsn-` | `dsn-0001-doc-layout.md` |
| `explanation/` | `exp-` | `exp-0001-architecture-rationale.md` |
| `reference/` | `ref-` | `ref-0001-naming-conventions.md` |
| `how-to/` | `how-` | `how-0001-add-a-collection.md` |
| `tutorials/` | `tut-` | `tut-0001-get-started.md` |

### 4.2 编号

- 四位补零，各类型内独立。
- `0000` 在**所有类型**中都保留给该类型的模板文件（如 `adr-0000-template.md`）；正文从 `0001` 起。
- 一经分配即固定——**不复用、不重排**；ADR 采用创建顺序，其余类型由维护者按需分配。
- **历史例外**：ADR 的 `0001` 从未创建且不回填，故 ADR 正文从 `0002` 起（见 `docs/architecture/index.md`）。

### 4.3 例外

- `index.md`（目录落地页）与 `README.md`（仓库/包入口）**不加前缀与编号**。
- `Packages/com.ronny.grow/` 内的文档遵循 UPM 惯例，不受本节约束。

### 4.4 其他约束

- `docs/` 内的文件一律不带 `grow-` 项目前缀——目录本身已表明是 Grow 仓库。
- `topic` 用小写 kebab-case；`how-to/` 用动词开头。
- 目录名一律小写，多词用连字符（`how-to/`、`explanation/`）。

## 5. 文件清单（穷举）

| 路径 | 名称 | 反例 | 职责 |
|---|---|---|---|
| `/` | `README.md` | `README`（缺扩展名） | 开源库门面：是什么、安装、快速开始 |
| `/` | `LICENSE` | `LICENSE.md`（根目录多加扩展名） | 开源协议（MIT）全文 |
| `/` | `CONTRIBUTING.md` | `CONTRIBUTE.md` | 如何贡献代码与文档 |
| `/` | `AGENTS.md` | `AGENT.md` | AI 协作者规则 |
| `/` | `SECURITY.md`（可选） | `SECURITY` | 漏洞报告方式；公开发布后添加 |
| `/` | `.gitattributes` | — | 行尾与 diff 属性 |
| `/` | `.gitignore` | — | 忽略规则 |
| `/` | `GrowFramework.sln.DotSettings` | `*.DotSettings.user`（个人设置不入库） | 共享 Rider 设置 |
| `/.github/` | `workflows/<name>.yml` | `ci.yaml`（扩展名混用）、`CI.yml`（大写） | GitHub Actions 工作流 |
| `/.github/` | `pull_request_template.md` | `PULL_REQUEST_TEMPLATE.md`（大写）、`pr_template.md` | PR 默认模板 |
| `/.github/` | `ISSUE_TEMPLATE/config.yml` | `config.yaml` | issue 选择器配置 |
| `/.github/` | `ISSUE_TEMPLATE/bug_report.yml` | `bug-report.md` | bug 报告表单 |
| `/.github/` | `dependabot.yml` | `dependabot.yaml` | 依赖更新（仅 `github-actions`） |
| `/.drafts/` | `plans/YYYY-MM-DD-topic.md` | 无日期前缀 | 实施计划（不跟踪） |
| `/.drafts/` | `specs/YYYY-MM-DD-topic.md` | 无日期前缀 | 设计稿（不跟踪） |
| `/Packages/com.ronny.grow/` | `README.md`、`CHANGELOG.md`、`LICENSE.md`、`Third Party Notices.md` | 套用 `docs/` 命名 | 包内元数据（UPM 惯例，不受本文约束） |
| `/docs/` | `README.md` | `index.md`（与子目录混用） | 文档总入口与地图 |
| `/docs/architecture/` | `index.md`、`adr-0000-template.md`、`adr-nnnn-topic.md` | `README.md`；`ADR-0002-…`（大写）、`0002-…`（缺前缀） | ADR 索引与规则 + 模板（`0000` 专用）+ 正文 |
| `/docs/design/` | `index.md`、`dsn-nnnn-topic.md` | `README.md`；`documentation-layout.md`（缺编号） | 设计索引 + 工程设计与规范正文 |
| `/docs/explanation/` | `index.md`、`exp-nnnn-topic.md` | `README.md`；`explanation-…` | 象限落地页 + 理解导向正文 |
| `/docs/reference/` | `index.md`、`ref-nnnn-topic.md` | `README.md`；`reference-…` | 象限落地页 + 信息导向正文 |
| `/docs/how-to/` | `index.md`、`how-nnnn-topic.md` | `README.md`；`how-to-…`（前缀多一横） | 象限落地页 + 任务导向正文 |
| `/docs/tutorials/` | `index.md`、`tut-nnnn-topic.md` | `README.md`；`tutorial-…`（缺前缀） | 象限落地页 + 学习导向正文 |

## 6. 生命周期与状态

```
草稿(.drafts/) ──定稿──▶ 文档正文(docs/)
   │                        ├─ 工程记录轨：design/
   │                        └─ 面向读者轨：tutorials/ how-to/ reference/ explanation/
   └──废弃──▶ 删除

重大且难回滚的选型 ──冻结──▶ ADR(docs/architecture/)   ← 与文档正文并列，可由 design 引用
```

- `docs/design/` 的每篇文档在开头标注状态：`草稿` / `已实现` / `已废弃`。
- 四象限无状态头；内容过时即直接修改。
- ADR 的状态词与"一经 `Accepted` 不可改写"规则以 `docs/architecture/index.md` 为准，本文不重复。
- 跟踪判定一句话：**承载已冻结结论、契约或对外承诺的，入库；仅是过程记录的，留在 `.drafts/`。**

## 7. 与 ADR 的分工

| | ADR | 本设计文档 |
|---|---|---|
| 记录什么 | 一个**已冻结**的选型决策及其备选 | 可演进的**布局与规则** |
| 可变性 | 不可改写，只能被取代 | 随需更新 |
| 触发时机 | 难以回滚、跨模块、易起争论 | 结构或约定变化时 |

简言之：ADR 回答"为什么当初这样定"，本文回答"现在应该怎么放"。本设计文档不重复记录 ADR 的决策理由与状态规则，只引用。
