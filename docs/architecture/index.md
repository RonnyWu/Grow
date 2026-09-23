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

**流转**：新 ADR 以 `Proposed` 写入并随 PR 提交；维护者复核通过后改为 `Accepted`，此时正文冻结（此后只可改状态行，不可改正文）。被取代时新增一条 ADR，旧条目状态行改为 `Superseded by ADR-NNNN` 并链接新条目。

## 何时不写

ADR 只记录**难以回滚、影响多个模块、或日后容易引发争论**的决策。判断口诀（借自 Socorro 项目）：**若一个决策看起来不关键，那它大概就真的不关键。** 实现细节、随手可改的选择、纯风格偏好不写 ADR。

## 怎么写

1. 复制 `adr-0000-template.md` 为 `adr-nnnn-topic.md`，填入下一个编号。
2. 补齐背景（Context）/ 决策驱动（Decision Drivers，可选）/ 决策（Decision）/ 结果（Consequences）/ 备选方案（Alternatives）/ 参考（References）。
3. 决策要可执行、可验证；备选方案要写清"为什么不选它"。
4. 冻结后即不可改；要改就推翻重来，新增一条。

## 清单

| 编号 | 标题 | 状态 |
|---|---|---|
| `ADR-0002` | [关闭 Domain Reload 作为 Grow 默认运行模式](adr-0002-domain-reload-disabled.md) | Accepted |
| `ADR-0003` | [文档体系采用 Diátaxis 四象限预建结构](adr-0003-diataxis-documentation-layout.md) | Accepted |
