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

    [Fact]
    public void MatchesType_HandlesSupportedPrimitiveTypes()
    {
        using var stringDoc = JsonDocument.Parse("\"value\"");
        using var intDoc = JsonDocument.Parse("42");
        using var numberDoc = JsonDocument.Parse("42.5");
        using var boolDoc = JsonDocument.Parse("true");
        using var objectDoc = JsonDocument.Parse("{\"x\":1}");
        using var arrayDoc = JsonDocument.Parse("[1,2]");

        Assert.True(RuleEvaluation.MatchesType("string", stringDoc.RootElement));
        Assert.True(RuleEvaluation.MatchesType("int", intDoc.RootElement));
        Assert.True(RuleEvaluation.MatchesType("number", numberDoc.RootElement));
        Assert.True(RuleEvaluation.MatchesType("bool", boolDoc.RootElement));
        Assert.True(RuleEvaluation.MatchesType("object", objectDoc.RootElement));
        Assert.True(RuleEvaluation.MatchesType("array", arrayDoc.RootElement));

        Assert.False(RuleEvaluation.MatchesType("int", numberDoc.RootElement));
        Assert.False(RuleEvaluation.MatchesType("bool", stringDoc.RootElement));
    }

    [Fact]
    public void EvaluateConstraints_DoesNotReportIssues_OnBoundaryValues()
    {
        using var valueDoc = JsonDocument.Parse("\"abc\"");
        using var constraintDoc = JsonDocument.Parse("""
        {
          "minLength": 3,
          "maxLength": 3,
          "pattern": "^[a-z]+$",
          "enum": ["abc", "def"]
        }
        """);

        var issues = RuleEvaluation.EvaluateConstraints(
                "staging",
                "Api:Key",
                valueDoc.RootElement.Clone(),
                constraintDoc.RootElement.Clone())
            .ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void EvaluateConstraints_ReportsMultipleIssues_WhenMultipleConstraintsFail()
    {
        using var valueDoc = JsonDocument.Parse("\"ab\"");
        using var constraintDoc = JsonDocument.Parse("""
        {
          "minLength": 3,
          "pattern": "^[0-9]+$"
        }
        """);

        var issues = RuleEvaluation.EvaluateConstraints(
                "staging",
                "Api:Key",
                valueDoc.RootElement.Clone(),
                constraintDoc.RootElement.Clone())
            .ToList();

        Assert.Contains(issues, i => i.Code == "constraint_minLength");
        Assert.Contains(issues, i => i.Code == "constraint_pattern");
    }
}
