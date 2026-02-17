using Configuard.Cli.Validation;
using System.Text.Json;

namespace Configuard.Cli.Tests;

public sealed class ExplainEngineTests
{
    [Fact]
    public void TryExplain_ReturnsPassWithProvenance_WhenRuleSatisfied()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "ConnectionStrings": {
                "Default": "Server=localhost;Database=demo;"
              }
            }
            """);

            var contract = BuildContract(
                new ContractKeyRule
                {
                    Path = "ConnectionStrings:Default",
                    Type = "string",
                    RequiredIn = ["staging"]
                });

            var ok = ExplainEngine.TryExplain(contract, tempDir, "staging", "ConnectionStrings:Default", out var result);

            Assert.True(ok);
            Assert.NotNull(result);
            Assert.True(result!.IsPass);
            Assert.Equal("pass", result.DecisionCode);
            Assert.Equal("ConnectionStrings:Default", result.ResolvedPath);
            Assert.Equal(SourceKinds.AppSettings, result.ResolvedSource);
            Assert.Contains("appsettings.json", result.ResolvedFrom, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryExplain_ReturnsFailure_WhenConstraintFails()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "Api": {
                "Key": "abc"
              }
            }
            """);

            var contract = BuildContract(
                new ContractKeyRule
                {
                    Path = "Api:Key",
                    Type = "string",
                    RequiredIn = ["staging"],
                    Constraints = ParseJsonElement("""
                    {
                      "minLength": 10
                    }
                    """)
                });

            var ok = ExplainEngine.TryExplain(contract, tempDir, "staging", "Api:Key", out var result);

            Assert.True(ok);
            Assert.NotNull(result);
            Assert.False(result!.IsPass);
            Assert.Equal("constraint_minLength", result.DecisionCode);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryExplain_ReturnsFalse_WhenKeyNotInContract()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var contract = BuildContract(
                new ContractKeyRule
                {
                    Path = "Api:Key",
                    Type = "string"
                });

            var ok = ExplainEngine.TryExplain(contract, tempDir, "staging", "Unknown:Key", out var result);

            Assert.False(ok);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryExplain_ReportsDotEnvProvenance_WhenResolvedFromDotEnv()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, ".env.staging"), """
            API__KEY=from-dotenv
            """);

            var contract = BuildContract(
                new ContractKeyRule
                {
                    Path = "Api:Key",
                    Type = "string",
                    RequiredIn = ["staging"]
                },
                includeDotEnv: true);

            var ok = ExplainEngine.TryExplain(contract, tempDir, "staging", "Api:Key", out var result);

            Assert.True(ok);
            Assert.NotNull(result);
            Assert.True(result!.IsPass);
            Assert.Equal(SourceKinds.DotEnv, result.ResolvedSource);
            Assert.Contains(".env.staging", result.ResolvedFrom, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryExplain_HonorsSourcePreference_ForProvenanceSelection()
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

            var contract = BuildContract(
                new ContractKeyRule
                {
                    Path = "Api:Key",
                    Type = "string",
                    RequiredIn = ["staging"],
                    SourcePreference = [SourceKinds.AppSettings]
                },
                includeDotEnv: true);

            var ok = ExplainEngine.TryExplain(contract, tempDir, "staging", "Api:Key", out var result);

            Assert.True(ok);
            Assert.NotNull(result);
            Assert.True(result!.IsPass);
            Assert.Equal(SourceKinds.AppSettings, result.ResolvedSource);
            Assert.Contains("appsettings.json", result.ResolvedFrom, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".env.staging", result.ResolvedFrom, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryExplain_ReportsEnvSnapshotProvenance_WhenResolvedFromEnvSnapshot()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var snapshotsDir = Path.Combine(tempDir, "snapshots");
            Directory.CreateDirectory(snapshotsDir);
            File.WriteAllText(Path.Combine(snapshotsDir, "staging.json"), """
            {
              "Api": {
                "Key": "from-snapshot"
              }
            }
            """);

            var contract = BuildContract(
                new ContractKeyRule
                {
                    Path = "Api:Key",
                    Type = "string",
                    RequiredIn = ["staging"]
                },
                includeDotEnv: false,
                includeEnvSnapshot: true);

            var ok = ExplainEngine.TryExplain(contract, tempDir, "staging", "Api:Key", out var result);

            Assert.True(ok);
            Assert.NotNull(result);
            Assert.True(result!.IsPass);
            Assert.Equal(SourceKinds.EnvSnapshot, result.ResolvedSource);
            Assert.Contains("snapshots", result.ResolvedFrom, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("staging.json", result.ResolvedFrom, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void TryExplain_ReportsAliasRuleMatchAndDiagnostics()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "Api": {
                "Key": "abc-123"
              }
            }
            """);

            var contract = BuildContract(
                new ContractKeyRule
                {
                    Path = "Api:Key",
                    Aliases = ["API__KEY"],
                    Type = "string",
                    RequiredIn = ["staging"]
                });

            var ok = ExplainEngine.TryExplain(contract, tempDir, "staging", "API__KEY", out var result);

            Assert.True(ok);
            Assert.NotNull(result);
            Assert.True(result!.IsPass);
            Assert.Equal("alias", result.MatchedRuleBy);
            Assert.Contains(SourceKinds.AppSettings, result.SourceOrderUsed);
            Assert.Contains("Api:Key", result.CandidatePaths);
            Assert.Contains(result.CandidatePaths, p => string.Equals(p, "API:KEY", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static ContractDocument BuildContract(
        ContractKeyRule key,
        bool includeDotEnv = false,
        bool includeEnvSnapshot = false) =>
        BuildContract([key], includeDotEnv, includeEnvSnapshot);

    private static ContractDocument BuildContract(
        IEnumerable<ContractKeyRule> keys,
        bool includeDotEnv = false,
        bool includeEnvSnapshot = false) =>
        new()
        {
            Version = "1",
            Environments = ["staging"],
            Sources = new ContractSources
            {
                AppSettings = new AppSettingsSource
                {
                    Base = "appsettings.json",
                    EnvironmentPattern = "appsettings.{env}.json"
                },
                DotEnv = includeDotEnv
                    ? new DotEnvSource
                    {
                        Base = ".env",
                        EnvironmentPattern = ".env.{env}",
                        Optional = true
                    }
                    : null,
                EnvSnapshot = includeEnvSnapshot
                    ? new EnvSnapshotSource
                    {
                        EnvironmentPattern = "snapshots/{env}.json",
                        Optional = true
                    }
                    : null
            },
            Keys = [.. keys]
        };

    private static JsonElement ParseJsonElement(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "configuard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
