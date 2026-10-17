// SPDX-License-Identifier: MIT

// Package-level structural checks that dotnet format cannot see: Unity meta pairing,
// SPDX headers, package/project version alignment, asmdef direction and accidental
// Unity 6 writes into a 2022.3 project. Run from the repository root:
//   node ci/check-package.mjs

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const packageRoot = path.join(root, "Packages", "com.ronny.grow");
const errors = [];

function fail(message) {
    errors.push(message);
}

function read(relative) {
    return fs.readFileSync(path.join(root, relative), "utf8").replace(/^\uFEFF/, "");
}

function* walk(directory) {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
        if (entry.name.endsWith("~") || entry.name === ".git") continue;
        const full = path.join(directory, entry.name);
        const relative = path.relative(root, full).replaceAll(path.sep, "/");
        yield { relative, isDirectory: entry.isDirectory() };
        if (entry.isDirectory()) yield* walk(full);
    }
}

// 1. Every package asset/folder (outside ~ folders) has a sibling .meta, and no .meta is orphaned.
for (const { relative, isDirectory } of walk(packageRoot)) {
    if (relative.endsWith(".meta")) continue;
    if (!fs.existsSync(path.join(root, `${relative}.meta`))) {
        fail(`missing meta: ${relative}${isDirectory ? "/" : ""}`);
    }
}
for (const { relative, isDirectory } of walk(packageRoot)) {
    if (isDirectory || !relative.endsWith(".meta")) continue;
    const asset = relative.slice(0, -".meta".length);
    if (!fs.existsSync(path.join(root, asset))) fail(`orphan meta: ${relative}`);
}

// 2. Every C# source starts with the SPDX header.
for (const { relative, isDirectory } of walk(packageRoot)) {
    if (isDirectory || !relative.endsWith(".cs")) continue;
    const firstLine = read(relative).split(/\r?\n/, 1)[0];
    if (firstLine !== "// SPDX-License-Identifier: MIT") {
        fail(`missing SPDX header: ${relative}`);
    }
}

// 3. package.json / CHANGELOG / ProjectVersion agree.
const packageJson = JSON.parse(read("Packages/com.ronny.grow/package.json"));
const changelog = read("Packages/com.ronny.grow/CHANGELOG.md");
const versions = [...changelog.matchAll(/^## \[([^\]]+)\]/gm)]
    .map(match => match[1])
    .filter(version => version !== "Unreleased");
if (versions[0] !== packageJson.version) {
    fail(`package.json version ${packageJson.version} != CHANGELOG latest ${versions[0]}`);
}
const projectVersion = read("ProjectSettings/ProjectVersion.txt").match(/^m_EditorVersion:\s*(\S+)/m)?.[1];
if (!projectVersion?.startsWith(`${packageJson.unity}.`)) {
    fail(`package.json unity ${packageJson.unity} != ProjectVersion ${projectVersion}`);
}

// 4. Assembly direction: Core has no references; Kernel references Core only; tests may not be referenced.
function asmdef(relative) {
    return JSON.parse(read(relative));
}

const core = asmdef("Packages/com.ronny.grow/Runtime/Core/Grow.Core.asmdef");
if ((core.references ?? []).length !== 0) fail("Grow.Core must not reference another assembly");

const kernel = asmdef("Packages/com.ronny.grow/Runtime/Kernel/Grow.Kernel.asmdef");
const kernelRefs = kernel.references ?? [];
if (kernelRefs.length !== 1 || kernelRefs[0] !== "Grow.Core") {
    fail(`Grow.Kernel references must be exactly Grow.Core, got ${kernelRefs.join(", ")}`);
}

for (const { relative, isDirectory } of walk(path.join(root, "Packages"))) {
    if (isDirectory || !relative.endsWith(".asmdef")) continue;
    const definition = asmdef(relative);
    for (const reference of definition.references ?? []) {
        if (reference.startsWith("Grow.Tests")) fail(`non-test assembly references ${reference}: ${relative}`);
    }
}

// 5. Assets/ stays empty except .gitkeep.
const assetFiles = [];
for (const { relative, isDirectory } of walk(path.join(root, "Assets"))) {
    if (!isDirectory && !relative.endsWith(".gitkeep")) assetFiles.push(relative);
}
if (assetFiles.length > 0) fail(`Assets must stay empty: ${assetFiles.join(", ")}`);

// PlayerSettings schema is editor-generated (2022.3.62f3 legitimately writes
// serializedVersion 26 and visionOS fields). ProjectVersion consistency above is the authority;
// do not infer the editor family from schema fields here.

if (errors.length > 0) {
    console.error(`[check-package] ${errors.length} problem(s):`);
    for (const error of errors) console.error(`  - ${error}`);
    process.exit(1);
}

console.log("[check-package] OK");
