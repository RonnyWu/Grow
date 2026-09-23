# Architecture Decision Records (ADR)

This directory holds Grow's **frozen decisions**. One ADR = one decision; once `Accepted`,
a record is immutable.

## Numbering

- Filename `adr-nnnn-topic.md` — four zero-padded digits, `topic` a short lowercase
  kebab-case description.
- Numbers increase in **creation order**; never reuse or renumber.
- `0000` is reserved for the template (`adr-0000-template.md`).
- Records start at `ADR-0002` (`ADR-0001` was never created and will not be backfilled).

## Status

| Status | Meaning |
|---|---|
| `Proposed` | Under discussion, not yet in effect |
| `Accepted` | In effect; immutable |
| `Deprecated` | No longer recommended, but may still be followed by old code |
| `Superseded by ADR-NNNN` | Replaced by a newer ADR; read that one instead |

The status lives in the metadata block at the top of the document.

**Lifecycle**: a new ADR is written as `Proposed` and submitted with a PR. Once the
maintainer approves it, it becomes `Accepted` and the body is frozen — from then on only
the status line may change. To replace a decision, add a new ADR and change the old
record's status line to `Superseded by ADR-NNNN`, linking to the replacement.

## When Not to Write One

An ADR records decisions that are **hard to reverse, span multiple modules, or are likely
to be argued about later**. Rule of thumb (borrowed from the Socorro project): **if a
decision does not look important, it probably isn't.** Implementation details, choices that
are cheap to change, and pure style preferences do not get an ADR.

## How to Write One

1. Copy `adr-0000-template.md` to `adr-nnnn-topic.md` and use the next number.
2. Fill in Context / Decision Drivers (optional) / Decision / Consequences / Alternatives /
   References.
3. Keep the decision actionable and verifiable; every alternative must state why it was
   not chosen.
4. Once frozen it cannot be edited; to change it, supersede it with a new ADR.

The template is written in English for open-source contributors. ADR bodies may be written
in any language, but keep a single document in one language.

## Index

| Number | Title | Status |
|---|---|---|
| `ADR-0002` | [关闭 Domain Reload 作为 Grow 默认运行模式](adr-0002-domain-reload-disabled.md) | Accepted |
| `ADR-0003` | [文档体系采用 Diátaxis 四象限预建结构](adr-0003-diataxis-documentation-layout.md) | Accepted |
