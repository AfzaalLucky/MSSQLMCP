using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.Application.Services;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class TypeTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "list_table_types")]
    [Description("List all user-defined table types in a schema (used as table-valued parameters).")]
    public Task<string> ListTableTypes(
        [Description("Schema name (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<ITypeRepository>();
            return ToolHelper.Serialize(await repo.GetTableTypesAsync(schema, token));
        }, ct);

    [McpServerTool(Name = "describe_table_type")]
    [Description("Describe a table type: its column definitions and constraints.")]
    public Task<string> DescribeTableType(
        [Description("Schema name")] string schema,
        [Description("Table type name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<TypeService>();
            var result = await svc.DescribeTableTypeAsync(schema, name, token);
            return result is null ? ToolHelper.NotFound($"[{schema}].[{name}]") : ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "describe_user_defined_type")]
    [Description("Describe a scalar user-defined type: base type, max length, precision, scale, and nullability.")]
    public Task<string> DescribeUserDefinedType(
        [Description("Schema name")] string schema,
        [Description("Type name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<TypeService>();
            var result = await svc.DescribeUserDefinedTypeAsync(schema, name, token);
            return result is null ? ToolHelper.NotFound($"[{schema}].[{name}]") : ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "get_type_definition")]
    [Description("Get the CREATE TYPE DDL statement for a user-defined type.")]
    public Task<string> GetTypeDefinition(
        [Description("Schema name")] string schema,
        [Description("Type name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<TypeService>();
            var def = await svc.GetTypeDefinitionAsync(schema, name, token);
            return def is null ? ToolHelper.NotFound($"[{schema}].[{name}]") : ToolHelper.Serialize(new { schema, name, definition = def });
        }, ct);

    [McpServerTool(Name = "list_all_types")]
    [Description("List all user-defined types (both scalar UDTs and table types) in a schema.")]
    public Task<string> ListAllTypes(
        [Description("Schema name (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<ITypeRepository>();
            var udts = await repo.GetUserDefinedTypesAsync(schema, token);
            var tableTypes = await repo.GetTableTypesAsync(schema, token);
            return ToolHelper.Serialize(new
            {
                userDefinedTypes = udts,
                tableTypes
            });
        }, ct);
}
