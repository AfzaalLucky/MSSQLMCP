using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Domain.Entities;

public sealed record ForeignKeyInfo(
    string Schema,
    string Table,
    string Name,
    IReadOnlyList<string> Columns,
    string ReferencedSchema,
    string ReferencedTable,
    IReadOnlyList<string> ReferencedColumns,
    ReferentialAction DeleteAction,
    ReferentialAction UpdateAction,
    bool IsDisabled,
    bool IsNotTrusted);
