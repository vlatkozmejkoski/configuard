using System.Text.Json;

namespace Configuard.Cli.Tests;

internal static class TestHelpers
{
    public static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "configuard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static JsonElement ParseJsonElement(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public static string EscapeJsonPath(string path) =>
        path.Replace("\\", "\\\\", StringComparison.Ordinal);
}
