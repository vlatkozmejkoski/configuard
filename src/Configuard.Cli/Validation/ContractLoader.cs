using System.Text.Json;

namespace Configuard.Cli.Validation;

internal static class ContractLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryLoad(string path, out ContractDocument? contract, out string? error)
    {
        contract = null;
        error = null;

        if (!File.Exists(path))
        {
            error = $"Contract file not found: {path}";
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            contract = JsonSerializer.Deserialize<ContractDocument>(json, JsonOptions);
            if (contract is null)
            {
                error = "Contract file is empty or invalid JSON.";
                return false;
            }

            if (contract.Version != "1")
            {
                error = $"Unsupported contract version '{contract.Version ?? "(null)"}'. Expected '1'.";
                return false;
            }

            if (contract.Environments.Count == 0)
            {
                error = "Contract must define at least one environment in 'environments'.";
                return false;
            }

            if (!TryValidateEnvironments(contract.Environments, out error))
            {
                return false;
            }

            if (contract.Keys.Count == 0)
            {
                error = "Contract must define at least one key rule in 'keys'.";
                return false;
            }

            if (contract.Sources.AppSettings is null)
            {
                error = "Missing required source configuration: sources.appsettings";
                return false;
            }

            if (string.IsNullOrWhiteSpace(contract.Sources.AppSettings.Base) ||
                string.IsNullOrWhiteSpace(contract.Sources.AppSettings.EnvironmentPattern))
            {
                error = "sources.appsettings.base and sources.appsettings.environmentPattern are required.";
                return false;
            }

            if (!contract.Sources.AppSettings.EnvironmentPattern.Contains("{env}", StringComparison.OrdinalIgnoreCase))
            {
                error = "sources.appsettings.environmentPattern must include '{env}' placeholder.";
                return false;
            }

            if (contract.Sources.DotEnv is not null &&
                (string.IsNullOrWhiteSpace(contract.Sources.DotEnv.Base) ||
                 string.IsNullOrWhiteSpace(contract.Sources.DotEnv.EnvironmentPattern)))
            {
                error = "sources.dotenv.base and sources.dotenv.environmentPattern are required when dotenv source is configured.";
                return false;
            }

            if (contract.Sources.DotEnv is not null &&
                !contract.Sources.DotEnv.EnvironmentPattern!.Contains("{env}", StringComparison.OrdinalIgnoreCase))
            {
                error = "sources.dotenv.environmentPattern must include '{env}' placeholder.";
                return false;
            }

            if (contract.Sources.EnvSnapshot is not null &&
                string.IsNullOrWhiteSpace(contract.Sources.EnvSnapshot.EnvironmentPattern))
            {
                error = "sources.envSnapshot.environmentPattern is required when envSnapshot source is configured.";
                return false;
            }

            if (contract.Sources.EnvSnapshot is not null &&
                !contract.Sources.EnvSnapshot.EnvironmentPattern!.Contains("{env}", StringComparison.OrdinalIgnoreCase))
            {
                error = "sources.envSnapshot.environmentPattern must include '{env}' placeholder.";
                return false;
            }

            if (!TryValidateKeyRules(contract.Keys, contract.Environments, out error))
            {
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = $"Contract JSON parse error: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Failed to load contract: {ex.Message}";
            return false;
        }
    }

    private static bool TryValidateKeyRules(
        IReadOnlyList<ContractKeyRule> keys,
        IReadOnlyList<string> environments,
        out string? error)
    {
        error = null;
        var seenIdentifiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var declaredEnvironments = new HashSet<string>(
            environments.Select(environment => environment.Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            var canonicalPath = RuleEvaluation.NormalizePath(key.Path).Trim();
            if (string.IsNullOrWhiteSpace(canonicalPath))
            {
                error = "keys[].path must not be empty.";
                return false;
            }

            if (seenIdentifiers.TryGetValue(canonicalPath, out var existingOwner))
            {
                error = $"Duplicate key path or alias '{canonicalPath}' conflicts with '{existingOwner}'.";
                return false;
            }

            seenIdentifiers[canonicalPath] = key.Path;

            foreach (var alias in key.Aliases)
            {
                var canonicalAlias = RuleEvaluation.NormalizePath(alias).Trim();
                if (string.IsNullOrWhiteSpace(canonicalAlias))
                {
                    error = $"Key '{key.Path}' contains an empty alias.";
                    return false;
                }

                if (seenIdentifiers.TryGetValue(canonicalAlias, out existingOwner))
                {
                    error = $"Duplicate key path or alias '{canonicalAlias}' conflicts with '{existingOwner}'.";
                    return false;
                }

                seenIdentifiers[canonicalAlias] = key.Path;
            }

            foreach (var env in key.RequiredIn)
            {
                if (key.ForbiddenIn.Contains(env, StringComparer.OrdinalIgnoreCase))
                {
                    error = $"Key '{key.Path}' cannot be both required and forbidden in environment '{env}'.";
                    return false;
                }
            }

            if (!TryValidateRuleEnvironments(key.Path, "requiredIn", key.RequiredIn, declaredEnvironments, out error))
            {
                return false;
            }

            if (!TryValidateRuleEnvironments(key.Path, "forbiddenIn", key.ForbiddenIn, declaredEnvironments, out error))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateEnvironments(IReadOnlyList<string> environments, out string? error)
    {
        error = null;
        var seenEnvironments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var environment in environments)
        {
            var canonicalEnvironment = environment.Trim();
            if (string.IsNullOrWhiteSpace(canonicalEnvironment))
            {
                error = "environments[] values must not be empty or whitespace.";
                return false;
            }

            if (!seenEnvironments.Add(canonicalEnvironment))
            {
                error = $"Duplicate environment '{canonicalEnvironment}' is not allowed.";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateRuleEnvironments(
        string keyPath,
        string propertyName,
        IReadOnlyList<string> ruleEnvironments,
        HashSet<string> declaredEnvironments,
        out string? error)
    {
        error = null;

        foreach (var environment in ruleEnvironments)
        {
            var canonicalEnvironment = environment.Trim();
            if (string.IsNullOrWhiteSpace(canonicalEnvironment))
            {
                error = $"Key '{keyPath}' contains an empty '{propertyName}' environment.";
                return false;
            }

            if (!declaredEnvironments.Contains(canonicalEnvironment))
            {
                error = $"Key '{keyPath}' references undeclared environment '{canonicalEnvironment}' in '{propertyName}'.";
                return false;
            }
        }

        return true;
    }
}
