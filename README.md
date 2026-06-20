# Cytrus

Download games from Ankama's content CDN — Dofus, Dofus Retro, Wakfu and the rest — from a terminal or a small desktop app.

Cytrus speaks the same protocol as the official launcher: it reads the binary FlatBuffers manifest for a build, fetches only the byte ranges it needs, and checks every chunk and every file against its SHA‑1 before anything lands on disk. Writes are atomic, so a cancelled or failed download never leaves a half‑written file behind.

It's a .NET 10 rewrite of the original `cytrus-v6` Node tool. The download engine lives in one library that both front‑ends share, so the CLI and the GUI behave identically.

> Unofficial project. Not affiliated with or endorsed by Ankama. Downloads come straight from the public CDN — use it within their terms of service.

## Install

Each [release](../../releases) ships self‑contained binaries for Windows, Linux and macOS (x64 and Apple Silicon). They bundle the runtime, so nothing else needs to be installed.

```sh
# Linux / macOS
tar xzf cytrus-cli-<version>-linux-x64.tar.gz
./cytrus version --game dofus --release dofus3
```

On Windows, unzip `cytrus-cli-<version>-win-x64.zip` and run `cytrus.exe`. The desktop app ships as `cytrus-app-<version>-<platform>` in the same release.

On macOS the binaries are unsigned; clear the quarantine flag once after extracting:

```sh
xattr -dr com.apple.quarantine ./cytrus ./cytrus-gui
```

## Command line

```sh
# Latest version published on a channel
cytrus version --game dofus --release dofus3

# Everything the CDN currently advertises
cytrus versions

# Pull specific files only (glob patterns, repeatable)
cytrus download --game dofus --release dofus3 -o ./out \
  --select "**/Dofus.exe" --select "**/*.d2i"

# A specific historical build
cytrus download --game dofus --release dofus3 --version 6.0_3.1.10.11 -o ./out

# The whole build
cytrus download --game dofus --release dofus3 -o ./out
```

Options for `download`:

| Option | Default | Description |
| --- | --- | --- |
| `-g, --game` | `dofus` | Game id (`dofus`, `retro`, `wakfu`, …) |
| `-p, --platform` | `windows` | `windows`, `darwin`, `linux` |
| `-r, --release` | `main` | Channel: `main`, `beta`, `dofus3`, … |
| `-o, --output` | `./output` | Output directory |
| `-v, --version` | latest | Exact version, e.g. `6.0_3.5.17.26` |
| `-s, --select` | everything | Glob pattern(s) to include |
| `--concurrency` | auto | Max bundles downloaded in parallel |
| `--no-verify` | off | Skip SHA‑1 checks (faster, unsafe) |
| `--force` | off | Redownload files already present |

Re‑running a download skips anything already on disk with the right size and hash; `--force` overrides that.

## Desktop app

`cytrus-gui` is an Avalonia app (MVVM with CommunityToolkit, themed with [ShadUI](https://github.com/accntech/shad-ui)) over the same engine. It lists the advertised versions, lets you pick a channel or type an exact build, choose what to fetch, and follow live byte/file progress. Downloads run off the UI thread and can be cancelled.

```sh
dotnet run --project src/Cytrus.App
```

## How it works

`cytrus.json` at the CDN root is the index of currently published versions per game, platform and release. From a version, Cytrus fetches `…/{game}/releases/{release}/{platform}/{version}.manifest` — a FlatBuffers buffer describing fragments, the files in each, and the bundles that hold their chunks.

Downloading a selection comes down to: resolve which chunks the selected files need, group them by bundle, coalesce adjacent chunks into as few HTTP range requests as possible, then reconstruct each file chunk by chunk. Each chunk is hashed as it's read and the whole file is hashed before it's committed, so corruption is caught immediately rather than at launch time. Bundles are streamed to a temporary store, which keeps memory flat even on a full‑game download.

The schema is compiled to C# at build time by [FlatSharp](https://github.com/jamescourtney/FlatSharp) straight from [`manifest.fbs`](src/Cytrus/FlatBuffers/manifest.fbs) — no external `flatc` needed.

## Layout

```text
src/
  Cytrus/        core library: CDN client, manifest reader, planner, assembler
  Cytrus.Cli/    Spectre.Console command-line front-end  (binary: cytrus)
  Cytrus.App/    Avalonia desktop front-end              (binary: cytrus-gui)
  Cytrus.Tests/  xUnit unit + integration tests
```

Everything behind `src/Cytrus` is split by responsibility and wired through interfaces, so the front‑ends only touch `IGameDownloader` / `ICytrusCdnClient` and `AddCytrus()`.

## Development

Requires the .NET 10 SDK.

```sh
dotnet build Cytrus.slnx -c Release
dotnet test  src/Cytrus.Tests/Cytrus.Tests.csproj --filter "Category!=Integration"
```

The integration suite downloads real files from the CDN and verifies them end to end. It's excluded from the default run and from CI; run it explicitly when you want to exercise the network path:

```sh
dotnet test src/Cytrus.Tests/Cytrus.Tests.csproj --filter "Category=Integration"
```

CI (`.github/workflows/ci.yml`) builds and runs the unit tests on Linux, Windows and macOS. Pushing a `v*` tag triggers `release.yml`, which publishes the self‑contained CLI and app for every platform and attaches the archives to a GitHub release.

## License

MIT — see [LICENSE](LICENSE).
