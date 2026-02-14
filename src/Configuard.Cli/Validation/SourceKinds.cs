namespace Configuard.Cli.Validation;

internal static class SourceKinds
{
    public const string AppSettings = "appsettings";
    public const string DotEnv = "dotenv";
    public const string EnvSnapshot = "envsnapshot";

    public static readonly string[] DefaultOrder = [EnvSnapshot, DotEnv, AppSettings];

    public static bool IsSupported(string source) =>
        string.Equals(source, AppSettings, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(source, DotEnv, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(source, EnvSnapshot, StringComparison.OrdinalIgnoreCase);
}
