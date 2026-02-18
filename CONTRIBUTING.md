# Contributing to Configuard

Thanks for contributing to Configuard.

This guide is for human contributors: how to set up locally, make changes, test them, and submit high-quality pull requests.

## Table of Contents

- [Ways to Contribute](#ways-to-contribute)
- [Ground Rules](#ground-rules)
- [Local Setup](#local-setup)
- [Development Workflow](#development-workflow)
- [Pull Request Requirements](#pull-request-requirements)
- [Commit Message Guidance](#commit-message-guidance)
- [Testing Expectations](#testing-expectations)
- [Documentation Expectations](#documentation-expectations)
- [Release Notes Guidance](#release-notes-guidance)
- [Security Reporting](#security-reporting)

## Ways to Contribute

- Bug reports
- Bug fixes
- Tests
- Documentation improvements
- Small scoped features
- Discovery pattern enhancements

For larger feature work, open an issue first and wait for scope confirmation.

## Ground Rules

- Keep changes focused and reviewable.
- Avoid unrelated refactors in the same PR.
- Add or update tests for behavior changes.
- Keep docs in sync when CLI behavior changes.
- Be respectful and collaborative in discussions.

## Local Setup

Prerequisites:

- .NET SDK `9.0.304` (or compatible feature band from `global.json`)

Setup and verify:

```bash
dotnet restore Configuard.sln
dotnet build Configuard.sln -c Release
dotnet test Configuard.sln -c Release
```

Optional local CLI run:

```bash
dotnet run --project src/Configuard.Cli -- --help
```

## Development Workflow

1. Create or pick an issue.
2. Create a branch from `main`.
3. Implement the smallest complete fix.
4. Add/update tests.
5. Run build and tests locally.
6. Update docs (`README-updated.md`, docs under `docs/configuard/`, or both).
7. Open a PR linked to the issue.

Recommended branch naming:

- `fix/<issue-id>-short-description`
- `feat/<issue-id>-short-description`
- `docs/<issue-id>-short-description`
- `chore/<issue-id>-short-description`

## Pull Request Requirements

Every PR should include:

- linked issue (`Closes #<id>` when appropriate);
- summary of what changed and why;
- test evidence (commands + results);
- notes on any trade-offs or follow-up work.

PR checklist (copy into description):

```text
## PR Checklist
- [ ] Linked issue
- [ ] Scoped change only (no unrelated refactors)
- [ ] Tests added/updated
- [ ] `dotnet build Configuard.sln -c Release` passes
- [ ] `dotnet test Configuard.sln -c Release` passes
- [ ] Docs updated (or N/A explained)
```

## Commit Message Guidance

Prefer concise, descriptive commit messages in imperative mood.

Examples:

- `fix discover include/exclude filtering for solution paths`
- `add tests for contract sourcePreference validation`
- `docs clarify validate exit code behavior`

Conventional commit prefixes are welcome but optional (`fix:`, `feat:`, `docs:`, `test:`, `chore:`).

## Testing Expectations

Minimum before opening a PR:

```bash
dotnet build Configuard.sln -c Release
dotnet test Configuard.sln -c Release
```

For behavior changes, add or update targeted tests under:

- `tests/Configuard.Cli.Tests/`

## Documentation Expectations

Update documentation when behavior or UX changes:

- `README-updated.md` for user-facing usage
- `docs/configuard/*.md` for deeper design/format details
- examples under `examples/` when command flows change

## Release Notes Guidance

If your change affects users, include a short release-note style line in the PR description:

- `User impact: <one sentence>`

Example:

- `User impact: discover --apply now de-duplicates against aliases before appending new keys.`

## Security Reporting

Do not open public issues for suspected security vulnerabilities.

Until a dedicated security contact is added, open a private channel with maintainers first and avoid posting exploit details publicly.
