using Configuard.Cli.Cli;

namespace Configuard.Cli.Tests;

public sealed class CommandParserTests
{
    [Fact]
    public void TryParse_ValidateCommand_ParsesKnownOptions()
    {
        var ok = CommandParser.TryParse(
            ["validate", "--contract", "configuard.contract.json", "--env", "staging", "--format", "json", "--verbosity", "detailed"],
            out var command,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(command);
        Assert.Equal("validate", command!.Name);
        Assert.Equal("configuard.contract.json", command.ContractPath);
        Assert.Single(command.Environments);
        Assert.Equal("staging", command.Environments[0]);
        Assert.Equal("json", command.OutputFormat);
        Assert.Equal("detailed", command.Verbosity);
    }

    [Fact]
    public void TryParse_UnknownCommand_ReturnsError()
    {
        var ok = CommandParser.TryParse(["deploy"], out var command, out var error);

        Assert.False(ok);
        Assert.Null(command);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_NoColorOption_IsAccepted()
    {
        var ok = CommandParser.TryParse(
            ["validate", "--contract", "configuard.contract.json", "--no-color", "--env", "staging"],
            out var command,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(command);
        Assert.True(command!.NoColor);
    }

    [Fact]
    public void TryParse_NoColorOption_Repeated_IsAccepted()
    {
        var ok = CommandParser.TryParse(
            ["diff", "--no-color", "--env", "staging", "--no-color", "--env", "production"],
            out var command,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(command);
        Assert.True(command!.NoColor);
    }

    [Fact]
    public void TryParse_NoArgs_ReturnsError()
    {
        var ok = CommandParser.TryParse([], out var command, out var error);

        Assert.False(ok);
        Assert.Null(command);
        Assert.NotNull(error);
        Assert.Contains("No command provided", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_UnknownOption_ReturnsError()
    {
        var ok = CommandParser.TryParse(
            ["validate", "--unknown", "value"],
            out var command,
            out var error);

        Assert.False(ok);
        Assert.Null(command);
        Assert.NotNull(error);
        Assert.Contains("Unknown option '--unknown'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_MissingOptionValue_ReturnsError()
    {
        var ok = CommandParser.TryParse(
            ["explain", "--env", "staging", "--key"],
            out var command,
            out var error);

        Assert.False(ok);
        Assert.Null(command);
        Assert.NotNull(error);
        Assert.Contains("Missing value for option '--key'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_CommandNameIsCaseInsensitive()
    {
        var ok = CommandParser.TryParse(
            ["VaLiDaTe", "--env", "staging"],
            out var command,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(command);
        Assert.Equal("validate", command!.Name);
    }

    [Fact]
    public void TryParse_DiscoverCommand_ParsesPhase2Options()
    {
        var ok = CommandParser.TryParse(
            ["discover", "--path", "src", "--output", "discover.json", "--format", "json", "--preset", "dotnet-solution", "--apply"],
            out var command,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(command);
        Assert.Equal("discover", command!.Name);
        Assert.Equal("src", command.ScanPath);
        Assert.Equal("discover.json", command.OutputPath);
        Assert.Equal("json", command.OutputFormat);
        Assert.Equal("dotnet-solution", command.ScopePreset);
        Assert.True(command.Apply);
    }

    [Fact]
    public void TryParse_DiscoverCommand_ParsesIncludeExcludePatterns()
    {
        var ok = CommandParser.TryParse(
            ["discover", "--path", "src", "--include", "**/Api/*.cs", "--include", "**/Features/*.cs", "--exclude", "**/obj/**"],
            out var command,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(command);
        Assert.Equal("discover", command!.Name);
        Assert.Equal(2, command.IncludePatterns!.Count);
        Assert.Single(command.ExcludePatterns!);
        Assert.Contains("**/Api/*.cs", command.IncludePatterns!);
        Assert.Contains("**/obj/**", command.ExcludePatterns!);
    }
}
