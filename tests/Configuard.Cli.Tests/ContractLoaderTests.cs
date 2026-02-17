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
              "keys": []
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
              "keys": []
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

}
