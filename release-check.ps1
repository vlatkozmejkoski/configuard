Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

[xml]$csproj = Get-Content "src/Configuard.Cli/Configuard.Cli.csproj"
$projectVersion = $csproj.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($projectVersion)) {
    throw "Project version is missing in src/Configuard.Cli/Configuard.Cli.csproj."
}

Write-Host "==> Release check: version"
Write-Host "Project version: $projectVersion"

Write-Host "==> Release check: build"
dotnet build "Configuard.sln" -c Release

Write-Host "==> Release check: test"
dotnet test "Configuard.sln" -c Release --no-build

Write-Host "==> Release check: pack"
dotnet pack "src/Configuard.Cli/Configuard.Cli.csproj" -c Release --no-build -o "artifacts"

Write-Host "==> Release check complete"
