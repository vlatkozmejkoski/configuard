using Configuard.Cli.Validation;
using System.Text.Json;

namespace Configuard.Cli.Tests;

public sealed class RuleEvaluationTests
{
    [Fact]
    public void GetCandidatePaths_NormalizesPrimaryAndAliases()
    {
        var rule = new ContractKeyRule
        {
            Path = "ConnectionStrings__Default",
            Aliases = ["DB__CONNECTION", "ConnectionStrings:Default"],
            Type = "string"
        };

        var candidates = RuleEvaluation.GetCandidatePaths(rule);

        Assert.Equal("ConnectionStrings:Default", candidates[0]);
        Assert.Contains("DB:CONNECTION", candidates);
        Assert.Contains("ConnectionStrings:Default", candidates);
    }

    [Fact]
    public void EvaluateConstraints_ReportsPatternInvalid()
    {
        using var valueDoc = JsonDocument.Parse("\"abc\"");
        using var constraintDoc = JsonDocument.Parse("{\"pattern\":\"[\"}");

        var issues = RuleEvaluation.EvaluateConstraints(
                "staging",
                "Api:Key",
                valueDoc.RootElement.Clone(),
                constraintDoc.RootElement.Clone())
            .ToList();

        Assert.Contains(issues, i => i.Code == "constraint_pattern_invalid");
    }
}
