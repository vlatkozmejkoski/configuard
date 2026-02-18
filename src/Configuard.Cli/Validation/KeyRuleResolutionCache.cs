using System.Runtime.CompilerServices;

namespace Configuard.Cli.Validation;

internal static class KeyRuleResolutionCache
{
    private static readonly ConditionalWeakTable<ContractKeyRule, CachedRule> Cache = new();

    public static IReadOnlyList<string> GetCandidatePaths(ContractKeyRule keyRule) =>
        Cache.GetValue(keyRule, Create).CandidatePaths;

    public static IReadOnlyList<string> GetSourceOrder(ContractKeyRule keyRule) =>
        Cache.GetValue(keyRule, Create).SourceOrder;

    private static CachedRule Create(ContractKeyRule keyRule)
    {
        return new CachedRule(
            CandidatePaths: RuleEvaluation.GetCandidatePaths(keyRule),
            SourceOrder: BuildSourceOrder(keyRule));
    }

    private static IReadOnlyList<string> BuildSourceOrder(ContractKeyRule keyRule)
    {
        if (keyRule.SourcePreference.Count == 0)
        {
            return SourceKinds.DefaultOrder;
        }

        var order = new List<string>();
        foreach (var source in keyRule.SourcePreference)
        {
            var normalized = source.Trim().ToLowerInvariant();
            if (!SourceKinds.IsSupported(normalized))
            {
                continue;
            }

            if (!order.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                order.Add(normalized);
            }
        }

        return order.Count == 0 ? SourceKinds.DefaultOrder : order;
    }

    private sealed record CachedRule(
        IReadOnlyList<string> CandidatePaths,
        IReadOnlyList<string> SourceOrder);
}
