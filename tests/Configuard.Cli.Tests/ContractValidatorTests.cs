using Configuard.Cli.Validation;
using System.Text;
using System.Text.Json;
using static Configuard.Cli.Tests.TestHelpers;

namespace Configuard.Cli.Tests;

public sealed class ContractValidatorTests
{
    [Fact]
    public void Validate_ReportsRequiredForbiddenAndConstraintViolations()
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

            File.WriteAllText(Path.Combine(tempDir, "appsettings.production.json"), """
            {
              "Features": {
                "UseMockPayments": true
              }
            }
            """);

            var contract = new ContractDocument
            {
                Version = "1",
                Environments = ["staging", "production"],
                Sources = new ContractSources
                {
                    AppSettings = new AppSettingsSource
                    {
                        Base = "appsettings.json",
                        EnvironmentPattern = "appsettings.{env}.json"
                    }
                },
                Keys =
                [
                    new ContractKeyRule
                    {
                        Path = "ConnectionStrings:Default",
                        Type = "string",
                        RequiredIn = ["staging", "production"],
                        Constraints = ParseJsonElement("""
                        {
                          "minLength": 40
                        }
                        """)
                    },
                    new ContractKeyRule
                    {
                        Path = "Features:UseMockPayments",
                        Type = "bool",
                        ForbiddenIn = ["production"]
                    },
                    new ContractKeyRule
                    {
                        Path = "Service:Port",
                        Type = "int",
                        RequiredIn = ["staging"]
                    }
                ]
            };

            var result = ContractValidator.Validate(contract, tempDir, []);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Issues, i => i.Code == "constraint_minLength" && i.Environment == "staging");
            Assert.Contains(result.Issues, i => i.Code == "forbidden_present" && i.Environment == "production");
            Assert.Contains(result.Issues, i => i.Code == "missing_required" && i.Path == "Service:Port");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_ReportsEnumAndInvalidPatternViolations()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "Serilog": {
                "MinimumLevel": {
                  "Default": "Verbose"
                }
              },
              "Api": {
                "Key": "abc-123"
              }
            }
            """);

            var contract = new ContractDocument
            {
                Version = "1",
                Environments = ["staging"],
                Sources = new ContractSources
                {
                    AppSettings = new AppSettingsSource
                    {
                        Base = "appsettings.json",
                        EnvironmentPattern = "appsettings.{env}.json"
                    }
                },
                Keys =
                [
                    new ContractKeyRule
                    {
                        Path = "Serilog:MinimumLevel:Default",
                        Type = "string",
                        RequiredIn = ["staging"],
                        Constraints = ParseJsonElement("""
                        {
                          "enum": ["Debug", "Information", "Warning", "Error"]
                        }
                        """)
                    },
                    new ContractKeyRule
                    {
                        Path = "Api:Key",
                        Type = "string",
                        RequiredIn = ["staging"],
                        Constraints = ParseJsonElement("""
                        {
                          "pattern": "["
                        }
                        """)
                    }
                ]
            };

            var result = ContractValidator.Validate(contract, tempDir, []);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Issues, i => i.Code == "constraint_enum" && i.Path == "Serilog:MinimumLevel:Default");
            Assert.Contains(result.Issues, i => i.Code == "constraint_pattern_invalid" && i.Path == "Api:Key");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_ReportsNumericAndArrayBoundViolations()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "Service": {
                "Port": 7000
              },
              "Features": {
                "AllowedHosts": ["a", "b", "c"]
              }
            }
            """);

            var contract = new ContractDocument
            {
                Version = "1",
                Environments = ["staging"],
                Sources = new ContractSources
                {
                    AppSettings = new AppSettingsSource
                    {
                        Base = "appsettings.json",
                        EnvironmentPattern = "appsettings.{env}.json"
                    }
                },
                Keys =
                [
                    new ContractKeyRule
                    {
                        Path = "Service:Port",
                        Type = "int",
                        RequiredIn = ["staging"],
                        Constraints = ParseJsonElement("""
                        {
                          "minimum": 1000,
                          "maximum": 6000
                        }
                        """)
                    },
                    new ContractKeyRule
                    {
                        Path = "Features:AllowedHosts",
                        Type = "array",
                        RequiredIn = ["staging"],
                        Constraints = ParseJsonElement("""
                        {
                          "minItems": 1,
                          "maxItems": 2
                        }
                        """)
                    }
                ]
            };

            var result = ContractValidator.Validate(contract, tempDir, []);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Issues, i => i.Code == "constraint_maximum" && i.Path == "Service:Port");
            Assert.Contains(result.Issues, i => i.Code == "constraint_maxItems" && i.Path == "Features:AllowedHosts");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_UsesDotEnvSourcesAndOverridesAppSettings()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "Features": {
                "UseMockPayments": false
              }
            }
            """);

            File.WriteAllText(Path.Combine(tempDir, ".env.production"), """
            FEATURES__USEMOCKPAYMENTS=true
            API__TIMEOUT=30
            """);

            var contract = new ContractDocument
            {
                Version = "1",
                Environments = ["production"],
                Sources = new ContractSources
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
                },
                Keys =
                [
                    new ContractKeyRule
                    {
                        Path = "Features:UseMockPayments",
                        Type = "bool",
                        ForbiddenIn = ["production"]
                    },
                    new ContractKeyRule
                    {
                        Path = "Api:Timeout",
                        Type = "int",
                        RequiredIn = ["production"]
                    }
                ]
            };

            var result = ContractValidator.Validate(contract, tempDir, []);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Issues, i => i.Code == "forbidden_present" && i.Path == "Features:UseMockPayments");
            Assert.DoesNotContain(result.Issues, i => i.Code == "missing_required" && i.Path == "Api:Timeout");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_HonorsSourcePreference_WhenConfiguredPerKey()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "Features": {
              }
            }
            """);

            File.WriteAllText(Path.Combine(tempDir, ".env.production"), """
            FEATURES__USEMOCKPAYMENTS=true
            """);

            var contract = new ContractDocument
            {
                Version = "1",
                Environments = ["production"],
                Sources = new ContractSources
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
                },
                Keys =
                [
                    new ContractKeyRule
                    {
                        Path = "Features:UseMockPayments",
                        Type = "bool",
                        RequiredIn = ["production"],
                        SourcePreference = [SourceKinds.AppSettings]
                    }
                ]
            };

            var result = ContractValidator.Validate(contract, tempDir, []);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Issues, i => i.Code == "missing_required" && i.Path == "Features:UseMockPayments");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_UsesEnvSnapshotSource_AndOverridesOtherSources()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "Features": {
                "UseMockPayments": false
              }
            }
            """);

            File.WriteAllText(Path.Combine(tempDir, ".env.production"), """
            FEATURES__USEMOCKPAYMENTS=false
            """);

            var snapshotsDir = Path.Combine(tempDir, "snapshots");
            Directory.CreateDirectory(snapshotsDir);
            File.WriteAllText(Path.Combine(snapshotsDir, "production.json"), """
            {
              "Features": {
                "UseMockPayments": true
              }
            }
            """);

            var contract = new ContractDocument
            {
                Version = "1",
                Environments = ["production"],
                Sources = new ContractSources
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
                },
                Keys =
                [
                    new ContractKeyRule
                    {
                        Path = "Features:UseMockPayments",
                        Type = "bool",
                        ForbiddenIn = ["production"]
                    }
                ]
            };

            var result = ContractValidator.Validate(contract, tempDir, []);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Issues, i => i.Code == "forbidden_present" && i.Path == "Features:UseMockPayments");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_ValidContractMatrixAcrossTypesSourcesAndConstraints_Passes()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "Api": {
                "Key": "abc-123"
              },
              "Service": {
                "Port": 8080,
                "Ratio": 2.5
              },
              "Features": {
                "UseMockPayments": true,
                "AllowedHosts": ["api.internal"]
              },
              "Metadata": {
                "Owner": "ops"
              }
            }
            """);

            File.WriteAllText(Path.Combine(tempDir, ".env.production"), """
            API__KEY=from-dotenv
            """);

            var snapshotsDir = Path.Combine(tempDir, "snapshots");
            Directory.CreateDirectory(snapshotsDir);
            File.WriteAllText(Path.Combine(snapshotsDir, "production.json"), """
            {
              "Metadata": {
                "Owner": "platform"
              }
            }
            """);

            var contract = new ContractDocument
            {
                Version = "1",
                Environments = ["production"],
                Sources = new ContractSources
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
                },
                Keys =
                [
                    new ContractKeyRule
                    {
                        Path = "Api:Key",
                        Type = "string",
                        RequiredIn = ["production"],
                        SourcePreference = [SourceKinds.DotEnv, SourceKinds.AppSettings],
                        Constraints = ParseJsonElement("""
                        {
                          "minLength": 3,
                          "maxLength": 40,
                          "pattern": "^[a-z\\-]+$"
                        }
                        """)
                    },
                    new ContractKeyRule
                    {
                        Path = "Service:Port",
                        Type = "int",
                        RequiredIn = ["production"],
                        Constraints = ParseJsonElement("""
                        {
                          "minimum": 1,
                          "maximum": 65535
                        }
                        """)
                    },
                    new ContractKeyRule
                    {
                        Path = "Service:Ratio",
                        Type = "number",
                        RequiredIn = ["production"],
                        Constraints = ParseJsonElement("""
                        {
                          "minimum": 0.5,
                          "maximum": 10.0
                        }
                        """)
                    },
                    new ContractKeyRule
                    {
                        Path = "Features:UseMockPayments",
                        Type = "bool",
                        RequiredIn = ["production"]
                    },
                    new ContractKeyRule
                    {
                        Path = "Features:AllowedHosts",
                        Type = "array",
                        RequiredIn = ["production"],
                        Constraints = ParseJsonElement("""
                        {
                          "minItems": 1,
                          "maxItems": 3
                        }
                        """)
                    },
                    new ContractKeyRule
                    {
                        Path = "Metadata",
                        Type = "object",
                        SourcePreference = [SourceKinds.EnvSnapshot, SourceKinds.AppSettings]
                    }
                ]
            };

            var result = ContractValidator.Validate(contract, tempDir, []);

            Assert.True(
                result.IsSuccess,
                $"Issues: {string.Join("; ", result.Issues.Select(i => $"{i.Code}:{i.Path}:{i.Environment}"))}");
            Assert.Empty(result.Issues);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_LargeContractRegression_ProducesExpectedResult()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            const int keyCount = 600;
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"Perf\": {");
            for (var i = 0; i < keyCount; i++)
            {
                var suffix = i == keyCount - 1 ? string.Empty : ",";
                json.AppendLine($"    \"Key{i}\": \"value-{i}\"{suffix}");
            }
            json.AppendLine("  }");
            json.AppendLine("}");
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), json.ToString());

            var keys = new List<ContractKeyRule>(capacity: keyCount + 1);
            for (var i = 0; i < keyCount; i++)
            {
                keys.Add(new ContractKeyRule
                {
                    Path = $"Perf:Key{i}",
                    Type = "string",
                    RequiredIn = ["staging"]
                });
            }

            keys.Add(new ContractKeyRule
            {
                Path = "Perf:MissingKey",
                Type = "string",
                RequiredIn = ["staging"]
            });

            var contract = new ContractDocument
            {
                Version = "1",
                Environments = ["staging"],
                Sources = new ContractSources
                {
                    AppSettings = new AppSettingsSource
                    {
                        Base = "appsettings.json",
                        EnvironmentPattern = "appsettings.{env}.json"
                    }
                },
                Keys = keys
            };

            var result = ContractValidator.Validate(contract, tempDir, []);

            Assert.False(result.IsSuccess);
            Assert.Single(result.Issues);
            var issue = Assert.Single(result.Issues);
            Assert.Equal("missing_required", issue.Code);
            Assert.Equal("Perf:MissingKey", issue.Path);
            Assert.Equal("staging", issue.Environment);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

}
