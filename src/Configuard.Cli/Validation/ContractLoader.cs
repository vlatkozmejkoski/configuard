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

            if (!ContractEnvironmentRulesValidator.TryValidate(contract.Environments, out error))
            {
                return false;
            }

            if (contract.Keys.Count == 0)
            {
                error = "Contract must define at least one key rule in 'keys'.";
                return false;
            }

            if (!ContractSourceRulesValidator.TryValidate(contract.Sources, out error))
            {
                return false;
            }

            if (!ContractKeyRulesValidator.TryValidate(contract.Keys, contract.Environments, out error))
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
}
