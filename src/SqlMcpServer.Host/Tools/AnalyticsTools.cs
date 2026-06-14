using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class AnalyticsTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "get_expensive_queries")]
    [Description("Show the top 20 most CPU-expensive queries cached in the plan cache, with average cost and execution count.")]
    public Task<string> GetExpensiveQueries(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var executor = sp.GetRequiredService<IQueryExecutor>();
            const string sql = """
                SELECT TOP 20
                    qs.execution_count,
                    qs.total_worker_time / qs.execution_count  AS avg_cpu_us,
                    qs.total_elapsed_time / qs.execution_count AS avg_elapsed_us,
                    qs.total_logical_reads / qs.execution_count AS avg_logical_reads,
                    qs.total_logical_writes / qs.execution_count AS avg_logical_writes,
                    SUBSTRING(qt.text, (qs.statement_start_offset / 2) + 1,
                        ((CASE qs.statement_end_offset
                            WHEN -1 THEN DATALENGTH(qt.text)
                            ELSE qs.statement_end_offset END
                            - qs.statement_start_offset) / 2) + 1) AS query_text
                FROM sys.dm_exec_query_stats qs
                CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) qt
                ORDER BY avg_cpu_us DESC
                """;
            return ToolHelper.Serialize(await executor.ExecuteQueryAsync(sql, null, 30, token));
        }, ct);

    [McpServerTool(Name = "get_blocking_queries")]
    [Description("Show currently blocked SQL sessions and the statements causing the blocking.")]
    public Task<string> GetBlockingQueries(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var executor = sp.GetRequiredService<IQueryExecutor>();
            const string sql = """
                SELECT
                    r.session_id,
                    r.blocking_session_id,
                    r.wait_type,
                    r.wait_time,
                    r.status,
                    r.command,
                    SUBSTRING(t.text, (r.statement_start_offset / 2) + 1, 200) AS statement_text,
                    s.login_name,
                    s.host_name,
                    s.program_name
                FROM sys.dm_exec_requests r
                JOIN sys.dm_exec_sessions s ON s.session_id = r.session_id
                CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
                WHERE r.blocking_session_id > 0
                ORDER BY r.wait_time DESC
                """;
            return ToolHelper.Serialize(await executor.ExecuteQueryAsync(sql, null, 15, token));
        }, ct);

    [McpServerTool(Name = "get_wait_statistics")]
    [Description("Show the top 20 SQL Server wait statistics (excluding benign background waits) ordered by total wait time.")]
    public Task<string> GetWaitStatistics(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var executor = sp.GetRequiredService<IQueryExecutor>();
            const string sql = """
                SELECT TOP 20
                    wait_type,
                    waiting_tasks_count,
                    wait_time_ms,
                    max_wait_time_ms,
                    signal_wait_time_ms,
                    wait_time_ms - signal_wait_time_ms AS resource_wait_time_ms
                FROM sys.dm_os_wait_stats
                WHERE wait_type NOT IN (
                    'SLEEP_TASK','BROKER_TO_FLUSH','BROKER_TASK_STOP','CLR_AUTO_EVENT',
                    'DISPATCHER_QUEUE_SEMAPHORE','FT_IFTS_SCHEDULER_IDLE_WAIT',
                    'HADR_WORK_QUEUE','ONDEMAND_TASK_QUEUE','REQUEST_FOR_DEADLOCK_SEARCH',
                    'RESOURCE_QUEUE','SERVER_IDLE_CHECK','SLEEP_DBSTARTUP','SLEEP_DCOMSTARTUP',
                    'SLEEP_MASTERDBREADY','SLEEP_MASTERMDREADY','SLEEP_MASTERUPGRADED',
                    'SLEEP_MSDBSTARTUP','SLEEP_SYSTEMTASK','SLEEP_TEMPDBSTARTUP',
                    'SNI_HTTP_ACCEPT','SP_SERVER_DIAGNOSTICS_SLEEP','SQLTRACE_BUFFER_FLUSH',
                    'SQLTRACE_INCREMENTAL_FLUSH_SLEEP','WAITFOR','XE_DISPATCHER_WAIT',
                    'XE_TIMER_EVENT','CHECKPOINT_QUEUE','DBMIRROR_EVENTS_QUEUE')
                ORDER BY wait_time_ms DESC
                """;
            return ToolHelper.Serialize(await executor.ExecuteQueryAsync(sql, null, 15, token));
        }, ct);

    [McpServerTool(Name = "get_index_fragmentation")]
    [Description("Show indexes with fragmentation above 10% and more than 100 pages. Useful for scheduling REBUILD/REORGANIZE maintenance.")]
    public Task<string> GetIndexFragmentation(CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var executor = sp.GetRequiredService<IQueryExecutor>();
            const string sql = """
                SELECT
                    OBJECT_SCHEMA_NAME(ips.object_id)            AS schema_name,
                    OBJECT_NAME(ips.object_id)                   AS table_name,
                    ix.name                                      AS index_name,
                    ips.index_type_desc,
                    CAST(ips.avg_fragmentation_in_percent AS DECIMAL(5,2)) AS avg_fragmentation_pct,
                    ips.page_count,
                    CASE WHEN ips.avg_fragmentation_in_percent >= 30 THEN 'REBUILD'
                         ELSE 'REORGANIZE' END                   AS recommended_action
                FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
                JOIN sys.indexes ix ON ips.object_id = ix.object_id AND ips.index_id = ix.index_id
                WHERE ips.avg_fragmentation_in_percent > 10
                  AND ips.page_count > 100
                ORDER BY ips.avg_fragmentation_in_percent DESC
                """;
            return ToolHelper.Serialize(await executor.ExecuteQueryAsync(sql, null, 60, token));
        }, ct);

    [McpServerTool(Name = "get_index_usage_stats")]
    [Description("Show how each index has been used (seeks, scans, lookups, updates) since the last server restart.")]
    public Task<string> GetIndexUsageStats(
        [Description("Filter to a specific table (optional)")] string? table = null,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var executor = sp.GetRequiredService<IQueryExecutor>();
            var whereClause = table is not null
                ? $"AND OBJECT_NAME(i.object_id) = '{table.Replace("'", "''")}'"
                : "";
            var sql = $"""
                SELECT
                    OBJECT_SCHEMA_NAME(i.object_id) AS schema_name,
                    OBJECT_NAME(i.object_id)         AS table_name,
                    i.name                           AS index_name,
                    i.type_desc                      AS index_type,
                    ISNULL(s.user_seeks, 0)          AS user_seeks,
                    ISNULL(s.user_scans, 0)          AS user_scans,
                    ISNULL(s.user_lookups, 0)        AS user_lookups,
                    ISNULL(s.user_updates, 0)        AS user_updates,
                    s.last_user_seek,
                    s.last_user_scan
                FROM sys.indexes i
                LEFT JOIN sys.dm_db_index_usage_stats s
                    ON s.object_id = i.object_id AND s.index_id = i.index_id AND s.database_id = DB_ID()
                WHERE i.type > 0
                  AND OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
                  {whereClause}
                ORDER BY ISNULL(s.user_seeks + s.user_scans + s.user_lookups, 0) DESC
                """;
            return ToolHelper.Serialize(await executor.ExecuteQueryAsync(sql, null, 30, token));
        }, ct);

    [McpServerTool(Name = "get_database_size")]
    [Description("Get the size breakdown of the current database: data files, log files, reserved, used, and unallocated space.")]
    public Task<string> GetDatabaseSize(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var executor = sp.GetRequiredService<IQueryExecutor>();
            const string sql = """
                EXEC sp_spaceused
                SELECT
                    name, type_desc, size * 8 / 1024 AS size_mb,
                    FILEPROPERTY(name, 'SpaceUsed') * 8 / 1024 AS used_mb,
                    (size - FILEPROPERTY(name, 'SpaceUsed')) * 8 / 1024 AS free_mb,
                    physical_name
                FROM sys.database_files
                """;
            return ToolHelper.Serialize(await executor.ExecuteQueryAsync(sql, null, 15, token));
        }, ct);

    [McpServerTool(Name = "get_file_io_stats")]
    [Description("Show IO statistics for database files (reads, writes, stall times) from sys.dm_io_virtual_file_stats.")]
    public Task<string> GetFileIoStats(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var executor = sp.GetRequiredService<IQueryExecutor>();
            const string sql = """
                SELECT
                    DB_NAME(vfs.database_id)    AS database_name,
                    mf.name                     AS file_name,
                    mf.type_desc,
                    vfs.io_stall_read_ms,
                    vfs.io_stall_write_ms,
                    vfs.io_stall,
                    vfs.num_of_reads,
                    vfs.num_of_writes,
                    vfs.num_of_bytes_read / 1048576  AS mb_read,
                    vfs.num_of_bytes_written / 1048576 AS mb_written,
                    mf.physical_name
                FROM sys.dm_io_virtual_file_stats(NULL, NULL) vfs
                JOIN sys.master_files mf ON mf.database_id = vfs.database_id AND mf.file_id = vfs.file_id
                ORDER BY vfs.io_stall DESC
                """;
            return ToolHelper.Serialize(await executor.ExecuteQueryAsync(sql, null, 15, token));
        }, ct);

    [McpServerTool(Name = "get_top_tables_by_size")]
    [Description("List the top 25 tables in the current database by total size (data + index space).")]
    public Task<string> GetTopTablesBySize(
        [Description("Number of tables to return (max 100)")] int top = 25,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var executor = sp.GetRequiredService<IQueryExecutor>();
            var sql = $"""
                SELECT TOP {Math.Clamp(top, 1, 100)}
                    OBJECT_SCHEMA_NAME(i.object_id) AS schema_name,
                    OBJECT_NAME(i.object_id)         AS table_name,
                    SUM(p.rows)                      AS row_count,
                    SUM(a.total_pages) * 8 / 1024    AS total_mb,
                    SUM(a.used_pages) * 8 / 1024     AS used_mb,
                    (SUM(a.total_pages) - SUM(a.used_pages)) * 8 / 1024 AS unused_mb
                FROM sys.indexes i
                JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
                JOIN sys.allocation_units a ON p.partition_id = a.container_id
                WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
                GROUP BY i.object_id
                ORDER BY total_mb DESC
                """;
            return ToolHelper.Serialize(await executor.ExecuteQueryAsync(sql, null, 30, token));
        }, ct);
}
