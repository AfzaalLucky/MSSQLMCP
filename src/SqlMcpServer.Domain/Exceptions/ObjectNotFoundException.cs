namespace SqlMcpServer.Domain.Exceptions;

public sealed class ObjectNotFoundException : McpDomainException
{
    public string ObjectName { get; }
    public string ObjectKind { get; }

    public ObjectNotFoundException(string objectKind, string objectName)
        : base("NOT_FOUND", $"{objectKind} '{objectName}' was not found.")
    {
        ObjectName = objectName;
        ObjectKind = objectKind;
    }
}
