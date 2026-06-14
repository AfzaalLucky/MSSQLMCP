using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Contracts.Services;
using SqlMcpServer.Domain.Enums;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class ComparisonTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "compare_schemas")]
    [Description("Compare two schemas (same or different databases) and return added, removed, and modified objects.")]
    public Task<string> CompareSchemas(
        [Description("Source database name")] string sourceDatabase,
        [Description("Source schema name")] string sourceSchema,
        [Description("Target database name")] string targetDatabase,
        [Description("Target schema name")] string targetSchema,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<ISchemaComparisonService>();
            var result = await svc.CompareSchemasAsync(sourceDatabase, sourceSchema, targetDatabase, targetSchema, token);
            return ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "compare_databases")]
    [Description("Compare the default (dbo) schema of two databases and return structural differences.")]
    public Task<string> CompareDatabases(
        [Description("Source database name")] string sourceDatabase,
        [Description("Target database name")] string targetDatabase,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<ISchemaComparisonService>();
            var result = await svc.CompareDatabasesAsync(sourceDatabase, targetDatabase, token);
            return ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "generate_migration_script")]
    [Description("Generate a T-SQL migration script stub from source schema to target schema based on structural differences.")]
    public Task<string> GenerateMigrationScript(
        [Description("Source database")] string sourceDatabase,
        [Description("Source schema")] string sourceSchema,
        [Description("Target database")] string targetDatabase,
        [Description("Target schema")] string targetSchema,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<ISchemaComparisonService>();
            var script = await svc.GenerateMigrationScriptAsync(sourceDatabase, sourceSchema, targetDatabase, targetSchema, token);
            return ToolHelper.Serialize(new { script });
        }, ct);

    [McpServerTool(Name = "validate_sql_safety")]
    [Description("Check whether a SQL statement is allowed by the safety rules without executing it. Returns the detected statement type and any violation reason.")]
    public Task<string> ValidateSqlSafety(
        [Description("SQL statement to check")] string sql,
        [Description("User role for rule evaluation: ReadOnly, Auditor, Developer, DBA, Admin")] string role = "Developer",
        CancellationToken ct = default)
        => ExecuteAsync((sp, _) =>
        {
            var validator = sp.GetRequiredService<IQuerySafetyValidator>();
            var userRole = Enum.TryParse<UserRole>(role, ignoreCase: true, out var r) ? r : UserRole.Developer;
            var result = validator.Validate(sql, userRole);
            return Task.FromResult(ToolHelper.Serialize(new
            {
                isAllowed = result.IsAllowed,
                detectedType = result.DetectedStatementType.ToString(),
                violationReason = result.ViolationReason
            }));
        }, ct);
}
