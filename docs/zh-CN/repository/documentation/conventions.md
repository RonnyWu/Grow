<!-- source: docs/en/repository/documentation/conventions.md @ blob:144b77c3328d812e0ff9da9a3716245ae7609fbe -->
<!-- 中文译文；以英文 docs/en/repository/documentation/conventions.md 为准，译文可能滞后于原文。blob 是英文文件的内容 sha，可用 git hash-object docs/en/repository/documentation/conventions.md 核验。 -->

# 文档约定

**其他语言：** [English](../../../en/repository/documentation/conventions.md) | [简体中文](conventions.md)

`docs/` 下每一页都要遵守的规则。为什么这样组织，见
[`documentation.md`](documentation.md)。

## 一页该放哪

| 主题 | 分区 | 满足这个条件就放这里 |
| --- | --- | --- |
| `framework/` | `architecture/` | 解释框架是什么、为什么这样设计 |
| `framework/` | `design/` | 记录一项已定稿的逐主题设计 |
| `framework/` | `reference/` | 是查的而不是读的——术语、表格、不变式 |
| `repository/` | `documentation/` | 解释或规定这套文档本身 |
| `repository/` | `guides/` | 告诉贡献者怎么完成一项任务 |

代码及其流程写进 `framework/` 或 `repository/`，**使用者**需要的一切写进 `Packages/com.ronny.grow/Documentation~/`。**不要**在 `docs/` 里讲怎么**使用**这个包。

一个主题一页；解释、操作、参考**不混写在同一页**。

## 单页骨架

每一页都是这个形状：

1. 唯一的 `#` 一级标题，点明本页主题。
2. 一句话说明本页讲什么。
3. 正文，从 `##` 开始；**不得跳级**。
4. 末尾需要时给 `## See also`，指向下一步。

## 文件命名

- 小写 `kebab-case` + `.md`，例如 `conventions.md`、`log-chunking.md`。
- 文件名在**同一语言树内唯一**。
- `README.md` 是**分区目录**的落地页，也是上面规则的唯一例外；`framework/` 与 `repository/`
  只是分组目录、不放页面，因此没有落地页。

## 语言

- **英文为正本**：每条正文位于 `docs/en/` 下的相对路径。
- **`zh-CN` 是镜像**：译文位于 `docs/zh-CN/` 下的**同一相对路径**。
- 译文开头带一条源标记，写明英文源与内容哈希：

  ```text
  <!-- source: docs/en/repository/documentation/conventions.md @ blob:<sha> -->
  ```

  其中 `<sha>` 是英文文件的 `git hash-object`。**改动英文正文时，必须在同一次提交里更新它。**
- 每页标题下方紧跟语言切换器，互相链接。
- **只翻散文**；代码、命令、链接、路径与标识符一律照抄。

## 版本

- 包文档跟随包版本：新增行为用 `Added in <version>` 标注，**不写** "currently / now / soon"。
- 仓库文档跟随 `main`，不复制到 release 分支。

## 书写

- 标题用 sentence case（只首词大写）。
- 链接文本要描述目标；**禁用** "click here" 与裸 URL。
- 用仓库相对路径链接，兼顾 GitHub 与包文档查看器。
- 同一概念只用同一个词，并在
  [`../../framework/reference/glossary.md`](../../framework/reference/glossary.md) 里定义。

## 维护

- 文档与它所描述的改动**同一次 Pull Request** 提交。
- 内容不再成立就删页；不留死文档。
- 已有内容**链接**过去，不要重写一遍。

## 自动检查

`Docs` workflow 在每个 Pull Request 上跑：

- `markdownlint-cli2`，配置见 `.markdownlint-cli2.jsonc`；
- `lychee`，查死链；
- `ci/docs/check-docs.mjs`，查结构性规则——两棵树互为镜像、同一语言树内文件名唯一、每篇译文的 `source` 标记存在**且 blob 哈希仍与英文一致**。

规范与结构两项，装着 Node 就能跑；链接检查需要 `lychee` 可执行文件：

```bash
npx markdownlint-cli2 "docs/**/*.md"
node ci/docs/check-docs.mjs
lychee --config .lychee.toml "docs/**/*.md"
```
