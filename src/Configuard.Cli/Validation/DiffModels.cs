namespace Configuard.Cli.Validation;

internal sealed record DiffIssue(
    string Path,
    string Kind,
    string LeftEnvironment,
    string RightEnvironment,
    string Message);

internal sealed class DiffResult
{
    public List<DiffIssue> Issues { get; } = [];

    public bool IsClean => Issues.Count == 0;
}
