# Documentation system

**Read this in other languages:** [English](documentation.md) | [简体中文](../../../zh-CN/repository/documentation/documentation.md)

Why Grow's documentation is split the way it is. The rules a page must follow are in
[`conventions.md`](conventions.md).

## Two layers

Grow is a library and a codebase at once, and those two things have different readers. The
documentation is split along that line rather than by topic:

| Layer | Location | Reader | Ships with the package |
| --- | --- | --- | --- |
| Package docs | `Packages/com.ronny.grow/Documentation~/` | Someone who installs the package | Yes |
| Repository docs | `docs/` | Someone who builds the framework | No |

The package layer travels with every release: it is what the Package Manager's **Documentation**
link opens, from the `Documentation~` folder inside the package. The repository layer explains how
the framework is built and why; shipping it would only add weight to a package someone installed in
order to *use* it.

So a mechanism's internals are never explained in the package docs, and installing the package is
never explained in the repository docs.

## Two subjects

Inside the repository layer the top split is by **subject**: the framework itself, and this
repository. They are not the same thing — how to run the tests says nothing about how the kernel
is designed — and mixing them makes both harder to find.

| Subject | Folder | Covers |
| --- | --- | --- |
| The framework | `framework/` | What Grow is, how it is designed, and the terms it uses |
| This repository | `repository/` | How the documentation works, and how to build and check the code |

Within a subject, the split is by **what the reader is trying to do**, not by where they are in
the codebase:

| Subject | Section | Reader's need | Example question |
| --- | --- | --- | --- |
| `framework/` | `architecture/` | Understand the system | Why is the kernel separate from the capabilities? |
| `framework/` | `design/` | Understand a settled design | How was the entry sequence designed? |
| `framework/` | `reference/` | Look something up | What does *capability* mean here? |
| `repository/` | `documentation/` | Understand how this documentation works | Why are there two doc trees? |
| `repository/` | `guides/` | Get something done | How do I run the tests? |

Those needs come from [Diátaxis](https://diataxis.fr/), which sorts documentation into tutorials,
how-tos, reference and explanation. This tree follows that split with two departures: `design/` holds
design records, which Diátaxis has no quadrant for, and there is no tutorials section — learning to
*use* the package is what the package docs are for.

The point of the split survives both: a reader who wants to *do* something is not helped by a page
that explains, so a page serves exactly one need.

Every subject does not need every section. `repository/` has no `architecture/`, because there is
no architecture to explain; it has `documentation/` instead, because that is what it needs to
explain.

`design/` holds a design once it has settled. While a design is still moving it is a draft and stays
outside the repository, so that no half-formed proposal is mistaken for a decision.

A design record is not a decision record. A design record explains part of the framework to whoever
reads it; a decision record logs the deliberation that chose it. Decision records are **not published
yet** — this repository does not carry them today, and when it does they will get their own home
rather than being mixed into `design/`.

## One language, mirrored

English is canonical, and every page is mirrored. Where a translation lives and what the `source`
marker records are mechanics, and they are in [`conventions.md`](conventions.md).

The mirror is worth its cost because of what it prevents: a translation that quietly falls behind is
worse than none at all, so the pairing is made checkable rather than trusted. That is why translating
a page is part of editing it, not bookkeeping that can wait.

## What is checked

The `Docs` workflow enforces all of the above on every pull request, as a required check on `main`.
What it runs, and how to run it locally, is in [`conventions.md`](conventions.md).

## What is deliberately absent

- **A hand-written API reference.** The public API is documented where it is defined, in XML
  documentation comments; a reference is generated from those, never written twice.
- **A contribution process.** There is no `CONTRIBUTING.md` and no `CODEOWNERS`. The project is
  early, and a process written now would be a guess.

## See also

- [`conventions.md`](conventions.md) — the rules a page must follow.
- [`../../framework/reference/glossary.md`](../../framework/reference/glossary.md) — the terms
  used across these pages.
