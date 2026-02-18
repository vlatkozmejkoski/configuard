using System.Text.Json;

namespace Configuard.Cli.Discovery;

internal static class DiscoverOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string ToJson(DiscoveryReport report) =>
        JsonSerializer.Serialize(report, JsonOptions);
}
