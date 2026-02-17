# Configuard Contract Format (`configuard.contract.json`)

This document defines the v1 contract file that drives validation.
The contract is explicit and user-authored in phase 1.

## Design Goals

- Be readable in code review.
- Encode required keys, type constraints, and environment presence rules.
- Support both section-style keys and flattened paths.
- Enable deterministic CI behavior.

## File Name and Location

- Default name: `configuard.contract.json`
- Default lookup path: repository root
- Override via CLI: `--contract <path>`

Contract validity minimums:

- `environments` must contain at least one value.
- `environments` values must be non-empty and unique (case-insensitive, after trimming).
- `keys` must contain at least one key rule.
- `keys[].type` must be one of the supported primitive types.
- `keys[].requiredIn` and `keys[].forbiddenIn` entries must reference declared values from `environments`.
- `keys[].requiredIn` and `keys[].forbiddenIn` values must be unique within each list (case-insensitive, after trimming).
- `keys[].sourcePreference` entries must be non-empty, unique after normalization, and one of: `appsettings`, `dotenv`, `envsnapshot`.
- `keys[].constraints` (when present) must be a JSON object.
- Constraint bound pairs must be ordered: `minLength <= maxLength`, `minimum <= maximum`, `minItems <= maxItems`.
- Integer constraints must be integral and non-negative: `minLength`, `maxLength`, `minItems`, `maxItems`.
- `constraints.enum` must be a non-empty array when specified.

## Core Structure

```json
{
  "$schema": "https://configuard.dev/schema/configuard.contract.v1.json",
  "version": "1",
  "environments": ["development", "staging", "production"],
  "sources": {
    "appsettings": {
      "base": "appsettings.json",
      "environmentPattern": "appsettings.{env}.json"
    },
    "dotenv": {
      "base": ".env",
      "environmentPattern": ".env.{env}",
      "optional": true
    },
    "envSnapshot": {
      "environmentPattern": "snapshots/{env}.json",
      "optional": true
    }
  },
  "keys": []
}
```

## Key Rule Object

Each item in `keys` defines one configuration requirement:

```json
{
  "path": "ConnectionStrings:Default",
  "aliases": ["CONNECTIONSTRINGS__DEFAULT", "DB_CONNECTION_STRING"],
  "type": "string",
  "requiredIn": ["staging", "production"],
  "forbiddenIn": [],
  "sensitive": true,
  "constraints": {
    "minLength": 10,
    "pattern": ".+"
  },
  "sourcePreference": ["dotenv", "appsettings"],
  "description": "Primary database connection string."
}
```

## Supported Primitive Types (v1)

- `string`
- `int`
- `number`
- `bool`
- `object`
- `array`

## Supported Constraints (v1)

- String: `minLength`, `maxLength`, `pattern`, `enum`
- Numeric: `minimum`, `maximum`, `enum`
- Boolean: no extra constraints
- Array: `minItems`, `maxItems`

## Environment Presence Semantics

- `requiredIn`: key must exist and satisfy type/constraints in these environments.
- `forbiddenIn`: key must not be present in these environments.
- If environment is in neither list, key is optional there.
- A key must not list the same environment in both `requiredIn` and `forbiddenIn`.

## Key Identifier Uniqueness

- Each key `path` must be unique after normalization (`__` -> `:`).
- Aliases participate in the same uniqueness set as key paths.
- Path/alias collisions are rejected as contract input errors.

## Source Resolution Semantics

For each environment:

1. Resolve available source maps (`appsettings*`, optional `dotenv*`, optional `envSnapshot`).
2. Resolve each key path with alias fallback.
3. Apply per-key `sourcePreference` when present.
4. If `sourcePreference` is omitted, default order is `envSnapshot`, then `dotenv`, then `appsettings`.
5. Validate final resolved value against type + constraints.
6. Emit source provenance in diagnostics (`resolvedSource`, `resolvedFrom`, `resolvedPath`).

Source file strictness:

- Parse/read failures for configured source files are treated as hard input errors.
- `appsettings.base` is required and must exist for each evaluated command run.
- Missing `appsettings.{env}.json` files are allowed (base-only resolution remains valid).
- `dotenv.optional: false` requires both configured dotenv files (`base` and resolved `environmentPattern`) to exist.
- `envSnapshot.optional: false` requires the resolved snapshot file to exist for each evaluated environment.
- Configured source paths must resolve under the contract directory (path traversal outside is rejected).
- Source `environmentPattern` values must include `{env}` for `appsettings`, `dotenv`, and `envSnapshot`.

## Normalization Rules

- Path separator canonical form is `:`.
- Environment-variable style separators (`__`) are normalized to `:`.
- Key matching is case-insensitive by default in v1.

## JSON Schema (Draft 2020-12) for Contract File

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://configuard.dev/schema/configuard.contract.v1.json",
  "title": "Configuard Contract v1",
  "type": "object",
  "required": ["version", "environments", "sources", "keys"],
  "properties": {
    "$schema": { "type": "string" },
    "version": { "const": "1" },
    "environments": {
      "type": "array",
      "minItems": 1,
      "items": { "type": "string", "minLength": 1 },
      "uniqueItems": true
    },
    "sources": {
      "type": "object",
      "properties": {
        "appsettings": {
          "type": "object",
          "required": ["base", "environmentPattern"],
          "properties": {
            "base": { "type": "string" },
            "environmentPattern": { "type": "string" }
          },
          "additionalProperties": false
        },
        "dotenv": {
          "type": "object",
          "required": ["base", "environmentPattern"],
          "properties": {
            "base": { "type": "string" },
            "environmentPattern": { "type": "string" },
            "optional": { "type": "boolean" }
          },
          "additionalProperties": false
        },
        "envSnapshot": {
          "type": "object",
          "required": ["environmentPattern"],
          "properties": {
            "environmentPattern": { "type": "string" },
            "optional": { "type": "boolean" }
          },
          "additionalProperties": false
        }
      },
      "required": ["appsettings"],
      "additionalProperties": false
    },
    "keys": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["path", "type"],
        "properties": {
          "path": { "type": "string", "minLength": 1 },
          "aliases": {
            "type": "array",
            "items": { "type": "string", "minLength": 1 },
            "uniqueItems": true
          },
          "type": {
            "type": "string",
            "enum": ["string", "int", "number", "bool", "object", "array"]
          },
          "requiredIn": {
            "type": "array",
            "items": { "type": "string", "minLength": 1 },
            "uniqueItems": true
          },
          "forbiddenIn": {
            "type": "array",
            "items": { "type": "string", "minLength": 1 },
            "uniqueItems": true
          },
          "sensitive": { "type": "boolean" },
          "sourcePreference": {
            "type": "array",
            "items": {
              "type": "string",
              "enum": ["appsettings", "dotenv", "envsnapshot"]
            },
            "uniqueItems": true
          },
          "description": { "type": "string" },
          "constraints": {
            "type": "object",
            "properties": {
              "minLength": { "type": "integer", "minimum": 0 },
              "maxLength": { "type": "integer", "minimum": 0 },
              "pattern": { "type": "string" },
              "enum": {
                "type": "array",
                "items": {},
                "minItems": 1
              },
              "minimum": { "type": "number" },
              "maximum": { "type": "number" },
              "minItems": { "type": "integer", "minimum": 0 },
              "maxItems": { "type": "integer", "minimum": 0 }
            },
            "additionalProperties": false
          }
        },
        "additionalProperties": false
      }
    }
  },
  "additionalProperties": false
}
```

## Example Contract for a Typical API

```json
{
  "$schema": "https://configuard.dev/schema/configuard.contract.v1.json",
  "version": "1",
  "environments": ["development", "staging", "production"],
  "sources": {
    "appsettings": {
      "base": "appsettings.json",
      "environmentPattern": "appsettings.{env}.json"
    },
    "dotenv": {
      "base": ".env",
      "environmentPattern": ".env.{env}",
      "optional": true
    },
    "envSnapshot": {
      "environmentPattern": "snapshots/{env}.json",
      "optional": true
    }
  },
  "keys": [
    {
      "path": "ConnectionStrings:Default",
      "type": "string",
      "requiredIn": ["staging", "production"],
      "sensitive": true,
      "constraints": { "minLength": 10 }
    },
    {
      "path": "Features:UseMockPayments",
      "type": "bool",
      "requiredIn": ["development", "staging"],
      "forbiddenIn": ["production"]
    },
    {
      "path": "Serilog:MinimumLevel:Default",
      "type": "string",
      "requiredIn": ["development", "staging", "production"],
      "constraints": { "enum": ["Debug", "Information", "Warning", "Error"] }
    }
  ]
}
```
