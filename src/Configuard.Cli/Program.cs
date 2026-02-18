using Configuard.Cli.Cli;

if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
{
    PrintUsage();
    return ExitCodes.Success;
}

if (args.Length == 1 && args[0] is "--version" or "-v" or "version")
{
    Console.WriteLine(CliVersionProvider.GetDisplayVersion());
    return ExitCodes.Success;
}

if (!CommandParser.TryParse(args, out var command, out var parseError))
{
    Console.Error.WriteLine(parseError);
    Console.Error.WriteLine();
    PrintUsage();
    return ExitCodes.InputError;
}

try
{
    return CommandHandlers.Execute(command!);
}
catch (Exception ex)
{
    Console.Error.WriteLine("Unhandled error.");
    Console.Error.WriteLine(ex.Message);
    return ExitCodes.InternalError;
}

static void PrintUsage()
{
    Console.WriteLine("Configuard CLI (v0)");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  configuard --version");
    Console.WriteLine("  configuard validate [--contract <path>] [--env <name>] [--format <text|json|sarif>] [--verbosity <quiet|normal|detailed>] [--no-color]");
    Console.WriteLine("  configuard diff [--contract <path>] --env <left> --env <right> [--format <text|json>] [--verbosity <quiet|normal|detailed>] [--no-color]");
    Console.WriteLine("  configuard explain [--contract <path>] --env <name> --key <path> [--format <text|json>] [--verbosity <quiet|normal|detailed>] [--no-color]");
}
