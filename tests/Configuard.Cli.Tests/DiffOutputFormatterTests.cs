using Configuard.Cli.Validation;
using System.Text.Json;

namespace Configuard.Cli.Tests;

public sealed class DiffOutputFormatterTests
{
    [Fact]
    public void ToJson_WithDifferences_HasExpectedShape()
    {
        var result = new DiffResult();
        result.Issues.Add(new DiffIssue(
            "ConnectionStrings:Default",
            "changed",
            "staging",
            "production",
            "Value differs."));

        var json = DiffOutputFormatter.ToJson("configuard.contract.json", "staging", "production", result);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("diff", root.GetProperty("command").GetString());
        Assert.Equal("different", root.GetProperty("result").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("differenceCount").GetInt32());
        Assert.Single(root.GetProperty("differences").EnumerateArray());
    }

    [Fact]
    public void ToText_NoDifferences_StatesCleanResult()
    {
        var result = new DiffResult();
        var text = DiffOutputFormatter.ToText("configuard.contract.json", "staging", "production", result);

        Assert.Contains("No differences.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToText_Detailed_IncludesKindCounts()
    {
        var result = new DiffResult();
        result.Issues.Add(new DiffIssue("Api:Key", "changed", "staging", "production", "Value differs."));
        result.Issues.Add(new DiffIssue("Api:Port", "missing", "staging", "production", "Missing key."));

        var text = DiffOutputFormatter.ToText(
            "configuard.contract.json",
            "staging",
            "production",
            result,
            detailed: true);

        Assert.Contains("Difference counts by kind:", text, StringComparison.Ordinal);
        Assert.Contains("- changed: 1", text, StringComparison.Ordinal);
        Assert.Contains("- missing: 1", text, StringComparison.Ordinal);
    }
}
