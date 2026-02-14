using System.Text.Json;

namespace Configuard.Cli.Validation;

internal static class ValidateOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string ToText(
        string contractPath,
        IReadOnlyList<string> environments,
        ValidationResult result,
        bool detailed = false)
    {
        var lines = new List<string>
        {
            "Configuard validate",
            $"Contract: {contractPath}",
            $"Environments: {string.Join(", ", environments)}",
            string.Empty
        };

        if (result.Warnings.Count > 0)
        {
            lines.Add("Warnings:");
            foreach (var warning in result.Warnings)
            {
                lines.Add($"- {warning.Path}: {warning.Message} ({warning.Code})");
            }
            lines.Add(string.Empty);
        }

        if (result.IsSuccess)
        {
            lines.Add("PASS");
            return string.Join(Environment.NewLine, lines);
        }

        lines.Add("FAIL");
        foreach (var issue in result.Issues)
        {
            lines.Add($"- [{issue.Environment}] {issue.Path}: {issue.Message} ({issue.Code})");
        }

        lines.Add(string.Empty);
        lines.Add($"Summary: {result.Issues.Count} violation(s)");

        if (detailed && result.Issues.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Issue counts by code:");
            foreach (var group in result.Issues.GroupBy(i => i.Code, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                lines.Add($"- {group.Key}: {group.Count()}");
            }

            lines.Add("Issue counts by environment:");
            foreach (var group in result.Issues.GroupBy(i => i.Environment, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"- {group.Key}: {group.Count()}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string ToJson(
        string contractPath,
        IReadOnlyList<string> environments,
        ValidationResult result)
    {
        var payload = new
        {
            command = "validate",
            contract = contractPath,
            result = result.IsSuccess ? "pass" : "fail",
            environments,
            summary = new
            {
                violationCount = result.Issues.Count,
                warningCount = result.Warnings.Count
            },
            warnings = result.Warnings.Select(w => new
            {
                path = w.Path,
                code = w.Code,
                message = w.Message
            }),
            violations = result.Issues.Select(i => new
            {
                environment = i.Environment,
                path = i.Path,
                code = i.Code,
                message = i.Message
            })
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string ToSarif(
        string contractPath,
        IReadOnlyList<string> environments,
        ValidationResult result)
    {
        var sarif = new
        {
            version = "2.1.0",
            schema = "https://json.schemastore.org/sarif-2.1.0.json",
            runs = new[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = "Configuard",
                            informationUri = "https://github.com/your-org/configuard",
                            rules = result.Issues
                                .Select(i => i.Code)
                                .Concat(result.Warnings.Select(w => w.Code))
                                .Distinct(StringComparer.Ordinal)
                                .OrderBy(code => code, StringComparer.Ordinal)
                                .Select(code => new
                                {
                                    id = $"CG-{code}",
                                    name = code,
                                    shortDescription = new { text = $"Configuard validation rule: {code}" }
                                })
                        }
                    },
                    properties = new
                    {
                        command = "validate",
                        contract = contractPath,
                        environments
                    },
                    results = result.Issues
                        .Select(i => new
                        {
                            ruleId = $"CG-{i.Code}",
                            level = "error",
                            message = new { text = $"[{i.Environment}] {i.Path}: {i.Message}" },
                            locations = new[]
                            {
                                new
                                {
                                    physicalLocation = new
                                    {
                                        artifactLocation = new { uri = contractPath }
                                    }
                                }
                            },
                            properties = new
                            {
                                environment = i.Environment,
                                path = i.Path,
                                code = i.Code
                            }
                        })
                        .Concat(
                            result.Warnings.Select(w => new
                            {
                                ruleId = $"CG-{w.Code}",
                                level = "warning",
                                message = new { text = $"{w.Path}: {w.Message}" },
                                locations = new[]
                                {
                                    new
                                    {
                                        physicalLocation = new
                                        {
                                            artifactLocation = new { uri = contractPath }
                                        }
                                    }
                                },
                                properties = new
                                {
                                    environment = string.Empty,
                                    path = w.Path,
                                    code = w.Code
                                }
                            }))
                }
            }
        };

        return JsonSerializer.Serialize(sarif, JsonOptions);
    }
}
