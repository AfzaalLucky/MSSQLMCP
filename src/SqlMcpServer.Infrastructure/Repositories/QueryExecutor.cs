using System.Data;
using System.Diagnostics;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;
using SqlMcpServer.Infrastructure.Configuration;

namespace SqlMcpServer.Infrastructure.Repositories;

internal sealed class QueryExecutor : RepositoryBase, IQueryExecutor
{
    private readonly SqlServerSettings _settings;
    private readonly ILogger<QueryExecutor> _logger;

    public QueryExecutor(
        IConnectionFactory connectionFactory,
        ResiliencePipeline pipeline,
        IOptions<SqlServerSettings> settings,
        ILogger<QueryExecutor> logger)
        : base(connectionFactory, pipeline)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<QueryResult> ExecuteQueryAsync(
        string sql, Dictionary<string, object?>? parameters, int timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async conn =>
        {
            var dynamicParams = BuildDynamicParameters(parameters);
            var sw = Stopwatch.StartNew();

            var rows = (await conn.QueryAsync(
                sql,
                dynamicParams,
                commandTimeout: timeoutSeconds > 0 ? timeoutSeconds : _settings.CommandTimeoutSeconds))
                .ToList();

            sw.Stop();
            _logger.LogDebug("Query executed in {ElapsedMs}ms, returned {RowCount} rows", sw.ElapsedMilliseconds, rows.Count);

            return BuildResult(rows, sw.ElapsedMilliseconds, 0, _settings.MaxRowsPerQuery);
        }, cancellationToken);
    }

    public async Task<QueryResult> ExecuteParameterizedQueryAsync(
        string sql, Dictionary<string, object?>? parameters,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteQueryAsync(sql, parameters, _settings.CommandTimeoutSeconds, cancellationToken);
    }

    public async Task<bool> ValidateQueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async conn =>
        {
            try
            {
                await conn.ExecuteAsync("SET PARSEONLY ON", commandTimeout: 10);
                await conn.ExecuteAsync(sql, commandTimeout: 10);
                return true;
            }
            catch (SqlException ex)
            {
                _logger.LogDebug(ex, "Query validation failed: {Message}", ex.Message);
                return false;
            }
            finally
            {
                try { await conn.ExecuteAsync("SET PARSEONLY OFF", commandTimeout: 5); } catch { }
            }
        }, cancellationToken);
    }

    public async Task<ExecutionPlanInfo> EstimateQueryCostAsync(
        string sql, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async conn =>
        {
            await conn.ExecuteAsync("SET SHOWPLAN_ALL ON", commandTimeout: 10);
            try
            {
                var plan = await conn.QueryFirstOrDefaultAsync<PlanAllRow>(sql, commandTimeout: _settings.CommandTimeoutSeconds);
                return new ExecutionPlanInfo(
                    sql, null,
                    plan?.TotalSubtreeCost ?? 0,
                    ParseStatementType(plan?.PlanStatementType),
                    (long)(plan?.EstimateRows ?? 0),
                    plan?.EstimateIO ?? 0,
                    plan?.EstimateCPU ?? 0);
            }
            finally
            {
                try { await conn.ExecuteAsync("SET SHOWPLAN_ALL OFF", commandTimeout: 5); } catch { }
            }
        }, cancellationToken);
    }

    public async Task<ExecutionPlanInfo> GetExecutionPlanAsync(
        string sql, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async conn =>
        {
            await conn.ExecuteAsync("SET SHOWPLAN_XML ON", commandTimeout: 10);
            try
            {
                var planXml = await conn.ExecuteScalarAsync<string?>(sql, commandTimeout: _settings.CommandTimeoutSeconds);
                return new ExecutionPlanInfo(sql, planXml, 0, Domain.Enums.StatementType.Unknown, 0, 0, 0);
            }
            finally
            {
                try { await conn.ExecuteAsync("SET SHOWPLAN_XML OFF", commandTimeout: 5); } catch { }
            }
        }, cancellationToken);
    }

    public async Task<QueryResult> AnalyzeQueryAsync(
        string sql, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async conn =>
        {
            await conn.ExecuteAsync("SET STATISTICS IO, TIME ON", commandTimeout: 10);
            try
            {
                var sw = Stopwatch.StartNew();
                var rows = (await conn.QueryAsync(sql, commandTimeout: _settings.CommandTimeoutSeconds)).ToList();
                sw.Stop();
                return BuildResult(rows, sw.ElapsedMilliseconds, 0, _settings.MaxRowsPerQuery);
            }
            finally
            {
                try { await conn.ExecuteAsync("SET STATISTICS IO, TIME OFF", commandTimeout: 5); } catch { }
            }
        }, cancellationToken);
    }

    public async Task<QueryResult> ExecuteProcedureAsync(
        string schema, string name, Dictionary<string, object?>? parameters,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(schema); ValidateIdentifier(name);
        return await ExecuteAsync(async conn =>
        {
            var dynamicParams = BuildDynamicParameters(parameters);
            var sw = Stopwatch.StartNew();
            var rows = (await conn.QueryAsync(
                $"[{schema}].[{name}]",
                dynamicParams,
                commandType: CommandType.StoredProcedure,
                commandTimeout: _settings.CommandTimeoutSeconds))
                .ToList();
            sw.Stop();
            return BuildResult(rows, sw.ElapsedMilliseconds, 0, _settings.MaxRowsPerQuery);
        }, cancellationToken);
    }

    private static DynamicParameters BuildDynamicParameters(Dictionary<string, object?>? parameters)
    {
        var dp = new DynamicParameters();
        if (parameters is not null)
            foreach (var (key, value) in parameters)
                dp.Add(key, value);
        return dp;
    }

    private static QueryResult BuildResult(List<dynamic> rows, long elapsedMs, int affected, int maxRows)
    {
        if (rows.Count == 0)
            return new QueryResult([], [], 0, elapsedMs, affected, false, null);

        var first = (IDictionary<string, object?>)rows[0];
        var columns = first.Keys.ToList().AsReadOnly();

        bool truncated = rows.Count >= maxRows;
        var take = truncated ? maxRows : rows.Count;

        var mapped = rows.Take(take)
            .Select(r => (IReadOnlyDictionary<string, object?>)
                ((IDictionary<string, object?>)r).ToDictionary(kv => kv.Key, kv => kv.Value))
            .ToList().AsReadOnly();

        return new QueryResult(
            columns, mapped, rows.Count, elapsedMs, affected,
            truncated, truncated ? $"Result truncated at {maxRows} rows" : null);
    }

    private static Domain.Enums.StatementType ParseStatementType(string? type) => type?.ToUpperInvariant() switch
    {
        "SELECT" => Domain.Enums.StatementType.Select,
        "INSERT" => Domain.Enums.StatementType.Insert,
        "UPDATE" => Domain.Enums.StatementType.Update,
        "DELETE" => Domain.Enums.StatementType.Delete,
        "MERGE" => Domain.Enums.StatementType.Merge,
        _ => Domain.Enums.StatementType.Unknown
    };

    private sealed class PlanAllRow
    {
        public string? PlanStatementType { get; init; }
        public double EstimateRows { get; init; }
        public double EstimateIO { get; init; }
        public double EstimateCPU { get; init; }
        public double TotalSubtreeCost { get; init; }
    }
}
