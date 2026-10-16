<!-- source: docs/en/guides/documentation.md @ blob:46e098a0ae4756d2e2b349ffe2e1274d892beb30 -->
<!-- 中文译文；以英文 docs/en/guides/documentation.md 为准，译文可能滞后于原文。blob 是英文文件的内容 sha，可用 git hash-object docs/en/guides/documentation.md 核验。 -->

# 文档约定

**其他语言：** [English](../../en/guides/documentation.md) | [简体中文](documentation.md)

本仓文档如何组织、书写与校验。

## 两层

| 层 | 位置 | 受众 | 跟随 |
| --- | --- | --- | --- |
| 包文档 | `Packages/com.ronny.grow/Documentation~/` | 使用包的人 | 包版本 |
| 仓库文档 | `docs/` | 构建框架的人 | `main` |

「怎么**用**这个包」写进包文档；「怎么**造**它」「**为什么**是这样」写进仓库文档。

## 分区

仓库文档按 [Diátaxis](https://diataxis.fr/) 的四种需求分区：

| 目录 | 需求 | 回答的问题 |
| --- | --- | --- |
| `architecture/` | 解释 | 它是什么？为什么这样设计？ |
| `design/` | 设计记录 | 我们决定造什么？权衡了什么？ |
| `guides/` | 操作 | 我要做 X，怎么做？ |
| `reference/` | 参考 | X 到底是什么？ |

一个主题一页；解释、操作、参考**不混写在同一页**。

决策记录**不在本仓公开**，另行记录。

## 单页骨架

每一页都是这个形状：

1. 唯一的 `#` 一级标题，点明本页主题。
2. 一句话说明本页讲什么。
3. 正文，从 `##` 开始；**不得跳级**。
4. 末尾需要时给 `## See also`，指向下一步。

## 文件命名

- 小写 `kebab-case` + `.md`，例如 `documentation.md`、`log-chunking.md`。
- 文件名在**同一语言树内唯一**。
- `README.md` 是目录的落地页，是上面规则的**唯一例外**。

## 语言

- **英文为正本**：每条正文位于 `docs/en/` 下的相对路径。
- **`zh-CN` 是镜像**：译文位于 `docs/zh-CN/` 下的**同一相对路径**。
- 译文开头带一条源标记，写明英文源与内容哈希：

  ```text
  <!-- source: docs/en/guides/documentation.md @ blob:<sha> -->
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
- 同一概念只用同一个词，并在 [`../reference/glossary.md`](../reference/glossary.md) 里定义。

## 维护

- 文档与它所描述的改动**同一次 Pull Request** 提交。
- 内容不再成立就删页；不留死文档。
- 已有内容**链接**过去，不要重写一遍。

## 自动检查

`Docs` workflow 在每个 Pull Request 上跑：

- `markdownlint-cli2`，配置见 `.markdownlint-cli2.jsonc`；
- `lychee`，查死链；
- `ci/docs/check-docs.mjs`，查结构性规则——两棵树互为镜像、同一语言树内文件名唯一、每篇译文都有 `source` 标记。

本地运行：

```bash
npx markdownlint-cli2 "docs/**/*.md"
node ci/docs/check-docs.mjs
```
