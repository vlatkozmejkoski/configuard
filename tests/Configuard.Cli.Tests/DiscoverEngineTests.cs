using Configuard.Cli.Discovery;
using static Configuard.Cli.Tests.TestHelpers;

namespace Configuard.Cli.Tests;

public sealed class DiscoverEngineTests
{
    [Fact]
    public void Discover_UsesDeterministicGeneratedAtUtcProvider()
    {
        var tempDir = CreateTempDirectory();
        var originalProvider = DiscoverEngine.UtcNowProvider;
        try
        {
            var fixedUtc = new DateTimeOffset(2026, 2, 18, 12, 0, 0, TimeSpan.Zero);
            DiscoverEngine.UtcNowProvider = () => fixedUtc;
            File.WriteAllText(Path.Combine(tempDir, "Sample.cs"), """
            using Microsoft.Extensions.Configuration;
            public class Sample { public void Run(IConfiguration c) { var x = c["Api:Key"]; } }
            """);

            var report = DiscoverEngine.Discover(tempDir);

            Assert.Equal(fixedUtc, report.GeneratedAtUtc);
        }
        finally
        {
            DiscoverEngine.UtcNowProvider = originalProvider;
            Directory.Delete(tempDir, recursive: true);
        }
    }

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

    [Fact]
    public void Discover_ComposedPathExpression_IsReportedAsMediumConfidenceWithNote()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Composed.cs"), """
            using Microsoft.Extensions.Configuration;

            public class Composed
            {
                public void Run(IConfiguration configuration, string suffix)
                {
                    var key = configuration.GetValue<string>("Api:" + suffix);
                }
            }
            """);

            var report = DiscoverEngine.Discover(tempDir);

            var finding = Assert.Single(report.Findings, finding => finding.Path == "Api:{expr}");
            Assert.Equal("medium", finding.Confidence);
            Assert.Contains("Contains unresolved dynamic segment(s).", finding.Notes);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Discover_IncludeExcludePatterns_FilterFiles()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var keepDir = Path.Combine(tempDir, "Keep");
            var skipDir = Path.Combine(tempDir, "Skip");
            Directory.CreateDirectory(keepDir);
            Directory.CreateDirectory(skipDir);

            File.WriteAllText(Path.Combine(keepDir, "One.cs"), """
            using Microsoft.Extensions.Configuration;
            public class One { public void Run(IConfiguration c) { var x = c["Keep:Key"]; } }
            """);

            File.WriteAllText(Path.Combine(skipDir, "Two.cs"), """
            using Microsoft.Extensions.Configuration;
            public class Two { public void Run(IConfiguration c) { var x = c["Skip:Key"]; } }
            """);

            var report = DiscoverEngine.Discover(
                tempDir,
                includePatterns: ["Keep/**", "Skip/**"],
                excludePatterns: ["Skip/**"]);

            Assert.Contains(report.Findings, finding => finding.Path == "Keep:Key");
            Assert.DoesNotContain(report.Findings, finding => finding.Path == "Skip:Key");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Discover_IsDeterministicAcrossDirectoryAndFileOrdering()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var oneDir = Path.Combine(tempDir, "ZFolder");
            var twoDir = Path.Combine(tempDir, "AFolder");
            Directory.CreateDirectory(oneDir);
            Directory.CreateDirectory(twoDir);

            File.WriteAllText(Path.Combine(oneDir, "Second.cs"), """
            using Microsoft.Extensions.Configuration;
            public class Second { public void Run(IConfiguration c) { var x = c["Second:Key"]; } }
            """);
            File.WriteAllText(Path.Combine(twoDir, "First.cs"), """
            using Microsoft.Extensions.Configuration;
            public class First { public void Run(IConfiguration c) { var x = c["First:Key"]; } }
            """);

            var firstRun = DiscoverEngine.Discover(tempDir);
            var secondRun = DiscoverEngine.Discover(tempDir);

            var firstPaths = firstRun.Findings.Select(f => f.Path).ToArray();
            var secondPaths = secondRun.Findings.Select(f => f.Path).ToArray();
            Assert.Equal(firstPaths, secondPaths);
            Assert.Equal(["First:Key", "Second:Key"], firstPaths);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
