# Grow 文档

本目录是 Grow 的文档正文。这份 README 同时承担两个职责：**文档地图**（哪里有什么）与**组织规范**（新文档该放哪、叫什么、算不算权威）。

## 1. 仓库三层

| 区域 | 职责 | 版本控制 |
|---|---|---|
| 根目录 | 门面与契约入口：`README.md` `LICENSE` `CONTRIBUTING.md` `SECURITY.md`(可选) `AGENTS.md` | 跟踪 |
| `docs/` | 文档正文（双轨，见 §3） | 跟踪 |
| `.github/` | GitHub 平台元数据：workflows、issue/PR 模板、dependabot | 跟踪 |
| `.drafts/` | 过程草稿：plans、specs、探索记录 | **不跟踪** |

## 2. 跟踪边界

判定一句话：**承载已冻结的结论、契约或对外承诺的，入库；仅是过程记录的，留在 `.drafts/`。**

| 类别 | 去向 | 保留义务 |
|---|---|---|
| 决策记录（ADR） | `docs/architecture/` | 永久，不可改写 |
| 实现设计与规划 | `docs/design/` | 随实现状态更新，可被取代 |
| 面向读者的文档 | 四象限（§3） | 随产品演进 |
| 实施计划、设计稿、探索笔记 | `.drafts/` | 无，可随时删除 |

`.drafts/` 在 `.gitignore` 中整目录忽略，**换机不保留**，重要结论必须在定稿时上提到 `docs/`。

## 3. 双轨

`docs/` 下并存两条轨，互不串味：

| 轨 | 目录 | 面向谁 | 回答什么 |
|---|---|---|---|
| **产品文档**（Diátaxis） | `tutorials/` `how-to/` `reference/` `explanation/` | 使用 Grow 的人（含贡献者） | 怎么学、怎么用、是什么、为什么 |
| **工程记录** | `architecture/`（ADR）、`design/`（设计） | 维护者 | 当初为什么这样定、打算怎么做 |

四象限的边界：

- **Tutorials** — 教一个我完全不会的人（学习导向）。
- **How-to** — 帮我解决一个已经知道要做什么的具体问题（任务导向）。
- **Reference** — 我工作时需要查的确定事实（信息导向）。
- **Explanation** — 帮我理解背景与取舍（理解导向）。

**工程记录永不放进四象限**。ADR 与设计文档是决策档案，不是教学或速查材料。

## 4. 命名约定

| 对象 | 规则 | 示例 |
|---|---|---|
| 四象限文档 | 小写 kebab-case；how-to 用动词开头 | `how-to/add-a-collection.md` |
| ADR | `ADR-NNNN-topic.md`，四位补零 | `ADR-0003-diataxis-documentation-layout.md` |
| 设计文档 | 小写 kebab-case | `design/container-catalog.md` |
| 草稿 | `YYYY-MM-DD-topic.md` | `.drafts/specs/2026-09-23-doc-layout-design.md` |
| 目录 | 小写，多词用连字符 | `docs/explanation/` |

**去前缀**：`docs/` 内的文件不带 `grow-` 前缀——目录本身已表明是 Grow 仓库。

## 5. 生命周期

```
草稿(.drafts/)  ──定稿──▶  工程设计(docs/design/)  ──决策冻结──▶  ADR(docs/architecture/)
      │                          │
      └──废弃──▶ 删除             └──转为面向读者──▶  四象限
```

- `docs/design/` 的每篇文档在开头标注状态（中文）：`草稿` / `已实现` / `已废弃`。
- ADR 一经 `Accepted` **不可改写**；被取代时新增一条，并在旧条目标注 `superseded by ADR-NNNN`。
- 四象限无状态头；内容过时即直接修改。

## 6. 新文档该放哪

按顺序问：

1. 它记录的是**一个已经做出的决策**吗？→ `docs/architecture/ADR-NNNN-topic.md`
2. 它是**某个模块的实现设计或规划**吗？→ `docs/design/`
3. 它是**给读者的文档**吗？→ 按 §3 选象限
4. 以上都不是（过程草稿、计划、spec）？→ `.drafts/`

## 7. 地图

| 路径 | 内容 |
|---|---|
| `docs/architecture/` | ADR 决策记录，见其 `README.md` |
| `docs/design/` | `container-catalog.md`（容器需求目录）、`container-necessity-review.md`（必要性验证与登记表）、`event-primitives.md`（事件原语设计）、`event-invocationlist-analysis.md`（派发内核机制分析与纠正） |
| `docs/explanation/` | `architecture-rationale.md`（架构总理由） |
| `docs/tutorials/` `docs/how-to/` `docs/reference/` | 暂无内容，见各自 README |
