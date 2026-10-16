# Documentation system

**Read this in other languages:** [English](documentation-system.md) | [简体中文](../../../zh-CN/repository/documentation/documentation-system.md)

Why Grow's documentation is split the way it is. The rules a page must follow are in
[`conventions.md`](conventions.md).

## Two layers

Grow is a library and a codebase at once, and those two things have different readers. The
documentation is split along that line rather than by topic:

| Layer | Location | Reader | Ships with the package |
| --- | --- | --- | --- |
| Package docs | `Packages/com.ronny.grow/Documentation~/` | Someone who installs the package | Yes |
| Repository docs | `docs/` | Someone who builds the framework | No |

The package layer is what the Unity Package Manager opens from **View documentation**, so it
travels with every release. The repository layer explains how the framework is built and why; it
would only add weight to a package someone installed in order to *use* it.

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

| Section | Reader's need | Example question |
| --- | --- | --- |
| `architecture/` | Understand the system | Why is the kernel separate from the capabilities? |
| `design/` | Understand a past design | How was the entry sequence designed? |
| `guides/` | Get something done | How do I run the tests? |
| `reference/` | Look something up | What does *capability* mean here? |

That second split is [Diátaxis](https://diataxis.fr/), and its point is that these needs differ:
a reader who wants to *do* something is not helped by a page that explains, and the reverse also
holds. A page therefore serves exactly one of them.

Every subject does not need every section. `repository/` has no `architecture/`, because there is
no architecture to explain; it has `documentation/` instead, because that is what it needs to
explain.

`design/` holds a design once it has settled. While a design is still moving it is a draft and
stays outside the repository, so that no half-formed proposal is mistaken for a decision.

Decision records are **not published** here. What reaches these pages is the reasoning that is
useful to someone reading the framework, not the deliberation that produced it.

## One language, mirrored

English is canonical: every page lives in `docs/en/`, and that is the text a translation is made
from. A translation lives at the **same relative path** under `docs/zh-CN/`, so the two trees can
be compared file by file.

A translation records the English text it came from, by content hash:

```text
<!-- source: docs/en/repository/documentation/documentation-system.md @ blob:<sha> -->
```

The hash is what makes the pairing checkable. When the English text changes and the marker is not
updated, the translation is *known* to be stale — nothing has to guess. That is why the marker is
the first line of every translated page, and why translating is part of editing a page rather
than bookkeeping that can wait.

Prose is translated. Code, commands, links, paths and identifiers are copied unchanged.

## What is checked

A `Docs` workflow runs on every pull request:

- **Markdown lint** — formatting and structure rules.
- **Link check** — repository-relative links and external URLs.
- **Structure check** — that both trees hold the same pages, that a file name occurs once per
  tree, and that every `source` marker exists and matches the English text.

The last one is the price of mirroring. A mirrored tree is only worth having if the halves stay
in step, and the only way to know they have is to compare hashes.

## What is deliberately absent

- **A hand-written API reference.** The public API is documented where it is defined, in XML
  documentation comments; a reference is generated from those, never written twice.
- **A contribution process.** There is no `CONTRIBUTING.md` and no `CODEOWNERS`. The project is
  early, and a process written now would be a guess.

## See also

- [`conventions.md`](conventions.md) — the rules a page must follow.
- [`../../framework/reference/glossary.md`](../../framework/reference/glossary.md) — the terms
  used across these pages.
