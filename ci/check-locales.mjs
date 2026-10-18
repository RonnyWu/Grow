#!/usr/bin/env node
// SPDX-License-Identifier: MIT

// Structural checks for editor locale files:
//   1. every locale file is valid JSON with a matching language tag;
//   2. keys and values are non-empty and keys are unique;
//   3. all languages in one package have the same key set;
//   4. placeholders ({0}, {1}, ...) match across languages;
//   5. keys use the "grow." namespace.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const packagesRoot = path.join(root, "Packages");
const problems = [];

function* walkFiles(directory) {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
        if (entry.name.endsWith("~") || entry.name === ".git") continue;
        const full = path.join(directory, entry.name);
        if (entry.isDirectory()) yield* walkFiles(full);
        else yield full;
    }
}

for (const packageEntry of fs.readdirSync(packagesRoot, { withFileTypes: true })) {
    if (!packageEntry.isDirectory()) continue;

    const packagePath = path.join(packagesRoot, packageEntry.name);
    const localesPath = path.join(packagePath, "Editor", "Localization", "Locales");
    if (!fs.existsSync(localesPath)) continue;

    const localeFiles = [...walkFiles(localesPath)]
        .filter((file) => file.endsWith(".json"))
        .sort();
    if (localeFiles.length === 0) continue;

    const byLanguage = new Map();
    for (const file of localeFiles) {
        const relative = path.relative(root, file).split(path.sep).join("/");
        let data;

        try {
            data = JSON.parse(fs.readFileSync(file, "utf8"));
        } catch (error) {
            problems.push(`${relative}: invalid JSON (${error.message})`);
            continue;
        }

        const fileLanguage = path.basename(file, ".json");
        if (data.language !== fileLanguage) {
            problems.push(
                `${relative}: language "${data.language}" does not match file name`);
        }

        if (!Array.isArray(data.entries)) {
            problems.push(`${relative}: entries must be an array`);
            continue;
        }

        const keys = new Map();
        for (const entry of data.entries) {
            if (typeof entry?.key !== "string" || !entry.key.startsWith("grow.")) {
                problems.push(`${relative}: key must start with "grow."`);
                continue;
            }

            if (typeof entry.value !== "string" || entry.value.length === 0) {
                problems.push(`${relative}: "${entry.key}" has an empty value`);
            }

            if (keys.has(entry.key)) {
                problems.push(`${relative}: duplicate key "${entry.key}"`);
            }

            const placeholders = [...entry.value.matchAll(/\{(\d+)\}/g)]
                .map((match) => match[1])
                .sort();
            keys.set(entry.key, placeholders);
        }

        byLanguage.set(fileLanguage, { relative, keys });
    }

    if (!byLanguage.has("en")) {
        problems.push(`${path.relative(root, localesPath)}: missing en.json`);
        continue;
    }

    const english = byLanguage.get("en");
    for (const [language, locale] of byLanguage) {
        if (language === "en") continue;

        for (const key of english.keys.keys()) {
            if (!locale.keys.has(key)) {
                problems.push(`${locale.relative}: missing key "${key}"`);
            }
        }

        for (const key of locale.keys.keys()) {
            if (!english.keys.has(key)) {
                problems.push(`${english.relative}: missing key "${key}"`);
            }
        }

        for (const [key, placeholders] of locale.keys) {
            if (!english.keys.has(key)) continue;
            const expected = english.keys.get(key);
            if (expected.join(",") !== placeholders.join(",")) {
                problems.push(
                    `${locale.relative}: placeholders differ for "${key}" ` +
                    `(${placeholders.join(",") || "none"} != ${expected.join(",") || "none"})`);
            }
        }
    }
}

if (problems.length > 0) {
    console.error(`[check-locales] ${problems.length} problem(s):`);
    for (const problem of problems) console.error(`  - ${problem}`);
    process.exit(1);
}

console.log("[check-locales] OK");
