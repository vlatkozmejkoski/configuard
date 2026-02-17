Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

[xml]$csproj = Get-Content "src/Configuard.Cli/Configuard.Cli.csproj"
$projectVersion = $csproj.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($projectVersion)) {
    throw "Project version is missing in src/Configuard.Cli/Configuard.Cli.csproj."
}

$props = $csproj.Project.PropertyGroup | Select-Object -First 1
if ($props.PackAsTool -ne "true") {
    throw "PackAsTool must be true in src/Configuard.Cli/Configuard.Cli.csproj."
}

if ([string]::IsNullOrWhiteSpace($props.ToolCommandName)) {
    throw "ToolCommandName is missing in src/Configuard.Cli/Configuard.Cli.csproj."
}

if ([string]::IsNullOrWhiteSpace($props.PackageReadmeFile)) {
    throw "PackageReadmeFile is missing in src/Configuard.Cli/Configuard.Cli.csproj."
}

if ([string]::IsNullOrWhiteSpace($props.PackageLicenseExpression)) {
    throw "PackageLicenseExpression is missing in src/Configuard.Cli/Configuard.Cli.csproj."
}

if (-not (Test-Path $props.PackageReadmeFile)) {
    throw "PackageReadmeFile '$($props.PackageReadmeFile)' was not found at repository root."
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
