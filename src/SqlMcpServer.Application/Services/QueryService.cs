using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlMcpServer.Application.Configuration;
using SqlMcpServer.Application.Models.Requests;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Contracts.Services;
using SqlMcpServer.Domain.Entities;
using SqlMcpServer.Domain.Enums;
using SqlMcpServer.Domain.Exceptions;

namespace SqlMcpServer.Application.Services;

public sealed class QueryService
{
    private readonly IQueryExecutor _executor;
    private readonly IQuerySafetyValidator _safetyValidator;
    private readonly IAuditLogger _auditLogger;
    private readonly IValidator<ExecuteQueryRequest> _queryValidator;
    private readonly IValidator<ExecuteProcedureRequest> _procValidator;
    private readonly SecuritySettings _security;
    private readonly ILogger<QueryService> _logger;

    public QueryService(
        IQueryExecutor executor,
        IQuerySafetyValidator safetyValidator,
        IAuditLogger auditLogger,
        IValidator<ExecuteQueryRequest> queryValidator,
        IValidator<ExecuteProcedureRequest> procValidator,
        IOptions<SecuritySettings> security,
        ILogger<QueryService> logger)
    {
        _executor = executor;
        _safetyValidator = safetyValidator;
        _auditLogger = auditLogger;
        _queryValidator = queryValidator;
        _procValidator = procValidator;
        _security = security.Value;
        _logger = logger;
    }

    public async Task<QueryResult> ExecuteQueryAsync(
        ExecuteQueryRequest request, UserRole userRole = UserRole.ReadOnly,
        string? user = null, string? database = null, CancellationToken ct = default)
    {
        var validation = await _queryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new Domain.Exceptions.ValidationException(
                "ExecuteQuery", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var safety = _safetyValidator.Validate(request.Sql, userRole);
        if (!safety.IsAllowed)
        {
            await _auditLogger.LogSecurityViolationAsync(user, "execute_query", safety.ViolationReason!, ct);
            throw new QuerySafetyViolationException(safety.ViolationReason!, safety.DetectedStatementType);
        }

        var clampedMaxRows = Math.Min(request.MaxRows, _security.MaxResultRows);
        var clampedTimeout = Math.Min(request.TimeoutSeconds, _security.MaxQueryTimeoutSeconds);

        var start = DateTimeOffset.UtcNow;
        var result = await _executor.ExecuteQueryAsync(
            request.Sql, request.Parameters, clampedTimeout, ct);

        await _auditLogger.LogQueryExecutionAsync(
            request.Sql, user, database, result.RowCount,
            result.ExecutionTimeMs, ct);

        return result;
    }

    public async Task<QueryResult> ExecuteParameterizedQueryAsync(
        ExecuteQueryRequest request, UserRole userRole = UserRole.ReadOnly,
        string? user = null, CancellationToken ct = default)
    {
        var safety = _safetyValidator.Validate(request.Sql, userRole);
        if (!safety.IsAllowed)
        {
            await _auditLogger.LogSecurityViolationAsync(user, "execute_parameterized_query",
                safety.ViolationReason!, ct);
            throw new QuerySafetyViolationException(safety.ViolationReason!, safety.DetectedStatementType);
        }

        return await _executor.ExecuteParameterizedQueryAsync(request.Sql, request.Parameters, ct);
    }

    public Task<bool> ValidateQueryAsync(string sql, CancellationToken ct = default) =>
        _executor.ValidateQueryAsync(sql, ct);

    public Task<ExecutionPlanInfo> EstimateQueryCostAsync(string sql, CancellationToken ct = default) =>
        _executor.EstimateQueryCostAsync(sql, ct);

    public Task<ExecutionPlanInfo> GetExecutionPlanAsync(string sql, CancellationToken ct = default) =>
        _executor.GetExecutionPlanAsync(sql, ct);

    public Task<QueryResult> AnalyzeQueryAsync(string sql, CancellationToken ct = default) =>
        _executor.AnalyzeQueryAsync(sql, ct);

    public string FormatQueryAsync(string sql)
    {
        // Uppercase SQL keywords and normalize whitespace
        var keywords = new[]
        {
            "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "IN", "IS", "NULL",
            "JOIN", "INNER", "LEFT", "RIGHT", "OUTER", "CROSS", "ON",
            "GROUP BY", "ORDER BY", "HAVING", "DISTINCT", "TOP",
            "INSERT INTO", "UPDATE", "DELETE FROM", "SET", "VALUES",
            "CREATE TABLE", "ALTER TABLE", "DROP TABLE",
            "WITH", "AS", "CASE", "WHEN", "THEN", "ELSE", "END",
            "EXISTS", "BETWEEN", "LIKE", "UNION", "ALL", "INTERSECT", "EXCEPT"
        };

        var result = sql;
        foreach (var keyword in keywords.OrderByDescending(k => k.Length))
        {
            result = System.Text.RegularExpressions.Regex.Replace(
                result, $@"\b{System.Text.RegularExpressions.Regex.Escape(keyword)}\b",
                keyword, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return result;
    }

    public async Task<QueryResult> ExecuteProcedureAsync(
        ExecuteProcedureRequest request, UserRole userRole = UserRole.Developer,
        string? user = null, CancellationToken ct = default)
    {
        var validation = await _procValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new Domain.Exceptions.ValidationException(
                "ExecuteProcedure", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        if (userRole == UserRole.ReadOnly || userRole == UserRole.Auditor)
            throw new SecurityException("ReadOnly and Auditor roles cannot execute stored procedures.");

        var result = await _executor.ExecuteProcedureAsync(request.Schema, request.Name, request.Parameters, ct);

        await _auditLogger.LogToolExecutionAsync(
            "execute_procedure", user,
            new { request.Schema, request.Name, request.Parameters },
            result, result.ExecutionTimeMs, ct);

        return result;
    }
}
