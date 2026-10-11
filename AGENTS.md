# AGENTS.md

Unity game framework "Grow", minimum **Unity 2022.3 LTS** — development is on **2022.3.62f3**
(C# 9, .NET Standard 2.1 API profile). Do not use APIs newer than 2022.3 LTS (`linearVelocity`,
Unity 6 APIs, etc. are unavailable here).

The framework is an **embedded UPM package at `Packages/com.ronny.grow`**, not under `Assets/`.
`Assets/` holds only `.gitkeep`. Put all code in the package.

- This is a **fresh baseline (`v3.0.0-exp.1`)**: the design is being re-planned; there is no
  implementation yet. Do not assume history — the previous repository was archived.
- Runtime / Editor / Tests assemblies: `Grow`, `Grow.Editor`, `Grow.Tests.Editor`
  (`Packages/com.ronny.grow/{Runtime,Editor,Tests/Editor}`). The test assembly is enabled via
  `"testables": ["com.ronny.grow"]` in `Packages/manifest.json`.
- Headless EditMode run (PowerShell), omitting `-quit` (it silently suppresses the run); write
  outputs under gitignored `ci/logs/`:

  ```powershell
  & "D:\UnityEditor\Unity 2022.3.62f3\Editor\Unity.exe" -batchmode -nographics `
    -projectPath "D:\UnityProjects\Grow" -runTests -testPlatform EditMode `
    -testResults "D:\UnityProjects\Grow\ci\logs\editmode-results.xml" `
    -logFile "D:\UnityProjects\Grow\ci\logs\editmode-unity.log"
  ```

- Every `.cs` file starts with the 3-line MIT header (Copyright 2026 Ronny Wu).
- Conventional Commits; keep `Packages/com.ronny.grow/CHANGELOG.md` current for user-visible changes.
- Commit or push only when explicitly asked.
