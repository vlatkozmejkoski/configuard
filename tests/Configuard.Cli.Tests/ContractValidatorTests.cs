using Configuard.Cli.Validation;
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

}
