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

    [Fact]
    public void ToText_Detailed_IncludesDiagnostics()
    {
        var result = new ExplainResult
        {
            Environment = "staging",
            RequestedKey = "API__KEY",
            RulePath = "Api:Key",
            RuleType = "string",
            IsPass = true,
            DecisionCode = "pass",
            DecisionMessage = "Key satisfies all applicable rules.",
            MatchedRuleBy = "alias",
            SourceOrderUsed = [SourceKinds.DotEnv, SourceKinds.AppSettings],
            CandidatePaths = ["Api:Key", "API:KEY"]
        };

        var text = ExplainOutputFormatter.ToText(result, detailed: true);

        Assert.Contains("Matched rule by: alias", text, StringComparison.Ordinal);
        Assert.Contains("Source order used: dotenv, appsettings", text, StringComparison.Ordinal);
        Assert.Contains("Candidate paths: Api:Key, API:KEY", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToJson_Detailed_IncludesDiagnosticsObject()
    {
        var result = new ExplainResult
        {
            Environment = "production",
            RequestedKey = "ConnectionStrings:Default",
            RulePath = "ConnectionStrings:Default",
            RuleType = "string",
            IsPass = true,
            DecisionCode = "pass",
            DecisionMessage = "Key satisfies all applicable rules.",
            MatchedRuleBy = "path",
            SourceOrderUsed = [SourceKinds.EnvSnapshot, SourceKinds.AppSettings],
            CandidatePaths = ["ConnectionStrings:Default"]
        };

        var json = ExplainOutputFormatter.ToJson(result, detailed: true);
        using var document = JsonDocument.Parse(json);
        var diagnostics = document.RootElement.GetProperty("diagnostics");

        Assert.Equal("path", diagnostics.GetProperty("matchedRuleBy").GetString());
        Assert.Equal("envsnapshot", diagnostics.GetProperty("sourceOrderUsed")[0].GetString());
        Assert.Equal("ConnectionStrings:Default", diagnostics.GetProperty("candidatePaths")[0].GetString());
    }
}
