<!-- source: docs/en/architecture/documentation-system.md @ blob:cdb3cc74d8b69786e803f18a45e45aa0e438e4fb -->
<!-- 中文译文；以英文 docs/en/architecture/documentation-system.md 为准，译文可能滞后于原文。blob 是英文文件的内容 sha，可用 git hash-object docs/en/architecture/documentation-system.md 核验。 -->

# 文档体系

**其他语言：** [English](../../en/architecture/documentation-system.md) | [简体中文](documentation-system.md)

Grow 的文档为什么这样分。每一页必须遵守的规则见
[`../guides/documentation.md`](../guides/documentation.md)。

## 两层

Grow 同时是「库」和「代码库」，两者的读者不同。文档就按这条线分，而不是按话题分：

| 层 | 位置 | 读者 | 随包分发 |
| --- | --- | --- | --- |
| 包文档 | `Packages/com.ronny.grow/Documentation~/` | 安装这个包的人 | 是 |
| 仓库文档 | `docs/` | 构建这个框架的人 | 否 |

包文档正是 Unity Package Manager 里 **View documentation** 打开的内容，因此随每个版本发布。仓库文档讲框架怎么造、为什么这样造；把它塞进一个用户只为**使用**而安装的包里，只会增加负担。

所以：某个机制的内部实现**永不**写进包文档，安装步骤**永不**写进仓库文档。

## 四个分区

仓库文档按「读者想做什么」组织，而不是按「代码在哪」：

| 分区 | 读者的需求 | 示例问题 |
| --- | --- | --- |
| `architecture/` | 理解系统 | 内核为什么和能力层分开？ |
| `design/` | 理解过去的设计 | 启动时序当初是怎么设计的？ |
| `guides/` | 把事做成 | 测试怎么跑？ |
| `reference/` | 查一个词 | 这里的「能力」指什么？ |

这就是 [Diátaxis](https://diataxis.fr/) 的分法，要点在于这四种需求彼此不同：想**动手**的读者，讲道理的文章帮不上忙，反之亦然。因此一页只服务其中一种需求。

`design/` 只收**已定稿**的设计。设计还在变动时属于草稿，留在仓库之外，免得半成品被当成结论。

决策记录**不在本仓公开**。这里留下的是「读框架的人用得上的推理」，而不是产生这个推理的全过程。

## 一种语言为正文，另一种镜像

英文为正文：每一页都在 `docs/en/`，译文从这份正文翻译而来。译文放在 `docs/zh-CN/` 的**同一相对路径**下，两棵树因此可以逐文件对照。

译文会记下自己译自哪份英文，以及那份英文的内容哈希：

```text
<!-- source: docs/en/architecture/documentation-system.md @ blob:<sha> -->
```

哈希让「配对」变得可校验：英文改了而标记没跟着更新，译文就**确定**过期了——不需要猜。所以这行标记放在每篇译文的第一行；也所以「翻译」是编辑一页的一部分，而不是可以往后拖的记账。

只翻散文。代码、命令、链接、路径与标识符一律照抄。

## 校什么

`Docs` workflow 在每个 Pull Request 上跑：

- **Markdown 规范**——格式与结构规则。
- **链接检查**——仓库内相对链接与外链。
- **结构校验**——两棵树页面一致、同一语言树内文件名唯一、每篇译文的 `source` 标记存在且与英文对得上。

最后一条是镜像的代价：镜像树只有在两半同步时才值得存在，而唯一能确认同步的办法就是比对哈希。

## 有意缺失的东西

- **手写 API 参考**。公开 API 就在它被定义的地方（XML 文档注释）说明；参考由那里生成，绝不写两遍。
- **贡献流程**。没有 `CONTRIBUTING.md`，也没有 `CODEOWNERS`。项目还早，现在写下的流程只是猜测。

## 另见

- [`../guides/documentation.md`](../guides/documentation.md)——每一页必须遵守的规则。
- [`../reference/glossary.md`](../reference/glossary.md)——这些页面里用到的术语。
