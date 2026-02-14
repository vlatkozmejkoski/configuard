namespace Configuard.Cli.Validation;

internal sealed record ValidationIssue(string Environment, string Path, string Code, string Message);
internal sealed record ValidationWarning(string Path, string Code, string Message);

internal sealed class ValidationResult
{
    public List<ValidationIssue> Issues { get; } = [];
    public List<ValidationWarning> Warnings { get; } = [];

    public bool IsSuccess => Issues.Count == 0;
}
