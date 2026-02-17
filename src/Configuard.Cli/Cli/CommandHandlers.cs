using Configuard.Cli.Validation;

namespace Configuard.Cli.Cli;

internal static class CommandHandlers
{
    public static int Execute(ParsedCommand command)
    {
        // Placeholder implementations: wire command contracts first, then add features incrementally.
        return command.Name switch
        {
            "validate" => HandleValidate(command),
            "diff" => HandleDiff(command),
            "explain" => HandleExplain(command),
            _ => ExitCodes.InputError
        };
    }

    private static int HandleValidate(ParsedCommand command)
    {
        if (!Verbosity.TryNormalize(command.Verbosity, out var verbosity))
        {
            Console.Error.WriteLine($"Unsupported verbosity '{command.Verbosity}'. Supported: quiet, normal, detailed.");
            return ExitCodes.InputError;
        }

        var contractPath = command.ContractPath ?? "configuard.contract.json";
        if (!ContractLoader.TryLoad(contractPath, out var contract, out var loadError))
        {
            Console.Error.WriteLine(loadError);
            return ExitCodes.InputError;
        }

        ValidationResult result;
        try
        {
            result = ContractValidator.Validate(
                contract!,
                repoRoot: GetContractBaseDirectory(contractPath),
                targetEnvironments: command.Environments);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.InputError;
        }
        var environments = command.Environments.Count == 0
            ? contract!.Environments
            : command.Environments.ToList();

        var format = string.IsNullOrWhiteSpace(command.OutputFormat)
            ? "text"
            : command.OutputFormat.Trim().ToLowerInvariant();

        if (format == "json")
        {
            if (verbosity != Verbosity.Quiet)
            {
                Console.WriteLine(ValidateOutputFormatter.ToJson(contractPath, environments, result));
            }
        }
        else if (format == "sarif")
        {
            if (verbosity != Verbosity.Quiet)
            {
                Console.WriteLine(ValidateOutputFormatter.ToSarif(contractPath, environments, result));
            }
        }
        else if (format == "text")
        {
            if (verbosity != Verbosity.Quiet)
            {
                Console.WriteLine(ValidateOutputFormatter.ToText(
                    contractPath,
                    environments,
                    result,
                    detailed: verbosity == Verbosity.Detailed));
            }
        }
        else
        {
            Console.Error.WriteLine($"Unsupported validate format '{command.OutputFormat}'. Supported: text, json, sarif.");
            return ExitCodes.InputError;
        }

        return result.IsSuccess ? ExitCodes.Success : ExitCodes.PolicyFailure;
    }

    private static int HandleDiff(ParsedCommand command)
    {
        if (!Verbosity.TryNormalize(command.Verbosity, out var verbosity))
        {
            Console.Error.WriteLine($"Unsupported verbosity '{command.Verbosity}'. Supported: quiet, normal, detailed.");
            return ExitCodes.InputError;
        }

        if (command.Environments.Count != 2)
        {
            Console.Error.WriteLine("diff requires exactly two --env values.");
            return ExitCodes.InputError;
        }

        var contractPath = command.ContractPath ?? "configuard.contract.json";
        if (!ContractLoader.TryLoad(contractPath, out var contract, out var loadError))
        {
            Console.Error.WriteLine(loadError);
            return ExitCodes.InputError;
        }

        var leftEnvironment = command.Environments[0];
        var rightEnvironment = command.Environments[1];
        DiffResult result;
        try
        {
            result = ContractDiffer.Diff(contract!, GetContractBaseDirectory(contractPath), leftEnvironment, rightEnvironment);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.InputError;
        }

        var format = string.IsNullOrWhiteSpace(command.OutputFormat)
            ? "text"
            : command.OutputFormat.Trim().ToLowerInvariant();

        if (format == "json")
        {
            if (verbosity != Verbosity.Quiet)
            {
                Console.WriteLine(DiffOutputFormatter.ToJson(contractPath, leftEnvironment, rightEnvironment, result));
            }
        }
        else if (format == "text")
        {
            if (verbosity != Verbosity.Quiet)
            {
                Console.WriteLine(DiffOutputFormatter.ToText(
                    contractPath,
                    leftEnvironment,
                    rightEnvironment,
                    result,
                    detailed: verbosity == Verbosity.Detailed));
            }
        }
        else
        {
            Console.Error.WriteLine($"Unsupported diff format '{command.OutputFormat}'. Supported: text, json.");
            return ExitCodes.InputError;
        }

        return result.IsClean ? ExitCodes.Success : ExitCodes.PolicyFailure;
    }

    private static int HandleExplain(ParsedCommand command)
    {
        if (!Verbosity.TryNormalize(command.Verbosity, out var verbosity))
        {
            Console.Error.WriteLine($"Unsupported verbosity '{command.Verbosity}'. Supported: quiet, normal, detailed.");
            return ExitCodes.InputError;
        }

        if (string.IsNullOrWhiteSpace(command.Key))
        {
            Console.Error.WriteLine("explain requires --key <path>.");
            return ExitCodes.InputError;
        }

        if (command.Environments.Count != 1)
        {
            Console.Error.WriteLine("explain requires exactly one --env value.");
            return ExitCodes.InputError;
        }

        var contractPath = command.ContractPath ?? "configuard.contract.json";
        if (!ContractLoader.TryLoad(contractPath, out var contract, out var loadError))
        {
            Console.Error.WriteLine(loadError);
            return ExitCodes.InputError;
        }

        var environment = command.Environments[0];
        ExplainResult? result;
        try
        {
            if (!ExplainEngine.TryExplain(contract!, GetContractBaseDirectory(contractPath), environment, command.Key, out result))
            {
                Console.Error.WriteLine($"Key '{command.Key}' not found in contract.");
                return ExitCodes.KeyNotFound;
            }
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.InputError;
        }

        var format = string.IsNullOrWhiteSpace(command.OutputFormat)
            ? "text"
            : command.OutputFormat.Trim().ToLowerInvariant();

        if (format == "json")
        {
            if (verbosity != Verbosity.Quiet)
            {
                Console.WriteLine(ExplainOutputFormatter.ToJson(result!, detailed: verbosity == Verbosity.Detailed));
            }
        }
        else if (format == "text")
        {
            if (verbosity != Verbosity.Quiet)
            {
                Console.WriteLine(ExplainOutputFormatter.ToText(result!, detailed: verbosity == Verbosity.Detailed));
            }
        }
        else
        {
            Console.Error.WriteLine($"Unsupported explain format '{command.OutputFormat}'. Supported: text, json.");
            return ExitCodes.InputError;
        }

        return result!.IsPass ? ExitCodes.Success : ExitCodes.PolicyFailure;
    }

    private static string GetContractBaseDirectory(string contractPath)
    {
        var fullPath = Path.GetFullPath(contractPath);
        return Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
    }
}
