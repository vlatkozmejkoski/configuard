using System.Text.Json;

namespace Configuard.Cli.Validation;

internal static class ExplainOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string ToText(ExplainResult result, bool detailed = false)
    {
        var lines = new List<string>
        {
            "Configuard explain",
            $"Environment: {result.Environment}",
            $"Requested key: {result.RequestedKey}",
            $"Rule path: {result.RulePath}",
            $"Rule type: {result.RuleType}",
            $"Decision: {(result.IsPass ? "PASS" : "FAIL")} ({result.DecisionCode})",
            $"Reason: {result.DecisionMessage}"
        };

        if (!string.IsNullOrWhiteSpace(result.ResolvedPath))
        {
            lines.Add($"Resolved path: {result.ResolvedPath}");
        }

        if (!string.IsNullOrWhiteSpace(result.ResolvedSource))
        {
            lines.Add($"Resolved source: {result.ResolvedSource}");
        }

        if (!string.IsNullOrWhiteSpace(result.ResolvedFrom))
        {
            lines.Add($"Resolved from: {result.ResolvedFrom}");
        }

        if (!string.IsNullOrWhiteSpace(result.ResolvedValueDisplay))
        {
            lines.Add($"Resolved value: {result.ResolvedValueDisplay}");
        }

        if (detailed)
        {
            lines.Add($"Matched rule by: {result.MatchedRuleBy}");
            if (result.SourceOrderUsed.Count > 0)
            {
                lines.Add($"Source order used: {string.Join(", ", result.SourceOrderUsed)}");
            }

            if (result.CandidatePaths.Count > 0)
            {
                lines.Add($"Candidate paths: {string.Join(", ", result.CandidatePaths)}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string ToJson(ExplainResult result, bool detailed = false)
    {
        var payload = new
        {
            command = "explain",
            environment = result.Environment,
            requestedKey = result.RequestedKey,
            rule = new
            {
                path = result.RulePath,
                type = result.RuleType
            },
            decision = new
            {
                pass = result.IsPass,
                code = result.DecisionCode,
                message = result.DecisionMessage
            },
            resolution = new
            {
                resolvedPath = result.ResolvedPath,
                resolvedSource = result.ResolvedSource,
                resolvedFrom = result.ResolvedFrom,
                resolvedValue = result.ResolvedValueDisplay
            },
            diagnostics = detailed
                ? new
                {
                    matchedRuleBy = result.MatchedRuleBy,
                    sourceOrderUsed = result.SourceOrderUsed,
                    candidatePaths = result.CandidatePaths
                }
                : null
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
