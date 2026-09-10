# Architecture Decision Records

This directory holds the architecture decisions (ADRs) for the Grow framework.
One document per topic, named `ADR-NNNN-topic.md` (zero-padded four-digit number
plus a kebab-case topic). A topic may cover several tightly related decisions,
numbered `D1`, `D2`, ... in its body.

Once accepted, a document's body is not edited. If a decision is later replaced,
only its status changes to `superseded by ADR-NNNN`.

## Index

| ADR | Title | Status |
|---|---|---|
| [ADR-0001](ADR-0001-project-and-packages.md) | Project and Package Structure | Accepted |
| [ADR-0002](ADR-0002-git-workflow.md) | Git Workflow | Accepted |

## Status values

`proposed` · `accepted` · `rejected` · `deprecated` · `superseded by ADR-NNNN`

## Conventions

- Documents are written in English.
- A new decision takes the next four-digit number and a kebab-case topic.
- The structure of [`ADR-0001`](ADR-0001-project-and-packages.md) can be used as a template.
