namespace SqlMcpServer.Domain.Entities;

public sealed record SynonymInfo(
    string Schema,
    string Name,
    string BaseObject,
    DateTime CreateDate,
    DateTime? ModifyDate)
{
    public string FullName => $"[{Schema}].[{Name}]";
}
