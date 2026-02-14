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
}
