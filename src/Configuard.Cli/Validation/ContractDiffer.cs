namespace Configuard.Cli.Validation;

internal static class ContractDiffer
{
    public static DiffResult Diff(
        ContractDocument contract,
        string repoRoot,
        string leftEnvironment,
        string rightEnvironment)
    {
        var result = new DiffResult();

        var leftValues = AppSettingsProvenanceResolver.Resolve(repoRoot, contract.Sources, leftEnvironment);
        var rightValues = AppSettingsProvenanceResolver.Resolve(repoRoot, contract.Sources, rightEnvironment);

        foreach (var keyRule in contract.Keys)
        {
            var leftFound = RuleValueResolver.TryResolveValue(leftValues, keyRule, out var leftPath, out var leftValue);
            var rightFound = RuleValueResolver.TryResolveValue(rightValues, keyRule, out var rightPath, out var rightValue);

            if (leftFound && !rightFound)
            {
                result.Issues.Add(new DiffIssue(
                    keyRule.Path,
                    "missing",
                    leftEnvironment,
                    rightEnvironment,
                    $"Present in {leftEnvironment} via '{leftPath}', missing in {rightEnvironment}."));
                continue;
            }

            if (!leftFound && rightFound)
            {
                result.Issues.Add(new DiffIssue(
                    keyRule.Path,
                    "missing",
                    leftEnvironment,
                    rightEnvironment,
                    $"Missing in {leftEnvironment}, present in {rightEnvironment} via '{rightPath}'."));
                continue;
            }

            if (!leftFound && !rightFound)
            {
                continue;
            }

            if (leftValue!.Value.ValueKind != rightValue!.Value.ValueKind)
            {
                result.Issues.Add(new DiffIssue(
                    keyRule.Path,
                    "typeChanged",
                    leftEnvironment,
                    rightEnvironment,
                    $"Type differs: {leftValue.Value.ValueKind} vs {rightValue.Value.ValueKind}."));
                continue;
            }

            if (!JsonElementEquals(leftValue.Value, rightValue.Value))
            {
                result.Issues.Add(new DiffIssue(
                    keyRule.Path,
                    "changed",
                    leftEnvironment,
                    rightEnvironment,
                    $"Value differs between {leftEnvironment} and {rightEnvironment}."));
            }
        }

        return result;
    }

    private static bool JsonElementEquals(System.Text.Json.JsonElement left, System.Text.Json.JsonElement right) =>
        left.GetRawText() == right.GetRawText();
}
