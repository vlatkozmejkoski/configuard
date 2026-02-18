# Configuard Phase 2 Implementation Notes

This file tracks **Phase 2-only** implementation slices and near-term planned additions.
Phase 1/v1 notes remain in `docs/configuard/implementation-notes.md`.

## Planned Next Additions (Rolling)

1. Expand discovery patterns to include `Bind(...)` and `AddOptions<T>().Bind(...)`.
2. Add confidence grading beyond literal-only (`medium` for partial constant composition).
3. Add include/exclude filtering for discovery scope (projects/directories).
4. Add deterministic output tests for larger multi-project layouts.
5. Add `discover --apply` (high-confidence only, never delete existing keys).

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
