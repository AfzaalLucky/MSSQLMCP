using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Domain.Entities;

public sealed record IndexInfo(
    string Schema,
    string Table,
    string Name,
    IndexType Type,
    bool IsUnique,
    bool IsPrimaryKey,
    bool IsDisabled,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> IncludedColumns,
    int FillFactor,
    bool HasFilter,
    string? FilterDefinition);
