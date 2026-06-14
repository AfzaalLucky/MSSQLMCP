namespace SqlMcpServer.Domain.Exceptions;

public abstract class McpDomainException : Exception
{
    public string ErrorCode { get; }

    protected McpDomainException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    protected McpDomainException(string errorCode, string message, Exception inner)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}
