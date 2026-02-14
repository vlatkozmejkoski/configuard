# Configuard MVP Boundaries (v1)

This document sets strict boundaries for the first release.
It is designed to prevent scope creep and ensure fast delivery.

## Product Goal for v1

Ship a reliable CLI that enforces configuration contract compliance and detects
cross-environment drift before deployment.

## In Scope (Must Have)

1. Contract file support via `configuard.contract.json`.
2. Source loading from:
   - `appsettings.json`
   - `appsettings.{env}.json`
   - optional `.env` and `.env.{env}`
   - optional environment snapshot JSON files
3. Rule enforcement:
   - required/forbidden environment presence
   - primitive type checks
   - basic constraints (`enum`, min/max, pattern)
4. CLI commands:
   - `validate`
   - `diff`
   - `explain`
5. Stable output and exit code semantics for CI.
6. Sensitive value redaction in outputs.

## Out of Scope (Must Not Ship in v1)

1. Automatic contract generation from source code.
2. IDE plugins or Visual Studio extension.
3. Automatic remediation / fixing config files.
4. Direct integrations with cloud secret stores (Key Vault, AWS Secrets Manager, etc.).
5. Runtime middleware/hosting integration.
6. UI dashboard or web app.
7. Policy as code over arbitrary formats beyond defined sources.
8. Multi-repo orchestration and centralized SaaS reporting.

## Target Runtime and Distribution

- CLI implemented for modern .NET (`.NET 8+` target recommended for v1 speed).
- Distributed as a global/local `dotnet tool`.

## Performance Targets

- Small/medium service repo (< 500 config keys): under 2 seconds on dev machine.
- Deterministic outputs independent of file discovery order.

## Security and Safety Constraints

- Never print raw values for keys marked `sensitive`.
- Avoid storing runtime secret values in temporary files.
- Treat unknown source parsing failures as hard errors (non-zero exit).

## Acceptance Criteria

v1 is considered complete when all conditions are met:

1. A team can define a contract and run `validate` in CI with deterministic pass/fail.
2. `diff` reliably reports environment drift for contract-scoped keys.
3. `explain` identifies where a value came from and why a rule passed or failed.
4. Documentation is sufficient for first-time setup in under 15 minutes.

## Future Expansion Triggers (for v2+)

Only start phase 2 work after v1 has:

- at least 3 real projects using it in CI, and
- validated evidence that manual contract authoring is the top friction point.
