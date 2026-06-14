namespace SqlMcpServer.Domain.Entities;

public sealed record TableInfo(
    string Schema,
    string Name,
    string Type,
    long RowCount,
    DateTime CreateDate,
    DateTime? ModifyDate,
    bool HasClusteredIndex,
    string? FileGroup)
{
    public string FullName => $"[{Schema}].[{Name}]";
}
