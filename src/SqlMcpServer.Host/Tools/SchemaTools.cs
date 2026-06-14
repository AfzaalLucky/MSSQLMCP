using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.Application.Models.Requests;
using SqlMcpServer.Application.Services;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class SchemaTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "list_databases")]
    [Description("List all accessible SQL Server databases with their metadata (state, compatibility level, collation).")]
    public Task<string> ListDatabases(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetDatabasesAsync(token));
        }, ct);

    [McpServerTool(Name = "list_schemas")]
    [Description("List all schemas in the specified database.")]
    public Task<string> ListSchemas(
        [Description("Database name")] string database,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetSchemasAsync(database, token));
        }, ct);

    [McpServerTool(Name = "list_tables")]
    [Description("List tables in a database, optionally filtered by schema. Supports pagination.")]
    public Task<string> ListTables(
        [Description("Database name")] string database,
        [Description("Schema filter (optional, e.g. 'dbo')")] string? schema = null,
        [Description("Page number (1-based)")] int page = 1,
        [Description("Items per page (max 500)")] int pageSize = 25,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetTablesAsync(new GetObjectsRequest(database, schema, page, pageSize), token));
        }, ct);

    [McpServerTool(Name = "list_views")]
    [Description("List all views in a database, optionally filtered by schema.")]
    public Task<string> ListViews(
        [Description("Database name")] string database,
        [Description("Schema filter (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetViewsAsync(database, schema, token));
        }, ct);

    [McpServerTool(Name = "list_procedures")]
    [Description("List all stored procedures in a database, optionally filtered by schema.")]
    public Task<string> ListProcedures(
        [Description("Database name")] string database,
        [Description("Schema filter (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetProceduresAsync(database, schema, token));
        }, ct);

    [McpServerTool(Name = "list_functions")]
    [Description("List all functions (scalar, inline TVF, multi-statement TVF) in a database.")]
    public Task<string> ListFunctions(
        [Description("Database name")] string database,
        [Description("Schema filter (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetFunctionsAsync(database, schema, token));
        }, ct);

    [McpServerTool(Name = "list_triggers")]
    [Description("List all triggers in a database, optionally filtered by schema.")]
    public Task<string> ListTriggers(
        [Description("Database name")] string database,
        [Description("Schema filter (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetTriggersAsync(database, schema, token));
        }, ct);

    [McpServerTool(Name = "list_sequences")]
    [Description("List all sequences in a schema.")]
    public Task<string> ListSequences(
        [Description("Schema name (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetSequencesAsync(schema, token));
        }, ct);

    [McpServerTool(Name = "list_synonyms")]
    [Description("List all synonyms in a schema.")]
    public Task<string> ListSynonyms(
        [Description("Schema name (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetSynonymsAsync(schema, token));
        }, ct);

    [McpServerTool(Name = "list_user_defined_types")]
    [Description("List all user-defined types (scalar and table types) in a schema.")]
    public Task<string> ListUserDefinedTypes(
        [Description("Schema name (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DatabaseDiscoveryService>();
            return ToolHelper.Serialize(await svc.GetUserDefinedTypesAsync(schema, token));
        }, ct);
}
