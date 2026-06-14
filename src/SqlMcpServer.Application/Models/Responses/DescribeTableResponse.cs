using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Application.Models.Responses;

public sealed record DescribeTableResponse(
    TableInfo Table,
    IReadOnlyList<ColumnInfo> Columns,
    IReadOnlyList<ConstraintInfo> PrimaryKeys,
    IReadOnlyList<ForeignKeyInfo> ForeignKeys,
    IReadOnlyList<IndexInfo> Indexes,
    IReadOnlyList<ConstraintInfo> Constraints,
    long RowCount,
    TableStatistics? Statistics);
