namespace Configuard.Cli.Validation;

internal static class RuleValueResolver
{
    public static bool TryResolveValue(
        ResolvedConfigBySource resolved,
        ContractKeyRule keyRule,
        out string resolvedPath,
        out ResolvedConfigValue? value)
    {
        var candidates = RuleEvaluation.GetCandidatePaths(keyRule);
        foreach (var source in GetSourceOrder(keyRule))
        {
            var map = GetSourceMap(resolved, source);
            if (map is null)
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                if (map.TryGetValue(candidate, out var found))
                {
                    resolvedPath = candidate;
                    value = found;
                    return true;
                }
            }
        }

        resolvedPath = string.Empty;
        value = null;
        return false;
    }

    public static IReadOnlyList<string> GetEffectiveSourceOrder(ContractKeyRule keyRule) =>
        [.. GetSourceOrder(keyRule)];

    private static IEnumerable<string> GetSourceOrder(ContractKeyRule keyRule)
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

    private static Dictionary<string, ResolvedConfigValue>? GetSourceMap(ResolvedConfigBySource resolved, string source)
    {
        if (string.Equals(source, SourceKinds.DotEnv, StringComparison.OrdinalIgnoreCase))
        {
            return resolved.DotEnv;
        }

        if (string.Equals(source, SourceKinds.AppSettings, StringComparison.OrdinalIgnoreCase))
        {
            return resolved.AppSettings;
        }

        if (string.Equals(source, SourceKinds.EnvSnapshot, StringComparison.OrdinalIgnoreCase))
        {
            return resolved.EnvSnapshot;
        }

        return null;
    }
}
