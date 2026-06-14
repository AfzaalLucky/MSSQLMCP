using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Contracts.Services;
using SqlMcpServer.Domain.Enums;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class DocumentationTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "generate_database_documentation")]
    [Description("Generate comprehensive documentation for an entire database in Markdown or JSON format.")]
    public Task<string> GenerateDatabaseDocumentation(
        [Description("Database name")] string database,
        [Description("Output format: Markdown or Json")] string format = "Markdown",
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<IDocumentationService>();
            var fmt = Enum.TryParse<DocumentFormat>(format, ignoreCase: true, out var f) ? f : DocumentFormat.Markdown;
            var doc = await svc.GenerateDatabaseDocumentationAsync(database, fmt, token);
            return ToolHelper.Serialize(new { database, format = fmt.ToString(), documentation = doc });
        }, ct);

    [McpServerTool(Name = "generate_schema_documentation")]
    [Description("Generate documentation for all objects within a specific schema in Markdown or JSON format.")]
    public Task<string> GenerateSchemaDocumentation(
        [Description("Schema name")] string schema,
        [Description("Output format: Markdown or Json")] string format = "Markdown",
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<IDocumentationService>();
            var fmt = Enum.TryParse<DocumentFormat>(format, ignoreCase: true, out var f) ? f : DocumentFormat.Markdown;
            var doc = await svc.GenerateSchemaDocumentationAsync(schema, fmt, token);
            return ToolHelper.Serialize(new { schema, format = fmt.ToString(), documentation = doc });
        }, ct);

    [McpServerTool(Name = "generate_table_documentation")]
    [Description("Generate detailed Markdown documentation for a specific table including all columns with types, nullability, and constraints.")]
    public Task<string> GenerateTableDocumentation(
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<IDocumentationService>();
            var doc = await svc.GenerateTableDocumentationAsync(schema, table, token);
            return ToolHelper.Serialize(new { schema, table, documentation = doc });
        }, ct);

    [McpServerTool(Name = "generate_api_documentation")]
    [Description("Generate API-style documentation for all stored procedures and functions in a database, including parameter signatures.")]
    public Task<string> GenerateApiDocumentation(
        [Description("Database name")] string database,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<IDocumentationService>();
            var doc = await svc.GenerateApiDocumentationAsync(database, token);
            return ToolHelper.Serialize(new { database, documentation = doc });
        }, ct);

    [McpServerTool(Name = "export_schema_as_json")]
    [Description("Export the full schema metadata of a database as structured JSON — tables, views, procedures, and functions.")]
    public Task<string> ExportSchemaAsJson(
        [Description("Database name")] string database,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<IDocumentationService>();
            return await svc.GenerateDatabaseDocumentationAsync(database, DocumentFormat.Json, token);
        }, ct);
}
