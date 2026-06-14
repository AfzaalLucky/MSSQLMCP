namespace SqlMcpServer.Domain.Exceptions;

public sealed class ValidationException : McpDomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string field, string message)
        : base("VALIDATION_ERROR", message)
    {
        Errors = new Dictionary<string, string[]> { [field] = [message] };
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("VALIDATION_ERROR", "One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>(errors);
    }
}
