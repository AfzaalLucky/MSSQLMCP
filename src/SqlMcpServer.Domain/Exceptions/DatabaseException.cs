namespace SqlMcpServer.Domain.Exceptions;

public sealed class DatabaseException : McpDomainException
{
    public int? SqlErrorNumber { get; }

    public DatabaseException(string message)
        : base("DATABASE_ERROR", message) { }

    public DatabaseException(string message, Exception inner, int? sqlErrorNumber = null)
        : base("DATABASE_ERROR", message, inner)
    {
        SqlErrorNumber = sqlErrorNumber;
    }
}
