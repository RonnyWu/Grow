# ADR-0002 — Git Workflow

- Status: Accepted
- Date: 2026-09-11
- Deciders: Ronny Wu

## Context

Grow is an open-source Unity framework with a single maintainer today, but
external contributors are expected. Development is heavily assisted by AI
agents, so commits are frequent and partly automated.

The deliverable is one UPM package embedded at `Packages/com.ronny.grow/` and
distributed by git URL. Unity resolves a git dependency to a specific revision
(tag, branch, or commit), so consumers install a **revision**, not a branch —
the distribution unit is a tag.

Existing conventions: Conventional Commits (`CONTRIBUTING.md`), ADRs in
`docs/architecture/`, Keep a Changelog, and the package version in
`Packages/com.ronny.grow/package.json`.

Current state: `main` holds the initial 5-commit baseline; no remote is
configured yet; a local-only `old` branch contains unrelated pre-redesign
history and must never be published.

## Decisions

### D1 · Workflow model = GitHub Flow

Use GitHub Flow: a single long-lived branch `main`, short-lived topic branches,
and pull requests. Releases are cut from `main` by tagging. Do **not** create a
permanent `develop` branch.

### D2 · Branch naming and lifetime

- Long-lived branches: only `main`.
- Topic branches: `feat/<topic>`, `fix/<topic>`, `docs/<topic>`, `chore/<topic>`,
  `refactor/<topic>`, `test/<topic>`, `ci/<topic>`, and `hotfix/<topic>` for
  urgent fixes. Topics are lowercase kebab-case, e.g. `feat/event-bus`.
- Branches are short-lived (merge within days) and deleted after merge.
- A topic branch is required even for solo work.

### D3 · All changes via pull request

Every change to `main` goes through a pull request; direct pushes to `main` are
disabled. A PR must have a title in Conventional Commit form and pass the checks
in D7. The approval requirement is currently 0 (the solo maintainer self-merges)
and becomes 1 when a second maintainer joins.

### D4 · Merge strategy = squash and merge

Squash each PR into a single commit on `main`; the PR title becomes that commit.
`main` keeps a linear history (no merge commits), and the source branch is
deleted after merge. Rebase-and-merge may be used only when a PR's individual
commits must be preserved.

### D5 · Versioning = SemVer, package.json as source of truth

The UPM package follows Semantic Versioning. `Packages/com.ronny.grow/package.json`
`version` is the single source of truth. Before 1.0.0 (`0.y.z`), breaking API
changes may ship in the minor position; the first real release is `0.1.0`.

### D6 · Releases and tags

- A release is prepared by a `chore(release): vX.Y.Z` PR that updates
  `package.json` and `CHANGELOG.md`.
- After merge, tag `main` with an annotated `vX.Y.Z` matching `package.json`, and
  publish a GitHub Release from that tag.
- Pre-releases use `vX.Y.Z-beta.N`.

### D7 · Quality gate

Once CI exists, a PR may merge only when it passes Unity batch-mode compilation,
EditMode tests, and package metadata validation (valid `package.json`, committed
`.meta` files, and — for releases — a version/tag match). While CI is still the
`workflow_dispatch` placeholder, the author must confirm a clean Unity compile
and green EditMode tests in the PR.

### D8 · main protection

Protect `main`: require a pull request, require status checks once CI exists,
require linear history, require resolved conversations, disallow force pushes and
deletions, and disallow bypassing the rules.

### D9 · AI-agent rules

Automated agents must not rewrite pushed history, must not force-push `main`, and
must not push tags. They work on topic branches (or git worktrees), keep each
commit focused and Conventional, and push only their own branch.

### D10 · Remote and legacy history

`origin` is `https://github.com/RonnyWu/Grow.git`. Push only `main`; keep the
local `old` branch local and never publish it. Commands such as `git push --all`
and `git push --mirror` are forbidden because `old` has no common ancestor with
`main`.

## Rationale

- **GitHub Flow over GitFlow**: the artifact consumers install is a tag-pinned
  revision, so a `develop`/release-branch line would never be what UPM clones,
  while adding merge overhead and a second integration point for no benefit.
- **PR even for solo work**: the pull request is the cheapest place to enforce
  Conventional Commits, run Unity compilation/tests, and review AI-generated
  changes before they reach `main`. Direct pushes would remove that checkpoint
  exactly where it is most valuable.
- **Squash**: keeps `main` linear and legible, one logical change per commit, and
  makes the PR title the canonical history entry.
- **Tag-based distribution**: Unity UPM git dependencies resolve to a revision;
  immutable tags give reproducible installs, whereas a branch would drift under
  consumers.
- **One long-lived branch**: minimizes merge conflicts and onboarding cost for a
  small open-source project, and matches GitHub's default review model.

## Consequences

- Positive: simple, linear, reviewable history; a single quality/review gate;
  reproducible tag-pinned installs; low contributor onboarding cost.
- Cost: no staging branch — `main` must always stay releasable; squash discards
  the individual commits inside a PR; each release needs a small
  version/changelog PR before tagging.

## Alternatives considered (rejected)

- **Direct commits to trunk (no PR)**: loses the CI and review gate, which matters
  most for AI-generated and external commits.
- **GitFlow (`develop` / `release` / `hotfix`)**: long-lived branches that are
  never what consumers install; unnecessary for a continuously-releasable package.
- **Per-version release branches**: premature; there are no concurrent
  maintenance lines to support yet.
- **Merge commits or rebase-and-merge as the default**: a non-linear or noisy
  `main`; squash is the default instead.

## Rollout

1. Add `origin` and push only `main` (`git push -u origin main`).
2. Configure the `main` protection rules from D8.
3. Never push `old`; archive it locally if it is no longer needed.

## References

- GitHub Flow: https://docs.github.com/en/get-started/using-github/github-flow
- Trunk-Based Development: https://trunkbaseddevelopment.com/
- Conventional Commits 1.0.0: https://www.conventionalcommits.org/en/v1.0.0/
- Semantic Versioning 2.0.0: https://semver.org/spec/v2.0.0.html
- Keep a Changelog 1.1.0: https://keepachangelog.com/en/1.1.0/
- Unity, Introduction to Git dependencies: https://docs.unity3d.com/Manual/upm-git.html
- GitHub, About protected branches: https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches
- GitHub, About merge methods: https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/about-merge-methods-on-github
- Git worktree: https://git-scm.com/docs/git-worktree
