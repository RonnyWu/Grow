# Documentation conventions

**Read this in other languages:** [English](documentation.md) | [简体中文](../../zh-CN/guides/documentation.md)

How documentation is organised, written and checked in this repository.

## Two layers

| Layer | Location | Audience | Tracks |
| --- | --- | --- | --- |
| Package docs | `Packages/com.ronny.grow/Documentation~/` | People who use the package | The package version |
| Repository docs | `docs/` | People who build the framework | `main` |

Explain how to *use* the package in the package docs; explain how it is *built*, or *why* it is
the way it is, in the repository docs.

## Sections

Repository docs follow the four [Diátaxis](https://diataxis.fr/) needs:

| Folder | Need | Question it answers |
| --- | --- | --- |
| `architecture/` | Explanation | What is it, and why is it shaped this way? |
| `design/` | Design record | What did we decide to build, and what did we weigh? |
| `guides/` | How-to | How do I do X? |
| `reference/` | Reference | What exactly is X? |

One topic per page. Do not mix explanation, how-to and reference in a single page.

Decision records are **not published** in this repository; they are tracked separately.

## Page anatomy

Every page has this shape:

1. A single level-1 heading (`#`) naming the page.
2. One sentence saying what the page is about.
3. The body, starting at `##`. Never skip a heading level.
4. A closing `## See also` section when there is anywhere useful to go next.

## File names

- Lower-case `kebab-case` with a `.md` extension — for example `documentation.md`,
  `log-chunking.md`.
- A file name is unique **within its locale tree**.
- `README.md` is the landing page of a folder and is the only exception to the rule above.

## Language

- **English is canonical.** A page lives at a relative path under `docs/en/`.
- **`zh-CN` is a mirror.** The translation lives at the same relative path under `docs/zh-CN/`.
- A translation starts with a marker naming the English source and its content hash:

  ```text
  <!-- source: docs/en/guides/documentation.md @ blob:<sha> -->
  ```

  where `<sha>` is `git hash-object` of the English file. Update the marker in the same commit
  that changes the English text.
- A page carries a language switcher directly under its title, linking to the mirror.
- Translate prose only. Code, commands, links, paths and identifiers are copied verbatim.

## Versioning

- Package docs track the package version. Mark new behaviour with `Added in <version>`, and never
  write "currently", "now" or "soon".
- Repository docs track `main` and are not copied to release branches.

## Writing

- Use sentence case for headings.
- Describe the link target in the link text; never write "click here" or a bare URL.
- Link with repository-relative paths, so links work on GitHub and in the package documentation
  viewer.
- Use a term consistently, and define it in [`../reference/glossary.md`](../reference/glossary.md).

## Maintenance

- Documentation ships in the same pull request as the change it describes.
- Delete a page when it is no longer true; do not keep dead documentation.
- Link to existing documentation instead of repeating it.

## Automated checks

The `Docs` workflow runs on every pull request:

- `markdownlint-cli2`, configured by `.markdownlint-cli2.jsonc`;
- `lychee`, for broken links;
- `ci/docs/check-docs.mjs`, for the structural rules — mirrored trees, unique file names within a
  locale, and a `source` marker on every translation.

Run them locally:

```bash
npx markdownlint-cli2 "docs/**/*.md"
node ci/docs/check-docs.mjs
```
