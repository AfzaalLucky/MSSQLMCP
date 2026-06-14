namespace SqlMcpServer.Domain.Entities;

public sealed record QueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    long RowCount,
    long ExecutionTimeMs,
    int AffectedRows,
    bool Truncated,
    string? TruncationReason);
