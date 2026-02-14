using Configuard.Cli.Validation;
using System.Text.Json;

namespace Configuard.Cli.Tests;

public sealed class ExplainOutputFormatterTests
{
    [Fact]
    public void ToJson_ContainsDecisionAndResolution()
    {
        var result = new ExplainResult
        {
            Environment = "production",
            RequestedKey = "ConnectionStrings:Default",
            RulePath = "ConnectionStrings:Default",
            RuleType = "string",
            ResolvedPath = "ConnectionStrings:Default",
            ResolvedSource = "appsettings",
            ResolvedFrom = "appsettings.production.json",
            ResolvedValueDisplay = "\"Server=...\"",
            IsPass = true,
            DecisionCode = "pass",
            DecisionMessage = "Key satisfies all applicable rules."
        };

        var json = ExplainOutputFormatter.ToJson(result);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("explain", root.GetProperty("command").GetString());
        Assert.True(root.GetProperty("decision").GetProperty("pass").GetBoolean());
        Assert.Equal("ConnectionStrings:Default", root.GetProperty("resolution").GetProperty("resolvedPath").GetString());
        Assert.Equal("appsettings", root.GetProperty("resolution").GetProperty("resolvedSource").GetString());
    }

    [Fact]
    public void ToText_ContainsPassFailAndReason()
    {
        var result = new ExplainResult
        {
            Environment = "staging",
            RequestedKey = "Api:Key",
            RulePath = "Api:Key",
            RuleType = "string",
            IsPass = false,
            DecisionCode = "constraint_minLength",
            DecisionMessage = "String length is 3, minimum is 10."
        };

        var text = ExplainOutputFormatter.ToText(result);

        Assert.Contains("Decision: FAIL (constraint_minLength)", text, StringComparison.Ordinal);
        Assert.Contains("Reason: String length is 3, minimum is 10.", text, StringComparison.Ordinal);
    }
}
