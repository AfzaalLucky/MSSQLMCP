using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Domain.Entities;

public sealed record FunctionInfo(
    string Schema,
    string Name,
    FunctionType Type,
    string? ReturnType,
    string? Definition,
    IReadOnlyList<ProcedureParameter> Parameters,
    DateTime CreateDate,
    DateTime? ModifyDate)
{
    public string FullName => $"[{Schema}].[{Name}]";
}
