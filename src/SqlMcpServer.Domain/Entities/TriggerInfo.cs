using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Domain.Entities;

public sealed record TriggerInfo(
    string Schema,
    string Name,
    string ParentTable,
    bool IsEnabled,
    string TriggerType,
    TriggerEvent Events,
    string? Definition,
    DateTime CreateDate,
    DateTime? ModifyDate)
{
    public string FullName => $"[{Schema}].[{Name}]";
}
