using Configuard.Cli.Validation;
using static Configuard.Cli.Tests.TestHelpers;

namespace Configuard.Cli.Tests;

public sealed class ContractLoaderTests
{
    [Fact]
    public void TryLoad_MissingFile_ReturnsInputError()
    {
        var ok = ContractLoader.TryLoad("does-not-exist.contract.json", out var contract, out var error);

        Assert.False(ok);
        Assert.Null(contract);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryLoad_ValidContract_LoadsVersionAndKeys()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging", "production"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "ConnectionStrings:Default",
                  "type": "string",
                  "requiredIn": ["staging", "production"]
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out var contract, out var error);

            Assert.True(ok);
            Assert.Null(error);
            Assert.NotNull(contract);
            Assert.Equal("1", contract!.Version);
            Assert.Single(contract.Keys);
            Assert.Equal("ConnectionStrings:Default", contract.Keys[0].Path);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_DotEnvConfiguredWithoutPattern_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                },
                "dotenv": {
                  "base": ".env"
                }
              },
              "keys": [
                { "path": "Api:Key", "type": "string" }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("sources.dotenv.base and sources.dotenv.environmentPattern", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_EnvSnapshotConfiguredWithoutPattern_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                },
                "envSnapshot": {
                  "optional": true
                }
              },
              "keys": [
                { "path": "Api:Key", "type": "string" }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("sources.envSnapshot.environmentPattern", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_DuplicateKeyPathAfterNormalization_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                { "path": "Api:Key", "type": "string" },
                { "path": "API__KEY", "type": "string" }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("Duplicate key path or alias", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_AliasCollidesWithAnotherKeyPath_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                { "path": "Api:Key", "type": "string", "aliases": ["MY_API_KEY"] },
                { "path": "MY_API_KEY", "type": "string" }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("Duplicate key path or alias", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyRequiredAndForbiddenInSameEnvironment_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": ["staging"],
                  "forbiddenIn": ["staging"]
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("both required and forbidden", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_EmptyEnvironments_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": [],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                { "path": "Api:Key", "type": "string" }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("at least one environment", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_EmptyKeys_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": []
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("at least one key rule", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_AppSettingsPatternWithoutEnvPlaceholder_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.production.json"
                }
              },
              "keys": [
                { "path": "Api:Key", "type": "string" }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("sources.appsettings.environmentPattern must include '{env}'", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_DotEnvPatternWithoutEnvPlaceholder_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                },
                "dotenv": {
                  "base": ".env",
                  "environmentPattern": ".env.production"
                }
              },
              "keys": [
                { "path": "Api:Key", "type": "string" }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("sources.dotenv.environmentPattern must include '{env}'", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_EnvSnapshotPatternWithoutEnvPlaceholder_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                },
                "envSnapshot": {
                  "environmentPattern": "snapshots/staging.json",
                  "optional": true
                }
              },
              "keys": [
                { "path": "Api:Key", "type": "string" }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("sources.envSnapshot.environmentPattern must include '{env}'", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_EnvironmentWhitespaceOnly_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging", "   "],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                { "path": "Api:Key", "type": "string" }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("environments[] values must not be empty or whitespace", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_DuplicateEnvironmentsAfterTrimAndCaseFold_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging", " Staging "],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                { "path": "Api:Key", "type": "string" }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("Duplicate environment", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyRequiredInReferencesUndeclaredEnvironment_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": ["production"]
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("references undeclared environment 'production' in 'requiredIn'", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyForbiddenInReferencesUndeclaredEnvironment_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "forbiddenIn": ["production"]
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("references undeclared environment 'production' in 'forbiddenIn'", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyRequiredInContainsDuplicatesAfterTrimAndCaseFold_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging", "production"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": ["staging", " Staging "]
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("duplicate environment 'staging' in 'requiredIn'", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyForbiddenInContainsDuplicatesAfterTrimAndCaseFold_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging", "production"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "forbiddenIn": ["production", " PRODUCTION "]
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("duplicate environment 'PRODUCTION' in 'forbiddenIn'", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyRequiredAndForbiddenOverlapAfterTrim_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging", "production"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "requiredIn": [" staging "],
                  "forbiddenIn": ["staging"]
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("both required and forbidden in environment 'staging'", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeySourcePreferenceContainsUnsupportedValue_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "sourcePreference": ["custom"]
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("unsupported sourcePreference 'custom'", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeySourcePreferenceContainsDuplicatesAfterTrimAndCaseFold_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "sourcePreference": ["dotenv", " DotEnv "]
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("duplicate sourcePreference 'dotenv'", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeySourcePreferenceContainsEmptyValue_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "sourcePreference": [" "]
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("empty 'sourcePreference' entry", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyTypeUnsupported_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "guid"
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("unsupported type 'guid'", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyConstraintsNonObject_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "constraints": [1, 2, 3]
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("invalid 'constraints' shape", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyConstraintsMinLengthGreaterThanMaxLength_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "constraints": {
                    "minLength": 10,
                    "maxLength": 3
                  }
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("'minLength' cannot be greater than 'maxLength'", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyConstraintsMinimumGreaterThanMaximum_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Service:Port",
                  "type": "number",
                  "constraints": {
                    "minimum": 100,
                    "maximum": 10
                  }
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("'minimum' cannot be greater than 'maximum'", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyConstraintsMinItemsGreaterThanMaxItems_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Features:AllowedHosts",
                  "type": "array",
                  "constraints": {
                    "minItems": 5,
                    "maxItems": 1
                  }
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("'minItems' cannot be greater than 'maxItems'", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyConstraintsNegativeMinLength_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Api:Key",
                  "type": "string",
                  "constraints": {
                    "minLength": -1
                  }
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("invalid 'minLength' constraint. Value must be >= 0", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyConstraintsFractionalMaxItems_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Features:AllowedHosts",
                  "type": "array",
                  "constraints": {
                    "maxItems": 2.5
                  }
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("invalid 'maxItems' constraint. Expected an integer", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryLoad_KeyConstraintsEnumMustBeNonEmptyArray_ReturnsError()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contractPath = Path.Combine(tempDir, "configuard.contract.json");
            File.WriteAllText(contractPath, """
            {
              "version": "1",
              "environments": ["staging"],
              "sources": {
                "appsettings": {
                  "base": "appsettings.json",
                  "environmentPattern": "appsettings.{env}.json"
                }
              },
              "keys": [
                {
                  "path": "Serilog:MinimumLevel:Default",
                  "type": "string",
                  "constraints": {
                    "enum": []
                  }
                }
              ]
            }
            """);

            var ok = ContractLoader.TryLoad(contractPath, out _, out var error);

            Assert.False(ok);
            Assert.Contains("invalid 'enum' constraint. Array must not be empty", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

}
