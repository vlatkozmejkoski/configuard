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
        var rootPath = Path.GetFullPath(repoRoot);

        if (sources.AppSettings is not null)
        {
            var basePath = ResolvePathUnderRoot(rootPath, sources.AppSettings.Base!);
            if (!TryLoadJsonInto(appSettingsValues, basePath))
            {
                throw new ValidationInputException($"Required appsettings source file not found: {basePath}");
            }

            var envFileName = sources.AppSettings.EnvironmentPattern!.Replace("{env}", environment, StringComparison.OrdinalIgnoreCase);
            var envPath = ResolvePathUnderRoot(rootPath, envFileName);
            TryLoadJsonInto(appSettingsValues, envPath);
        }

        if (sources.DotEnv is not null)
        {
            var dotEnvBasePath = ResolvePathUnderRoot(rootPath, sources.DotEnv.Base!);
            if (!TryLoadDotEnvInto(dotEnvValues, dotEnvBasePath) && !sources.DotEnv.Optional)
            {
                throw new ValidationInputException($"Required dotenv source file not found: {dotEnvBasePath}");
            }

            var dotEnvEnvFileName = sources.DotEnv.EnvironmentPattern!.Replace("{env}", environment, StringComparison.OrdinalIgnoreCase);
            var dotEnvEnvPath = ResolvePathUnderRoot(rootPath, dotEnvEnvFileName);
            if (!TryLoadDotEnvInto(dotEnvValues, dotEnvEnvPath) && !sources.DotEnv.Optional)
            {
                throw new ValidationInputException($"Required dotenv source file not found: {dotEnvEnvPath}");
            }
        }

        if (sources.EnvSnapshot is not null)
        {
            var snapshotFileName = sources.EnvSnapshot.EnvironmentPattern!.Replace("{env}", environment, StringComparison.OrdinalIgnoreCase);
            var snapshotPath = ResolvePathUnderRoot(rootPath, snapshotFileName);
            if (!TryLoadEnvSnapshotInto(envSnapshotValues, snapshotPath) && !sources.EnvSnapshot.Optional)
            {
                throw new ValidationInputException($"Required envSnapshot source file not found: {snapshotPath}");
            }
        }

        return new ResolvedConfigBySource(appSettingsValues, dotEnvValues, envSnapshotValues);
    }

    private static string ResolvePathUnderRoot(string rootPath, string configuredPath)
    {
        var candidatePath = Path.GetFullPath(Path.Combine(rootPath, configuredPath));
        if (IsPathUnderRoot(rootPath, candidatePath))
        {
            return candidatePath;
        }

        throw new ValidationInputException(
            $"Configured source path '{configuredPath}' resolves outside the contract directory.");
    }

    private static bool IsPathUnderRoot(string rootPath, string candidatePath)
    {
        var rootWithSeparator = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(candidatePath, rootPath, comparison) ||
               candidatePath.StartsWith(rootWithSeparator, comparison);
    }

    private static bool TryLoadJsonInto(Dictionary<string, ResolvedConfigValue> values, string path)
    {
        try
        {
            LoadJsonInto(values, path);
            return true;
        }
        catch (ValidationInputException ex) when (ex.InnerException is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static bool TryLoadDotEnvInto(Dictionary<string, ResolvedConfigValue> values, string path)
    {
        try
        {
            LoadDotEnvInto(values, path);
            return true;
        }
        catch (ValidationInputException ex) when (ex.InnerException is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static bool TryLoadEnvSnapshotInto(Dictionary<string, ResolvedConfigValue> values, string path)
    {
        try
        {
            LoadEnvSnapshotInto(values, path);
            return true;
        }
        catch (ValidationInputException ex) when (ex.InnerException is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
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
            throw new ValidationInputException($"Failed to read source file '{path}': {ex.Message}", ex);
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
            throw new ValidationInputException($"Failed to read source file '{path}': {ex.Message}", ex);
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
            throw new ValidationInputException($"Failed to read source file '{path}': {ex.Message}", ex);
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
