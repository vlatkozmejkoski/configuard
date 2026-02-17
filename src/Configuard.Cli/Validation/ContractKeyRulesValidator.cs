namespace Configuard.Cli.Validation;

internal static class ContractKeyRulesValidator
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string",
        "int",
        "number",
        "bool",
        "object",
        "array"
    };

    public static bool TryValidate(
        IReadOnlyList<ContractKeyRule> keys,
        IReadOnlyList<string> environments,
        out string? error)
    {
        error = null;
        var seenIdentifiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var declaredEnvironments = new HashSet<string>(
            environments.Select(environment => environment.Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            var canonicalPath = RuleEvaluation.NormalizePath(key.Path).Trim();
            if (string.IsNullOrWhiteSpace(canonicalPath))
            {
                error = "keys[].path must not be empty.";
                return false;
            }

            if (!TryValidateKeyType(key.Path, key.Type, out error))
            {
                return false;
            }

            if (seenIdentifiers.TryGetValue(canonicalPath, out var existingOwner))
            {
                error = $"Duplicate key path or alias '{canonicalPath}' conflicts with '{existingOwner}'.";
                return false;
            }

            seenIdentifiers[canonicalPath] = key.Path;

            foreach (var alias in key.Aliases)
            {
                var canonicalAlias = RuleEvaluation.NormalizePath(alias).Trim();
                if (string.IsNullOrWhiteSpace(canonicalAlias))
                {
                    error = $"Key '{key.Path}' contains an empty alias.";
                    return false;
                }

                if (seenIdentifiers.TryGetValue(canonicalAlias, out existingOwner))
                {
                    error = $"Duplicate key path or alias '{canonicalAlias}' conflicts with '{existingOwner}'.";
                    return false;
                }

                seenIdentifiers[canonicalAlias] = key.Path;
            }

            var canonicalForbiddenEnvironments = new HashSet<string>(
                key.ForbiddenIn.Select(environment => environment.Trim()),
                StringComparer.OrdinalIgnoreCase);

            foreach (var env in key.RequiredIn)
            {
                var canonicalEnvironment = env.Trim();
                if (canonicalForbiddenEnvironments.Contains(canonicalEnvironment))
                {
                    error = $"Key '{key.Path}' cannot be both required and forbidden in environment '{canonicalEnvironment}'.";
                    return false;
                }
            }

            if (!TryValidateRuleEnvironments(key.Path, "requiredIn", key.RequiredIn, declaredEnvironments, out error))
            {
                return false;
            }

            if (!TryValidateRuleEnvironments(key.Path, "forbiddenIn", key.ForbiddenIn, declaredEnvironments, out error))
            {
                return false;
            }

            if (!TryValidateSourcePreference(key.Path, key.SourcePreference, out error))
            {
                return false;
            }

            if (!ContractConstraintRulesValidator.TryValidate(key.Path, key.Constraints, out error))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateRuleEnvironments(
        string keyPath,
        string propertyName,
        IReadOnlyList<string> ruleEnvironments,
        HashSet<string> declaredEnvironments,
        out string? error)
    {
        error = null;
        var seenRuleEnvironments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var environment in ruleEnvironments)
        {
            var canonicalEnvironment = environment.Trim();
            if (string.IsNullOrWhiteSpace(canonicalEnvironment))
            {
                error = $"Key '{keyPath}' contains an empty '{propertyName}' environment.";
                return false;
            }

            if (!declaredEnvironments.Contains(canonicalEnvironment))
            {
                error = $"Key '{keyPath}' references undeclared environment '{canonicalEnvironment}' in '{propertyName}'.";
                return false;
            }

            if (!seenRuleEnvironments.Add(canonicalEnvironment))
            {
                error = $"Key '{keyPath}' contains duplicate environment '{canonicalEnvironment}' in '{propertyName}'.";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateSourcePreference(
        string keyPath,
        IReadOnlyList<string> sourcePreference,
        out string? error)
    {
        error = null;
        var seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sourcePreference)
        {
            var canonicalSource = source.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(canonicalSource))
            {
                error = $"Key '{keyPath}' contains an empty 'sourcePreference' entry.";
                return false;
            }

            if (!SourceKinds.IsSupported(canonicalSource))
            {
                error = $"Key '{keyPath}' contains unsupported sourcePreference '{canonicalSource}'.";
                return false;
            }

            if (!seenSources.Add(canonicalSource))
            {
                error = $"Key '{keyPath}' contains duplicate sourcePreference '{canonicalSource}'.";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateKeyType(string keyPath, string keyType, out string? error)
    {
        error = null;
        var canonicalType = keyType.Trim();
        if (string.IsNullOrWhiteSpace(canonicalType))
        {
            error = $"Key '{keyPath}' must define a non-empty 'type'.";
            return false;
        }

        if (!SupportedTypes.Contains(canonicalType))
        {
            error = $"Key '{keyPath}' has unsupported type '{canonicalType}'.";
            return false;
        }

        return true;
    }
}
