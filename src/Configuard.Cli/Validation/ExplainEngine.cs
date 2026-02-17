using System.Text.Json;

namespace Configuard.Cli.Validation;

internal static class ExplainEngine
{
    public static bool TryExplain(
        ContractDocument contract,
        string repoRoot,
        string environment,
        string requestedKey,
        out ExplainResult? result)
    {
        result = null;
        var keyRule = FindRule(contract.Keys, requestedKey, out var matchedRuleBy);
        if (keyRule is null)
        {
            return false;
        }

        var sourceOrderUsed = RuleValueResolver.GetEffectiveSourceOrder(keyRule);
        var candidatePaths = RuleEvaluation.GetCandidatePaths(keyRule);
        var resolvedValues = AppSettingsProvenanceResolver.Resolve(repoRoot, contract.Sources, environment);
        var found = RuleValueResolver.TryResolveValue(resolvedValues, keyRule, out var foundPath, out var foundValue);

        if (keyRule.ForbiddenIn.Contains(environment, StringComparer.OrdinalIgnoreCase) && found)
        {
            result = new ExplainResult
            {
                Environment = environment,
                RequestedKey = requestedKey,
                RulePath = keyRule.Path,
                RuleType = keyRule.Type,
                ResolvedPath = foundPath,
                ResolvedSource = foundValue!.SourceKind,
                ResolvedFrom = foundValue!.SourceFile,
                ResolvedValueDisplay = DisplayValue(foundValue.Value, keyRule.Sensitive),
                IsPass = false,
                DecisionCode = "forbidden_present",
                DecisionMessage = $"Key is forbidden in environment '{environment}' but is present.",
                MatchedRuleBy = matchedRuleBy,
                SourceOrderUsed = sourceOrderUsed,
                CandidatePaths = candidatePaths
            };
            return true;
        }

        if (keyRule.RequiredIn.Contains(environment, StringComparer.OrdinalIgnoreCase) && !found)
        {
            result = new ExplainResult
            {
                Environment = environment,
                RequestedKey = requestedKey,
                RulePath = keyRule.Path,
                RuleType = keyRule.Type,
                IsPass = false,
                DecisionCode = "missing_required",
                DecisionMessage = $"Key is required in environment '{environment}' but was not found.",
                MatchedRuleBy = matchedRuleBy,
                SourceOrderUsed = sourceOrderUsed,
                CandidatePaths = candidatePaths
            };
            return true;
        }

        if (!found)
        {
            result = new ExplainResult
            {
                Environment = environment,
                RequestedKey = requestedKey,
                RulePath = keyRule.Path,
                RuleType = keyRule.Type,
                IsPass = true,
                DecisionCode = "optional_missing",
                DecisionMessage = "Key is optional in this environment and not present.",
                MatchedRuleBy = matchedRuleBy,
                SourceOrderUsed = sourceOrderUsed,
                CandidatePaths = candidatePaths
            };
            return true;
        }

        if (!RuleEvaluation.MatchesType(keyRule.Type, foundValue!.Value))
        {
            result = new ExplainResult
            {
                Environment = environment,
                RequestedKey = requestedKey,
                RulePath = keyRule.Path,
                RuleType = keyRule.Type,
                ResolvedPath = foundPath,
                ResolvedSource = foundValue.SourceKind,
                ResolvedFrom = foundValue.SourceFile,
                ResolvedValueDisplay = DisplayValue(foundValue.Value, keyRule.Sensitive),
                IsPass = false,
                DecisionCode = "type_mismatch",
                DecisionMessage = $"Expected type '{keyRule.Type}', got '{foundValue.Value.ValueKind}'.",
                MatchedRuleBy = matchedRuleBy,
                SourceOrderUsed = sourceOrderUsed,
                CandidatePaths = candidatePaths
            };
            return true;
        }

        var constraintIssue = RuleEvaluation.EvaluateConstraints(environment, keyRule.Path, foundValue.Value, keyRule.Constraints).FirstOrDefault();
        if (constraintIssue is not null)
        {
            result = new ExplainResult
            {
                Environment = environment,
                RequestedKey = requestedKey,
                RulePath = keyRule.Path,
                RuleType = keyRule.Type,
                ResolvedPath = foundPath,
                ResolvedSource = foundValue.SourceKind,
                ResolvedFrom = foundValue.SourceFile,
                ResolvedValueDisplay = DisplayValue(foundValue.Value, keyRule.Sensitive),
                IsPass = false,
                DecisionCode = constraintIssue.Code,
                DecisionMessage = constraintIssue.Message,
                MatchedRuleBy = matchedRuleBy,
                SourceOrderUsed = sourceOrderUsed,
                CandidatePaths = candidatePaths
            };
            return true;
        }

        result = new ExplainResult
        {
            Environment = environment,
            RequestedKey = requestedKey,
            RulePath = keyRule.Path,
            RuleType = keyRule.Type,
            ResolvedPath = foundPath,
            ResolvedSource = foundValue.SourceKind,
            ResolvedFrom = foundValue.SourceFile,
            ResolvedValueDisplay = DisplayValue(foundValue.Value, keyRule.Sensitive),
            IsPass = true,
            DecisionCode = "pass",
            DecisionMessage = "Key satisfies all applicable rules.",
            MatchedRuleBy = matchedRuleBy,
            SourceOrderUsed = sourceOrderUsed,
            CandidatePaths = candidatePaths
        };
        return true;
    }

    private static ContractKeyRule? FindRule(IEnumerable<ContractKeyRule> rules, string requestedKey, out string matchedRuleBy)
    {
        matchedRuleBy = "path";
        var normalized = RuleEvaluation.NormalizePath(requestedKey);
        foreach (var rule in rules)
        {
            if (rule.Aliases.Any(a => string.Equals(RuleEvaluation.NormalizePath(a), normalized, StringComparison.OrdinalIgnoreCase)))
            {
                matchedRuleBy = "alias";
                return rule;
            }

            if (string.Equals(RuleEvaluation.NormalizePath(rule.Path), normalized, StringComparison.OrdinalIgnoreCase))
            {
                matchedRuleBy = "path";
                return rule;
            }
        }

        return null;
    }

    private static string DisplayValue(JsonElement value, bool sensitive) =>
        sensitive ? "<redacted>" : value.GetRawText();
}
