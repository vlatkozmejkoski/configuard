namespace Configuard.Cli.Discovery;

internal sealed class DiscoveryReport
{
    public string Version { get; init; } = "1";
    public string ScanPath { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public List<DiscoveredKeyFinding> Findings { get; init; } = [];
}

internal sealed class DiscoveredKeyFinding
{
    public string Path { get; init; } = string.Empty;
    public string Confidence { get; set; } = "high";
    public string SuggestedType { get; init; } = "string";
    public List<DiscoveryEvidence> Evidence { get; init; } = [];
    public List<string> Notes { get; init; } = [];
}

internal sealed class DiscoveryEvidence
{
    public string File { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Pattern { get; init; } = string.Empty;
}
