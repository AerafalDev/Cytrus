# Security Policy

## Supported versions

Cytrus is developed on the `main` branch. Security fixes are applied to `main` and shipped in the next release;
only the **latest release** is supported.

| Version | Supported |
| --- | --- |
| Latest release | ✅ |
| Older releases | ❌ |

## Reporting a vulnerability

**Please do not report security issues in public GitHub issues or pull requests.**

Cytrus downloads files from a remote CDN and writes them to your disk. The security surface that matters most is
therefore **what a malformed or hostile manifest or CDN response could make it do** — for example writing outside
the output directory (path traversal, rooted paths, symlinks), accepting content whose SHA-1 does not match, or
leaving a partially written file behind.

Report privately through either channel:

- **GitHub Security Advisories** — open the repository's **Security → Report a vulnerability** tab to start a
  private advisory (preferred).
- **Email** — <aerafal.github@gmail.com>.

Please include:

- a description of the issue and its impact,
- a minimal repro (OS, architecture, Cytrus version, and the command or input that triggers it),
- the version (or commit) you observed it on.

## What to expect

- We aim to acknowledge a report within a few days.
- We'll confirm the issue, keep you updated as we work on a fix, and credit you in the release notes unless you
  prefer to stay anonymous.
- Once a fix is released, the advisory is published.

Thank you for helping keep Cytrus and its users safe.
