using System.Text.Json;

namespace Configuard.Cli.Validation;

internal static class AppSettingsResolver
{
    public static Dictionary<string, JsonElement> Resolve(
        string repoRoot,
        ContractSources sources,
        string environment)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (sources.AppSettings is not null)
        {
            var basePath = Path.Combine(repoRoot, sources.AppSettings.Base!);
            if (File.Exists(basePath))
            {
                LoadJsonInto(values, basePath);
            }

            var envFileName = sources.AppSettings.EnvironmentPattern!.Replace("{env}", environment, StringComparison.OrdinalIgnoreCase);
            var envPath = Path.Combine(repoRoot, envFileName);
            if (File.Exists(envPath))
            {
                LoadJsonInto(values, envPath);
            }
        }

        if (sources.DotEnv is not null)
        {
            var dotEnvBasePath = Path.Combine(repoRoot, sources.DotEnv.Base!);
            if (File.Exists(dotEnvBasePath))
            {
                LoadDotEnvInto(values, dotEnvBasePath);
            }

            var dotEnvEnvFileName = sources.DotEnv.EnvironmentPattern!.Replace("{env}", environment, StringComparison.OrdinalIgnoreCase);
            var dotEnvEnvPath = Path.Combine(repoRoot, dotEnvEnvFileName);
            if (File.Exists(dotEnvEnvPath))
            {
                LoadDotEnvInto(values, dotEnvEnvPath);
            }
        }

        return values;
    }

    private static void LoadJsonInto(Dictionary<string, JsonElement> values, string path)
    {
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        Flatten(document.RootElement, null, values);
    }

    private static void LoadDotEnvInto(Dictionary<string, JsonElement> values, string path)
    {
        var parsed = DotEnvParser.ParseFile(path);
        foreach (var pair in parsed)
        {
            values[pair.Key] = pair.Value;
        }
    }

    private static void Flatten(JsonElement element, string? prefix, Dictionary<string, JsonElement> values)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                values[prefix] = element.Clone();
            }

            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var currentPath = string.IsNullOrWhiteSpace(prefix)
                ? property.Name
                : $"{prefix}:{property.Name}";

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                Flatten(property.Value, currentPath, values);
                continue;
            }

            values[currentPath] = property.Value.Clone();
        }
    }
}
