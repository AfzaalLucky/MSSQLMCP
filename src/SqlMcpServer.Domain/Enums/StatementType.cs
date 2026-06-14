namespace SqlMcpServer.Domain.Enums;

public enum StatementType
{
    Select,
    Insert,
    Update,
    Delete,
    Merge,
    Execute,
    DDL,
    Admin,
    Unknown
}
