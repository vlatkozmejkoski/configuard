# Configuard Implementation Notes

This file captures incremental implementation decisions, with "what" and "why".

## Step 1: Scaffold solution and CLI

### What I changed

- Created solution `Configuard.sln`.
- Added CLI project `src/Configuard.Cli`.
- Confirmed SDK/runtime baseline (`.NET SDK 9.0.304` available locally).

### Why

- A dedicated CLI project gives us a clean boundary for command UX and CI semantics.
- Starting with a solution early makes future split into `Core` + `Cli` straightforward.

### Trade-off

- We start with a minimal parser (no heavy CLI package yet) to keep v1 explicit and easy to reason about.
- If the command surface grows, we can later switch to a command framework with low migration cost.

## Step 2: Add command routing skeleton

### What I changed

- Replaced `Hello, World` with real CLI entrypoint flow in `src/Configuard.Cli/Program.cs`.
- Added parsing + routing files:
  - `src/Configuard.Cli/Cli/CommandParser.cs`
  - `src/Configuard.Cli/Cli/ParsedCommand.cs`
  - `src/Configuard.Cli/Cli/CommandHandlers.cs`
  - `src/Configuard.Cli/Cli/ExitCodes.cs`
- Wired v1 commands: `validate`, `diff`, `explain`.
- Implemented documented baseline exit codes (`0`, `2`, `4`) and input validation for `explain --key`.

### Why

- This establishes stable command contracts first, so feature work (contract loading, validation engine) can be added behind handlers without changing UX shape.
- Separating parser/handlers/exit-codes now keeps code testable and avoids monolithic `Program.cs`.

### Trade-off

- Parser is intentionally simple and explicit (manual token parsing).
- It is not as feature-rich as a CLI framework, but easier to debug while requirements settle.

## Step 3: Implement first real `validate` engine

### What I changed

- Added contract models and loading:
  - `src/Configuard.Cli/Validation/ContractModels.cs`
  - `src/Configuard.Cli/Validation/ContractLoader.cs`
- Added appsettings resolver and validation engine:
  - `src/Configuard.Cli/Validation/AppSettingsResolver.cs`
  - `src/Configuard.Cli/Validation/ContractValidator.cs`
  - `src/Configuard.Cli/Validation/ValidationModels.cs`
- Updated `validate` handler to:
  - load contract from `--contract` or default `configuard.contract.json`
  - run checks across selected/default environments
  - print pass/fail summary and violation list
  - return policy exit code `3` when rules fail

### Why

- This is the smallest meaningful vertical slice: contract file -> source resolution -> rule evaluation -> CLI result.
- It validates the end-to-end architecture before adding advanced features (constraints, provenance, diff/explain engine).

### Implemented rule scope in this slice

- `requiredIn`
- `forbiddenIn`
- primitive type checks (`string`, `int`, `number`, `bool`, `object`, `array`)
- alias matching with `__` to `:` normalization

### Trade-off

- Current resolver focuses on `appsettings` to keep behavior deterministic and easy to review first.
- `.env`, env snapshots, and richer constraints are intentionally deferred to next slices.

## Step 4: Add constraint checks to `validate`

### What I changed

- Extended `ContractValidator` to evaluate v1 constraints:
  - `enum`
  - `minLength`, `maxLength`, `pattern`
  - `minimum`, `maximum`
  - `minItems`, `maxItems`
- Added clear violation codes/messages (for example `constraint_enum`, `constraint_pattern`).
- Added safe regex handling: invalid regex patterns become validation issues instead of crashing.

### Why

- Constraints are where contract files become truly useful; required/forbidden/type alone are not enough for production guardrails.
- Keeping constraint logic in `ContractValidator` centralizes policy behavior and keeps command handlers simple.

### Trade-off

- Constraint evaluation currently happens only after type match (to reduce noisy multi-error output).
- Advanced coercion (for example string `"true"` to bool) is intentionally not added yet to preserve strictness and predictability.

## Step 5: Introduce unit test project

### What I changed

- Added test project: `tests/Configuard.Cli.Tests`.
- Added project reference to CLI assembly.
- Enabled testing of internal classes by adding:
  - `src/Configuard.Cli/Properties/AssemblyInfo.cs` with `InternalsVisibleTo("Configuard.Cli.Tests")`.
- Added first test suites:
  - `CommandParserTests` for parse success and unknown command failure.
  - `ContractLoaderTests` for missing-file failure and valid-load success.
  - `ContractValidatorTests` for required/forbidden/constraint behavior.

### Why

- Tests now protect the behavior of the core policy pipeline while we continue adding features.
- Using temporary test directories/files keeps tests isolated and independent from repository sample files.

### Trade-off

- Current tests focus on high-value core flow rather than exhaustive edge-case matrices.
- This keeps momentum while still establishing a reliable regression safety net.

## Step 6: Expand constraint edge-case tests

### What I changed

- Added targeted validator tests for constraint edge cases in `ContractValidatorTests`:
  - `enum` mismatch reporting
  - invalid regex pattern reporting (`constraint_pattern_invalid`)
  - numeric bounds (`minimum` / `maximum`)
  - array bounds (`minItems` / `maxItems`)

### Why

- These are the highest-risk policy paths because they combine parsing + type handling + rule logic.
- Edge-case tests make future refactors safer, especially when we add output formatting and `diff`/`explain`.

### Trade-off

- Tests are still focused on deterministic cases rather than large randomized matrices.
- This keeps tests fast and readable while growing coverage where bugs are most likely.

## Step 7: Add `validate --format json` (with tests)

### What I changed

- Added output formatter utility:
  - `src/Configuard.Cli/Validation/ValidateOutputFormatter.cs`
  - supports both text and JSON rendering from a single `ValidationResult`.
- Updated `HandleValidate` to:
  - support `--format text` (default) and `--format json`
  - reject unsupported formats with input error (`2`)
  - derive exit code from validation result (`0` or `3`)
- Added formatter unit tests:
  - `tests/Configuard.Cli.Tests/ValidateOutputFormatterTests.cs`

### Why

- Extracting formatting from command handler keeps command orchestration separate from presentation logic.
- Dedicated formatter tests verify JSON payload shape without brittle console-capture tests.

### Trade-off

- This slice implements JSON output only for `validate`.
- `sarif` and structured output for `diff`/`explain` remain for later slices.

## Step 8: Add concise project README

### What I changed

- Added root `README.md` with:
  - project purpose and current status
  - quick-start build/run/test commands
  - command use cases for `validate`, `diff`, and `explain`
  - validate exit codes
  - links to detailed docs and current project structure

### Why

- A concise README gives contributors immediate orientation without reading all design docs.
- Command use cases make it clear how each command should be used in practice.

### Trade-off

- README intentionally stays short and high-signal.
- Deep design rationale remains in `docs/configuard/*` to avoid duplication and drift.

## Step 9: Implement `diff` command with tests

### What I changed

- Added contract-scoped diff engine:
  - `src/Configuard.Cli/Validation/ContractDiffer.cs`
  - `src/Configuard.Cli/Validation/DiffModels.cs`
- Added diff output formatter (text/json):
  - `src/Configuard.Cli/Validation/DiffOutputFormatter.cs`
- Updated `HandleDiff` in `CommandHandlers`:
  - requires exactly two `--env` values
  - loads contract
  - runs diff and prints text/json output
  - returns `0` when clean, `3` when differences exist, `2` on input/format errors
- Added unit tests:
  - `tests/Configuard.Cli.Tests/ContractDifferTests.cs`
  - `tests/Configuard.Cli.Tests/DiffOutputFormatterTests.cs`

### Why

- This closes the largest command gap in the MVP command surface.
- Keeping diff logic separate from command orchestration mirrors the validate design and improves testability.

### Trade-off

- Diff currently compares contract-scoped resolved values only (intentional MVP boundary).
- Advanced explain/provenance and SARIF support remain future slices.

## Step 10: Implement `explain` command with provenance (and tests)

### What I changed

- Added provenance-aware appsettings resolver:
  - `src/Configuard.Cli/Validation/AppSettingsProvenanceResolver.cs`
- Added explain domain + engine:
  - `src/Configuard.Cli/Validation/ExplainModels.cs`
  - `src/Configuard.Cli/Validation/ExplainEngine.cs`
- Added explain formatter (text/json):
  - `src/Configuard.Cli/Validation/ExplainOutputFormatter.cs`
- Updated command handler:
  - `src/Configuard.Cli/Cli/CommandHandlers.cs`
  - `explain` now requires exactly one `--env`, loads contract, resolves rule + value, and returns:
    - `0` for pass
    - `1` when key is not in contract
    - `2` for input/format/contract errors
    - `3` for policy failures
- Extended contract model with `sensitive` for explain redaction support.

### Why

- `explain` closes the MVP triad (`validate` / `diff` / `explain`) and improves debuggability.
- Provenance (`resolvedFrom`) is crucial for understanding environment layering behavior.

### Trade-off

- Constraint/type helpers are currently duplicated between validator and explain engine.
- A shared rule-evaluation utility can be extracted in a future refactor once behavior stabilizes.

## Step 11: Refactor shared rule evaluation logic (with tests)

### What I changed

- Extracted shared rule logic into:
  - `src/Configuard.Cli/Validation/RuleEvaluation.cs`
  - shared methods:
    - path normalization and candidate-path generation
    - type matching
    - constraint evaluation
- Updated consumers to use the shared helper:
  - `ContractValidator`
  - `ExplainEngine`
  - `ContractDiffer` (candidate path normalization reuse)
- Added targeted unit tests:
  - `tests/Configuard.Cli.Tests/RuleEvaluationTests.cs`

### Why

- Removes duplication between `validate` and `explain`, reducing divergence risk.
- Centralizing rule logic makes future enhancements (new constraints/types) safer and faster.

### Trade-off

- Refactor touches core paths, so regression risk is managed by running full existing suite plus new helper tests.

## Step 12: Add command-level exit code tests

### What I changed

- Added `tests/Configuard.Cli.Tests/CommandHandlersTests.cs` to validate command orchestration behavior:
  - `diff` with invalid env count returns input error (`2`)
  - `explain` with unknown contract key returns key-not-found (`1`)
  - `validate` with unsupported format returns input error (`2`)
- Tests use temporary contract + appsettings files with absolute paths to avoid reliance on process working directory.

### Why

- Internal engine tests are strong, but command-layer tests ensure CLI behavior and exit code contracts are stable.
- This protects automation scenarios where CI depends on exact return codes.

### Trade-off

- These are focused behavior tests, not full console snapshot tests.
- We prioritize deterministic exit semantics over full text output matching.

## Step 13: Move samples to `examples/quickstart` and harden default-path behavior

### What I changed

- Added dedicated example assets under:
  - `examples/quickstart/configuard.contract.json`
  - `examples/quickstart/appsettings.json`
  - `examples/quickstart/appsettings.production.json`
- Updated README quick-start and command examples to reference `examples/quickstart`.
- Added command-level test:
  - `Execute_ValidateUsesDefaultContractPath_WhenFileExistsInCurrentDirectory`
  - verifies default contract path behavior (`configuard.contract.json`) by setting process working directory in a guarded section.

### Why

- Keeps repository root clean and makes examples discoverable.
- Documents realistic command usage without relying on ad-hoc root sample files.
- Protects a subtle but important CLI behavior: default contract path lookup from current working directory.

### Trade-off

- Current-directory mutation in test is global state, so test uses a lock to avoid concurrency issues.
- As CLI grows, dedicated integration-test harness could isolate process-level state even better.

### Follow-up fix in same slice

- During smoke verification, example commands exposed a path-resolution bug:
  relative source files were resolved from process working directory instead of contract file location.
- Fixed by resolving appsettings inputs relative to the contract file directory in command handlers.
- Added test:
  - `Execute_ValidateResolvesAppSettingsRelativeToContractLocation`

## Step 14: Add `validate --format sarif` (with tests)

### What I changed

- Added SARIF formatter support:
  - `ValidateOutputFormatter.ToSarif(...)`
- Updated validate command handling to accept `--format sarif`.
- Added/updated tests:
  - `ToSarif_FailureResult_ContainsRulesAndResults`
  - `Execute_ValidateSarifFormat_IsAccepted`

### Why

- SARIF enables straightforward CI code-scanning integrations and machine-readable diagnostics pipelines.
- Adding tests at formatter and command levels ensures both payload shape and CLI compatibility are covered.

### Trade-off

- SARIF output currently maps issues to contract file location only (no fine-grained file regions yet).
- Region-level mapping can be added later when richer source provenance is introduced.

## Step 15: Add `--verbosity` support (with tests)

### What I changed

- Added global verbosity parsing and normalization:
  - `--verbosity quiet|normal|detailed`
  - new helper: `src/Configuard.Cli/Cli/Verbosity.cs`
- Extended command model and parser:
  - `ParsedCommand` now carries `Verbosity`
  - `CommandParser` reads `--verbosity`
- Updated all command handlers (`validate`, `diff`, `explain`) to:
  - validate verbosity value
  - support `quiet` mode by suppressing non-error output while preserving exit codes
- Added richer detailed text output:
  - validate: grouped counts by code and environment
  - diff: grouped counts by difference kind
- Updated usage/help text and README notes.

### Tests added/updated

- Parser test verifies verbosity parsing.
- Command test verifies unsupported verbosity returns input error.
- Formatter tests verify detailed text sections for validate/diff.

### Why

- Verbosity is important for CI and local debugging ergonomics.
- `quiet` enables clean logs; `detailed` gives deeper diagnosis without changing JSON/SARIF contracts.

### Trade-off

- `detailed` currently enriches text format only; JSON/SARIF remain schema-stable by design.

## Step 16: Add `.env` source support (with tests)

### What I changed

- Extended contract source model with optional dotenv source:
  - `sources.dotenv.base`
  - `sources.dotenv.environmentPattern`
  - `sources.dotenv.optional`
- Added dotenv parser:
  - `src/Configuard.Cli/Validation/DotEnvParser.cs`
  - supports simple `KEY=VALUE` and `export KEY=VALUE` lines
  - normalizes `__` to `:`
  - infers scalar types (`bool`, `int`, `number`, otherwise `string`)
- Updated value resolution to merge sources in this order:
  1. `appsettings.json`
  2. `appsettings.{env}.json`
  3. `.env`
  4. `.env.{env}`
  (later sources override earlier values)
- Updated provenance resolver so explain can show dotenv files as `resolvedFrom`.

### Tests added/updated

- `ContractValidatorTests`:
  - `Validate_UsesDotEnvSourcesAndOverridesAppSettings`
- `ExplainEngineTests`:
  - `TryExplain_ReportsDotEnvProvenance_WhenResolvedFromDotEnv`
- `ContractLoaderTests`:
  - validation error when dotenv source is partially configured

### Why

- `.env` support unlocks common local development workflows and makes command behavior closer to real-world app configuration layering.
- Provenance for dotenv values is essential for explainability and debugging.

### Trade-off

- Dotenv parsing is intentionally conservative (single-line scalar values). Advanced dotenv features can be expanded later as needed.

## Step 17: Add per-key `sourcePreference` resolution (with tests)

### What I changed

- Added contract model support for per-key source ordering:
  - `keys[].sourcePreference` (e.g. `["appsettings"]`, `["dotenv", "appsettings"]`)
- Added source constants and centralized rule value resolver:
  - `SourceKinds`
  - `RuleValueResolver`
- Refactored `validate`, `diff`, and `explain` resolution to use source-aware lookup:
  - each key is resolved using candidate paths + preferred source order
  - default order remains `dotenv` then `appsettings` for backward compatibility

### Tests added/updated

- `ContractValidatorTests`:
  - `Validate_HonorsSourcePreference_WhenConfiguredPerKey`
- `ExplainEngineTests`:
  - `TryExplain_HonorsSourcePreference_ForProvenanceSelection`

### Why

- `sourcePreference` enables deterministic per-key behavior when the same key exists in multiple sources.
- This is especially useful for mixed local/dev setups where dotenv and appsettings coexist.

### Trade-off

- Unknown values in `sourcePreference` are ignored, and fallback defaults apply if the list is effectively empty.
- This keeps behavior robust while avoiding hard failures for minor contract typos.

## Step 18: Expose resolved source kind in explain output (with tests)

### What I changed

- Extended provenance payload model to include `SourceKind` on resolved values.
- Extended `ExplainResult` with `ResolvedSource`.
- Updated explain engine to populate `ResolvedSource` from actual resolver selection.
- Updated explain formatters:
  - text output includes `Resolved source: ...`
  - JSON output includes `resolution.resolvedSource`

### Tests updated

- `ExplainEngineTests` now assert source kind (`appsettings` / `dotenv`) in relevant scenarios.
- `ExplainOutputFormatterTests` assert JSON includes `resolvedSource`.

### Why

- This makes source-preference behavior transparent to users.
- It reduces ambiguity when both appsettings and dotenv provide the same key.

## Step 19: Align design docs with implemented behavior

### What I changed

- Updated `docs/configuard/03-cli-ux.md` to reflect current behavior:
  - verbosity semantics (`quiet|normal|detailed`)
  - output modes and exit codes per command
  - explain policy-failure exit code (`3`)
  - updated validate JSON example shape
- Updated `docs/configuard/02-contract-format.md`:
  - removed unimplemented `envSnapshot` references
  - clarified implemented source resolution semantics
  - documented `sourcePreference` support for `appsettings`/`dotenv`
  - documented `resolvedSource` provenance concept

### Why

- Keeps specification docs trustworthy and aligned with the running implementation.
- Prevents confusion for contributors using docs to build or review future slices.

## Step 20: Add warnings for unknown `sourcePreference` values

### What I changed

- Extended validation result model with warnings:
  - `ValidationWarning`
  - `ValidationResult.Warnings`
- Added warning generation in `ContractValidator` for unknown per-key `sourcePreference` entries.
- Updated validate output formatters to surface warnings:
  - text: `Warnings:` section
  - json: `summary.warningCount` + `warnings[]`
  - sarif: warning-level SARIF results

### Tests added/updated

- `ContractValidatorTests`:
  - warning emitted for unknown `sourcePreference`
- `ValidateOutputFormatterTests`:
  - JSON includes warning summary/details
  - SARIF includes warning rules/results

### Why

- Silent fallback can hide contract mistakes.
- Warning diagnostics keep behavior resilient while making configuration issues visible to users and CI systems.

## Step 21: Document warning semantics in user-facing docs

### What I changed

- Updated `README.md` to clarify warning behavior:
  - warnings are surfaced in output
  - warnings alone do not fail builds
  - exit codes are still driven by violations/errors
- Updated `docs/configuard/03-cli-ux.md`:
  - added `Warning Semantics` section under validate
  - updated JSON output example to include `warningCount` and `warnings[]`

### Why

- This prevents confusion in CI pipelines where users might otherwise interpret warnings as policy failures.
- Keeps CLI UX documentation aligned with implemented warning behavior.

## Step 22: Add optional `envSnapshot` source support (with tests)

### What I changed

- Extended contract source model with optional environment snapshot source:
  - `sources.envSnapshot.environmentPattern`
  - `sources.envSnapshot.optional`
- Updated contract loader validation:
  - requires `sources.envSnapshot.environmentPattern` when `envSnapshot` is configured
- Extended source resolution/provenance pipeline:
  - `AppSettingsProvenanceResolver` now loads JSON snapshot file per env using `environmentPattern`
  - source kind added: `envsnapshot`
  - default source order is now `envsnapshot`, then `dotenv`, then `appsettings`
- Updated source preference support:
  - `sourcePreference` now accepts `envsnapshot`
- Updated docs and README to include environment snapshot usage.

### Tests added/updated

- `ContractLoaderTests`:
  - validation error when envSnapshot source is partially configured
- `ContractValidatorTests`:
  - env snapshot value is resolved and can override lower-priority sources
- `ExplainEngineTests`:
  - explain provenance reports `resolvedSource = envsnapshot` when value comes from snapshot

### Why

- Environment snapshots were listed in MVP boundaries but not implemented.
- This closes the remaining source-loading gap and improves parity with real deployment validation workflows.

### Trade-off

- Snapshot loading currently reuses flat JSON key semantics used for appsettings.
- Optional/required missing-file strictness for sources remains unchanged in this slice.

## Step 23: Enforce hard failures for non-optional source load errors (with tests)

### What I changed

- Hardened source loading in `AppSettingsProvenanceResolver`:
  - throws deterministic errors when non-optional source files are missing
  - wraps source read/parse failures (`JSON`, I/O, access) as hard failures
- Updated command handlers to map source-load failures to input error exit code (`2`) for:
  - `validate`
  - `diff`
  - `explain`
- Scope of strict required-file checks:
  - `dotenv.optional: false` requires both configured dotenv files (`base` and env-specific) to exist
  - `envSnapshot.optional: false` requires env-specific snapshot file to exist

### Tests added

- `CommandHandlersTests`:
  - `Execute_ValidateMissingRequiredDotEnvSource_ReturnsInputError`
  - `Execute_ExplainMissingRequiredEnvSnapshot_ReturnsInputError`

### Why

- Aligns runtime behavior with MVP safety constraints so source load issues cannot silently degrade validation quality.
- Makes CI failures explicit and actionable when required source inputs are missing or malformed.

## Step 24: Expand command-level hard-failure coverage for `diff`

### What I changed

- Added command-layer regression tests to ensure `diff` maps source load failures to input error (`2`):
  - missing required dotenv source files
  - malformed envSnapshot JSON

### Why

- Completes exit-code hardening across all command paths (`validate`, `diff`, `explain`).
- Ensures future refactors keep CI-facing error semantics stable.

## Step 25: Implement `--no-color` global option parsing

### What I changed

- Extended CLI command model with `ParsedCommand.NoColor`.
- Updated parser to accept `--no-color` for all commands.
- Updated `Program` usage text to advertise `--no-color`.

### Tests added

- `CommandParserTests`:
  - `TryParse_NoColorOption_IsAccepted`
  - `TryParse_NoColorOption_Repeated_IsAccepted`

### Why

- CLI UX docs listed `--no-color` as a global option, but parser previously rejected it.
- This aligns runtime behavior with documented command surface and avoids confusing input errors.

### Trade-off

- Colorized output is not currently emitted, so `--no-color` is presently a compatibility/no-op flag.
- Keeping the option now prevents a breaking UX change when color output is introduced later.

## Step 26: Add `dotnet tool` packaging metadata (with pack smoke check)

### What I changed

- Updated `src/Configuard.Cli/Configuard.Cli.csproj` with tool-pack metadata:
  - `PackAsTool=true`
  - `ToolCommandName=configuard`
  - package metadata (`PackageId`, `Version`, `Authors`, `Description`, `PackageTags`)
- Updated README with local tool packaging/install commands.

### Why

- MVP boundaries require distribution as a global/local `dotnet tool`.
- Explicit packaging metadata makes release/build pipelines straightforward and reproducible.

## Step 27: Add NuGet package readme wiring for tool package

### What I changed

- Added package readme metadata in `Configuard.Cli.csproj`:
  - `PackageReadmeFile=README-NUGET.md`
  - packed root `README-NUGET.md` into the NuGet package
- Added `README-NUGET.md` with concise install/command docs for package consumers.

### Why

- `dotnet pack` emitted NuGet warning about missing package readme.
- Including a package readme improves package quality and removes avoidable release-time warnings.

## Step 28: Add one-command release verification script

### What I changed

- Added `release-check.ps1` at repository root.
- Script runs a fail-fast release pipeline:
  - `dotnet build Configuard.sln -c Release`
  - `dotnet test Configuard.sln -c Release --no-build`
  - `dotnet pack src/Configuard.Cli/Configuard.Cli.csproj -c Release --no-build -o artifacts`
- Added README usage example for the script.

### Why

- Reduces manual pre-release drift by standardizing build/test/pack checks in one command.
- Makes release readiness checks faster and repeatable for contributors.

## Step 29: Add MIT license for repo and NuGet package metadata

### What I changed

- Added root `LICENSE` file with MIT terms.
- Updated `Configuard.Cli.csproj` package metadata:
  - `PackageLicenseExpression=MIT`
  - `PackageProjectUrl`
  - `RepositoryUrl`
  - `RepositoryType=git`
- Updated README with explicit license section.

### Why

- Clarifies legal use rights for public GitHub and NuGet consumers.
- Improves package compliance metadata and discoverability for tooling.

## Step 30: Automate GitHub releases and NuGet publish

### What I changed

- Added release workflow:
  - `.github/workflows/release.yml`
- Workflow supports:
  - tag-based releases (`vX.Y.Z`)
  - manual dispatch with explicit `version`
- Pipeline actions:
  - build + test (Release)
  - pack tool package with resolved version
  - publish to NuGet using `NUGET_API_KEY` secret
  - create GitHub Release with autogenerated notes and attach `.nupkg`
- Updated README with setup and usage instructions.

### Why

- Removes manual release friction and reduces mistakes in versioning/publish sequence.
- Standardizes release output so NuGet and GitHub releases stay in sync.

## Step 31: Add explicit AI-generated warning in user-facing docs

### What I changed

- Added visible warning banners to:
  - `README.md`
  - `README-NUGET.md`
- Warning text explicitly states the project/package is AI-generated ("vibe-coded") and should be reviewed before production use.

### Why

- Sets clear expectations for users and maintainers.
- Encourages responsible validation before adoption in critical environments.

## Step 32: Add baseline CI workflow for pull requests and main

### What I changed

- Added `.github/workflows/ci.yml`.
- Workflow runs on:
  - pushes to `main`
  - all pull requests
- Pipeline steps:
  - restore
  - build (Release, no-restore)
  - test (Release, no-build)
- Updated README with a short CI section.

### Why

- Provides fast feedback and regression protection before release tags are cut.
- Keeps release workflow focused on publish/release concerns while CI covers day-to-day validation.

## Step 33: Enrich `explain` diagnostics in detailed verbosity

### What I changed

- Extended `ExplainResult` with diagnostics fields:
  - `MatchedRuleBy` (`path` or `alias`)
  - `SourceOrderUsed`
  - `CandidatePaths`
- Updated explain engine to populate diagnostics for all decision outcomes.
- Updated explain formatter:
  - text: detailed mode prints matched-rule/source-order/candidate-path details
  - json: detailed mode emits `diagnostics` object
- Updated command handler to pass `--verbosity detailed` into explain formatters.

### Tests added/updated

- `ExplainEngineTests`:
  - alias match and diagnostics coverage
- `ExplainOutputFormatterTests`:
  - detailed text diagnostics rendering
  - detailed JSON diagnostics payload

### Why

- Makes explain output more actionable when debugging key resolution behavior.
- Improves transparency for alias matching and source precedence decisions.
