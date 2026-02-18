# Configuard Phase 2 Implementation Notes

This file tracks **Phase 2-only** implementation slices and near-term planned additions.
Phase 1/v1 notes remain in `docs/configuard/implementation-notes.md`.

## Planned Next Additions (Rolling)

1. Add deterministic output tests for larger multi-project layouts.
2. Add `discover --apply` (high-confidence only, never delete existing keys).
3. Evaluate `BindConfiguration("A:B")` and related options-binding API variants.
4. Expand confidence levels to include explicit `low` bucket for unresolved indirection.
5. Add solution/project-level scoped discovery mode shortcuts.

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
