using Configuard.Cli.Discovery;
using static Configuard.Cli.Tests.TestHelpers;

namespace Configuard.Cli.Tests;

public sealed class DiscoverEngineTests
{
    [Fact]
    public void Discover_FindsKeyPathsFromSupportedPatterns()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Sample.cs"), """
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Configuration;

            public class Sample
            {
                public void A(IConfiguration configuration)
                {
                    var a = configuration["ConnectionStrings:Default"];
                    var b = configuration.GetValue<string>("Api:Timeout");
                    var c = configuration.GetSection("Features:Flags");
                }

                public void B(IServiceCollection services, IConfiguration configuration)
                {
                    services.Configure<DemoOptions>(configuration.GetSection("Demo:Section"));
                }

                public void C(IServiceCollection services, IConfiguration configuration, DemoOptions options)
                {
                    services.AddOptions<DemoOptions>().Bind(configuration.GetSection("Options:Bound"));
                    configuration.Bind("Options:Literal", options);
                }
            }
            """);

            var report = DiscoverEngine.Discover(tempDir);

            Assert.Equal("1", report.Version);
            Assert.Contains(report.Findings, finding => finding.Path == "ConnectionStrings:Default");
            Assert.Contains(report.Findings, finding => finding.Path == "Api:Timeout");
            Assert.Contains(report.Findings, finding => finding.Path == "Features:Flags");
            Assert.Contains(report.Findings, finding => finding.Path == "Demo:Section");
            Assert.Contains(report.Findings, finding => finding.Path == "Options:Bound");
            Assert.Contains(report.Findings, finding => finding.Path == "Options:Literal");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Discover_MergesDuplicatePathEvidenceDeterministically()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "A.cs"), """
            using Microsoft.Extensions.Configuration;
            public class A { public void Run(IConfiguration c) { var x = c["Api:Key"]; } }
            """);
            File.WriteAllText(Path.Combine(tempDir, "B.cs"), """
            using Microsoft.Extensions.Configuration;
            public class B { public void Run(IConfiguration c) { var x = c.GetSection("Api:Key"); } }
            """);

            var report = DiscoverEngine.Discover(tempDir);

            var finding = Assert.Single(report.Findings, f => f.Path == "Api:Key");
            Assert.Equal(2, finding.Evidence.Count);
            Assert.True(string.Compare(
                finding.Evidence[0].File,
                finding.Evidence[1].File,
                StringComparison.OrdinalIgnoreCase) <= 0);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Discover_TracksBindPatternEvidenceKinds()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "BindCases.cs"), """
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Configuration;

            public class BindCases
            {
                public void Run(IServiceCollection services, IConfiguration configuration, DemoOptions options)
                {
                    services.AddOptions<DemoOptions>().Bind(configuration.GetSection("A:B"));
                    configuration.Bind("A:B", options);
                }
            }
            """);

            var report = DiscoverEngine.Discover(tempDir);

            var finding = Assert.Single(report.Findings, finding => finding.Path == "A:B");
            Assert.True(finding.Evidence.Count >= 2);
            Assert.Contains(finding.Evidence, evidence => evidence.Pattern == "Bind(GetSection)");
            Assert.Contains(finding.Evidence, evidence => evidence.Pattern == "Bind(literal)");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
