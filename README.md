# Configuard

Configuard is a .NET CLI for validating configuration contracts across environments before deployment.

Current focus: reliable `validate` behavior with contract-based rules and CI-friendly exit codes.

## Why Configuard

- Prevent config drift between environments.
- Catch missing/forbidden keys before runtime.
- Enforce type and constraint rules from a single contract file.

## Current Status

Implemented:

- CLI command routing (`validate`, `diff`, `explain`)
- Contract loading (`configuard.contract.json`, version `1`)
- `validate` engine for:
  - `requiredIn`, `forbiddenIn`
  - type checks (`string`, `int`, `number`, `bool`, `object`, `array`)
  - constraints (`enum`, length, regex, numeric bounds, array bounds)
  - per-key `sourcePreference` (`appsettings` / `dotenv`)
- Source loading from:
  - `appsettings.json` + `appsettings.{env}.json`
  - optional `.env` + `.env.{env}`
  - optional environment snapshots via `sources.envSnapshot.environmentPattern`
- Output formats for `validate`:
  - `text` (default)
  - `json`
- Unit tests for parser, loader, validator, and formatter

In progress:

- Additional source types beyond `appsettings*`
- SARIF output and richer explain diagnostics

## Quick Start

1) Build:

```bash
dotnet build Configuard.sln
```

2) Run validation:

```bash
dotnet run --project src/Configuard.Cli -- validate --contract examples/quickstart/configuard.contract.json
```

3) Run tests:

```bash
dotnet test Configuard.sln
```

## Install as dotnet tool

Build a local tool package:

```bash
dotnet pack src/Configuard.Cli/Configuard.Cli.csproj -c Release -o artifacts
```

Install from the local package folder:

```bash
dotnet tool install --global Configuard.Cli --add-source ./artifacts
```

Then run:

```bash
configuard --help
```

Pre-release verification:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File .\release-check.ps1
```

## Command Use Cases

### `validate`

Use case: block deployments when contract rules are violated.

```bash
dotnet run --project src/Configuard.Cli -- validate --contract examples/quickstart/configuard.contract.json --env staging --env production
```

Use case: feed machine-readable output into CI tooling.

```bash
dotnet run --project src/Configuard.Cli -- validate --contract examples/quickstart/configuard.contract.json --format json
```

Use case: get compact output in pipelines while keeping exit semantics.

```bash
dotnet run --project src/Configuard.Cli -- validate --contract examples/quickstart/configuard.contract.json --verbosity quiet
```

Exit codes:

- `0`: pass
- `2`: input/contract error
- `3`: policy violations
- `4`: internal error

Warning behavior:

- Warnings (for example unknown `sourcePreference` values) are reported in output.
- Warnings do **not** fail the command by themselves.
- Non-zero exit code is driven by violations/errors, not warnings alone.
- Source loading failures (for example malformed JSON or missing non-optional source files) return input error exit code `2`.

Verbosity levels (all commands):

- `quiet`: suppress command output, keep exit codes
- `normal`: default output
- `detailed`: include extra aggregate details for text output

### `diff`

Use case: detect contract-scoped drift between two environments before deployment.

```bash
dotnet run --project src/Configuard.Cli -- diff --contract examples/quickstart/configuard.contract.json --env staging --env production
```

Use case: emit machine-readable drift details.

```bash
dotnet run --project src/Configuard.Cli -- diff --contract examples/quickstart/configuard.contract.json --env staging --env production --format json
```

### `explain`

Use case: explain how one key resolves and why it passes/fails policy checks.

```bash
dotnet run --project src/Configuard.Cli -- explain --contract examples/quickstart/configuard.contract.json --env production --key ConnectionStrings:Default
```

Use case: inspect the same explanation in structured JSON.

```bash
dotnet run --project src/Configuard.Cli -- explain --contract examples/quickstart/configuard.contract.json --env production --key ConnectionStrings:Default --format json
```

## Contract File

Default contract path: `configuard.contract.json`

Reference docs:

- `docs/configuard/02-contract-format.md`
- `docs/configuard/03-cli-ux.md`
- `docs/configuard/04-mvp-boundaries.md`

## Project Structure

```text
docs/configuard/
  01-competitor-matrix.md
  02-contract-format.md
  03-cli-ux.md
  04-mvp-boundaries.md
  05-phase2-roslyn.md
  implementation-notes.md

src/Configuard.Cli/
  Cli/
  Validation/

tests/Configuard.Cli.Tests/

examples/quickstart/
  configuard.contract.json
  appsettings.json
  appsettings.production.json
```
