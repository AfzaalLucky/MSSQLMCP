using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class DataTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "sample_table_data")]
    [Description("Return the top N rows from a table as JSON. Useful for understanding table structure and data patterns.")]
    public Task<string> SampleTableData(
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        [Description("Number of rows to return (1–1000)")] int rowCount = 10,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<ITableRepository>();
            var result = await repo.SampleTableDataAsync(schema, table, Math.Clamp(rowCount, 1, 1000), token);
            return ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "search_table_data")]
    [Description("Search for a term across all varchar/nvarchar columns in a table using OR matching.")]
    public Task<string> SearchTableData(
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        [Description("Text to search for")] string searchTerm,
        [Description("Comma-separated column names to search (optional, defaults to all text columns)")] string? columns = null,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<ITableRepository>();
            var cols = columns?.Split(',').Select(c => c.Trim()).Where(c => c.Length > 0);
            var result = await repo.SearchTableDataAsync(schema, table, searchTerm, cols, token);
            return ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "find_unused_indexes")]
    [Description("Find indexes that have never been used (zero seeks, scans, and lookups) since the last server restart.")]
    public Task<string> FindUnusedIndexes(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var executor = sp.GetRequiredService<IQueryExecutor>();
            const string sql = """
                SELECT
                    OBJECT_SCHEMA_NAME(i.object_id) AS schema_name,
                    OBJECT_NAME(i.object_id)         AS table_name,
                    i.name                           AS index_name,
                    i.type_desc                      AS index_type,
                    ISNULL(s.user_seeks, 0)          AS user_seeks,
                    ISNULL(s.user_scans, 0)          AS user_scans,
                    ISNULL(s.user_lookups, 0)        AS user_lookups,
                    ISNULL(s.user_updates, 0)        AS user_updates
                FROM sys.indexes i
                LEFT JOIN sys.dm_db_index_usage_stats s
                    ON s.object_id = i.object_id AND s.index_id = i.index_id AND s.database_id = DB_ID()
                WHERE i.type > 0
                  AND OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
                  AND ISNULL(s.user_seeks, 0) = 0
                  AND ISNULL(s.user_scans, 0) = 0
                  AND ISNULL(s.user_lookups, 0) = 0
                ORDER BY ISNULL(s.user_updates, 0) DESC
                """;
            var result = await executor.ExecuteQueryAsync(sql, null, 30, token);
            return ToolHelper.Serialize(result);
        }, ct);
}
