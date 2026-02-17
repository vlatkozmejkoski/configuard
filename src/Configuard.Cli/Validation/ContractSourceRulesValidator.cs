namespace Configuard.Cli.Validation;

internal static class ContractSourceRulesValidator
{
    public static bool TryValidate(ContractSources sources, out string? error)
    {
        error = null;

        if (sources.AppSettings is null)
        {
            error = "Missing required source configuration: sources.appsettings";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sources.AppSettings.Base) ||
            string.IsNullOrWhiteSpace(sources.AppSettings.EnvironmentPattern))
        {
            error = "sources.appsettings.base and sources.appsettings.environmentPattern are required.";
            return false;
        }

        if (!sources.AppSettings.EnvironmentPattern.Contains("{env}", StringComparison.OrdinalIgnoreCase))
        {
            error = "sources.appsettings.environmentPattern must include '{env}' placeholder.";
            return false;
        }

        if (sources.DotEnv is not null &&
            (string.IsNullOrWhiteSpace(sources.DotEnv.Base) ||
             string.IsNullOrWhiteSpace(sources.DotEnv.EnvironmentPattern)))
        {
            error = "sources.dotenv.base and sources.dotenv.environmentPattern are required when dotenv source is configured.";
            return false;
        }

        if (sources.DotEnv is not null &&
            !sources.DotEnv.EnvironmentPattern!.Contains("{env}", StringComparison.OrdinalIgnoreCase))
        {
            error = "sources.dotenv.environmentPattern must include '{env}' placeholder.";
            return false;
        }

        if (sources.EnvSnapshot is not null &&
            string.IsNullOrWhiteSpace(sources.EnvSnapshot.EnvironmentPattern))
        {
            error = "sources.envSnapshot.environmentPattern is required when envSnapshot source is configured.";
            return false;
        }

        if (sources.EnvSnapshot is not null &&
            !sources.EnvSnapshot.EnvironmentPattern!.Contains("{env}", StringComparison.OrdinalIgnoreCase))
        {
            error = "sources.envSnapshot.environmentPattern must include '{env}' placeholder.";
            return false;
        }

        return true;
    }
}
