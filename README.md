# Grow

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-blue)](https://unity.com/releases/2021-lts)
[![Semantic Versioning](https://img.shields.io/badge/semver-2.0.0-e10079.svg)](https://semver.org/)
[![Keep a Changelog](https://img.shields.io/badge/changelog-kept-%2384cc16)](https://keepachangelog.com/)

**Grow** is a modular, lightweight Unity game framework, distributed as a single
[UPM](https://docs.unity3d.com/Manual/CustomPackages.html) package, `com.ronny.grow`.

> **Status: early design.** The framework is being redesigned from scratch. The API and
> structure are not stable yet and no release is available.

## Repository layout

This repository is a Unity project with the package embedded under `Packages/`:

```
Grow/
├── Assets/                       development scenes and samples
├── Packages/
│   ├── manifest.json
│   └── com.ronny.grow/           the package (distributed via git subpath)
│       ├── Runtime/              Grow (assembly)
│       ├── Editor/               Grow.Editor (assembly)
│       └── Tests/
├── ProjectSettings/
└── docs/                         design documentation
```

See [`docs/architecture/`](docs/architecture) for the architecture decisions.

## Installation

No release is available yet. During development, add the package by git URL:

```json
{
  "dependencies": {
    "com.ronny.grow": "https://github.com/RonnyWu/Grow.git?path=Packages/com.ronny.grow"
  }
}
```

## License

MIT — see [LICENSE](LICENSE).
