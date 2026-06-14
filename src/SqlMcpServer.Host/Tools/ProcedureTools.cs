using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.Application.Services;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class ProcedureTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "describe_procedure")]
    [Description("Describe a stored procedure including its parameters, creation date, and metadata.")]
    public Task<string> DescribeProcedure(
        [Description("Schema name")] string schema,
        [Description("Procedure name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<ProcedureService>();
            var result = await svc.DescribeProcedureAsync(schema, name, token);
            return result is null ? ToolHelper.NotFound($"[{schema}].[{name}]") : ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "get_procedure_parameters")]
    [Description("Get the parameters for a stored procedure with data types, direction, and defaults.")]
    public Task<string> GetProcedureParameters(
        [Description("Schema name")] string schema,
        [Description("Procedure name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<IProcedureRepository>();
            return ToolHelper.Serialize(await repo.GetProcedureParametersAsync(schema, name, token));
        }, ct);

    [McpServerTool(Name = "get_procedure_definition")]
    [Description("Get the CREATE PROCEDURE DDL source code for a stored procedure.")]
    public Task<string> GetProcedureDefinition(
        [Description("Schema name")] string schema,
        [Description("Procedure name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<ProcedureService>();
            var def = await svc.GetProcedureDefinitionAsync(schema, name, token);
            return def is null ? ToolHelper.NotFound($"[{schema}].[{name}]") : ToolHelper.Serialize(new { schema, name, definition = def });
        }, ct);

    [McpServerTool(Name = "get_procedure_dependencies")]
    [Description("List all database objects that a stored procedure references.")]
    public Task<string> GetProcedureDependencies(
        [Description("Schema name")] string schema,
        [Description("Procedure name")] string name,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<IProcedureRepository>();
            return ToolHelper.Serialize(await repo.AnalyzeProcedureDependenciesAsync(schema, name, token));
        }, ct);

    [McpServerTool(Name = "list_procedures_with_parameters")]
    [Description("List stored procedures in a database together with their parameter signatures.")]
    public Task<string> ListProceduresWithParameters(
        [Description("Database name")] string database,
        [Description("Schema filter (optional)")] string? schema = null,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var repo = sp.GetRequiredService<IProcedureRepository>();
            var procs = await repo.GetProceduresAsync(database, schema, token);
            var result = new List<object>(procs.Count);
            foreach (var p in procs)
            {
                var parameters = await repo.GetProcedureParametersAsync(p.Schema, p.Name, token);
                result.Add(new { p.Schema, p.Name, p.CreateDate, Parameters = parameters });
            }
            return ToolHelper.Serialize(result);
        }, ct);
}
