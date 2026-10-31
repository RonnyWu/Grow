// SPDX-License-Identifier: MIT

// Package-level structural checks that dotnet format cannot see: Unity meta pairing,
// SPDX headers, package/project version alignment, embedded dependency versions,
// asmdef direction and file-only Store persistence. Every folder under Packages/
// with a package.json is treated as a first-party embedded package; registry packages
// in Library/PackageCache are out of scope. Run from any directory:
//   node ci/check-package.mjs

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const packagesRoot = path.join(root, "Packages");
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

// Package samples are ignored by Unity's normal import pipeline (`~` suffix), but they are
// still shipped and referenced by GUID from their scenes. They need their own checks.
function* walkIncludingTilde(directory) {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
        if (entry.name === ".git") continue;
        const full = path.join(directory, entry.name);
        const relative = path.relative(root, full).replaceAll(path.sep, "/");
        yield { relative, isDirectory: entry.isDirectory() };
        if (entry.isDirectory()) yield* walkIncludingTilde(full);
    }
}

// Every embedded package (a folder under Packages/ with a package.json). Registry
// packages live in Library/PackageCache and are out of scope.
const packages = fs.readdirSync(packagesRoot, { withFileTypes: true })
    .filter((entry) =>
        entry.isDirectory() && fs.existsSync(path.join(packagesRoot, entry.name, "package.json")))
    .map((entry) => `Packages/${entry.name}`)
    .sort();

if (packages.length === 0) fail("no embedded package found under Packages/");

const packageByName = new Map();
for (const packagePath of packages) {
    const json = JSON.parse(read(`${packagePath}/package.json`));
    packageByName.set(json.name, { path: packagePath, json });
}

// Package samples (`Samples~`) are skipped by `walk` for meta checks, so verify registration,
// meta pairing and SPDX explicitly.
for (const [packageName, info] of packageByName) {
    const samplesRoot = path.join(root, info.path, "Samples~");
    if (!fs.existsSync(samplesRoot)) continue;

    const samples = info.json.samples ?? [];
    for (const entry of fs.readdirSync(samplesRoot, { withFileTypes: true })) {
        if (!entry.isDirectory()) continue;
        const relative = `Samples~/${entry.name}`;
        if (!samples.some((sample) => sample.path === relative)) {
            fail(`${packageName}: sample ${relative} is not registered in package.json samples[]`);
        }
    }

    for (const sample of samples) {
        if (!fs.existsSync(path.join(root, info.path, sample.path))) {
            fail(`${packageName}: package.json samples path does not exist: ${sample.path}`);
        }
    }

    for (const { relative, isDirectory } of walkIncludingTilde(samplesRoot)) {
        if (isDirectory || relative.endsWith(".meta")) continue;
        if (!fs.existsSync(path.join(root, `${relative}.meta`))) {
            fail(`missing sample meta: ${relative}`);
        }

        if (relative.endsWith(".cs")) {
            const firstLine = read(relative).split(/\r?\n/, 1)[0];
            if (firstLine !== "// SPDX-License-Identifier: MIT") {
                fail(`missing SPDX header: ${relative}`);
            }
        }
    }
}

// 1. Every package asset/folder (outside ~ folders) has a sibling .meta, and no .meta is orphaned.
for (const packagePath of packages) {
    for (const { relative, isDirectory } of walk(path.join(root, packagePath))) {
        if (relative.endsWith(".meta")) continue;
        if (!fs.existsSync(path.join(root, `${relative}.meta`))) {
            fail(`missing meta: ${relative}${isDirectory ? "/" : ""}`);
        }
    }
    for (const { relative, isDirectory } of walk(path.join(root, packagePath))) {
        if (isDirectory || !relative.endsWith(".meta")) continue;
        const asset = relative.slice(0, -".meta".length);
        if (!fs.existsSync(path.join(root, asset))) fail(`orphan meta: ${relative}`);
    }
}

// 2. Every C# source starts with the SPDX header.
for (const packagePath of packages) {
    for (const { relative, isDirectory } of walk(path.join(root, packagePath))) {
        if (isDirectory || !relative.endsWith(".cs")) continue;
        const firstLine = read(relative).split(/\r?\n/, 1)[0];
        if (firstLine !== "// SPDX-License-Identifier: MIT") {
            fail(`missing SPDX header: ${relative}`);
        }
    }
}

// 3. package.json / CHANGELOG / ProjectVersion agree, for every package.
const projectVersion = read("ProjectSettings/ProjectVersion.txt").match(/^m_EditorVersion:\s*(\S+)/m)?.[1];
for (const packagePath of packages) {
    const packageJson = JSON.parse(read(`${packagePath}/package.json`));
    const changelog = read(`${packagePath}/CHANGELOG.md`);
    const versions = [...changelog.matchAll(/^## \[([^\]]+)\]/gm)]
        .map((match) => match[1])
        .filter((version) => version !== "Unreleased");
    if (versions[0] !== packageJson.version) {
        fail(`${packagePath}: package.json version ${packageJson.version} != CHANGELOG latest ${versions[0]}`);
    }
    if (!projectVersion?.startsWith(`${packageJson.unity}.`)) {
        fail(`${packagePath}: package.json unity ${packageJson.unity} != ProjectVersion ${projectVersion}`);
    }

    for (const [dependency, version] of Object.entries(packageJson.dependencies ?? {})) {
        if (!dependency.startsWith("com.ronny.")) continue;
        const target = packageByName.get(dependency);
        if (!target) {
            fail(`${packagePath}: dependency ${dependency} is not an embedded package`);
            continue;
        }

        if (target.json.version !== version) {
            fail(
                `${packagePath}: dependency ${dependency}@${version} != ` +
                `${target.path} version ${target.json.version}`);
        }
    }
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

const editor = asmdef("Packages/com.ronny.grow/Editor/Grow.Editor.asmdef");
const editorAllowed = new Set(["Grow.Core", "Grow.Kernel"]);
for (const reference of editor.references ?? []) {
    if (!editorAllowed.has(reference)) {
        fail(`Grow.Editor has an unexpected reference: ${reference}`);
    }
}
if (!(editor.includePlatforms ?? []).includes("Editor")) {
    fail("Grow.Editor must include the Editor platform only");
}

const lens = asmdef("Packages/com.ronny.grow.lens/Editor/Grow.Lens.Editor.asmdef");
const lensAllowed = new Set(["Grow.Editor"]);
for (const reference of lens.references ?? []) {
    if (!lensAllowed.has(reference)) {
        fail(`Grow.Lens.Editor has an unexpected reference: ${reference}`);
    }
}
if (!(lens.includePlatforms ?? []).includes("Editor")) {
    fail("Grow.Lens.Editor must include the Editor platform only");
}

const contracts = asmdef("Packages/com.ronny.grow.store.contracts/Runtime/Grow.Store.Contracts.asmdef");
const contractAllowed = new Set(["Grow.Core", "Grow.Kernel"]);
for (const reference of contracts.references ?? []) {
    if (!contractAllowed.has(reference)) {
        fail(`Grow.Store.Contracts has an unexpected reference: ${reference}`);
    }
}
if (contracts.noEngineReferences !== true) {
    fail("Grow.Store.Contracts must set noEngineReferences to true");
}

const store = asmdef("Packages/com.ronny.grow.store/Runtime/Store/Grow.Store.asmdef");
const storeAllowed = new Set(["Grow.Core", "Grow.Kernel", "Grow.Store.Contracts"]);
for (const reference of store.references ?? []) {
    if (!storeAllowed.has(reference)) {
        fail(`Grow.Store has an unexpected reference: ${reference}`);
    }
}
if (store.autoReferenced !== false) {
    fail("Grow.Store must set autoReferenced to false; consumers reference Grow.Store explicitly");
}

for (const { relative, isDirectory } of walk(
    path.join(root, "Packages/com.ronny.grow.store/Runtime/Store"))) {
    if (isDirectory || !relative.endsWith(".cs")) continue;
    if (/\bUnityEditor\b/.test(read(relative))) {
        fail(`editor-only API leaked into Grow.Store runtime code: ${relative}`);
    }
}

// The Store packages must stay file-based. EditorPrefs/PlayerPrefs write to the Windows
// registry, which leaks keys across unrelated Unity projects.
for (const storePackage of ["com.ronny.grow.store", "com.ronny.grow.store.contracts"]) {
    for (const { relative, isDirectory } of walk(path.join(root, "Packages", storePackage))) {
        if (isDirectory || !relative.endsWith(".cs")) continue;
        if (/\b(EditorPrefs|PlayerPrefs)\.|Microsoft\.Win32|\bRegistry\./.test(read(relative))) {
            fail(`registry-backed persistence is forbidden in ${storePackage}: ${relative}`);
        }
    }
}

for (const { relative, isDirectory } of walk(path.join(root, "Packages"))) {
    if (isDirectory || !relative.endsWith(".asmdef")) continue;
    const definition = asmdef(relative);
    const references = definition.references ?? [];
    const includePlatforms = definition.includePlatforms ?? [];

    if (!includePlatforms.includes("Editor") && references.includes("Grow.Editor")) {
        fail(`runtime assembly references Grow.Editor: ${relative}`);
    }

    for (const reference of references) {
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

console.log(`[check-package] OK (${packages.length} package(s): ${packages.join(", ")})`);
