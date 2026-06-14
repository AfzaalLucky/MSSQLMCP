using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.Application.Services;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class HealthTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "health_check")]
    [Description("Check overall server health: database connectivity and cache status.")]
    public Task<string> HealthCheck(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<HealthService>();
            return ToolHelper.Serialize(await svc.HealthCheckAsync(token));
        }, ct);

    [McpServerTool(Name = "test_connection")]
    [Description("Test whether the SQL Server connection is alive and responsive.")]
    public Task<string> TestConnection(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<HealthService>();
            var ok = await svc.DatabaseConnectivityTestAsync(token);
            return ToolHelper.Serialize(new { connected = ok });
        }, ct);

    [McpServerTool(Name = "get_cache_stats")]
    [Description("Get cache performance statistics: hit rate, miss count, and current item count.")]
    public Task<string> GetCacheStats(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<HealthService>();
            var stats = await svc.CacheStatusAsync(token);
            return ToolHelper.Serialize(stats);
        }, ct);

    [McpServerTool(Name = "get_server_info")]
    [Description("Get SQL Server version, edition, hostname, and current UTC time.")]
    public Task<string> GetServerInfo(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var executor = sp.GetRequiredService<IQueryExecutor>();
            const string sql = """
                SELECT
                    @@VERSION        AS server_version,
                    @@SERVERNAME     AS server_name,
                    @@SERVICENAME    AS service_name,
                    SERVERPROPERTY('Edition')           AS edition,
                    SERVERPROPERTY('ProductVersion')    AS product_version,
                    SERVERPROPERTY('ProductLevel')      AS product_level,
                    SERVERPROPERTY('IsClustered')       AS is_clustered,
                    SERVERPROPERTY('IsHadrEnabled')     AS is_hadr_enabled,
                    GETUTCDATE()     AS utc_time
                """;
            return ToolHelper.Serialize(await executor.ExecuteQueryAsync(sql, null, 10, token));
        }, ct);

    [McpServerTool(Name = "get_database_properties")]
    [Description("Get database-level properties using DATABASEPROPERTYEX: recovery model, collation, compatibility level, state.")]
    public Task<string> GetDatabaseProperties(
        [Description("Database name")] string database,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var executor = sp.GetRequiredService<IQueryExecutor>();
            var sql = $"""
                SELECT
                    DATABASEPROPERTYEX('{database}', 'Recovery')           AS recovery_model,
                    DATABASEPROPERTYEX('{database}', 'Collation')          AS collation,
                    DATABASEPROPERTYEX('{database}', 'CompatibilityLevel') AS compat_level,
                    DATABASEPROPERTYEX('{database}', 'Status')             AS status,
                    DATABASEPROPERTYEX('{database}', 'UserAccess')         AS user_access,
                    DATABASEPROPERTYEX('{database}', 'IsAutoShrink')       AS is_auto_shrink,
                    DATABASEPROPERTYEX('{database}', 'IsReadOnly')         AS is_read_only
                """;
            return ToolHelper.Serialize(await executor.ExecuteQueryAsync(sql, null, 10, token));
        }, ct);

    [McpServerTool(Name = "list_active_connections")]
    [Description("List active database sessions and connections from sys.dm_exec_sessions.")]
    public Task<string> ListActiveConnections(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var executor = sp.GetRequiredService<IQueryExecutor>();
            const string sql = """
                SELECT
                    s.session_id, s.login_name, s.host_name, s.program_name,
                    s.status, s.database_id, DB_NAME(s.database_id) AS database_name,
                    s.cpu_time, s.memory_usage, s.reads, s.writes,
                    s.last_request_start_time, s.open_transaction_count
                FROM sys.dm_exec_sessions s
                WHERE s.is_user_process = 1
                ORDER BY s.session_id
                """;
            return ToolHelper.Serialize(await executor.ExecuteQueryAsync(sql, null, 30, token));
        }, ct);
}
