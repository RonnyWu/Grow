# Design Documents

This directory holds Grow's **engineering designs and conventions**: system design, module
design, and pre-implementation reviews.

See [`dsn-0001-doc-layout.md`](dsn-0001-doc-layout.md) §7 for the split with ADRs — an ADR
records *why* a decision was made; this directory records *how* things are meant to work.

## Conventions

- Naming: `dsn-<nnnn>-<topic>.md` — numbers are per-type, start at `0001`, never reused.
- Every document carries a status header: `Draft` / `Implemented` / `Deprecated`.
- New documents start from [`dsn-0000-template.md`](dsn-0000-template.md).

## Index

| Document | Content |
|---|---|
| [dsn-0001-doc-layout.md](dsn-0001-doc-layout.md) | Documentation layout: directory duties, naming, tracking boundary, lifecycle |
| [dsn-0002-container-catalog.md](dsn-0002-container-catalog.md) | Container catalog: gaps, contracts, and priorities for `Core/Collections` and `Core/Pool` |
| [dsn-0003-container-necessity-review.md](dsn-0003-container-necessity-review.md) | Necessity review process and register for containers |
| [dsn-0004-event-primitives.md](dsn-0004-event-primitives.md) | Event primitives design (`GrowEvent` / `InvocationList`) |
| [dsn-0005-event-invocationlist-analysis.md](dsn-0005-event-invocationlist-analysis.md) | Dispatch kernel analysis and primitive-extraction correction |
