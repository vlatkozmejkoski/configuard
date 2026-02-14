# Configuard Competitor Matrix

This matrix compares representative tools adjacent to Configuard's problem space.
The goal is to make differentiation explicit before implementation.

## Legend

- `Yes`: first-class capability
- `Partial`: possible with custom workarounds
- `No`: not part of normal workflow

## Feature Matrix

| Tool | Category | Config Source Support (`appsettings*`, `.env`, env snapshot) | Cross-Environment Drift Check | Contract-Driven Validation | Static Key Discovery from Code | CI-Friendly Exit Codes | Notes |
|---|---|---|---|---|---|---|---|
| `dotenv.net` | `.env` loader | Partial (`.env`) | No | Partial (required keys via app logic) | No | Partial | Good loader, not a parity validator. |
| `DotEnv.Core` | `.env` loader + validator | Partial (`.env`) | No | Partial (required env keys) | No | Partial | Helpful for env loading; no appsettings/environment diff model. |
| `ConfigurationValidation.AspNetCore` | Runtime ASP.NET validation | Partial (`IOptions`-based sections) | No | Yes (runtime validity) | No | Partial | Runtime-only checks; no pre-deploy parity checks across files/environments. |
| `ConfigurationValidator` (InFurSecDen) | Runtime builder extension | Partial | No | Yes (JSON rule file) | No | Partial | Rule-based at configuration build time; not cross-environment aware. |
| `Json.NET Schema` / `Microsoft.Json.Schema.Validation` | JSON schema validator | Partial (`json` files only) | No | Yes (schema) | No | Yes | Strong for schema validation, not .NET config semantics or drift analysis. |
| `AppSettingsMerger` | File layering helper | Yes (`appsettings*`) | Partial (indirect by merge behavior) | No | No | Partial | Optimization/normalization utility, not policy enforcement. |
| `.NET` `ValidateOnStart` | Built-in options validation | Partial | No | Yes (runtime with annotations/custom) | No | Partial | Good fail-fast pattern, but app runtime scope only. |
| `NotNot.AppSettings` / appsettings source generators | Type-safe config access | Yes (`appsettings*`) | No | Partial (typing) | Partial (generation, not dependency intent) | Partial | Great DX for typing; does not validate environment parity contracts. |

## Missing Capability Intersection (The Gap)

No reviewed OSS tool offers all of the following in one coherent workflow:

1. Discover config dependencies from code (or strongly assist with it).
2. Validate against an explicit contract with environment-specific rules.
3. Compare parity/drift across multiple deployment environments pre-runtime.
4. Produce deterministic CI results and human-friendly explain output.

That intersection is Configuard's primary differentiation target.

## Positioning Statement

Configuard is not another `.env` loader and not another runtime options validator.
It is a deployment-safety toolchain for configuration parity and contract compliance
before code reaches production.

## Candidate Personas

1. Solo developer shipping one service to multiple environments.
2. Small team running CI/CD with frequent environment-specific overrides.
3. Enterprise platform team enforcing configuration policy across many services.
