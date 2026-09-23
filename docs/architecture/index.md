# 架构决策记录（ADR）

本目录存放 Grow 的**已冻结决策**。一条 ADR = 一个决策，一经 `Accepted` 不可改写。

## 编号

- 文件名 `adr-nnnn-topic.md`，四位补零，`topic` 为小写 kebab-case 短描述。
- 编号按**创建顺序**递增，不复用、不跳号修正。
- 本仓库从 `ADR-0002` 起记录（`ADR-0001` 从未创建，属历史遗留，不再补）。

## 状态

| 状态 | 含义 |
|---|---|
| `Proposed` | 提议中，尚未生效 |
| `Accepted` | 已生效，不可改写 |
| `Deprecated` | 不再推荐，但仍可能被旧代码遵循 |
| `Superseded by ADR-NNNN` | 被新 ADR 取代，请读新条目 |

状态写在正文顶部元信息区。取代发生时：**新增**一条 ADR，并在旧条目的顶部把状态改为 `Superseded by ADR-NNNN`——正文不改。

## 怎么写

1. 复制 `adr-0000-template.md` 为 `adr-nnnn-topic.md`，填入下一个编号。
2. 补齐背景（Context）/ 决策（Decision）/ 结果（Consequences）/ 备选方案（Alternatives）/ 参考（References）。
3. 决策要可执行、可验证；备选方案要写清"为什么不选它"。
4. 冻结后即不可改；要改就推翻重来，新增一条。

## 清单

| 编号 | 标题 | 状态 |
|---|---|---|
| `ADR-0002` | [关闭 Domain Reload 作为 Grow 默认运行模式](adr-0002-domain-reload-disabled.md) | Accepted |
| `ADR-0003` | [文档体系采用 Diátaxis 四象限预建结构](adr-0003-diataxis-documentation-layout.md) | Accepted |
