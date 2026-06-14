using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Domain.Contracts.Repositories;

public interface ITableRepository
{
    Task<IReadOnlyList<TableInfo>> GetTablesAsync(string database, string? schema, CancellationToken cancellationToken = default);
    Task<TableInfo?> DescribeTableAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ColumnInfo>> GetTableColumnsAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConstraintInfo>> GetPrimaryKeysAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForeignKeyInfo>> GetTableRelationshipsAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<TableStatistics?> GetTableStatisticsAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<long> GetRowCountAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<QueryResult> SampleTableDataAsync(string schema, string table, int rowCount, CancellationToken cancellationToken = default);
    Task<QueryResult> SearchTableDataAsync(string schema, string table, string searchTerm, IEnumerable<string>? columns, CancellationToken cancellationToken = default);
}
