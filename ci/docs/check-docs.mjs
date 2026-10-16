#!/usr/bin/env node
// Structural checks for docs/ (see docs/en/guides/documentation.md):
//   1. docs/en and docs/zh-CN hold the same set of pages (mirrored trees);
//   2. a file name is unique within a locale tree, except for README.md;
//   3. every translated page starts with a `source` marker pointing at an existing page;
//   4. the same for the root translations under i18n/ (for example i18n/README.zh-CN.md).
//
// Run from the repository root: node ci/docs/check-docs.mjs

import { createHash } from "node:crypto";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const docsRoot = join(repoRoot, "docs");
const LOCALES = ["en", "zh-CN"];
const SOURCE = LOCALES[0];
const SOURCE_MARKER = /^<!--\s*source:\s*(\S+)\s*@\s*blob:([0-9a-f]{40})\s*-->$/;

const problems = [];

// The same hash `git hash-object` prints for a file whose working-tree bytes are LF.
function gitBlobSha(text) {
  const body = Buffer.from(text, "utf8");
  const header = Buffer.from(`blob ${body.length}\0`, "utf8");
  return createHash("sha1").update(Buffer.concat([header, body])).digest("hex");
}

function pages(locale) {
  const base = join(docsRoot, locale);
  const found = [];
  const walk = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = join(dir, entry.name);
      if (entry.isDirectory()) {
        walk(full);
      } else if (entry.isFile() && entry.name.endsWith(".md")) {
        found.push(relative(base, full).split(sep).join("/"));
      }
    }
  };
  walk(base);
  return found.sort();
}

const byLocale = Object.fromEntries(LOCALES.map((locale) => [locale, pages(locale)]));

// 1. Mirrored trees.
for (const locale of LOCALES.filter((it) => it !== SOURCE)) {
  const source = new Set(byLocale[SOURCE]);
  const target = new Set(byLocale[locale]);
  for (const page of source) {
    if (!target.has(page)) problems.push(`docs/${locale}/${page}: missing (not translated)`);
  }
  for (const page of target) {
    if (!source.has(page)) problems.push(`docs/${locale}/${page}: orphan (no docs/${SOURCE} source)`);
  }
}

// 2. Unique file names within a locale tree.
for (const locale of LOCALES) {
  const seen = new Map();
  for (const page of byLocale[locale]) {
    const name = page.slice(page.lastIndexOf("/") + 1);
    if (name === "README.md") continue;
    if (seen.has(name)) {
      problems.push(`docs/${locale}: duplicate file name "${name}" (${seen.get(name)}, ${page})`);
    } else {
      seen.set(name, page);
    }
  }
}

// 3. Source marker on every translated page.
for (const locale of LOCALES.filter((it) => it !== SOURCE)) {
  for (const page of byLocale[locale]) {
    const first = readFileSync(join(docsRoot, locale, page), "utf8").split("\n", 1)[0].trim();
    const match = SOURCE_MARKER.exec(first);
    if (!match) {
      problems.push(`docs/${locale}/${page}: first line must be a "<!-- source: <path> @ blob:<sha> -->" marker`);
      continue;
    }
    if (match[1] !== `docs/${SOURCE}/${page}`) {
      problems.push(`docs/${locale}/${page}: source marker points at "${match[1]}", expected "docs/${SOURCE}/${page}"`);
      continue;
    }
    const source = readFileSync(join(docsRoot, SOURCE, page), "utf8");
    if (match[2] !== gitBlobSha(source)) {
      problems.push(`docs/${locale}/${page}: source marker is stale -- the translated text changed after the marker was written`);
    }
  }
}

// 4. Root translations under i18n/ (for example i18n/README.zh-CN.md <- README.md).
const i18nRoot = join(repoRoot, "i18n");
if (existsSync(i18nRoot)) {
  for (const entry of readdirSync(i18nRoot, { withFileTypes: true })) {
    if (!entry.isFile() || !entry.name.endsWith(".md")) continue;
    const localized = /^(.*)\.([a-zA-Z-]+)\.md$/.exec(entry.name);
    if (!localized) continue;

    const rel = `i18n/${entry.name}`;
    const sourceName = `${localized[1]}.md`;
    const sourcePath = join(repoRoot, sourceName);
    if (!existsSync(sourcePath)) {
      problems.push(`${rel}: no "${sourceName}" at the repository root`);
      continue;
    }

    const first = readFileSync(join(i18nRoot, entry.name), "utf8").split("\n", 1)[0].trim();
    const match = SOURCE_MARKER.exec(first);
    if (!match) {
      problems.push(`${rel}: first line must be a "<!-- source: <path> @ blob:<sha> -->" marker`);
    } else if (match[1] !== sourceName) {
      problems.push(`${rel}: source marker points at "${match[1]}", expected "${sourceName}"`);
    } else if (match[2] !== gitBlobSha(readFileSync(sourcePath, "utf8"))) {
      problems.push(`${rel}: source marker is stale -- run "git hash-object ${sourceName}" and update it`);
    }
  }
}

if (problems.length > 0) {
  console.error("Documentation structure check failed:");
  for (const problem of problems) console.error(`  - ${problem}`);
  process.exit(1);
}

console.log(`Documentation structure check passed (${byLocale[SOURCE].length} pages per locale).`);
