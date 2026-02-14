using Configuard.Cli.Validation;
using System.Text.Json;

namespace Configuard.Cli.Tests;

public sealed class ValidateOutputFormatterTests
{
    [Fact]
    public void ToJson_FailureResult_ContainsExpectedShape()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue("production", "Features:UseMockPayments", "forbidden_present", "Forbidden key is present."));
        result.Warnings.Add(new ValidationWarning("Api:Key", "unknown_source_preference", "Unknown sourcePreference value."));

        var json = ValidateOutputFormatter.ToJson(
            "configuard.contract.json",
            ["staging", "production"],
            result);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("validate", root.GetProperty("command").GetString());
        Assert.Equal("fail", root.GetProperty("result").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("violationCount").GetInt32());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("warningCount").GetInt32());
        Assert.Equal(2, root.GetProperty("environments").GetArrayLength());
        Assert.Single(root.GetProperty("warnings").EnumerateArray());
        Assert.Single(root.GetProperty("violations").EnumerateArray());
    }

    [Fact]
    public void ToJson_SuccessResult_HasPassAndNoViolations()
    {
        var result = new ValidationResult();

        var json = ValidateOutputFormatter.ToJson(
            "configuard.contract.json",
            ["staging"],
            result);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("pass", root.GetProperty("result").GetString());
        Assert.Equal(0, root.GetProperty("summary").GetProperty("violationCount").GetInt32());
        Assert.Empty(root.GetProperty("violations").EnumerateArray());
    }

    [Fact]
    public void ToSarif_FailureResult_ContainsRulesAndResults()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue("staging", "Api:Key", "missing_required", "Required key not found."));
        result.Issues.Add(new ValidationIssue("production", "Api:Key", "missing_required", "Required key not found."));
        result.Warnings.Add(new ValidationWarning("Api:Key", "unknown_source_preference", "Unknown sourcePreference value."));

        var sarif = ValidateOutputFormatter.ToSarif(
            "examples/quickstart/configuard.contract.json",
            ["staging", "production"],
            result);

        using var document = JsonDocument.Parse(sarif);
        var root = document.RootElement;

        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        var run = root.GetProperty("runs")[0];
        Assert.Equal("Configuard", run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());
        Assert.Equal(2, run.GetProperty("tool").GetProperty("driver").GetProperty("rules").GetArrayLength());
        Assert.Equal(3, run.GetProperty("results").GetArrayLength());
    }

    [Fact]
    public void ToText_Detailed_IncludesAggregatedSections()
    {
        var result = new ValidationResult();
        result.Issues.Add(new ValidationIssue("staging", "Api:Key", "missing_required", "Required key not found."));
        result.Issues.Add(new ValidationIssue("production", "Api:Key", "missing_required", "Required key not found."));

        var text = ValidateOutputFormatter.ToText(
            "configuard.contract.json",
            ["staging", "production"],
            result,
            detailed: true);

        Assert.Contains("Issue counts by code:", text, StringComparison.Ordinal);
        Assert.Contains("- missing_required: 2", text, StringComparison.Ordinal);
        Assert.Contains("Issue counts by environment:", text, StringComparison.Ordinal);
    }
}
