# Security Policy

Grow is a game framework: the code in this repository is installed into other people's Unity
projects and runs inside their games. A vulnerability here reaches further than this repository.

## Supported versions

Grow has no stable release yet. Only the **latest pre-release** on the default branch receives
security fixes. Earlier `3.0.0-exp.*` pre-releases are not patched, and fixes are not
backported: a fix ships as a new pre-release.

## Reporting a vulnerability

**Do not open a public issue, pull request, or discussion for a security problem.**

Use GitHub's private vulnerability reporting:

<https://github.com/RonnyWu/Grow/security/advisories/new>

Please include, as far as you can:

- the affected version, tag, or commit;
- what an attacker gains, and the conditions needed to trigger it;
- a minimal reproduction;
- any fix you would suggest.

We aim to acknowledge a report within **7 days** and to publish a remediation plan within
**30 days**. Because this is an early pre-release, no fix-by date is promised. Disclosure is
coordinated with you, and you are credited in the published advisory unless you ask us not to.

There is no bug bounty.

## Scope

In scope — code published in this repository:

- `Packages/com.ronny.grow` (`Runtime/`, `Editor/`, `Tests/`) and any future package published
  from this repository.

Out of scope:

- the Unity engine, Unity packages, and Unity editor behaviour — report those to Unity;
- a defect in a third-party library that Grow wraps — report it upstream. Only the case where
  Grow's own code uses that library insecurely is in scope;
- games and applications built with Grow, including their assets, save data, and network code;
- findings that require a modified engine, a compromised developer machine, or privileges an
  attacker would already need;
- testing against a game, server, or deployment you do not own or have explicit permission to
  test.
