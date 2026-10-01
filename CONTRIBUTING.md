# Contributing to Cytrus

Thanks for taking the time to contribute! Cytrus downloads games from Ankama's content CDN, from a terminal or a
small desktop app. Issues, bug reports and pull requests are all welcome.

By participating you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

## Getting started

You need the **.NET 10 SDK**. The exact band is pinned in [`global.json`](global.json), so the right SDK is
selected automatically. The FlatBuffers code generator (`FlatSharp.Compiler` 7.9) runs on a **.NET 9 runtime**,
so install that runtime as well.

```sh
git clone https://github.com/AerafalDev/Cytrus.git
cd Cytrus

dotnet build -c Release                                          # build every project
dotnet test  -c Release --filter-not-trait "Category=Integration"   # run the offline test suite
```

Tests marked `[Trait("Category", "Integration")]` download from the live CDN; CI skips them, run them locally
(`--filter-trait "Category=Integration"`) when you touch the download path. The suite uses xUnit v3 on
Microsoft.Testing.Platform, which `global.json` enables for `dotnet test`.

## Layout

- `src/Cytrus` — the download engine shared by both front-ends: CDN client, FlatBuffers manifest, selection,
  planning, chunk fetching, SHA-1 verification and atomic writes.
- `src/Cytrus.Cli` — the `cytrus` command line ([Spectre.Console](https://spectreconsole.net/)).
- `src/Cytrus.App` — the desktop app ([Avalonia](https://avaloniaui.net/)).
- `src/Cytrus.Tests` — the xUnit v3 test suite.

Keep behaviour in the engine: the CLI and the app should stay thin so they behave identically.

## Coding conventions

Style is enforced by [`.editorconfig`](.editorconfig) and the .NET analyzers; please don't fight them.

- **Warnings are errors** — the build runs with `TreatWarningsAsErrors`, so a green build means zero warnings.
- **Never trust the manifest** — every path that comes from a manifest goes through `PathSafety` before it
  touches the disk, and every chunk and file is checked against its SHA-1.

Files are **UTF-8 (no BOM)**, stored with LF line endings and checked out with your platform's native ones (see `.gitattributes`).

## Pull requests

- Branch off `main`; keep each change small and self-contained.
- Write clear, present-tense commit messages — one logical change per commit.
- Make sure `dotnet build -c Release` and `dotnet test -c Release --filter-not-trait "Category=Integration"` are green.
- Describe *what* changed and *why*.

## Reporting bugs

Open an issue with the OS and architecture, the Cytrus version, the exact command (game, platform, release,
version), what you expected and what happened. For security-sensitive reports, follow the
[Security Policy](SECURITY.md) instead of opening a public issue.
