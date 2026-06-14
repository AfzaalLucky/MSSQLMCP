using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.Application.Services;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class ViewTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "describe_view")]
    [Description("Describe a view including its columns, definition, and schema-binding properties.")]
    public Task<string> DescribeView(
        [Description("Schema name")] string schema,
        [Description("View name")] string view,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<ViewService>();
            var result = await svc.DescribeViewAsync(schema, view, token);
            return result is null ? ToolHelper.NotFound($"[{schema}].[{view}]") : ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "get_view_columns")]
    [Description("Get the columns returned by a view with their data types.")]
    public Task<string> GetViewColumns(
        [Description("Schema name")] string schema,
        [Description("View name")] string view,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<IViewRepository>();
            return ToolHelper.Serialize(await repo.GetViewColumnsAsync(schema, view, token));
        }, ct);

    [McpServerTool(Name = "get_view_definition")]
    [Description("Get the CREATE VIEW DDL source code for a view.")]
    public Task<string> GetViewDefinition(
        [Description("Schema name")] string schema,
        [Description("View name")] string view,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<ViewService>();
            var def = await svc.GetViewDefinitionAsync(schema, view, token);
            return def is null ? ToolHelper.NotFound($"[{schema}].[{view}]") : ToolHelper.Serialize(new { schema, view, definition = def });
        }, ct);

    [McpServerTool(Name = "get_view_dependencies")]
    [Description("List the tables, views, and functions that a view references.")]
    public Task<string> GetViewDependencies(
        [Description("Schema name")] string schema,
        [Description("View name")] string view,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<IViewRepository>();
            return ToolHelper.Serialize(await repo.GetViewDependenciesAsync(schema, view, token));
        }, ct);

    [McpServerTool(Name = "list_views_with_definitions")]
    [Description("List all views in a database schema together with their full CREATE VIEW definitions.")]
    public Task<string> ListViewsWithDefinitions(
        [Description("Database name")] string database,
        [Description("Schema filter (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<IViewRepository>();
            var views = await repo.GetViewsAsync(database, schema, token);
            var results = new List<object>(views.Count);
            foreach (var v in views)
            {
                var def = await repo.GetViewDefinitionAsync(v.Schema, v.Name, token);
                results.Add(new { v.Schema, v.Name, v.IsUpdatable, definition = def });
            }
            return ToolHelper.Serialize(results);
        }, ct);
}
