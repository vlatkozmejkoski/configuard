# Configuard

Configuard is a .NET CLI that validates configuration contracts across environments before deployment.

> [!WARNING]
> This project is AI-generated ("vibe-coded"). Review behavior, tests, and outputs before production usage.

## Table of Contents

- [Why Configuard](#why-configuard)
- [Current Status](#current-status)
- [Key Features](#key-features)
- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [Installation](#installation)
- [CLI Reference](#cli-reference)
- [Exit Codes](#exit-codes)
- [Contract File Guide](#contract-file-guide)
- [Discovery (Phase 2)](#discovery-phase-2)
- [CI/CD Integration](#cicd-integration)
- [Developing Locally](#developing-locally)
- [Project Structure](#project-structure)
- [Contributing](#contributing)
- [Troubleshooting](#troubleshooting)
- [Documentation](#documentation)
- [License](#license)

## Why Configuard

Configuard helps teams:

- prevent config drift between environments;
- fail fast on missing/forbidden keys before runtime;
- enforce type and constraint rules from a single contract file;
- integrate config policy checks into CI with machine-readable output.

## Current Status

Implemented commands:

- `validate`
- `diff`
- `explain`
- `discover` (experimental, phase 2)

Current package version in source: `0.3.0`.

## Key Features

- Contract-driven validation from `configuard.contract.json` (version `1`).
- Source resolution from:
  - `appsettings.json` + `appsettings.{env}.json`
  - optional `.env` + `.env.{env}`
  - optional environment snapshots via `envSnapshot.environmentPattern`
- Rule checks:
  - presence (`requiredIn`, `forbiddenIn`)
  - type checks (`string`, `int`, `number`, `bool`, `object`, `array`)
  - constraints (`enum`, regex, length and numeric bounds, array bounds)
  - per-key source priority (`appsettings`, `dotenv`, `envsnapshot`)
- Output formats:
  - `validate`: `text`, `json`, `sarif`
  - `diff`: `text`, `json`
  - `explain`: `text`, `json`
  - `discover`: `json`
- CI-friendly deterministic behavior and explicit non-zero exit codes.

## Requirements

- .NET SDK `9.0.304` or compatible feature band (see `global.json`).
- PowerShell (optional, for the local playground script).

## Quick Start

### 1) Build

```bash
dotnet build Configuard.sln
```

### 2) Validate with the quickstart contract

```bash
dotnet run --project src/Configuard.Cli -- validate --contract examples/quickstart/configuard.contract.json --env staging --env production
```

### 3) Run tests

```bash
dotnet test Configuard.sln
```

## Installation

### Option A: Run from source (recommended for contributors)

```bash
dotnet run --project src/Configuard.Cli -- --help
```

### Option B: Install as a global dotnet tool

Install from NuGet:

```bash
dotnet tool install --global Configuard.Cli
configuard --help
configuard --version
```

### Option C: Install local package from this repository

```bash
dotnet pack src/Configuard.Cli/Configuard.Cli.csproj -c Release -o artifacts
dotnet tool install --global Configuard.Cli --add-source ./artifacts
```

### Option D: Use a local tool manifest (repo/playground style)

```bash
dotnet new tool-manifest
dotnet tool install Configuard.Cli --version 0.3.0
dotnet tool run configuard --help
```

## CLI Reference

### Top-level usage

```text
configuard --version
configuard validate [--contract <path>] [--env <name>] [--format <text|json|sarif>] [--verbosity <quiet|normal|detailed>] [--no-color]
configuard diff [--contract <path>] --env <left> --env <right> [--format <text|json>] [--verbosity <quiet|normal|detailed>] [--no-color]
configuard explain [--contract <path>] --env <name> --key <path> [--format <text|json>] [--verbosity <quiet|normal|detailed>] [--no-color]
configuard discover [--path <path>] [--output <path>] [--format <json>] [--verbosity <quiet|normal|detailed>] [--apply]
```

### Common options

- `--contract <path>`: path to contract file (defaults to `configuard.contract.json`).
- `--env <name>`: target environment (repeatable where supported).
- `--format <...>`: command-specific output format.
- `--verbosity <quiet|normal|detailed>`:
  - `quiet`: suppress normal output (exit code only)
  - `normal`: default behavior
  - `detailed`: includes extra command diagnostics for text output
- `--no-color`: accepted for compatibility/no-color scripting flow.

### `validate`

Validate contract compliance for one or more environments.

Examples:

```bash
configuard validate --contract ./configuard.contract.json --env staging --env production
configuard validate --contract ./configuard.contract.json --format json
configuard validate --contract ./configuard.contract.json --format sarif
configuard validate --contract ./configuard.contract.json --verbosity quiet
```

### `diff`

Compare resolved contract-scoped values between exactly two environments.

Examples:

```bash
configuard diff --contract ./configuard.contract.json --env staging --env production
configuard diff --contract ./configuard.contract.json --env staging --env production --format json
```

### `explain`

Explain resolution/provenance and policy evaluation for one key in one environment.

Examples:

```bash
configuard explain --contract ./configuard.contract.json --env production --key ConnectionStrings:Default
configuard explain --contract ./configuard.contract.json --env production --key ConnectionStrings:Default --format json --verbosity detailed
```

### `discover` (experimental)

Statically scans C# code for configuration key usage and emits JSON findings.

Examples:

```bash
configuard discover --path . --format json --output discover-report.json
configuard discover --path ./MySolution.sln --include "src/**" --exclude "**/bin/**" --exclude "**/obj/**" --output discover-report.json
configuard discover --path . --contract ./configuard.contract.json --apply
```

Additional options:

- `--path <path>` supports:
  - directory
  - single `.cs` file
  - `.csproj` (scans that project directory)
  - `.sln` (scans the solution directory)
- `--output <path>` writes report to file instead of stdout.
- `--include <glob>` / `--exclude <glob>` are repeatable filters.
- `--apply` appends only high-confidence discovered keys to contract, without deleting existing keys.

## Exit Codes

- `0`: success
- `1`: key not found in contract (`explain`)
- `2`: input/contract/source loading error
- `3`: policy failure (violations or diffs found)
- `4`: unexpected internal error

## Contract File Guide

Default file: `configuard.contract.json`

Minimal example:

```json
{
  "version": "1",
  "environments": ["staging", "production"],
  "sources": {
    "appsettings": {
      "base": "appsettings.json",
      "environmentPattern": "appsettings.{env}.json"
    }
  },
  "keys": [
    {
      "path": "ConnectionStrings:Default",
      "type": "string",
      "requiredIn": ["staging", "production"]
    },
    {
      "path": "Features:UseMockPayments",
      "type": "bool",
      "forbiddenIn": ["production"]
    }
  ]
}
```

Validation notes:

- `environments` and `keys` must be non-empty.
- `type` must be one of the supported primitive types.
- `requiredIn`/`forbiddenIn` must only reference declared environments.
- `sourcePreference` values are restricted to `appsettings`, `dotenv`, `envsnapshot`.
- Contract and source parsing problems return exit code `2` (hard input error).

## Discovery (Phase 2)

Discovery currently detects patterns such as:

- `configuration["A:B"]`
- `configuration.GetValue<T>("A:B")`
- `configuration.GetSection("A:B")`
- `services.Configure<T>(configuration.GetSection("A:B"))`
- `configuration.Bind("A:B", target)`
- `services.AddOptions<T>().Bind(configuration.GetSection("A:B"))`
- `services.AddOptions<T>().BindConfiguration("A:B")`

Confidence levels:

- `high`: fully literal path
- `medium`: partially dynamic path (for example `Api:{expr}`)
- `low`: unresolved runtime indirection (`{expr}`)

Safe apply behavior (`discover --apply`):

- adds only `high` confidence findings;
- does not remove existing keys;
- de-duplicates against existing key paths and aliases.

## CI/CD Integration

### Continuous integration

The repository includes `.github/workflows/ci.yml`:

- triggers on pushes to `main` and pull requests;
- restores, builds, and tests in Release mode.

### Release automation

The repository includes `.github/workflows/release.yml`:

- runs on tag push `v*.*.*` or manual dispatch;
- validates package metadata and version consistency;
- packs `Configuard.Cli`;
- publishes to NuGet and GitHub Packages;
- creates a GitHub Release and attaches `.nupkg`.

Release flow:

```bash
git tag vX.Y.Z
git push origin vX.Y.Z
```

## Developing Locally

Typical contributor flow:

```bash
dotnet restore Configuard.sln
dotnet build Configuard.sln -c Release
dotnet test Configuard.sln -c Release
```

Pre-release checks:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\release-check.ps1
```

### Local playground

You can bootstrap a disposable sample solution:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\examples\local-playground\create-sample-solution.ps1 -Root .\playground
cd .\playground
dotnet tool run configuard validate --contract .\configuard.contract.json --env staging --env production
dotnet tool run configuard discover --path .\Playground.sln --format json --output .\discover-report.json
```

See `examples/local-playground/README.md` for full walkthrough.

## Project Structure

```text
src/Configuard.Cli/            # CLI app and core command/validation/discovery logic
tests/Configuard.Cli.Tests/    # unit tests
examples/quickstart/           # small contract + sample appsettings files
examples/local-playground/     # end-to-end local sandbox bootstrap script
docs/configuard/               # design docs and implementation notes
.github/workflows/             # CI and release automation
```

## Contributing

Contributions are welcome.

- Start with `CONTRIBUTING.md` for contributor workflow, testing requirements, and PR checklist.
- Use `general-issue.yml` in `.github/ISSUE_TEMPLATE/` when opening issues.

## Troubleshooting

### Input errors with exit code `2`

Common causes:

- missing `appsettings.base` file;
- malformed JSON source file;
- invalid contract structure (unknown sourcePreference/type/constraint shape);
- source path escaping outside contract directory.

## Documentation

- `docs/configuard/02-contract-format.md` - contract schema and semantics.
- `docs/configuard/03-cli-ux.md` - CLI behavior and output goals.
- `docs/configuard/04-mvp-boundaries.md` - scope boundaries.
- `docs/configuard/05-phase2-roslyn.md` - discovery implementation notes.
- `docs/configuard/phase2-implementation-notes.md` - phase 2 details.

## License

MIT - see `LICENSE`.
