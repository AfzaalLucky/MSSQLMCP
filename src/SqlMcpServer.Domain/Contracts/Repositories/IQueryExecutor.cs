using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Domain.Contracts.Repositories;

public interface IQueryExecutor
{
    Task<QueryResult> ExecuteQueryAsync(string sql, Dictionary<string, object?>? parameters, int timeoutSeconds, CancellationToken cancellationToken = default);
    Task<QueryResult> ExecuteParameterizedQueryAsync(string sql, Dictionary<string, object?>? parameters, CancellationToken cancellationToken = default);
    Task<bool> ValidateQueryAsync(string sql, CancellationToken cancellationToken = default);
    Task<ExecutionPlanInfo> EstimateQueryCostAsync(string sql, CancellationToken cancellationToken = default);
    Task<ExecutionPlanInfo> GetExecutionPlanAsync(string sql, CancellationToken cancellationToken = default);
    Task<QueryResult> AnalyzeQueryAsync(string sql, CancellationToken cancellationToken = default);
    Task<QueryResult> ExecuteProcedureAsync(string schema, string name, Dictionary<string, object?>? parameters, CancellationToken cancellationToken = default);
}
