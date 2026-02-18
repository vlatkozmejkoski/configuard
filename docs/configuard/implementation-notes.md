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

## Step 34: Harden source and release policy for 0.2.0 baseline

### What I changed

- Finalized appsettings source strictness in resolver:
  - `appsettings.base` is now required and missing file is a hard input error
  - `appsettings.{env}.json` remains optional
- Added command-level regression test:
  - `Execute_ValidateMissingRequiredAppSettingsBase_ReturnsInputError`
- Hardened release workflow:
  - added explicit `NUGET_API_KEY` presence check before publish
- Updated README + contract format docs to reflect the finalized behaviors.

### Why

- Prevents silent validations against incomplete source sets when the base appsettings input is absent.
- Makes release pipeline failures explicit and easier to diagnose when repo secrets are not configured.

## Step 35: Start 0.2.x stabilization operations

### What I changed

- Added stabilization plan doc:
  - `docs/configuard/06-0.2x-stabilization.md`
  - defines pilot scope, feedback capture, stability gates, and patch-release policy
- Added structured GitHub issue template:
  - (later simplified to regular issues + label flow in Step 37)
  - captures command, exit code, expected/actual behavior, output snippet, and source context
- Updated README doc links and project structure listing.

### Why

- Translates stabilization intent into a repeatable, trackable operating process.
- Improves signal quality of pilot feedback to speed triage and 0.2.x patch decisions.

## Step 36: Add stabilization label taxonomy and triage automation

### What I changed

- Added label definitions:
  - `.github/labels.yml`
  - includes stabilization, triage, command-area, and release-area labels
- Added label synchronization workflow:
  - `.github/workflows/label-sync.yml`
  - syncs labels from `.github/labels.yml` on push/workflow_dispatch
- Added stabilization triage workflow:
  - `.github/workflows/stabilization-triage.yml`
  - for `stabilization` issues, auto-adds `needs-triage` and appends triage checklist guidance
- Updated README with stabilization automation references.

### Why

- Ensures issue labeling is consistent and low-friction.
- Speeds triage cycles by adding immediate structure to incoming stabilization reports.

## Step 37: Remove dedicated stabilization issue template

### What I changed

- Removed `.github/ISSUE_TEMPLATE/stabilization-feedback.yml`.
- Updated `docs/configuard/06-0.2x-stabilization.md` to use regular issues with `stabilization` label.

### Why

- Keeps contribution flow simpler while still preserving triage structure via labels and automation.

## Step 38: De-scope stabilization ops scaffolding pre-major

### What I changed

- Removed stabilization-specific operational artifacts:
  - `docs/configuard/06-0.2x-stabilization.md`
  - `.github/workflows/stabilization-triage.yml`
  - `.github/workflows/label-sync.yml`
  - `.github/labels.yml`
- Updated README to remove stabilization automation/doc references.

### Why

- Prioritized shipping feature work and product maturity before adding process overhead.
- Keeps repository workflow minimal while pre-major development is still moving quickly.

## Step 39: Remove duplicate unused appsettings resolver

### What I changed

- Removed `src/Configuard.Cli/Validation/AppSettingsResolver.cs`.
- Kept `AppSettingsProvenanceResolver` as the single source-loading implementation used by `validate`, `diff`, and `explain`.

### Why

- Eliminates duplicate loading/flattening logic that was not part of active execution paths.
- Reduces maintenance drift risk and clarifies the canonical source-resolution path.

## Step 40: Add contract semantic validation for key rule conflicts

### What I changed

- Extended `ContractLoader` with semantic key-rule validation:
  - rejects duplicate key identifiers after normalization (`path` + `aliases`, `__` -> `:`)
  - rejects keys that are both required and forbidden in the same environment
  - rejects empty/whitespace normalized key paths and aliases
- Added loader tests for:
  - normalized duplicate path detection
  - alias/path collision detection
  - required/forbidden overlap detection
- Updated contract format docs to clarify uniqueness and overlap constraints.

### Why

- Prevents ambiguous contracts from silently producing non-deterministic behavior.
- Moves conflict detection to load-time for faster and clearer user feedback.

## Step 41: Refactor command orchestration helpers in `CommandHandlers`

### What I changed

- Extracted shared command-layer helpers:
  - verbosity normalization + error output
  - contract loading + error output
  - output format normalization/validation
  - quiet-mode output writer helper
- Updated `validate`, `diff`, and `explain` handlers to use shared helpers instead of repeating the same control flow blocks.

### Why

- Reduces duplicated command orchestration logic and drift risk.
- Makes command handlers easier to read and safer to extend with future commands/formats.

## Step 42: Add source path traversal guard for configured file inputs

### What I changed

- Added root-bound path resolution in `AppSettingsProvenanceResolver`:
  - configured source paths are normalized via `Path.GetFullPath`
  - paths resolving outside the contract directory now fail with input error
- Applied guard to all configured sources:
  - appsettings (`base`, `environmentPattern`)
  - dotenv (`base`, `environmentPattern`)
  - envSnapshot (`environmentPattern`)
- Added command-level regression test:
  - `Execute_ValidateRejectsAppSettingsPathTraversal_ReturnsInputError`
- Updated contract format docs to document root-bound source path requirement.

### Why

- Prevents contract-driven path traversal from reading files outside intended project scope.
- Improves safety and predictability for source loading behavior.

## Step 43: Consolidate shared test utilities

### What I changed

- Added `tests/Configuard.Cli.Tests/TestHelpers.cs` with shared helpers:
  - `CreateTempDirectory()`
  - `ParseJsonElement(...)`
  - `EscapeJsonPath(...)`
- Updated test suites to use shared helpers and removed duplicate helper methods from:
  - `CommandHandlersTests`
  - `ContractLoaderTests`
  - `ContractValidatorTests`
  - `ContractDifferTests`
  - `ExplainEngineTests`

### Why

- Reduces repeated test boilerplate and maintenance overhead.
- Improves consistency of test setup utilities across suites.

## Step 44: Expand command parser edge-case test coverage

### What I changed

- Added parser tests for:
  - empty args (`No command provided`)
  - unknown options
  - missing option value errors
  - case-insensitive command names

### Why

- Locks down high-value parser failure paths and reduces risk of CLI UX regressions.
- Strengthens confidence in command-line contract behavior across common user mistakes.

## Step 45: Expand rule evaluation tests for type and constraint boundaries

### What I changed

- Added `RuleEvaluationTests` coverage for:
  - supported primitive type matching behavior
  - no-issue boundary cases (`minLength`/`maxLength`/`enum`/`pattern`)
  - multi-violation reporting when more than one constraint fails

### Why

- Protects the core policy-evaluation engine with clearer behavioral expectations.
- Reduces risk of regressions in high-impact validation paths.

## Step 46: Introduce explicit validation input exception type

### What I changed

- Added `ValidationInputException` in validation layer.
- Updated source resolution (`AppSettingsProvenanceResolver`) to throw `ValidationInputException` for:
  - missing required source files
  - source parse/read failures
  - out-of-root source path resolution
- Updated command handlers to catch `ValidationInputException` instead of generic `InvalidOperationException`.

### Why

- Improves error taxonomy by separating expected input/load failures from generic runtime exceptions.
- Reduces accidental catch-all behavior and makes command-level error mapping clearer.

## Step 47: Align CI/release workflow build semantics and SDK pinning

### What I changed

- Added `global.json` to pin .NET SDK baseline (`9.0.304`).
- Updated `ci.yml` and `release.yml` to use `setup-dotnet` with `global-json-file`.
- Added explicit `dotnet restore` in release workflow and switched build to `--no-restore` for consistency with CI.

### Why

- Reduces environment drift across local, CI, and release runs.
- Makes workflow behavior more deterministic and easier to reason about.

## Step 48: Enforce release version parity across workflow and project metadata

### What I changed

- Added release workflow guard that compares:
  - resolved release version (`tag`/`workflow_dispatch`)
  - `src/Configuard.Cli/Configuard.Cli.csproj` `<Version>`
- Updated `release-check.ps1` to explicitly report project version during pre-release validation.
- Updated README release automation notes to document version parity enforcement.

### Why

- Prevents accidental publish drift between Git tag version and packaged project version.
- Makes release failures earlier and more actionable.

## Step 49: Remove TOCTOU-style source existence prechecks

### What I changed

- Refactored `AppSettingsProvenanceResolver` to stop using `File.Exists(...)` before file reads.
- Source loading now follows read-and-handle semantics:
  - attempt read/parse directly
  - treat missing-file exceptions as "not found" for optional inputs
  - surface parse/read errors as `ValidationInputException`
- Preserved required-source behavior by explicitly failing when required sources are not found.

### Why

- Reduces race windows between existence checks and reads.
- Simplifies source loading behavior and makes missing-file handling explicit.

## Step 50: Add direct unit tests for provenance resolver behavior

### What I changed

- Added `AppSettingsProvenanceResolverTests` with direct coverage for:
  - missing required appsettings base file
  - missing optional vs required dotenv behavior
  - source path traversal rejection
  - malformed appsettings JSON handling
  - per-source provenance map population (`appsettings`, `dotenv`, `envSnapshot`)

### Why

- Moves critical source-loading/security checks under direct unit coverage instead of relying only on command-level tests.
- Improves confidence in the resolver as the core configuration ingestion boundary.

## Step 51: Harden contract shape invariants in ContractLoader

### What I changed

- Added contract-level invariants in `ContractLoader.TryLoad(...)`:
  - `environments` must contain at least one entry.
  - `keys` must contain at least one key rule.
  - `sources.*.environmentPattern` must include `{env}` for configured `appsettings`, `dotenv`, and `envSnapshot`.
- Expanded `ContractLoaderTests` to cover:
  - empty `environments`
  - empty `keys`
  - missing `{env}` placeholder in each supported source pattern type

### Why

- Rejects structurally valid but semantically unusable contracts early.
- Prevents silent misconfiguration where environment expansion can never happen due to static patterns.

## Step 52: Add release-time packaging metadata guards

### What I changed

- Added `Validate packaging metadata` step in `.github/workflows/release.yml` to fail fast if packaging-critical metadata is missing:
  - `PackAsTool == true`
  - `ToolCommandName` present
  - `PackageReadmeFile` present and exists at repo root
  - `PackageLicenseExpression` present
- Extended `release-check.ps1` with equivalent local preflight checks.

### Why

- Catches NuGet/tool packaging regressions before build-and-publish phases.
- Keeps local release checks aligned with CI/CD release gates.

## Step 53: Validate environment list semantics in ContractLoader

### What I changed

- Added `TryValidateEnvironments(...)` in `ContractLoader` to enforce:
  - no empty/whitespace environment names
  - no duplicates (case-insensitive, after trimming)
- Added `ContractLoaderTests` coverage for:
  - whitespace-only environment entries
  - duplicate environment names differing only by case/spacing

### Why

- Prevents subtle environment routing bugs caused by malformed or repeated environment identifiers.
- Keeps contract semantics consistent with strict key/path validation already enforced elsewhere.

## Step 54: Validate rule environment references against contract environments

### What I changed

- Extended `ContractLoader.TryValidateKeyRules(...)` to validate that each key rule's:
  - `requiredIn` environments are declared in `environments`
  - `forbiddenIn` environments are declared in `environments`
- Added helper validation to reject empty rule environment entries and undeclared environment references with explicit errors.
- Added tests in `ContractLoaderTests` for undeclared references in both `requiredIn` and `forbiddenIn`.

### Why

- Prevents silently ignored policy intent caused by typos or stale environment names in key rules.
- Ensures policy lists are always aligned with the contract's declared environment universe.

## Step 55: Enforce uniqueness in rule environment lists

### What I changed

- Extended `TryValidateRuleEnvironments(...)` in `ContractLoader` to reject duplicates within:
  - `keys[].requiredIn`
  - `keys[].forbiddenIn`
  using trim + case-insensitive normalization.
- Hardened required/forbidden overlap check to compare normalized environment values.
- Added `ContractLoaderTests` for:
  - duplicate values in `requiredIn`
  - duplicate values in `forbiddenIn`
  - overlap detection when one side includes extra whitespace

### Why

- Prevents redundant or ambiguous rule policy lists from being accepted.
- Closes normalization gaps that could hide required/forbidden overlap bugs.

## Step 56: Validate sourcePreference semantics at contract load time

### What I changed

- Added `TryValidateSourcePreference(...)` in `ContractLoader` and enforced it per key:
  - rejects empty `sourcePreference` entries
  - rejects unsupported sources (must be `appsettings`, `dotenv`, or `envsnapshot`)
  - rejects duplicates after trim + case-insensitive normalization
- Added `ContractLoaderTests` for unsupported, duplicate, and empty `sourcePreference` values.

### Why

- Moves source preference contract correctness checks to load-time hard errors rather than runtime warnings.
- Prevents ambiguous source ordering and typo-driven misconfiguration from entering validation flows.

## Step 57: Harden key type and constraints semantics at load time

### What I changed

- Added key type validation in `ContractLoader`:
  - rejects empty `keys[].type`
  - rejects unsupported types (must match v1 supported primitives)
- Added constraints semantic validation in `ContractLoader`:
  - rejects non-object `constraints`
  - rejects inverted bound pairs:
    - `minLength > maxLength`
    - `minimum > maximum`
    - `minItems > maxItems`
- Added `ContractLoaderTests` coverage for each invalid case.

### Why

- Fails malformed contracts early before they reach runtime evaluation.
- Prevents silent no-op constraints and confusing policy behavior due to impossible bounds.

## Step 58: Enforce integer/non-negative and enum-shape constraint invariants

### What I changed

- Extended `ContractLoader` constraints validation to enforce:
  - `minLength`, `maxLength`, `minItems`, `maxItems` are integers
  - these integer constraints are non-negative (`>= 0`)
  - `constraints.enum` is an array and is not empty
- Added `ContractLoaderTests` for:
  - negative `minLength`
  - fractional `maxItems`
  - empty `enum` array

### Why

- Prevents malformed constraints that would otherwise be ignored or misinterpreted at runtime.
- Aligns contract-load semantics more closely with documented JSON schema expectations.

## Step 59: Remove redundant runtime warning path for sourcePreference

### What I changed

- Removed `ContractValidator` contract-warning pass for unknown `sourcePreference` values.
- Removed now-unused `RuleValueResolver.GetInvalidSourcePreferences(...)`.
- Removed legacy validator unit test that asserted unknown-source warnings during runtime validation.
- Updated CLI UX JSON example to show warning-free output for this scenario (`warningCount: 0`), reflecting load-time strict contract rejection.

### Why

- Source preference correctness is now enforced at contract-load time as input errors.
- Eliminates duplicate validation pathways and keeps diagnostics ownership clear (loader vs runtime validator).

## Step 60: Refactor ContractLoader into focused validation components

### What I changed

- Extracted `ContractLoader` semantic checks into focused internal validators:
  - `ContractEnvironmentRulesValidator`
  - `ContractSourceRulesValidator`
  - `ContractKeyRulesValidator`
  - `ContractConstraintRulesValidator`
- Kept all validation behavior and error messages equivalent while reducing method size and responsibility overlap in `ContractLoader`.

### Why

- Improves maintainability and reviewability for a validation surface that grew significantly during hardening slices.
- Makes future rule changes safer by isolating concerns and reducing coupling.

## Step 61: Add command-level regression coverage for strict contract-load failures

### What I changed

- Added `CommandHandlersTests` cases to verify `ExitCodes.InputError` for semantic contract errors across:
  - `validate`
  - `diff`
  - `explain`
- Used invalid `sourcePreference` contract input as the shared strict-load failure vector.

### Why

- Ensures command-level error mapping remains consistent as loader validations evolve.
- Prevents regressions where one command might incorrectly downgrade strict load failures.

## Step 62: Add full valid-contract matrix validation test

### What I changed

- Added `ContractValidatorTests.Validate_ValidContractMatrixAcrossTypesSourcesAndConstraints_Passes`.
- Covers mixed source resolution (`appsettings`, `dotenv`, `envSnapshot`) and representative type/constraint combinations in one success-path scenario.

### Why

- Adds confidence that stricter semantic guards did not break realistic end-to-end validation flows.
- Provides a compact integration-style baseline for future refactors.

## Step 63: Add quickstart contract validity guard test

### What I changed

- Added `ContractLoaderTests.TryLoad_QuickstartExampleContract_IsValid` to ensure `examples/quickstart/configuard.contract.json` remains compatible with current strict load semantics.

### Why

- Prevents documentation/sample drift as semantic rules tighten.
- Keeps quickstart onboarding aligned with real parser/loader behavior.

## Step 64: Cut patch release 0.2.2

### What I changed

- Bumped `src/Configuard.Cli/Configuard.Cli.csproj` version from `0.2.1` to `0.2.2`.
- Updated README with `0.2.2` patch highlights.
- Ran release verification checks and prepared tag-based release flow.

### Why

- Packages accumulated stability/refactor and validation-hardening work into a consumable patch release.

## Step 65: Add key-resolution metadata caching and large-contract regression coverage

### What I changed

- Added `KeyRuleResolutionCache` to cache per-key:
  - normalized candidate paths
  - effective source resolution order
- Updated `RuleValueResolver` and `ExplainEngine` to use cached metadata instead of rebuilding these structures on every resolution call.
- Added `ContractValidatorTests.Validate_LargeContractRegression_ProducesExpectedResult` with a 600-key scenario to guard high-volume behavior and expected result consistency.

### Why

- Reduces repeated normalization/allocation overhead in hot resolution paths used by `validate`, `diff`, and `explain`.
- Adds confidence that large contracts remain stable while performance optimizations are introduced.

## Step 66: Add command-level sourcePreference + envSnapshot resolution regressions

### What I changed

- Added `CommandHandlersTests` coverage for command-layer source-resolution behavior:
  - `diff` uses `sourcePreference: ["envsnapshot", "appsettings"]` and reports drift from snapshot values across environments.
  - `explain` honors `sourcePreference: ["appsettings"]` and does not fall back to `envSnapshot`, producing expected policy failure when required key exists only in snapshot.

### Why

- Validates end-to-end command behavior (exit code mapping and resolution semantics), not only engine-level unit paths.
- Protects against regressions where source precedence is accidentally changed during performance/refactor work.

## Step 67: Add explicit CLI version flag support

### What I changed

- Added `configuard --version`/`-v`/`version` handling in `Program`.
- Added `CliVersionProvider` to centralize display-version retrieval and strip build metadata suffix (`+...`) for clean CLI output.
- Added unit test `CliVersionProviderTests.GetDisplayVersion_ReturnsNonEmptyVersionWithoutBuildMetadata`.
- Updated README install usage snippet to include `configuard --version`.

### Why

- Provides a script-friendly way to verify installed tool version in CI/local debugging.
- Keeps version formatting logic centralized and reusable for future help/banner improvements.

## Step 68: Start Phase 2 tracking in dedicated notes file

### What I changed

- Added dedicated Phase 2 tracking doc: `docs/configuard/phase2-implementation-notes.md`.
- Added rolling "Planned Next Additions" section in that file and recorded first Phase 2 slice there.

### Why

- Keeps v1 stabilization notes and Phase 2 discovery work clearly separated.
- Makes upcoming Phase 2 planning explicit and easier to review incrementally.
