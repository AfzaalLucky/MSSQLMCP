namespace SqlMcpServer.Domain.Entities;

public sealed record ProcedureInfo(
    string Schema,
    string Name,
    string? Definition,
    IReadOnlyList<ProcedureParameter> Parameters,
    DateTime CreateDate,
    DateTime? ModifyDate)
{
    public string FullName => $"[{Schema}].[{Name}]";
}
