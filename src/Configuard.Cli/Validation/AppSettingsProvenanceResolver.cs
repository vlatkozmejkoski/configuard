using System.Text.Json;

namespace Configuard.Cli.Validation;

internal sealed record ResolvedConfigValue(JsonElement Value, string SourceFile, string SourceKind);
internal sealed record ResolvedConfigBySource(
    Dictionary<string, ResolvedConfigValue> AppSettings,
    Dictionary<string, ResolvedConfigValue> DotEnv,
    Dictionary<string, ResolvedConfigValue> EnvSnapshot);

internal static class AppSettingsProvenanceResolver
{
    public static ResolvedConfigBySource Resolve(
        string repoRoot,
        ContractSources sources,
        string environment)
    {
        var appSettingsValues = new Dictionary<string, ResolvedConfigValue>(StringComparer.OrdinalIgnoreCase);
        var dotEnvValues = new Dictionary<string, ResolvedConfigValue>(StringComparer.OrdinalIgnoreCase);
        var envSnapshotValues = new Dictionary<string, ResolvedConfigValue>(StringComparer.OrdinalIgnoreCase);

        if (sources.AppSettings is not null)
        {
            var basePath = Path.Combine(repoRoot, sources.AppSettings.Base!);
            if (File.Exists(basePath))
            {
                LoadJsonInto(appSettingsValues, basePath);
            }

            var envFileName = sources.AppSettings.EnvironmentPattern!.Replace("{env}", environment, StringComparison.OrdinalIgnoreCase);
            var envPath = Path.Combine(repoRoot, envFileName);
            if (File.Exists(envPath))
            {
                LoadJsonInto(appSettingsValues, envPath);
            }
        }

        if (sources.DotEnv is not null)
        {
            var dotEnvBasePath = Path.Combine(repoRoot, sources.DotEnv.Base!);
            if (File.Exists(dotEnvBasePath))
            {
                LoadDotEnvInto(dotEnvValues, dotEnvBasePath);
            }
            else if (!sources.DotEnv.Optional)
            {
                throw new InvalidOperationException($"Required dotenv source file not found: {dotEnvBasePath}");
            }

            var dotEnvEnvFileName = sources.DotEnv.EnvironmentPattern!.Replace("{env}", environment, StringComparison.OrdinalIgnoreCase);
            var dotEnvEnvPath = Path.Combine(repoRoot, dotEnvEnvFileName);
            if (File.Exists(dotEnvEnvPath))
            {
                LoadDotEnvInto(dotEnvValues, dotEnvEnvPath);
            }
            else if (!sources.DotEnv.Optional)
            {
                throw new InvalidOperationException($"Required dotenv source file not found: {dotEnvEnvPath}");
            }
        }

        if (sources.EnvSnapshot is not null)
        {
            var snapshotFileName = sources.EnvSnapshot.EnvironmentPattern!.Replace("{env}", environment, StringComparison.OrdinalIgnoreCase);
            var snapshotPath = Path.Combine(repoRoot, snapshotFileName);
            if (File.Exists(snapshotPath))
            {
                LoadEnvSnapshotInto(envSnapshotValues, snapshotPath);
            }
            else if (!sources.EnvSnapshot.Optional)
            {
                throw new InvalidOperationException($"Required envSnapshot source file not found: {snapshotPath}");
            }
        }

        return new ResolvedConfigBySource(appSettingsValues, dotEnvValues, envSnapshotValues);
    }

    private static void LoadJsonInto(Dictionary<string, ResolvedConfigValue> values, string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            Flatten(document.RootElement, null, path, values);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidOperationException($"Failed to read source file '{path}': {ex.Message}", ex);
        }
    }

    private static void LoadDotEnvInto(Dictionary<string, ResolvedConfigValue> values, string path)
    {
        Dictionary<string, JsonElement> parsed;
        try
        {
            parsed = DotEnvParser.ParseFile(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidOperationException($"Failed to read source file '{path}': {ex.Message}", ex);
        }

        foreach (var pair in parsed)
        {
            values[pair.Key] = new ResolvedConfigValue(pair.Value, path, SourceKinds.DotEnv);
        }
    }

    private static void LoadEnvSnapshotInto(Dictionary<string, ResolvedConfigValue> values, string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            Flatten(document.RootElement, null, path, values, SourceKinds.EnvSnapshot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidOperationException($"Failed to read source file '{path}': {ex.Message}", ex);
        }
    }

    private static void Flatten(
        JsonElement element,
        string? prefix,
        string sourceFile,
        Dictionary<string, ResolvedConfigValue> values,
        string sourceKind = SourceKinds.AppSettings)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                values[prefix] = new ResolvedConfigValue(element.Clone(), sourceFile, sourceKind);
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
                Flatten(property.Value, currentPath, sourceFile, values, sourceKind);
                continue;
            }

            values[currentPath] = new ResolvedConfigValue(property.Value.Clone(), sourceFile, sourceKind);
        }
    }
}
