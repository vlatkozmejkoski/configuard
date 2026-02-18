param(
    [string]$Root = ".\playground"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$fullRoot = [System.IO.Path]::GetFullPath($Root)
if (-not (Test-Path -Path $fullRoot)) {
    New-Item -ItemType Directory -Path $fullRoot | Out-Null
}

Write-Host "Creating playground at: $fullRoot"
Push-Location $fullRoot
try {
    dotnet new sln -n Playground
    dotnet new webapi -n Sample.Api
    dotnet sln Playground.sln add .\Sample.Api\Sample.Api.csproj

    dotnet new tool-manifest
    dotnet tool install Configuard.Cli

    $contract = @'
{
  "version": "1",
  "environments": ["staging", "production"],
  "sources": {
    "appsettings": {
      "base": "Sample.Api/appsettings.json",
      "environmentPattern": "Sample.Api/appsettings.{env}.json"
    }
  },
  "keys": [
    {
      "path": "Logging:LogLevel:Default",
      "type": "string",
      "requiredIn": ["staging", "production"]
    },
    {
      "path": "ConnectionStrings:Default",
      "type": "string",
      "requiredIn": ["staging", "production"]
    }
  ]
}
'@
    Set-Content -Path ".\configuard.contract.json" -Value $contract -Encoding UTF8

    $stagingSettings = @'
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=sample_staging;"
  }
}
'@
    Set-Content -Path ".\Sample.Api\appsettings.staging.json" -Value $stagingSettings -Encoding UTF8

    $productionSettings = @'
{
  "ConnectionStrings": {
    "Default": "Host=prod;Database=sample_prod;"
  }
}
'@
    Set-Content -Path ".\Sample.Api\appsettings.production.json" -Value $productionSettings -Encoding UTF8

    Write-Host ""
    Write-Host "Playground created. Try:"
    Write-Host "  dotnet tool run configuard validate --contract .\configuard.contract.json --env staging --env production"
    Write-Host "  dotnet tool run configuard diff --contract .\configuard.contract.json --env staging --env production --format json"
    Write-Host "  dotnet tool run configuard explain --contract .\configuard.contract.json --env production --key ConnectionStrings:Default --format json"
    Write-Host "  dotnet tool run configuard discover --path .\Playground.sln --format json --output .\discover-report.json"
}
finally {
    Pop-Location
}
