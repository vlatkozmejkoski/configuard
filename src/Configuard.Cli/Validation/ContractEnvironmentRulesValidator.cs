namespace Configuard.Cli.Validation;

internal static class ContractEnvironmentRulesValidator
{
    public static bool TryValidate(IReadOnlyList<string> environments, out string? error)
    {
        error = null;
        var seenEnvironments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var environment in environments)
        {
            var canonicalEnvironment = environment.Trim();
            if (string.IsNullOrWhiteSpace(canonicalEnvironment))
            {
                error = "environments[] values must not be empty or whitespace.";
                return false;
            }

            if (!seenEnvironments.Add(canonicalEnvironment))
            {
                error = $"Duplicate environment '{canonicalEnvironment}' is not allowed.";
                return false;
            }
        }

        return true;
    }
}
