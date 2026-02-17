namespace Configuard.Cli.Validation;

internal sealed class ExplainResult
{
    public required string Environment { get; init; }

    public required string RequestedKey { get; init; }

    public required string RulePath { get; init; }

    public required string RuleType { get; init; }

    public string? ResolvedPath { get; init; }

    public string? ResolvedSource { get; init; }

    public string? ResolvedFrom { get; init; }

    public string? ResolvedValueDisplay { get; init; }

    public bool IsPass { get; init; }

    public required string DecisionCode { get; init; }

    public required string DecisionMessage { get; init; }

    public string MatchedRuleBy { get; init; } = "path";

    public IReadOnlyList<string> SourceOrderUsed { get; init; } = [];

    public IReadOnlyList<string> CandidatePaths { get; init; } = [];
}
