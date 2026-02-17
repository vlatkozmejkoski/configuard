# Configuard CLI UX (v1)

This document defines the initial command-line experience for Configuard.

## Principles

- Predictable for CI, readable for humans.
- Fast local feedback, deterministic machine output.
- Minimal command surface in v1.

## Command Surface (v1)

- `configuard validate`
- `configuard diff`
- `configuard explain`

## Global Options

- `--contract <path>`: path to `configuard.contract.json`
- `--env <name>`: target environment (repeatable where supported)
- `--format <text|json|sarif>`: output format (`sarif` only for `validate`)
- `--verbosity <quiet|normal|detailed>`: output level (`quiet` suppresses normal output)
- `--no-color`

## 1) `validate`

Validate contract compliance for one or more environments.

### Usage

```bash
configuard validate --contract ./configuard.contract.json --env staging --env production
```

### Behavior

1. Load contract.
2. Resolve configured sources for each environment.
3. Evaluate required/forbidden presence.
4. Evaluate type and constraint checks.
5. Emit diagnostics grouped by environment and key path.

### Output Modes

- `text`: human summary and grouped failures.
- `json`: machine-consumable diagnostics.
- `sarif`: static-analysis style output for CI annotations.

### Verbosity

- `quiet`: no normal command output; exit code only.
- `normal`: default output.
- `detailed`: text output includes aggregate sections (by code/environment).

### Warning Semantics

- Warnings can be emitted for non-fatal contract issues (for example unknown `sourcePreference` values).
- Warnings appear in text/json/sarif outputs.
- Warnings alone do not change successful exit code.

### Exit Codes

- `0`: validation passed, no violations.
- `2`: contract or input loading/parsing failure.
- `3`: validation violations found (policy failure).
- `4`: unexpected internal error.

## 2) `diff`

Compare resolved configuration across two environments.

### Usage

```bash
configuard diff --contract ./configuard.contract.json --env staging --env production
```

### Behavior

- Resolves effective values per environment.
- Compares keys from contract scope only (v1).
- Categorizes differences:
  - `missing` (exists in one env, not the other)
  - `changed` (both exist, value differs)
  - `typeChanged`

### Output Modes

- `text`
- `json`

### Verbosity

- `quiet`: no normal command output.
- `normal`: default output.
- `detailed`: text output includes aggregate sections (difference counts by kind).

### Exit Codes

- `0`: no differences in contract-scoped keys.
- `2`: contract or input loading/parsing failure.
- `3`: differences found.
- `4`: unexpected internal error.

## 3) `explain`

Show detailed provenance for a single key in one environment.

### Usage

```bash
configuard explain --contract ./configuard.contract.json --env production --key ConnectionStrings:Default
```

### Behavior

- Displays:
  - key path and aliases
  - resolution order used
  - selected source kind and file
  - raw value (redacted if `sensitive`)
  - type/constraint evaluation detail
  - reason for pass/fail if violated

### Output Modes

- `text`
- `json`

### Verbosity

- `quiet`: no normal command output.
- `normal`: default output.
- `detailed`: includes extra diagnostics (`matchedRuleBy`, source order used, candidate paths checked).

### Exit Codes

- `0`: explain succeeded and key passed policy.
- `1`: key not found in contract.
- `2`: contract or input loading/parsing failure.
- `3`: explain succeeded but key failed policy.
- `4`: unexpected internal error.

## Example `validate` Text Output

```text
Configuard validate
Contract: ./configuard.contract.json
Environments: staging, production

[staging] FAIL
  - ConnectionStrings:Default: missing required key
  - Serilog:MinimumLevel:Default: value "Verbose" not in enum [Debug, Information, Warning, Error]

[production] FAIL
  - Features:UseMockPayments: forbidden key is present

Summary: 3 violations across 2 environments
Exit code: 3
```

## Example `validate` JSON Output Shape

```json
{
  "command": "validate",
  "contract": "./configuard.contract.json",
  "result": "fail",
  "summary": { "violationCount": 1, "warningCount": 0 },
  "warnings": [],
  "violations": [
    {
      "environment": "staging",
      "path": "ConnectionStrings:Default",
      "code": "missing_required",
      "message": "Required key not found."
    }
  ]
}
```

## CI Usage Examples

### GitHub Actions

```yaml
- name: Config validation
  run: configuard validate --contract ./configuard.contract.json --env staging --env production --format sarif
```

### Azure DevOps

```yaml
- script: configuard validate --contract ./configuard.contract.json --env production --format json
  displayName: Validate config contract
```

## v1 Non-Goals (CLI)

- No interactive wizard.
- No auto-fix command.
- No remote secret manager fetch by default.
- No project-wide auto-discovery command yet (phase 2).
