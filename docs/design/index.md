# 设计文档

本目录存放 Grow 的**工程设计与规范**：体系设计、模块设计，以及实现前的评审记录。

与 ADR 的分工见 [`dsn-0001-doc-layout.md`](dsn-0001-doc-layout.md) §8——ADR 记录"为什么当初这样定"，本目录记录"打算怎么做 / 现在怎么规定"。

## 约定

- 命名：`dsn-<nnnn>-<topic>.md`，编号在本目录内独立、从 `0001` 起、分配后不复用。
- 每篇文档在开头标注状态：`草稿` / `已实现` / `已废弃`。

## 清单

| 文档 | 内容 |
|---|---|
| [dsn-0001-doc-layout.md](dsn-0001-doc-layout.md) | 文档分布设计：目录职责、命名约定、跟踪边界、生命周期 |
| [dsn-0002-container-catalog.md](dsn-0002-container-catalog.md) | 容器需求目录：`Core/Collections` 与 `Core/Pool` 的缺口、契约与优先级 |
| [dsn-0003-container-necessity-review.md](dsn-0003-container-necessity-review.md) | 容器必要性验证流程与登记表 |
| [dsn-0004-event-primitives.md](dsn-0004-event-primitives.md) | 事件原语设计（`GrowEvent` / `InvocationList`） |
| [dsn-0005-event-invocationlist-analysis.md](dsn-0005-event-invocationlist-analysis.md) | 派发内核机制分析与原语抽取纠正 |
