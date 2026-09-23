# Contributing

Thanks for your interest in Grow.

## Documentation

The documentation layout and conventions are defined in [`docs/README.md`](docs/README.md) —
read it before adding or moving a document. In short:

- **Decisions** are recorded as ADRs under [`docs/architecture/`](docs/architecture), named
  `ADR-NNNN-topic.md`. Each new decision takes the next zero-padded four-digit number.
- **Module designs** live in `docs/design/`, each carrying a status header.
- **Reader-facing docs** follow Diátaxis under `docs/tutorials/`, `docs/how-to/`,
  `docs/reference/`, and `docs/explanation/`.
- **Drafts** (plans, specs, notes) stay in the untracked `.drafts/` directory and are never
  committed.

## Development

This repository is a Unity project. Open it with Unity 2021.3.45f2; the package lives under
`Packages/com.ronny.grow`. All code belongs in the package — `Assets/` stays empty.

Run the EditMode tests from the Unity Test Runner (`Grow.Tests` suite).

## Commits

- Keep commits focused and explain the *why*.
- Follow [Conventional Commits](https://www.conventionalcommits.org/).
- Update `Packages/com.ronny.grow/CHANGELOG.md` for user-visible changes.

## License

By contributing, you agree that your contributions are licensed under the MIT License.
