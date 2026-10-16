<!-- source: docs/en/architecture/README.md @ blob:fb346f743e9141a74ab8272c8878a8e1793dbbfb -->
<!-- 中文译文；以英文 docs/en/architecture/README.md 为准，译文可能滞后于原文。blob 是英文文件的内容 sha，可用 git hash-object docs/en/architecture/README.md 核验。 -->

# 架构

**其他语言：** [English](../../en/architecture/README.md) | [简体中文](README.md)

*解释*——Grow 是什么、为什么这样设计。

这里放：

- 架构描述：**三视图**（Module / C&C / Allocation）；
- **分层**（`Core` → `Kernel` → `Capabilities` → `Framework` → `Game`）与层间关系；
- 设计背后的理由，以及被权衡过的替代方案。

这里不放：

- 操作步骤——见 [`../guides/`](../guides/)；
- 已定稿的逐主题设计——见 [`../design/`](../design/)；
- 术语与定义——见 [`../reference/glossary.md`](../reference/glossary.md)。
