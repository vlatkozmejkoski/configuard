# Configuard Phase 2 Implementation Notes

This file tracks **Phase 2-only** implementation slices and near-term planned additions.
Phase 1/v1 notes remain in `docs/configuard/implementation-notes.md`.

## Planned Next Additions (Rolling)

1. Add deterministic output tests for larger multi-project layouts.
2. Add solution/project-level scoped discovery mode shortcuts.

## P2 Step 1: Add read-only `discover` CLI command baseline

### What I changed

- Added `discover` command plumbing in CLI parse/dispatch:
  - command name: `discover`
  - options: `--path`, `--output`, `--format json`, `--verbosity`, `--apply`
- Added read-only discovery engine (`DiscoverEngine`) using Roslyn syntax analysis with initial literal-key patterns:
  - `configuration["A:B"]`
  - `configuration.GetValue<T>("A:B")`
  - `configuration.GetSection("A:B")`
  - `Configure<T>(configuration.GetSection("A:B"))`
- Added JSON report model/formatter (`DiscoveryReport`, findings + evidence output).
- Added tests:
  - parser discover options
  - command-level discover output + `--apply` not implemented path
  - discovery engine pattern/evidence merge behavior

### Why

- Establishes Phase 2 architecture with safe, read-only behavior before contract mutation.
- Provides real key-discovery value immediately while keeping false-positive risk bounded to explicit literal patterns.

## P2 Step 2: Expand discovery to options-binding `Bind(...)` flows

### What I changed

- Extended discovery matching for binding APIs:
  - `configuration.Bind("A:B", target)`
  - `AddOptions<T>().Bind(configuration.GetSection("A:B"))`
- Added explicit evidence pattern labels:
  - `Bind(literal)`
  - `Bind(GetSection)`
- Added discovery tests for:
  - newly supported bind patterns
  - evidence pattern classification for the same discovered key path

### Why

- Covers a high-frequency real-world .NET configuration style beyond direct indexer/get-value access.
- Improves discovery utility early while remaining deterministic and read-only.

## P2 Step 3: Add confidence grading for composed path expressions

### What I changed

- Added path-expression resolution for discovery arguments:
  - string literal (`high`)
  - binary string concatenation
  - interpolated string
  - parenthesized expression passthrough
- Added `medium` confidence + note (`Contains unresolved dynamic segment(s).`) when only partial path composition is statically resolvable (e.g. `"Api:" + suffix`).
- Merged confidence/notes into findings deterministically while preserving strongest confidence observed for a discovered key path.
- Added test coverage for composed-expression confidence behavior.

### Why

- Improves discovery usefulness for realistic code that partially composes keys while keeping uncertainty transparent.
- Creates a clear foundation for future confidence expansion (`low`) and safer `--apply` behavior.

## P2 Step 4: Add discover scope filters (`--include` / `--exclude`)

### What I changed

- Added discover command parser support for repeatable:
  - `--include <glob>`
  - `--exclude <glob>`
- Added file-level glob filtering in discovery engine prior to syntax parsing.
- Added tests for:
  - parser include/exclude capture
  - engine include/exclude behavior
  - command-level filtered report output behavior

### Why

- Improves signal quality on large repos by allowing teams to constrain discovery scope.
- Reduces unnecessary parse work by filtering file candidates early.

## P2 Step 5: Deterministic output hardening for discovery reports

### What I changed

- Added a deterministic timestamp injection seam in discovery:
  - `DiscoverEngine.UtcNowProvider` (defaults to `DateTimeOffset.UtcNow`)
  - report generation now reads `GeneratedAtUtc` from the provider
- Added deterministic behavior tests:
  - fixed-time provider test validates repeatable `GeneratedAtUtc`
  - multi-folder ordering test validates stable findings order across runs

### Why

- Makes discovery output easier to snapshot-test and compare in CI.
- Reduces flaky diffs for repeated scans in larger nested repository layouts.

## P2 Step 6: Add safe `discover --apply` contract merge mode

### What I changed

- Implemented `discover --apply` in command handling:
  - requires/uses the contract path (`--contract` or default `configuard.contract.json`)
  - merges only `high` confidence discovered keys
  - never removes existing keys
  - skips discoveries that already match an existing key path or alias
- Contract update behavior:
  - appends new key entries as `{ "path": "...", "type": "string" }`
  - preserves existing contract shape and only mutates the `keys` array
- Added command tests for:
  - high-confidence-only merge behavior
  - alias-aware duplicate prevention

### Why

- Delivers practical Phase 2 value by reducing manual key-entry work while staying conservative.
- Keeps mutation safety high before introducing richer auto-typing or lower-confidence apply modes.

## P2 Step 7: Add `BindConfiguration("A:B")` discovery coverage

### What I changed

- Extended invocation matching to detect `BindConfiguration(...)` key-path usage.
- Reused existing path-resolution logic so literal and partially composed arguments follow the same confidence semantics.
- Added discovery tests for:
  - baseline pattern detection includes `BindConfiguration("Options:Configured")`
  - evidence pattern classification includes `BindConfiguration`

### Why

- Covers another common .NET options-binding API used in production codebases.
- Improves discovery completeness without broadening mutation risk or changing apply rules.

## P2 Step 8: Add explicit `low` confidence for unresolved indirection

### What I changed

- Added a third confidence bucket for discovery findings:
  - `low` when path expressions cannot be statically resolved and are represented as `{expr}`
- Added a dedicated unresolved-indirection note:
  - `Path is unresolved due to runtime indirection.`
- Updated confidence ranking logic to preserve `high > medium > low`.
- Added test coverage for unresolved identifier-based key access producing `low` confidence.

### Why

- Makes uncertainty explicit for dynamic access patterns instead of silently dropping them.
- Provides better operator visibility while keeping `discover --apply` safe (high-confidence-only merge).
