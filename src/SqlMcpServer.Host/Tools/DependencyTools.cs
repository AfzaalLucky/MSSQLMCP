using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.Application.Services;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class DependencyTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "find_object_dependencies")]
    [Description("Find all objects that a given database object (table, view, procedure, function) depends on.")]
    public Task<string> FindObjectDependencies(
        [Description("Schema name")] string schema,
        [Description("Object name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DependencyService>();
            return ToolHelper.Serialize(await svc.FindObjectDependenciesAsync(schema, name, token));
        }, ct);

    [McpServerTool(Name = "find_referencing_objects")]
    [Description("Find all objects that reference (depend on) a given database object — useful for impact analysis before changes.")]
    public Task<string> FindReferencingObjects(
        [Description("Schema name")] string schema,
        [Description("Object name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DependencyService>();
            return ToolHelper.Serialize(await svc.FindReferencingObjectsAsync(schema, name, token));
        }, ct);

    [McpServerTool(Name = "generate_dependency_graph")]
    [Description("Generate a full dependency graph for all objects in a schema as a structured list of relationships.")]
    public Task<string> GenerateDependencyGraph(
        [Description("Schema name (optional, null for all schemas)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DependencyService>();
            return ToolHelper.Serialize(await svc.GenerateDependencyGraphAsync(schema, token));
        }, ct);

    [McpServerTool(Name = "generate_erd")]
    [Description("Generate an Entity Relationship Diagram in Mermaid erDiagram format from the foreign key relationships in a schema.")]
    public Task<string> GenerateErd(
        [Description("Database name")] string database,
        [Description("Schema name (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<DependencyService>();
            var mermaid = await svc.GenerateErdAsync(database, schema, token);
            return ToolHelper.Serialize(new { format = "mermaid", diagram = mermaid });
        }, ct);

    [McpServerTool(Name = "find_broken_dependencies")]
    [Description("Find objects with unresolved (ambiguous or missing) dependencies in sys.sql_expression_dependencies.")]
    public Task<string> FindBrokenDependencies(CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<IDependencyRepository>();
            var all = await repo.GenerateDependencyGraphAsync(null, token);
            var broken = all.Where(d => d.IsAmbiguous).ToList();
            return ToolHelper.Serialize(new { brokenCount = broken.Count, dependencies = broken });
        }, ct);
}
