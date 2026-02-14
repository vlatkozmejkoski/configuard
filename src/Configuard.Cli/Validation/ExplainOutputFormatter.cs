using System.Text.Json;

namespace Configuard.Cli.Validation;

internal static class ExplainOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string ToText(ExplainResult result)
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

        return string.Join(Environment.NewLine, lines);
    }

    public static string ToJson(ExplainResult result)
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
            }
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
