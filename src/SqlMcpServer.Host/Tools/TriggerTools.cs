using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.Application.Services;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class TriggerTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "describe_trigger")]
    [Description("Describe a trigger: target table, event type (INSERT/UPDATE/DELETE), timing (AFTER/INSTEAD OF), and enabled state.")]
    public Task<string> DescribeTrigger(
        [Description("Schema name")] string schema,
        [Description("Trigger name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<TriggerService>();
            var result = await svc.DescribeTriggerAsync(schema, name, token);
            return result is null ? ToolHelper.NotFound($"[{schema}].[{name}]") : ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "get_trigger_definition")]
    [Description("Get the CREATE TRIGGER DDL source code for a trigger.")]
    public Task<string> GetTriggerDefinition(
        [Description("Schema name")] string schema,
        [Description("Trigger name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<TriggerService>();
            var def = await svc.GetTriggerDefinitionAsync(schema, name, token);
            return def is null ? ToolHelper.NotFound($"[{schema}].[{name}]") : ToolHelper.Serialize(new { schema, name, definition = def });
        }, ct);

    [McpServerTool(Name = "get_trigger_dependencies")]
    [Description("List all database objects that a trigger references.")]
    public Task<string> GetTriggerDependencies(
        [Description("Schema name")] string schema,
        [Description("Trigger name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<ITriggerRepository>();
            return ToolHelper.Serialize(await repo.GetTriggerDependenciesAsync(schema, name, token));
        }, ct);

    [McpServerTool(Name = "list_triggers_for_table")]
    [Description("List all triggers associated with a specific table.")]
    public Task<string> ListTriggersForTable(
        [Description("Database name")] string database,
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<ITriggerRepository>();
            var all = await repo.GetTriggersAsync(database, schema, token);
            var forTable = all.Where(t => t.ParentTable.Equals(table, StringComparison.OrdinalIgnoreCase)).ToList();
            return ToolHelper.Serialize(forTable);
        }, ct);
}
