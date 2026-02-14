using System.Text.Json;

namespace Configuard.Cli.Validation;

internal static class DiffOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string ToText(
        string contractPath,
        string leftEnvironment,
        string rightEnvironment,
        DiffResult result,
        bool detailed = false)
    {
        var lines = new List<string>
        {
            "Configuard diff",
            $"Contract: {contractPath}",
            $"Environments: {leftEnvironment}, {rightEnvironment}",
            string.Empty
        };

        if (result.IsClean)
        {
            lines.Add("No differences.");
            return string.Join(Environment.NewLine, lines);
        }

        lines.Add("Differences:");
        foreach (var issue in result.Issues)
        {
            lines.Add($"- {issue.Path}: {issue.Message} ({issue.Kind})");
        }

        lines.Add(string.Empty);
        lines.Add($"Summary: {result.Issues.Count} difference(s)");

        if (detailed && result.Issues.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Difference counts by kind:");
            foreach (var group in result.Issues.GroupBy(i => i.Kind, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                lines.Add($"- {group.Key}: {group.Count()}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string ToJson(
        string contractPath,
        string leftEnvironment,
        string rightEnvironment,
        DiffResult result)
    {
        var payload = new
        {
            command = "diff",
            contract = contractPath,
            environments = new[] { leftEnvironment, rightEnvironment },
            result = result.IsClean ? "clean" : "different",
            summary = new
            {
                differenceCount = result.Issues.Count
            },
            differences = result.Issues.Select(i => new
            {
                path = i.Path,
                kind = i.Kind,
                leftEnvironment = i.LeftEnvironment,
                rightEnvironment = i.RightEnvironment,
                message = i.Message
            })
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
