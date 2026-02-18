using Configuard.Cli.Validation;
using Configuard.Cli.Discovery;

namespace Configuard.Cli.Cli;

internal static class CommandHandlers
{
    private static readonly string[] ValidateFormats = ["text", "json", "sarif"];
    private static readonly string[] DiffFormats = ["text", "json"];
    private static readonly string[] ExplainFormats = ["text", "json"];
    private static readonly string[] DiscoverFormats = ["json"];

    public static int Execute(ParsedCommand command)
    {
        // Placeholder implementations: wire command contracts first, then add features incrementally.
        return command.Name switch
        {
            "validate" => HandleValidate(command),
            "diff" => HandleDiff(command),
            "explain" => HandleExplain(command),
            "discover" => HandleDiscover(command),
            _ => ExitCodes.InputError
        };
    }

    private static int HandleValidate(ParsedCommand command)
    {
        if (!TryNormalizeVerbosity(command.Verbosity, out var verbosity))
        {
            return ExitCodes.InputError;
        }

        if (!TryLoadContract(command.ContractPath, out var contractPath, out var contract))
        {
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
        catch (ValidationInputException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.InputError;
        }
        var environments = command.Environments.Count == 0
            ? contract!.Environments
            : command.Environments.ToList();

        if (!TryNormalizeFormat("validate", command.OutputFormat, ValidateFormats, out var format))
        {
            return ExitCodes.InputError;
        }

        if (format == "json")
        {
            WriteIfNotQuiet(verbosity, () => ValidateOutputFormatter.ToJson(contractPath, environments, result));
        }
        else if (format == "sarif")
        {
            WriteIfNotQuiet(verbosity, () => ValidateOutputFormatter.ToSarif(contractPath, environments, result));
        }
        else
        {
            WriteIfNotQuiet(
                verbosity,
                () => ValidateOutputFormatter.ToText(
                    contractPath,
                    environments,
                    result,
                    detailed: verbosity == Verbosity.Detailed));
        }

        return result.IsSuccess ? ExitCodes.Success : ExitCodes.PolicyFailure;
    }

    private static int HandleDiff(ParsedCommand command)
    {
        if (!TryNormalizeVerbosity(command.Verbosity, out var verbosity))
        {
            return ExitCodes.InputError;
        }

        if (command.Environments.Count != 2)
        {
            Console.Error.WriteLine("diff requires exactly two --env values.");
            return ExitCodes.InputError;
        }

        if (!TryLoadContract(command.ContractPath, out var contractPath, out var contract))
        {
            return ExitCodes.InputError;
        }

        var leftEnvironment = command.Environments[0];
        var rightEnvironment = command.Environments[1];
        DiffResult result;
        try
        {
            result = ContractDiffer.Diff(contract!, GetContractBaseDirectory(contractPath), leftEnvironment, rightEnvironment);
        }
        catch (ValidationInputException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.InputError;
        }

        if (!TryNormalizeFormat("diff", command.OutputFormat, DiffFormats, out var format))
        {
            return ExitCodes.InputError;
        }

        if (format == "json")
        {
            WriteIfNotQuiet(verbosity, () => DiffOutputFormatter.ToJson(contractPath, leftEnvironment, rightEnvironment, result));
        }
        else
        {
            WriteIfNotQuiet(
                verbosity,
                () => DiffOutputFormatter.ToText(
                    contractPath,
                    leftEnvironment,
                    rightEnvironment,
                    result,
                    detailed: verbosity == Verbosity.Detailed));
        }

        return result.IsClean ? ExitCodes.Success : ExitCodes.PolicyFailure;
    }

    private static int HandleExplain(ParsedCommand command)
    {
        if (!TryNormalizeVerbosity(command.Verbosity, out var verbosity))
        {
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

        if (!TryLoadContract(command.ContractPath, out var contractPath, out var contract))
        {
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
        catch (ValidationInputException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.InputError;
        }

        if (!TryNormalizeFormat("explain", command.OutputFormat, ExplainFormats, out var format))
        {
            return ExitCodes.InputError;
        }

        if (format == "json")
        {
            WriteIfNotQuiet(verbosity, () => ExplainOutputFormatter.ToJson(result!, detailed: verbosity == Verbosity.Detailed));
        }
        else
        {
            WriteIfNotQuiet(verbosity, () => ExplainOutputFormatter.ToText(result!, detailed: verbosity == Verbosity.Detailed));
        }

        return result!.IsPass ? ExitCodes.Success : ExitCodes.PolicyFailure;
    }

    private static int HandleDiscover(ParsedCommand command)
    {
        if (!TryNormalizeVerbosity(command.Verbosity, out var verbosity))
        {
            return ExitCodes.InputError;
        }

        if (command.Apply)
        {
            Console.Error.WriteLine("discover --apply is not implemented yet.");
            return ExitCodes.InputError;
        }

        var requestedFormat = string.IsNullOrWhiteSpace(command.OutputFormat)
            ? "json"
            : command.OutputFormat;
        if (!TryNormalizeFormat("discover", requestedFormat, DiscoverFormats, out _))
        {
            return ExitCodes.InputError;
        }

        var scanPath = command.ScanPath ?? Environment.CurrentDirectory;
        var fullScanPath = Path.GetFullPath(scanPath);
        if (!Directory.Exists(fullScanPath) && !File.Exists(fullScanPath))
        {
            Console.Error.WriteLine($"Discover path does not exist: {scanPath}");
            return ExitCodes.InputError;
        }

        DiscoveryReport report;
        try
        {
            report = DiscoverEngine.Discover(
                fullScanPath,
                includePatterns: command.IncludePatterns,
                excludePatterns: command.ExcludePatterns);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Discover failed: {ex.Message}");
            return ExitCodes.InternalError;
        }

        var json = DiscoverOutputFormatter.ToJson(report);
        if (!string.IsNullOrWhiteSpace(command.OutputPath))
        {
            File.WriteAllText(command.OutputPath, json);
            WriteIfNotQuiet(verbosity, () => $"Discover report written: {command.OutputPath}");
            return ExitCodes.Success;
        }

        WriteIfNotQuiet(verbosity, () => json);
        return ExitCodes.Success;
    }

    private static bool TryNormalizeVerbosity(string? rawVerbosity, out string verbosity)
    {
        if (Verbosity.TryNormalize(rawVerbosity, out verbosity))
        {
            return true;
        }

        Console.Error.WriteLine($"Unsupported verbosity '{rawVerbosity}'. Supported: quiet, normal, detailed.");
        return false;
    }

    private static bool TryLoadContract(
        string? rawContractPath,
        out string contractPath,
        out ContractDocument? contract)
    {
        contractPath = rawContractPath ?? "configuard.contract.json";
        if (ContractLoader.TryLoad(contractPath, out contract, out var loadError))
        {
            return true;
        }

        Console.Error.WriteLine(loadError);
        return false;
    }

    private static bool TryNormalizeFormat(
        string commandName,
        string? rawFormat,
        IReadOnlyCollection<string> supportedFormats,
        out string normalizedFormat)
    {
        normalizedFormat = string.IsNullOrWhiteSpace(rawFormat)
            ? "text"
            : rawFormat.Trim().ToLowerInvariant();

        if (supportedFormats.Contains(normalizedFormat, StringComparer.Ordinal))
        {
            return true;
        }

        Console.Error.WriteLine(
            $"Unsupported {commandName} format '{rawFormat}'. Supported: {string.Join(", ", supportedFormats)}.");
        return false;
    }

    private static void WriteIfNotQuiet(string verbosity, Func<string> render)
    {
        if (verbosity == Verbosity.Quiet)
        {
            return;
        }

        Console.WriteLine(render());
    }

    private static string GetContractBaseDirectory(string contractPath)
    {
        var fullPath = Path.GetFullPath(contractPath);
        return Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
    }
}
