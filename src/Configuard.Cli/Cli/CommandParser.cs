using System.Collections.ObjectModel;

namespace Configuard.Cli.Cli;

internal static class CommandParser
{
    public static bool TryParse(string[] args, out ParsedCommand? command, out string? error)
    {
        command = null;
        error = null;

        if (args.Length == 0)
        {
            error = "No command provided.";
            return false;
        }

        var name = args[0].Trim().ToLowerInvariant();
        if (name is not ("validate" or "diff" or "explain"))
        {
            error = $"Unknown command '{args[0]}'.";
            return false;
        }

        string? contractPath = null;
        string? outputFormat = null;
        string? verbosity = null;
        string? key = null;
        var noColor = false;
        var envs = new List<string>();

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];
            switch (token)
            {
                case "--contract":
                    if (!TryReadValue(args, ref i, token, out contractPath, out error))
                    {
                        return false;
                    }

                    break;
                case "--env":
                    if (!TryReadValue(args, ref i, token, out var env, out error))
                    {
                        return false;
                    }

                    envs.Add(env!);
                    break;
                case "--format":
                    if (!TryReadValue(args, ref i, token, out outputFormat, out error))
                    {
                        return false;
                    }

                    break;
                case "--verbosity":
                    if (!TryReadValue(args, ref i, token, out verbosity, out error))
                    {
                        return false;
                    }

                    break;
                case "--key":
                    if (!TryReadValue(args, ref i, token, out key, out error))
                    {
                        return false;
                    }

                    break;
                case "--no-color":
                    noColor = true;
                    break;
                default:
                    error = $"Unknown option '{token}'.";
                    return false;
            }
        }

        command = new ParsedCommand(
            Name: name,
            ContractPath: contractPath,
            Environments: new ReadOnlyCollection<string>(envs),
            OutputFormat: outputFormat,
            Verbosity: verbosity,
            Key: key,
            NoColor: noColor);
        return true;
    }

    private static bool TryReadValue(
        string[] args,
        ref int index,
        string optionName,
        out string? value,
        out string? error)
    {
        value = null;
        error = null;

        if (index + 1 >= args.Length)
        {
            error = $"Missing value for option '{optionName}'.";
            return false;
        }

        value = args[++index];
        return true;
    }
}
