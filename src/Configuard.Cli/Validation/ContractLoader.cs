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

            if (contract.Sources.DotEnv is not null &&
                (string.IsNullOrWhiteSpace(contract.Sources.DotEnv.Base) ||
                 string.IsNullOrWhiteSpace(contract.Sources.DotEnv.EnvironmentPattern)))
            {
                error = "sources.dotenv.base and sources.dotenv.environmentPattern are required when dotenv source is configured.";
                return false;
            }

            if (contract.Sources.EnvSnapshot is not null &&
                string.IsNullOrWhiteSpace(contract.Sources.EnvSnapshot.EnvironmentPattern))
            {
                error = "sources.envSnapshot.environmentPattern is required when envSnapshot source is configured.";
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
}
