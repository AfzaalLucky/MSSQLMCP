using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.Application.Services;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class TableTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "describe_table")]
    [Description("Full description of a table: columns, primary keys, foreign keys, indexes, constraints, row count, and statistics.")]
    public Task<string> DescribeTable(
        [Description("Schema name (e.g. 'dbo')")] string schema,
        [Description("Table name")] string table,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<TableService>();
            var result = await svc.DescribeTableAsync(schema, table, token);
            return result is null ? ToolHelper.NotFound($"[{schema}].[{table}]") : ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "get_table_columns")]
    [Description("Get detailed column information for a table including data types, nullability, identity, and defaults.")]
    public Task<string> GetTableColumns(
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<ITableRepository>();
            return ToolHelper.Serialize(await repo.GetTableColumnsAsync(schema, table, token));
        }, ct);

    [McpServerTool(Name = "get_table_indexes")]
    [Description("Get all indexes on a table, including index type, key columns, included columns, and uniqueness.")]
    public Task<string> GetTableIndexes(
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetIndexesAsync(schema, table, token));
        }, ct);

    [McpServerTool(Name = "get_table_constraints")]
    [Description("Get all constraints on a table: CHECK, DEFAULT, PRIMARY KEY, and UNIQUE constraints.")]
    public Task<string> GetTableConstraints(
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetConstraintsAsync(schema, table, token));
        }, ct);

    [McpServerTool(Name = "get_foreign_keys")]
    [Description("Get all foreign key relationships for a table including referenced tables and columns.")]
    public Task<string> GetForeignKeys(
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetForeignKeysAsync(schema, table, token));
        }, ct);

    [McpServerTool(Name = "get_primary_keys")]
    [Description("Get the primary key columns and constraint for a table.")]
    public Task<string> GetPrimaryKeys(
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<ITableRepository>();
            return ToolHelper.Serialize(await repo.GetPrimaryKeysAsync(schema, table, token));
        }, ct);

    [McpServerTool(Name = "get_table_statistics")]
    [Description("Get table storage statistics: row count, reserved space, data size, index size, and unused space.")]
    public Task<string> GetTableStatistics(
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<TableService>();
            var result = await svc.GetTableStatisticsAsync(schema, table, token);
            return result is null ? ToolHelper.NotFound($"[{schema}].[{table}]") : ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "get_row_count")]
    [Description("Get the approximate row count for a table using partition metadata (fast, no table scan).")]
    public Task<string> GetRowCount(
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<ITableRepository>();
            var count = await repo.GetRowCountAsync(schema, table, token);
            return ToolHelper.Serialize(new { schema, table, rowCount = count });
        }, ct);

    [McpServerTool(Name = "get_missing_indexes")]
    [Description("Show index recommendations from SQL Server's missing index DMVs, ordered by estimated impact.")]
    public Task<string> GetMissingIndexes(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<IIndexRepository>();
            return ToolHelper.Serialize(await repo.GetMissingIndexesAsync(token));
        }, ct);
}
