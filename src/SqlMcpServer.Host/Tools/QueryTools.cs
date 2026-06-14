using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using SqlMcpServer.Application.Models.Requests;
using SqlMcpServer.Application.Services;
using SqlMcpServer.CrossCutting.Throttling;
using SqlMcpServer.Domain.Enums;
using SqlMcpServer.Host.Helpers;

namespace SqlMcpServer.Host.Tools;

[McpServerToolType]
public sealed class QueryTools(IServiceScopeFactory sf, IRequestThrottler throttler)
    : McpToolBase(sf, throttler)
{
    [McpServerTool(Name = "execute_query")]
    [Description("Execute a SQL SELECT query and return results as JSON. Validated against safety rules before execution.")]
    public Task<string> ExecuteQuery(
        [Description("SQL statement to execute")] string sql,
        [Description("JSON object of named parameters, e.g. {\"@id\": 42}")] string? parameters = null,
        [Description("Query timeout in seconds (max 300)")] int timeoutSeconds = 30,
        [Description("Maximum rows to return (max 10000)")] int maxRows = 1000,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<QueryService>();
            var paramDict = DeserializeParameters(parameters);
            var request = new ExecuteQueryRequest(sql, paramDict, timeoutSeconds, maxRows);
            var result = await svc.ExecuteQueryAsync(request, UserRole.Developer, ct: token);
            return ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "execute_parameterized_query")]
    [Description("Execute a parameterized SQL query. Parameters are supplied as a JSON object to prevent SQL injection.")]
    public Task<string> ExecuteParameterizedQuery(
        [Description("Parameterized SQL statement (use @param syntax)")] string sql,
        [Description("JSON object mapping parameter names to values")] string parameters,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<QueryService>();
            var paramDict = DeserializeParameters(parameters) ?? [];
            var request = new ExecuteQueryRequest(sql, paramDict);
            var result = await svc.ExecuteParameterizedQueryAsync(request, UserRole.Developer, ct: token);
            return ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "execute_procedure")]
    [Description("Execute a stored procedure with named parameters and return the result set.")]
    public Task<string> ExecuteProcedure(
        [Description("Schema name")] string schema,
        [Description("Procedure name")] string name,
        [Description("JSON object of parameter values, e.g. {\"@customerId\": 1}")] string? parameters = null,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<QueryService>();
            var paramDict = DeserializeParameters(parameters);
            var request = new ExecuteProcedureRequest(schema, name, paramDict);
            var result = await svc.ExecuteProcedureAsync(request, UserRole.Developer, ct: token);
            return ToolHelper.Serialize(result);
        }, ct);

    [McpServerTool(Name = "validate_query")]
    [Description("Syntax-validate a SQL statement using SET PARSEONLY ON without executing it.")]
    public Task<string> ValidateQuery(
        [Description("SQL statement to validate")] string sql,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<QueryService>();
            var isValid = await svc.ValidateQueryAsync(sql, token);
            return ToolHelper.Serialize(new { isValid, sql });
        }, ct);

    [McpServerTool(Name = "format_query")]
    [Description("Format a SQL statement: uppercase keywords, normalize whitespace.")]
    public Task<string> FormatQuery(
        [Description("SQL statement to format")] string sql,
        CancellationToken ct = default)
        => ExecuteAsync((sp, _) =>
        {
            var svc = sp.GetRequiredService<QueryService>();
            var formatted = svc.FormatQueryAsync(sql);
            return Task.FromResult(ToolHelper.Serialize(new { formatted }));
        }, ct);

    [McpServerTool(Name = "estimate_query_cost")]
    [Description("Estimate the cost of a SQL query using SET SHOWPLAN_ALL ON (returns estimated row counts and CPU/IO costs without executing).")]
    public Task<string> EstimateQueryCost(
        [Description("SQL statement to estimate")] string sql,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<QueryService>();
            var plan = await svc.EstimateQueryCostAsync(sql, token);
            return ToolHelper.Serialize(plan);
        }, ct);

    [McpServerTool(Name = "get_execution_plan")]
    [Description("Get the XML execution plan for a SQL statement using SET SHOWPLAN_XML ON.")]
    public Task<string> GetExecutionPlan(
        [Description("SQL statement")] string sql,
        CancellationToken ct = default)
        => ExecuteAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<QueryService>();
            var plan = await svc.GetExecutionPlanAsync(sql, token);
            return ToolHelper.Serialize(plan);
        }, ct);

    [McpServerTool(Name = "analyze_query")]
    [Description("Analyze a SQL query's IO and time statistics using SET STATISTICS IO, TIME ON.")]
    public Task<string> AnalyzeQuery(
        [Description("SQL statement to analyze")] string sql,
        CancellationToken ct = default)
        => ExecuteThrottledAsync(async (sp, token) =>
        {
            var svc = sp.GetRequiredService<QueryService>();
            var result = await svc.AnalyzeQueryAsync(sql, token);
            return ToolHelper.Serialize(result);
        }, ct);

    private static Dictionary<string, object?>? DeserializeParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        }
        catch
        {
            return null;
        }
    }
}
