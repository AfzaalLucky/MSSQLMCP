namespace SqlMcpServer.Domain.Entities;

public sealed record TableTypeInfo(
    string Schema,
    string Name,
    IReadOnlyList<ColumnInfo> Columns)
{
    public string FullName => $"[{Schema}].[{Name}]";
}
