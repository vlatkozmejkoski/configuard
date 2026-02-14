namespace Configuard.Cli.Cli;

internal static class Verbosity
{
    public const string Quiet = "quiet";
    public const string Normal = "normal";
    public const string Detailed = "detailed";

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? Normal : value.Trim().ToLowerInvariant();
        return normalized is Quiet or Normal or Detailed;
    }
}
