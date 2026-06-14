namespace SqlMcpServer.Domain.Entities;

public sealed record TableStatistics(
    string Schema,
    string TableName,
    long RowCount,
    long ReservedKB,
    long DataKB,
    long IndexKB,
    long UnusedKB,
    DateTime? LastUpdated,
    long PageCount,
    double FragmentationPercent);
