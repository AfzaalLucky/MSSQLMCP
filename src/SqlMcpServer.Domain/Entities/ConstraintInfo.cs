using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Domain.Entities;

public sealed record ConstraintInfo(
    string Schema,
    string Table,
    string Name,
    ConstraintType Type,
    string? Definition,
    IReadOnlyList<string> Columns,
    bool IsDisabled,
    bool IsSystemNamed);
