namespace SqlMcpServer.Domain.Exceptions;

public class SecurityException : McpDomainException
{
    public SecurityException(string message)
        : base("SECURITY_ERROR", message) { }

    public SecurityException(string message, Exception inner)
        : base("SECURITY_ERROR", message, inner) { }
}
