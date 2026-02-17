using Configuard.Cli.Validation;

namespace Configuard.Cli.Tests;

using static TestHelpers;

public sealed class AppSettingsProvenanceResolverTests
{
    [Fact]
    public void Resolve_MissingRequiredAppSettingsBase_ThrowsValidationInputException()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var sources = new ContractSources
            {
                AppSettings = new AppSettingsSource
                {
                    Base = "appsettings.json",
                    EnvironmentPattern = "appsettings.{env}.json"
                }
            };

            var ex = Assert.Throws<ValidationInputException>(() =>
                AppSettingsProvenanceResolver.Resolve(tempDir, sources, "staging"));

            Assert.Contains("Required appsettings source file not found", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_MissingOptionalDotEnv_DoesNotThrow()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), "{}");
            var sources = new ContractSources
            {
                AppSettings = new AppSettingsSource
                {
                    Base = "appsettings.json",
                    EnvironmentPattern = "appsettings.{env}.json"
                },
                DotEnv = new DotEnvSource
                {
                    Base = ".env",
                    EnvironmentPattern = ".env.{env}",
                    Optional = true
                }
            };

            var resolved = AppSettingsProvenanceResolver.Resolve(tempDir, sources, "staging");

            Assert.Empty(resolved.DotEnv);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_MissingRequiredDotEnv_ThrowsValidationInputException()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), "{}");
            var sources = new ContractSources
            {
                AppSettings = new AppSettingsSource
                {
                    Base = "appsettings.json",
                    EnvironmentPattern = "appsettings.{env}.json"
                },
                DotEnv = new DotEnvSource
                {
                    Base = ".env",
                    EnvironmentPattern = ".env.{env}",
                    Optional = false
                }
            };

            var ex = Assert.Throws<ValidationInputException>(() =>
                AppSettingsProvenanceResolver.Resolve(tempDir, sources, "staging"));

            Assert.Contains("Required dotenv source file not found", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_PathTraversalInConfiguredSource_ThrowsValidationInputException()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var sources = new ContractSources
            {
                AppSettings = new AppSettingsSource
                {
                    Base = "../appsettings.json",
                    EnvironmentPattern = "appsettings.{env}.json"
                }
            };

            var ex = Assert.Throws<ValidationInputException>(() =>
                AppSettingsProvenanceResolver.Resolve(tempDir, sources, "staging"));

            Assert.Contains("resolves outside the contract directory", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_MalformedAppSettingsJson_ThrowsValidationInputException()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), "{ invalid json");
            var sources = new ContractSources
            {
                AppSettings = new AppSettingsSource
                {
                    Base = "appsettings.json",
                    EnvironmentPattern = "appsettings.{env}.json"
                }
            };

            var ex = Assert.Throws<ValidationInputException>(() =>
                AppSettingsProvenanceResolver.Resolve(tempDir, sources, "staging"));

            Assert.Contains("Failed to read source file", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_ProvenanceAndSourceMaps_ArePopulatedPerSource()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "Api": {
                "Key": "from-appsettings"
              }
            }
            """);
            File.WriteAllText(Path.Combine(tempDir, ".env.staging"), """
            API__KEY=from-dotenv
            """);

            var snapshotDir = Path.Combine(tempDir, "snapshots");
            Directory.CreateDirectory(snapshotDir);
            File.WriteAllText(Path.Combine(snapshotDir, "staging.json"), """
            {
              "Api": {
                "Key": "from-snapshot"
              }
            }
            """);

            var sources = new ContractSources
            {
                AppSettings = new AppSettingsSource
                {
                    Base = "appsettings.json",
                    EnvironmentPattern = "appsettings.{env}.json"
                },
                DotEnv = new DotEnvSource
                {
                    Base = ".env",
                    EnvironmentPattern = ".env.{env}",
                    Optional = true
                },
                EnvSnapshot = new EnvSnapshotSource
                {
                    EnvironmentPattern = "snapshots/{env}.json",
                    Optional = true
                }
            };

            var resolved = AppSettingsProvenanceResolver.Resolve(tempDir, sources, "staging");

            Assert.True(resolved.AppSettings.TryGetValue("Api:Key", out var appsettingsValue));
            Assert.Equal(SourceKinds.AppSettings, appsettingsValue.SourceKind);
            Assert.Contains("appsettings.json", appsettingsValue.SourceFile, StringComparison.OrdinalIgnoreCase);

            Assert.True(resolved.DotEnv.TryGetValue("API:KEY", out var dotEnvValue));
            Assert.Equal(SourceKinds.DotEnv, dotEnvValue.SourceKind);
            Assert.Contains(".env.staging", dotEnvValue.SourceFile, StringComparison.OrdinalIgnoreCase);

            Assert.True(resolved.EnvSnapshot.TryGetValue("Api:Key", out var snapshotValue));
            Assert.Equal(SourceKinds.EnvSnapshot, snapshotValue.SourceKind);
            Assert.Contains("snapshots", snapshotValue.SourceFile, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
