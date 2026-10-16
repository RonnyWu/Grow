# Documentation conventions

**Read this in other languages:** [English](conventions.md) | [简体中文](../../../zh-CN/repository/documentation/conventions.md)

The rules every page in `docs/` follows. For why the documentation is arranged this way, see
[`documentation.md`](documentation.md).

## Where a page goes

| Subject | Section | A page belongs here when it |
| --- | --- | --- |
| `framework/` | `architecture/` | explains what the framework is and why it is shaped that way |
| `framework/` | `design/` | records a settled, per-topic design |
| `framework/` | `reference/` | is looked up rather than read — terms, tables, invariants |
| `repository/` | `documentation/` | explains or rules on this documentation itself |
| `repository/` | `guides/` | tells a contributor how to do a task |

Use `framework/` or `repository/` for the code and its process, and
`Packages/com.ronny.grow/Documentation~/` for anything a package user needs. Never explain how to
*use* the package in `docs/`.

One topic per page. Do not mix explanation, how-to and reference in a single page.

## Page anatomy

Every page has this shape:

1. A single level-1 heading (`#`) naming the page.
2. One sentence saying what the page is about.
3. The body, starting at `##`. Never skip a heading level.
4. A closing `## See also` section when there is anywhere useful to go next.

## File names

- Lower-case `kebab-case` with a `.md` extension — for example `conventions.md`,
  `log-chunking.md`.
- A file name is unique **within its locale tree**.
- `README.md` is the landing page of a section folder, and the only exception to the rule above.
  `framework/` and `repository/` group sections instead of holding pages, so they have no landing
  page.

## Language

- **English is canonical.** A page lives at a relative path under `docs/en/`.
- **`zh-CN` is a mirror.** The translation lives at the same relative path under `docs/zh-CN/`.
- A translation starts with a marker naming the English source and its content hash:

  ```text
  <!-- source: docs/en/repository/documentation/conventions.md @ blob:<sha> -->
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
- Use a term consistently, and define it in
  [`../../framework/reference/glossary.md`](../../framework/reference/glossary.md).

## Maintenance

- Documentation ships in the same pull request as the change it describes.
- Delete a page when it is no longer true; do not keep dead documentation.
- Link to existing documentation instead of repeating it.

## Automated checks

The `Docs` workflow runs on every pull request:

- `markdownlint-cli2`, configured by `.markdownlint-cli2.jsonc`;
- `lychee`, for broken links;
- `ci/docs/check-docs.mjs`, for the structural rules — mirrored trees, unique file names within a
  locale, and a `source` marker on every translation whose blob hash still matches the English
  text.

The lint and structure checks run anywhere Node is installed. The link check needs the `lychee`
binary:

```bash
npx markdownlint-cli2 "docs/**/*.md"
node ci/docs/check-docs.mjs
lychee --config .lychee.toml "docs/**/*.md"
```
