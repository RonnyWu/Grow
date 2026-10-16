<!-- source: docs/en/repository/documentation/conventions.md @ blob:f89915b85e78a90ccbd6a31cd8bc2777674cd156 -->
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

分区落地页同样是这个形状，另外还要：

1. 开头标明本分区的内容类型——`*解释*——…`、`*参考*——…`、`*操作*——…` 或 `*设计记录*——…`；
2. 列出「这里放 / 这里不放」，并指向邻近分区，把边界写清楚。

## 目录与文件命名

目录分三类，只有后两类放页面：

| 目录 | 放什么 | 落地页 |
| --- | --- | --- |
| `docs/`，以及各语言根（`en/`、`zh-CN/`） | 该语言的主题目录 | `README.md` |
| 主题目录（`framework/`、`repository/`） | 分区目录 | 无 |
| 分区目录（`architecture/`、`design/`、`reference/`、`documentation/`、`guides/`） | 页面 | `README.md` |

- 小写 `kebab-case` + `.md`，例如 `conventions.md`、`log-chunking.md`。
- 文件名在**同一语言树内唯一**；`README.md` 是唯一例外——每个落地页各自独占一个目录。
- `docs/README.md` 是这套文档的根页：它在语言树之外，因此**不带** `source` 标记，也不参与镜像。

## 语言

英文为正本。有两类页面需要镜像，各自的路径规则不同：

- `docs/en/` 下的页面，镜像到 `docs/zh-CN/` 下的**同一相对路径**；
- 仓库根目录下的页面，镜像到 `i18n/<name>.<locale>.md`——即 `README.md` → `i18n/README.zh-CN.md`。

无论走哪条规则，译文开头都带一条源标记，写明英文源与内容哈希：

```text
<!-- source: docs/en/repository/documentation/conventions.md @ blob:<sha> -->
```

其中 `<sha>` 是英文文件的 `git hash-object`。**改动英文正文时，必须在同一次提交里更新它**，否则结构校验会失败。

- 每页标题下方紧跟语言切换器，互相链接。
- **只翻散文**；代码、命令、链接、路径与标识符一律照抄。

## 版本

- 包文档跟随包版本：新增行为用 `Added in <version>` 标注，**不写** "currently / now / soon"。
- 仓库文档跟随 `main`，不复制到 release 分支。

## 书写

- 标题用 sentence case（只首词大写）。
- 链接文本要描述目标；**禁用** "click here" 与裸 URL。
- 用仓库相对路径链接，兼顾 GitHub 与包文档查看器。
- 链接指向**页面**，不要指向它的某个标题：译文会重命名标题，锚点会失效。
- 同一概念只用同一个词，并在
  [`../../framework/reference/glossary.md`](../../framework/reference/glossary.md) 里定义。

## 维护

- 文档与它所描述的改动**同一次 Pull Request** 提交。
- 内容不再成立就删页；不留死文档。
- 已有内容**链接**过去，不要重写一遍。

## 自动检查

`Docs` workflow 在每个 Pull Request 上跑，覆盖 `docs/**`、`i18n/**`、`Packages/**/Documentation~/**`、`Packages/**/CHANGELOG.md` 与仓库根 `README.md`：

- `markdownlint-cli2`，配置见 `.markdownlint-cli2.jsonc`；
- `ci/docs/check-docs.mjs`，查结构性规则——两棵树互为镜像、同一语言树内文件名唯一、每篇译文的 `source` 标记存在**且 blob 哈希仍与英文一致**；
- `lychee`，查死链。

前两项，连同链接检查里**仓库内相对链接**那一半，都是确定性的，合并为同一个 job——也就是 `main` 要求通过的那个 job。外链检查需要网络，因此单独成一个 advisory job。

确定性那一半可以本地跑：`markdownlint-cli2` 与 `check-docs.mjs` 只需要 Node，`lychee` 需要它的可执行文件：

```bash
npx markdownlint-cli2 "docs/**/*.md" "i18n/**/*.md" "Packages/**/Documentation~/**/*.md" "Packages/**/CHANGELOG.md" "README.md"
node ci/docs/check-docs.mjs
lychee --config .lychee.toml --offline './docs/**/*.md' './i18n/**/*.md' './Packages/**/Documentation~/**/*.md' './Packages/**/CHANGELOG.md' './README.md'
```
