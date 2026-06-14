using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.Application.Services;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class FunctionTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "describe_function")]
    [Description("Describe a function: type (scalar/TVF), parameters, return type, and creation date.")]
    public Task<string> DescribeFunction(
        [Description("Schema name")] string schema,
        [Description("Function name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<FunctionService>();
            var result = await svc.DescribeFunctionAsync(schema, name, token);
            return result is null ? ToolHelper.NotFound($"[{schema}].[{name}]") : ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "get_function_definition")]
    [Description("Get the CREATE FUNCTION DDL source code for a function.")]
    public Task<string> GetFunctionDefinition(
        [Description("Schema name")] string schema,
        [Description("Function name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<FunctionService>();
            var def = await svc.GetFunctionDefinitionAsync(schema, name, token);
            return def is null ? ToolHelper.NotFound($"[{schema}].[{name}]") : ToolHelper.Serialize(new { schema, name, definition = def });
        }, ct);

    [McpServerTool(Name = "get_function_dependencies")]
    [Description("List all database objects that a function references.")]
    public Task<string> GetFunctionDependencies(
        [Description("Schema name")] string schema,
        [Description("Function name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<IFunctionRepository>();
            return ToolHelper.Serialize(await repo.AnalyzeFunctionDependenciesAsync(schema, name, token));
        }, ct);

    [McpServerTool(Name = "list_scalar_functions")]
    [Description("List all scalar (single-value returning) functions in a schema.")]
    public Task<string> ListScalarFunctions(
        [Description("Schema name (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<IFunctionRepository>();
            return ToolHelper.Serialize(await repo.GetScalarFunctionsAsync(schema, token));
        }, ct);

    [McpServerTool(Name = "list_table_valued_functions")]
    [Description("List all table-valued functions (inline and multi-statement) in a schema.")]
    public Task<string> ListTableValuedFunctions(
        [Description("Schema name (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<IFunctionRepository>();
            return ToolHelper.Serialize(await repo.GetTableValuedFunctionsAsync(schema, token));
        }, ct);
}
