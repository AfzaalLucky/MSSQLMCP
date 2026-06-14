namespace SqlMcpServer.Domain.Exceptions;

public sealed class TransientException : McpDomainException
{
    public int RetryAfterSeconds { get; }

    public TransientException(string message, int retryAfterSeconds = 5)
        : base("TRANSIENT_ERROR", message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    public TransientException(string message, Exception inner, int retryAfterSeconds = 5)
        : base("TRANSIENT_ERROR", message, inner)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
