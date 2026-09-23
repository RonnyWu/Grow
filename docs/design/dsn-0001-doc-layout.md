# 文档分布设计

- 状态：已实现
- 日期：2026-09-24
- 关联：`docs/README.md`（导航入口）、`docs/architecture/adr-0003-diataxis-documentation-layout.md`、`CONTRIBUTING.md`、`AGENTS.md`

## 1. 目标

- **权威文档随克隆可得**：架构、设计、决策必须进入版本控制。
- **过程草稿不污染历史**：计划、spec、探索笔记留在本地。
- **结构对人与 AI 都直观**：贡献者不必先读规范即可猜到落点。
- **规则成本低**：单人维护为主，不引入机器强制。

## 2. 总体分布

| 区域 | 职责 | 版本控制 |
|---|---|---|
| 仓库根 | 门面与契约入口 | 跟踪 |
| `docs/` | 文档正文，两条轨（见 §3） | 跟踪 |
| `.github/` | 平台元数据：workflows、PR/issue 模板、dependabot | 跟踪 |
| `.drafts/` | 过程草稿：计划、spec、探索笔记 | **不跟踪** |

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

**工程记录永不放入四象限**：ADR 与设计文档是决策档案，不是教学或速查材料。

## 4. 文件清单（穷举）

正文命名统一为 `<前缀>-<nnnn>-<topic>.md`：前缀按类型固定，编号四位补零、各类型独立、从 `0001` 起、分配后不复用；`index.md` 与 `README.md` 为固定例外。

| 路径 | 名称 | 反例 | 职责 |
|---|---|---|---|
| `/` | `README.md` | `README`（漏扩展名） | 开源库门面：是什么、安装、快速开始 |
| `/` | `LICENSE` | `LICENSE.md`（多加扩展名） | 开源协议（MIT）全文 |
| `/` | `CONTRIBUTING.md` | `CONTRIBUTE.md` | 如何贡献代码与文档 |
| `/` | `AGENTS.md` | `AGENT.md` | AI 协作者规则 |
| `/` | `SECURITY.md`（可选） | `SECURITY.txt` | 漏洞报告方式；公开发布后添加 |
| `/` | `.gitignore` | `.gitignore.txt` | 忽略规则 |
| `/.github/` | `workflows/<name>.yml` | `ci.yaml`（扩展名混用）、`CI.yml`（大写） | GitHub Actions 工作流 |
| `/.github/` | `pull_request_template.md` | `PULL_REQUEST_TEMPLATE.md`（大写）、`pr_template.md` | PR 默认模板 |
| `/.github/` | `ISSUE_TEMPLATE/config.yml` | `config.yaml` | issue 选择器配置 |
| `/.github/` | `ISSUE_TEMPLATE/bug_report.yml` | `bug-report.md` | bug 报告表单 |
| `/.github/` | `dependabot.yml` | `dependabot.yaml` | 依赖更新（仅 `github-actions`） |
| `/.drafts/` | `plans/YYYY-MM-DD-topic.md` | 无日期前缀 | 实施计划（不跟踪） |
| `/.drafts/` | `specs/YYYY-MM-DD-topic.md` | 无日期前缀 | 设计稿（不跟踪） |
| `/docs/` | `README.md` | `index.md`（与子目录混用） | 文档总入口与地图 |
| `/docs/architecture/` | `index.md`、`adr-0000-template.md`、`adr-nnnn-topic.md` | `README.md`；`template.md`；`ADR-0002-…`（大写）、`0002-…`（缺前缀） | ADR 索引与规则 + 模板（`0000` 专用于模板）+ 正文 |
| `/docs/design/` | `index.md`、`dsn-nnnn-topic.md` | `README.md`；`design-…`、`documentation-layout.md`（缺编号） | 设计索引 + 工程设计与规范正文 |
| `/docs/explanation/` | `index.md`、`exp-nnnn-topic.md` | `README.md`、`explanation-…` | 象限落地页 + 理解导向正文 |
| `/docs/reference/` | `index.md`、`ref-nnnn-topic.md` | `README.md`、`reference-…` | 象限落地页 + 信息导向正文 |
| `/docs/how-to/` | `index.md`、`how-nnnn-topic.md` | `README.md`、`howto-…`、`how-to-…` | 象限落地页 + 任务导向正文 |
| `/docs/tutorials/` | `index.md`、`tut-nnnn-topic.md` | `README.md`、`tutorial-…`（缺前缀） | 象限落地页 + 学习导向正文 |

**两条额外约束**：

- `docs/` 内的文件一律不带 `grow-` 项目前缀——目录本身已表明是 Grow 仓库。
- 目录名一律小写，多词用连字符（`how-to/`、`explanation/`）。

## 5. 生命周期

```
草稿(.drafts/)  ──定稿──▶  工程设计(docs/design/)  ──决策冻结──▶  ADR(docs/architecture/)
      │                          │
      └──废弃──▶ 删除             └──转为面向读者──▶  四象限
```

- `docs/design/` 的每篇文档在开头标注状态：`草稿` / `已实现` / `已废弃`。
- ADR 一经 `Accepted` 不可改写；被取代时新增一条，并在旧条目标注 `Superseded by ADR-NNNN`。
- 四象限无状态头；内容过时即直接修改。
- 跟踪判定一句话：**承载已冻结结论、契约或对外承诺的，入库；仅是过程记录的，留在 `.drafts/`。**

## 6. 与 ADR 的分工

| | ADR | 本设计文档 |
|---|---|---|
| 记录什么 | 一个**已冻结**的选型决策及其备选 | 可演进的**布局与规则** |
| 可变性 | 不可改写，只能被取代 | 随需更新 |
| 触发时机 | 难以回滚、跨模块、易起争论 | 结构或约定变化时 |

简言之：ADR 回答"为什么当初这样定"，本文回答"现在应该怎么放"。本设计文档不重复记录 ADR 的决策理由，只引用。
