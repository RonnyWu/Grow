<!-- source: docs/en/repository/documentation/documentation.md @ blob:acd6d9e0d28fec7c4cd519874403298235b9e930 -->
<!-- 中文译文；以英文 docs/en/repository/documentation/documentation.md 为准，译文可能滞后于原文。blob 是英文文件的内容 sha，可用 git hash-object docs/en/repository/documentation/documentation.md 核验。 -->

# 文档体系

**其他语言：** [English](../../../en/repository/documentation/documentation.md) | [简体中文](documentation.md)

Grow 的文档为什么这样分。每一页必须遵守的规则见 [`conventions.md`](conventions.md)。

## 两层

Grow 同时是「库」和「代码库」，两者的读者不同。文档就按这条线分，而不是按话题分：

| 层 | 位置 | 读者 | 随包分发 |
| --- | --- | --- | --- |
| 包文档 | `Packages/com.ronny.grow/Documentation~/` | 安装这个包的人 | 是 |
| 仓库文档 | `docs/` | 构建这个框架的人 | 否 |

包文档随每个版本一起发布：Package Manager 的 **Documentation** 链接打开的就是它，内容来自包内的 `Documentation~` 目录。仓库文档讲框架怎么造、为什么这样造；把它塞进一个用户只为**使用**而安装的包里，只会增加负担。

所以：某个机制的内部实现**永不**写进包文档，安装步骤**永不**写进仓库文档。

## 两个主题

仓库文档这一层内部，最上面按**主题**分：框架本身，与这个仓库。两者不是一回事——「测试怎么跑」说明不了「内核怎么设计」——混在一起只会让两边都更难找。

| 主题 | 目录 | 覆盖 |
| --- | --- | --- |
| 框架 | `framework/` | Grow 是什么、怎么设计、用了哪些术语 |
| 本仓库 | `repository/` | 文档怎么运作，以及如何构建与检查代码 |

主题内部，再按**读者想做什么**分，而不是按「代码在哪」：

| 主题 | 分区 | 读者的需求 | 示例问题 |
| --- | --- | --- | --- |
| `framework/` | `architecture/` | 理解系统 | 内核为什么和能力层分开？ |
| `framework/` | `design/` | 理解一项已定稿的设计 | 启动时序当初是怎么设计的？ |
| `framework/` | `reference/` | 查一个词 | 这里的「能力」指什么？ |
| `repository/` | `documentation/` | 理解这套文档本身怎么运作 | 为什么有两棵文档树？ |
| `repository/` | `guides/` | 把事做成 | 测试怎么跑？ |

这些需求来自 [Diátaxis](https://diataxis.fr/)：它把文档分成 tutorial、how-to、reference、explanation 四类。本仓遵循这套分法，但有两处不同：`design/` 收设计记录，Diátaxis 没有对应象限；本仓也没有 tutorial 分区——教人**使用**这个包是包文档的职责。

分法的要点在两处让步之后仍然成立：想**动手**的读者，讲道理的文章帮不上忙，因此一页只服务一种需求。

**不是每个主题都需要每个分区。** `repository/` 没有 `architecture/`，因为它没有可解释的架构；它有的是 `documentation/`，因为它要解释的正是文档本身。

`design/` 只收**已定稿**的设计。设计还在变动时属于草稿，留在仓库之外，免得半成品被当成结论。

设计记录不是决策记录。设计记录是写给「读框架的人」看的，讲清框架的一部分；决策记录记的是「当初为什么这样选」的全过程。**决策记录暂不公开**——本仓目前不放，将来要放也会另设一处，而不会混进 `design/`。

## 一种语言为正文，另一种镜像

英文为正文，每一页都有镜像。译文放哪儿、`source` 标记记什么，都是机制问题，见 [`conventions.md`](conventions.md)。

镜像之所以值得这份成本，在于它防住的事：一份悄悄落后的译文，比没有译文更糟——所以要把配对关系做成**可校验**的，而不是靠信任。也正因如此，翻译一页是编辑它的一部分，而不是可以往后拖的记账。

## 校什么

`Docs` workflow 在每个 Pull Request 上把这些规则全部执行一遍，并且本身就是 `main` 要求的检查项。它跑什么、怎么本地跑，见 [`conventions.md`](conventions.md)。

## 有意缺失的东西

- **手写 API 参考**。公开 API 就在它被定义的地方（XML 文档注释）说明；参考由那里生成，绝不写两遍。
- **贡献流程**。没有 `CONTRIBUTING.md`，也没有 `CODEOWNERS`。项目还早，现在写下的流程只是猜测。

## 另见

- [`conventions.md`](conventions.md)——每一页必须遵守的规则。
- [`../../framework/reference/glossary.md`](../../framework/reference/glossary.md)——这些页面里用到的术语。
