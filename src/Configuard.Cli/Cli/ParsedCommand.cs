namespace Configuard.Cli.Cli;

internal sealed record ParsedCommand(
    string Name,
    string? ContractPath,
    IReadOnlyList<string> Environments,
    string? OutputFormat,
    string? Verbosity,
    string? Key,
    bool NoColor = false,
    string? ScanPath = null,
    string? OutputPath = null,
    bool Apply = false);
