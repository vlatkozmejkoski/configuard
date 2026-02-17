namespace Configuard.Cli.Validation;

internal sealed class ValidationInputException : Exception
{
    public ValidationInputException(string message)
        : base(message)
    {
    }

    public ValidationInputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
