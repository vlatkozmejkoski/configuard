using System.Globalization;
using System.Text.Json;

namespace Configuard.Cli.Validation;

internal static class DotEnvParser
{
    public static Dictionary<string, JsonElement> ParseFile(string path)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var lines = File.ReadAllLines(path);

        foreach (var line in lines)
        {
            if (!TryParseLine(line, out var key, out var rawValue))
            {
                continue;
            }

            var normalizedKey = RuleEvaluation.NormalizePath(key);
            values[normalizedKey] = ParseScalar(rawValue);
        }

        return values;
    }

    private static bool TryParseLine(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        if (trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..].Trim();
        }

        var index = trimmed.IndexOf('=');
        if (index <= 0)
        {
            return false;
        }

        key = trimmed[..index].Trim();
        value = trimmed[(index + 1)..].Trim();

        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            value = value[1..^1];
        }

        return !string.IsNullOrWhiteSpace(key);
    }

    private static JsonElement ParseScalar(string raw)
    {
        if (bool.TryParse(raw, out var boolValue))
        {
            return FromJsonLiteral(boolValue ? "true" : "false");
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return FromJsonLiteral(intValue.ToString(CultureInfo.InvariantCulture));
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var numberValue))
        {
            return FromJsonLiteral(numberValue.ToString(CultureInfo.InvariantCulture));
        }

        return FromJsonLiteral(JsonSerializer.Serialize(raw));
    }

    private static JsonElement FromJsonLiteral(string literal)
    {
        using var doc = JsonDocument.Parse(literal);
        return doc.RootElement.Clone();
    }
}
