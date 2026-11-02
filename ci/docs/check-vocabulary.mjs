#!/usr/bin/env node
// Rejects internal phase labels and manual references in user-facing documentation.
//
// Scope matches the Markdown lint globs: the root README, i18n/, docs/, package Documentation~
// pages and package CHANGELOGs. Fenced code, inline code and HTML comments are blanked first, so
// examples and the translation source markers cannot trip the check.
//
// The terms below are internal to the project's planning: a reader outside the repository cannot
// resolve them, and an external document must use the package version or the public feature name
// instead (see AGENTS.md "Version language").
//
// Run from the repository root: node ci/docs/check-vocabulary.mjs

import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");

const RULES = [
    { pattern: /\bD[1-7]\b/, label: "internal decision label (D1-D7)" },
    { pattern: /\bF[1-4]\b/, label: "internal delivery-phase label (F1-F4)" },
    { pattern: /\bInv[1-8]\b/, label: "internal invariant label (Inv1-Inv8)" },
    { pattern: /\bT[1-5]\b/, label: "internal tension label (T1-T5)" },
    { pattern: /\bK[1-8]\b/, label: "internal milestone label (K1-K8)" },
    { pattern: /\bB[0-3]\b/, label: "internal hardening label (B0-B3)" },
    { pattern: /\bP[0-2]\b/, label: "internal priority label (P0-P2)" },
    { pattern: /\bV[124]\b/, label: "internal iteration label (V1/V2/V4)" },
    { pattern: /\bFirst Boot\b/i, label: "internal direction name" },
    { pattern: /试水/, label: "internal direction name" },
    { pattern: /\bADR-\d+/, label: "internal decision-record reference" },
    { pattern: /docs\/grow\//, label: "path inside the maintainer's manual" },
    { pattern: /\bmanual\//, label: "path inside the maintainer's manual" },
    { pattern: /\b(todos|decisions)\.md\b/, label: "maintainer's manual file" },
];

function* markdownFiles(directory) {
    if (!existsSync(directory)) {
        return;
    }

    for (const entry of readdirSync(directory, { withFileTypes: true })) {
        const full = join(directory, entry.name);
        if (entry.isDirectory()) {
            yield* markdownFiles(full);
        } else if (entry.isFile() && entry.name.endsWith(".md")) {
            yield full;
        }
    }
}

function collect() {
    const files = [];
    const readme = join(root, "README.md");
    if (existsSync(readme)) {
        files.push(readme);
    }

    for (const relativePath of ["i18n", "docs"]) {
        files.push(...markdownFiles(join(root, relativePath)));
    }

    const packages = join(root, "Packages");
    if (existsSync(packages)) {
        for (const entry of readdirSync(packages, { withFileTypes: true })) {
            if (!entry.isDirectory()) {
                continue;
            }

            const packageRoot = join(packages, entry.name);
            files.push(...markdownFiles(join(packageRoot, "Documentation~")));

            const changelog = join(packageRoot, "CHANGELOG.md");
            if (existsSync(changelog)) {
                files.push(changelog);
            }
        }
    }

    return files.sort();
}

// Blanks code and comments while keeping one array entry per source line.
function stripNonProse(text) {
    const lines = text.split("\n");
    let fence = false;
    let comment = false;

    for (let index = 0; index < lines.length; index++) {
        const line = lines[index];

        if (fence) {
            if (/^\s*```/.test(line)) {
                fence = false;
            }

            lines[index] = "";
            continue;
        }

        if (!comment && /^\s*```/.test(line)) {
            fence = true;
            lines[index] = "";
            continue;
        }

        let kept = "";
        let cursor = 0;
        while (cursor < line.length) {
            if (comment) {
                const end = line.indexOf("-->", cursor);
                if (end < 0) {
                    cursor = line.length;
                } else {
                    comment = false;
                    cursor = end + 3;
                }

                continue;
            }

            const start = line.indexOf("<!--", cursor);
            if (start < 0) {
                kept += line.slice(cursor);
                break;
            }

            kept += line.slice(cursor, start);
            comment = true;
            cursor = start + 4;
        }

        lines[index] = kept.replace(/`[^`]*`/g, "");
    }

    return lines;
}

const problems = [];
const files = collect();

for (const file of files) {
    const relativePath = relative(root, file).split(sep).join("/");
    const lines = stripNonProse(readFileSync(file, "utf8"));

    for (let index = 0; index < lines.length; index++) {
        const line = lines[index];
        if (!line) {
            continue;
        }

        for (const rule of RULES) {
            const match = rule.pattern.exec(line);
            if (match) {
                problems.push(`${relativePath}:${index + 1}: "${match[0]}" -- ${rule.label}`);
            }
        }
    }
}

if (problems.length > 0) {
    console.error("Vocabulary check failed; external documents must not use internal terms:");
    for (const problem of problems) {
        console.error(`  - ${problem}`);
    }

    process.exit(1);
}

console.log(`Vocabulary check passed (${files.length} files).`);
