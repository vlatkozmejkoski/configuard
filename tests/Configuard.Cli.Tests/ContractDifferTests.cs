using Configuard.Cli.Validation;

namespace Configuard.Cli.Tests;

public sealed class ContractDifferTests
{
    [Fact]
    public void Diff_ReportsMissingAndChangedKeys()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "appsettings.json"), """
            {
              "ConnectionStrings": {
                "Default": "Server=localhost;Database=base;"
              }
            }
            """);

            File.WriteAllText(Path.Combine(tempDir, "appsettings.staging.json"), """
            {
              "ConnectionStrings": {
                "Default": "Server=localhost;Database=staging;"
              }
            }
            """);

            File.WriteAllText(Path.Combine(tempDir, "appsettings.production.json"), """
            {
              "Features": {
                "UseMockPayments": false
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
                    new ContractKeyRule { Path = "ConnectionStrings:Default", Type = "string" },
                    new ContractKeyRule { Path = "Features:UseMockPayments", Type = "bool" }
                ]
            };

            var result = ContractDiffer.Diff(contract, tempDir, "staging", "production");

            Assert.False(result.IsClean);
            Assert.Contains(result.Issues, i => i.Path == "ConnectionStrings:Default" && i.Kind == "changed");
            Assert.Contains(result.Issues, i => i.Path == "Features:UseMockPayments" && i.Kind == "missing");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "configuard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
