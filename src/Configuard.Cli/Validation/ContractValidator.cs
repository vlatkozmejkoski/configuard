namespace Configuard.Cli.Validation;

internal static class ContractValidator
{
    public static ValidationResult Validate(
        ContractDocument contract,
        string repoRoot,
        IReadOnlyList<string> targetEnvironments)
    {
        var result = new ValidationResult();
        AddContractWarnings(contract, result);
        var environments = targetEnvironments.Count > 0 ? targetEnvironments : contract.Environments;

        foreach (var environment in environments)
        {
            var values = AppSettingsProvenanceResolver.Resolve(repoRoot, contract.Sources, environment);
            ValidateEnvironment(contract, environment, values, result);
        }

        return result;
    }

    private static void AddContractWarnings(ContractDocument contract, ValidationResult result)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var keyRule in contract.Keys)
        {
            foreach (var invalidSource in RuleValueResolver.GetInvalidSourcePreferences(keyRule))
            {
                var token = $"{keyRule.Path}|{invalidSource}";
                if (!seen.Add(token))
                {
                    continue;
                }

                result.Warnings.Add(new ValidationWarning(
                    keyRule.Path,
                    "unknown_source_preference",
                    $"Unknown sourcePreference value '{invalidSource}' for key '{keyRule.Path}'."));
            }
        }
    }

    private static void ValidateEnvironment(
        ContractDocument contract,
        string environment,
        ResolvedConfigBySource values,
        ValidationResult result)
    {
        foreach (var keyRule in contract.Keys)
        {
            var found = RuleValueResolver.TryResolveValue(values, keyRule, out var foundPath, out var resolved);

            if (keyRule.RequiredIn.Contains(environment, StringComparer.OrdinalIgnoreCase) && !found)
            {
                result.Issues.Add(new ValidationIssue(environment, keyRule.Path, "missing_required", "Required key not found."));
                continue;
            }

            if (keyRule.ForbiddenIn.Contains(environment, StringComparer.OrdinalIgnoreCase) && found)
            {
                result.Issues.Add(new ValidationIssue(environment, keyRule.Path, "forbidden_present", $"Forbidden key is present via '{foundPath}'."));
                continue;
            }

            if (!found)
            {
                continue;
            }

            var value = resolved!.Value;
            if (!RuleEvaluation.MatchesType(keyRule.Type, value))
            {
                result.Issues.Add(
                    new ValidationIssue(
                        environment,
                        keyRule.Path,
                        "type_mismatch",
                        $"Expected type '{keyRule.Type}', got '{value.ValueKind}'."));
                continue;
            }

            foreach (var constraintIssue in RuleEvaluation.EvaluateConstraints(environment, keyRule.Path, value, keyRule.Constraints))
            {
                result.Issues.Add(constraintIssue);
            }
        }
    }
}
