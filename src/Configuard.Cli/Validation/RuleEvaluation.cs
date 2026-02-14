using System.Text.Json;
using System.Text.RegularExpressions;

namespace Configuard.Cli.Validation;

internal static class RuleEvaluation
{
    public static List<string> GetCandidatePaths(ContractKeyRule keyRule)
    {
        var all = new List<string>(capacity: 1 + keyRule.Aliases.Count)
        {
            NormalizePath(keyRule.Path)
        };

        foreach (var alias in keyRule.Aliases)
        {
            all.Add(NormalizePath(alias));
        }

        return all;
    }

    public static string NormalizePath(string path) =>
        path.Replace("__", ":", StringComparison.Ordinal);

    public static bool MatchesType(string expectedType, JsonElement value) =>
        expectedType.ToLowerInvariant() switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "int" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            "bool" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            _ => false
        };

    public static IEnumerable<ValidationIssue> EvaluateConstraints(
        string environment,
        string path,
        JsonElement value,
        JsonElement constraints)
    {
        if (constraints.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || constraints.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        if (constraints.TryGetProperty("enum", out var enumValues) &&
            enumValues.ValueKind == JsonValueKind.Array &&
            !MatchesEnum(value, enumValues))
        {
            yield return new ValidationIssue(environment, path, "constraint_enum", "Value is not in allowed enum list.");
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString() ?? string.Empty;

            if (constraints.TryGetProperty("minLength", out var minLengthEl) &&
                minLengthEl.ValueKind == JsonValueKind.Number &&
                minLengthEl.TryGetInt32(out var minLength) &&
                text.Length < minLength)
            {
                yield return new ValidationIssue(environment, path, "constraint_minLength", $"String length is {text.Length}, minimum is {minLength}.");
            }

            if (constraints.TryGetProperty("maxLength", out var maxLengthEl) &&
                maxLengthEl.ValueKind == JsonValueKind.Number &&
                maxLengthEl.TryGetInt32(out var maxLength) &&
                text.Length > maxLength)
            {
                yield return new ValidationIssue(environment, path, "constraint_maxLength", $"String length is {text.Length}, maximum is {maxLength}.");
            }

            if (constraints.TryGetProperty("pattern", out var patternEl) &&
                patternEl.ValueKind == JsonValueKind.String)
            {
                var pattern = patternEl.GetString() ?? string.Empty;
                bool isMatch = false;
                bool patternInvalid = false;
                try
                {
                    isMatch = Regex.IsMatch(text, pattern);
                }
                catch (ArgumentException)
                {
                    patternInvalid = true;
                }

                if (patternInvalid)
                {
                    yield return new ValidationIssue(environment, path, "constraint_pattern_invalid", $"Regex pattern is invalid: '{pattern}'.");
                }
                else if (!isMatch)
                {
                    yield return new ValidationIssue(environment, path, "constraint_pattern", $"Value does not match regex pattern '{pattern}'.");
                }
            }
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            var number = value.GetDouble();

            if (constraints.TryGetProperty("minimum", out var minimumEl) &&
                minimumEl.ValueKind == JsonValueKind.Number &&
                number < minimumEl.GetDouble())
            {
                yield return new ValidationIssue(environment, path, "constraint_minimum", $"Numeric value is {number}, minimum is {minimumEl.GetDouble()}.");
            }

            if (constraints.TryGetProperty("maximum", out var maximumEl) &&
                maximumEl.ValueKind == JsonValueKind.Number &&
                number > maximumEl.GetDouble())
            {
                yield return new ValidationIssue(environment, path, "constraint_maximum", $"Numeric value is {number}, maximum is {maximumEl.GetDouble()}.");
            }
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            var count = value.GetArrayLength();

            if (constraints.TryGetProperty("minItems", out var minItemsEl) &&
                minItemsEl.ValueKind == JsonValueKind.Number &&
                minItemsEl.TryGetInt32(out var minItems) &&
                count < minItems)
            {
                yield return new ValidationIssue(environment, path, "constraint_minItems", $"Array item count is {count}, minimum is {minItems}.");
            }

            if (constraints.TryGetProperty("maxItems", out var maxItemsEl) &&
                maxItemsEl.ValueKind == JsonValueKind.Number &&
                maxItemsEl.TryGetInt32(out var maxItems) &&
                count > maxItems)
            {
                yield return new ValidationIssue(environment, path, "constraint_maxItems", $"Array item count is {count}, maximum is {maxItems}.");
            }
        }
    }

    private static bool MatchesEnum(JsonElement value, JsonElement allowedValues)
    {
        foreach (var allowed in allowedValues.EnumerateArray())
        {
            if (IsEqual(value, allowed))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEqual(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        return left.ValueKind switch
        {
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.Number => left.GetDouble().Equals(right.GetDouble()),
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => left.GetRawText() == right.GetRawText()
        };
    }
}
