# ADR-0003: 文档体系采用 Diátaxis 四象限预建结构

- 状态：Accepted
- 日期：2026-09-23
- 关联：`docs/README.md`（文档规范正文）、`docs/explanation/architecture-rationale.md`、`CONTRIBUTING.md`

## 背景（Context）

仓库此前没有文档组织规范，`docs/` 整体被 `.gitignore` 排除，`CONTRIBUTING.md` 却承诺 ADR 可在 `docs/architecture/` 找到，形成矛盾。确立规范前调查了三类先例（共 21 个 C#/Unity 仓库 + Diátaxis 采用者 + 开源健康文件规则），得到两条与直觉相反的结论：

1. **Diátaxis 官方明确反对预建四个空象限目录**：*"It certainly does not mean that you should create empty structures for tutorials / how-to guides / reference / explanation with nothing in them. Don't do that. It's horrible."*；官方同时强调 Diátaxis 不是"四个盒子"式的目录方案，结构应"从内部生长"。
2. **C# / Unity 生态未发现采用 Diátaxis 四目录的仓库**（抽样 dotnet/runtime、Polly、UniTask、Mirror、VContainer 等 21 个，零命中）；其 `docs/` 要么是文档站源码（docfx / Sphinx / Docusaurus），要么是扁平主题。工程记录（ADR / RFC / KEP）在主流做法中与产品文档分家，甚至分仓。

但本仓库当前真实内容几乎全是**面向维护者的工程记录**，产品文档为零。若严格按"按需生长"，四象限中只有 `explanation/` 有内容，其余将长期空缺，未来贡献者无从判断结构。

## 决策（Decision）

1. **`docs/` 采用双轨**：产品文档轨走 Diátaxis 四象限（`tutorials/` `how-to/` `reference/` `explanation/`）；工程记录轨独立为 `architecture/`（ADR）与 `design/`（设计文档）。**工程记录永不放入四象限。**
2. **预建四个象限目录**，这是对 Diátaxis 官方建议的**有意偏离**。偏离的代价以"每个象限放一份 `index.md` landing page"来抵消：目录因此**不是空结构**，而是一份写明清收范围与象限边界的导航页。
3. 象限 `index.md` 中明确"本象限暂无内容"，避免贡献者误判为未完成。
4. 本决策由 `docs/README.md` 作为规范正文承载；根 `README.md`、`CONTRIBUTING.md`、`AGENTS.md` 只引用不重写。

## 结果（Consequences）

**正面**

- 结构一目了然，贡献者无需理解 Diátaxis 理论即可找到落点。
- 每个象限的边界被显式写出，减少"文档串味"（把 explanation 塞进 reference 等）。
- 工程记录与产品文档分离，避免未冻结的规划冒充权威说明。

**负面**

- 与 Diátaxis 官方建议相悖，未来若引入外部文档贡献者可能引起疑问——由本文与 `docs/README.md` 解释即可。
- 空象限存在被误读为"没做完"的风险，靠 `index.md` 文案缓解。
- 四个目录在内容填充前，导航收益大于实际内容收益。

**中性**

- 若未来某象限长期无内容，可在不违反本 ADR 的前提下删除该目录（本 ADR 只规定"允许预建"，不强制"必须保留"）。

## 备选方案（Alternatives）

- **严格按需生长（只建 `explanation/`）**：符合官方建议，且 C#/Unity 生态无先例可证其必要性；但结构不直观，贡献者需先读规范才能定位，故未采纳。
- **不分轨，全部塞进四象限**：把 ADR 与设计文档归入 `explanation/`。会使"未冻结的规划"与"已冻结的决策"混在一起，且 ADR 的编号与状态语义无处安放，故否决。
- **不建 `docs/`，文档外置到站点或 wiki**：当前文档量（6 篇）远未到需要站点的规模，且脱离版本控制不利于与代码同步演进，故否决。

## 参考（References）

- Diátaxis — How to use Diátaxis：https://diataxis.fr/how-to-use-diataxis/
- Diátaxis — Complex hierarchies：https://diataxis.fr/complex-hierarchies/
- `docs/README.md`
- `docs/architecture/adr-0002-domain-reload-disabled.md`（ADR 格式先例）
