namespace SqlMcpServer.Domain.Entities;

public sealed record ViewInfo(
    string Schema,
    string Name,
    string? Definition,
    bool IsUpdatable,
    string? CheckOption,
    DateTime CreateDate,
    DateTime? ModifyDate)
{
    public string FullName => $"[{Schema}].[{Name}]";
}
