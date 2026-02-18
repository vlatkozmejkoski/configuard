using Configuard.Cli.Cli;

namespace Configuard.Cli.Tests;

public sealed class CliVersionProviderTests
{
    [Fact]
    public void GetDisplayVersion_ReturnsNonEmptyVersionWithoutBuildMetadata()
    {
        var version = CliVersionProvider.GetDisplayVersion();

        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.False(version.Contains('+'));
    }
}
