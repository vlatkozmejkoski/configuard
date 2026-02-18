# Local Playground

Use this example to create a throwaway .NET solution, install Configuard as a local tool, and run the core commands end-to-end.

## 1) Bootstrap a sample solution

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\examples\local-playground\create-sample-solution.ps1 -Root .\playground
```

This creates:

- `playground/Playground.sln`
- `playground/Sample.Api/` (web API project)
- `playground/configuard.contract.json`
- environment files for `staging` and `production`
- local tool manifest with `Configuard.Cli` installed

## 2) Run Configuard commands

```powershell
cd .\playground

dotnet tool run configuard validate --contract .\configuard.contract.json --env staging --env production
dotnet tool run configuard diff --contract .\configuard.contract.json --env staging --env production --format json
dotnet tool run configuard explain --contract .\configuard.contract.json --env production --key ConnectionStrings:Default --format json
dotnet tool run configuard discover --path .\Playground.sln --format json --output .\discover-report.json
```

Optional safe apply flow (high-confidence additions only):

```powershell
dotnet tool run configuard discover --path .\Playground.sln --contract .\configuard.contract.json --apply
```

## 3) Inspect outputs

- `discover-report.json` contains discovered keys + confidence + evidence.
- `configuard.contract.json` is updated only when `--apply` is used.
