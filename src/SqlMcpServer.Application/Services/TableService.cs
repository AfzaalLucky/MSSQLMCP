using Microsoft.Extensions.Options;
using SqlMcpServer.Application.Configuration;
using SqlMcpServer.Application.Models.Requests;
using SqlMcpServer.Application.Models.Responses;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Application.Services;

public sealed class TableService
{
    private readonly ITableRepository _tableRepo;
    private readonly IIndexRepository _indexRepo;
    private readonly IConstraintRepository _constraintRepo;
    private readonly ICacheService _cache;
    private readonly ToolSettings _settings;

    public TableService(
        ITableRepository tableRepo,
        IIndexRepository indexRepo,
        IConstraintRepository constraintRepo,
        ICacheService cache,
        IOptions<ToolSettings> settings)
    {
        _tableRepo = tableRepo;
        _indexRepo = indexRepo;
        _constraintRepo = constraintRepo;
        _cache = cache;
        _settings = settings.Value;
    }

    public async Task<DescribeTableResponse?> DescribeTableAsync(
        string schema, string table, CancellationToken ct = default)
    {
        var tableInfo = await _tableRepo.DescribeTableAsync(schema, table, ct);
        if (tableInfo is null) return null;

        var (columns, pks, fks, indexes, constraints, rowCount, stats) = await (
            _cache.GetOrSetAsync($"columns:{schema}:{table}",
                _ => _tableRepo.GetTableColumnsAsync(schema, table, ct),
                TimeSpan.FromSeconds(_settings.ObjectsCacheTtl), ct),
            _tableRepo.GetPrimaryKeysAsync(schema, table, ct),
            _constraintRepo.GetForeignKeysAsync(schema, table, ct),
            _indexRepo.GetIndexesAsync(schema, table, ct),
            _constraintRepo.GetConstraintsAsync(schema, table, ct),
            _tableRepo.GetRowCountAsync(schema, table, ct),
            _tableRepo.GetTableStatisticsAsync(schema, table, ct)
        ).WhenAll();

        return new DescribeTableResponse(
            tableInfo, columns, pks, fks, indexes, constraints, rowCount, stats);
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetTableColumnsAsync(
        string schema, string table, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            $"columns:{schema}:{table}",
            _ => _tableRepo.GetTableColumnsAsync(schema, table, ct),
            TimeSpan.FromSeconds(_settings.ObjectsCacheTtl), ct);
    }

    public Task<IReadOnlyList<ConstraintInfo>> GetPrimaryKeysAsync(
        string schema, string table, CancellationToken ct = default) =>
        _tableRepo.GetPrimaryKeysAsync(schema, table, ct);

    public Task<IReadOnlyList<ForeignKeyInfo>> GetTableRelationshipsAsync(
        string schema, string table, CancellationToken ct = default) =>
        _tableRepo.GetTableRelationshipsAsync(schema, table, ct);

    public Task<TableStatistics?> GetTableStatisticsAsync(
        string schema, string table, CancellationToken ct = default) =>
        _tableRepo.GetTableStatisticsAsync(schema, table, ct);

    public Task<long> GetRowCountAsync(
        string schema, string table, CancellationToken ct = default) =>
        _tableRepo.GetRowCountAsync(schema, table, ct);

    public Task<QueryResult> SampleTableDataAsync(
        SampleDataRequest request, CancellationToken ct = default) =>
        _tableRepo.SampleTableDataAsync(request.Schema, request.Table, request.RowCount, ct);

    public Task<QueryResult> SearchTableDataAsync(
        SearchDataRequest request, CancellationToken ct = default) =>
        _tableRepo.SearchTableDataAsync(request.Schema, request.Table, request.SearchTerm, request.Columns, ct);
}

file static class TaskExtensions
{
    public static async Task<(T1, T2, T3, T4, T5, T6, T7)> WhenAll<T1, T2, T3, T4, T5, T6, T7>(
        this (Task<T1>, Task<T2>, Task<T3>, Task<T4>, Task<T5>, Task<T6>, Task<T7>) tasks)
    {
        await Task.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3, tasks.Item4,
            tasks.Item5, tasks.Item6, tasks.Item7);
        return (tasks.Item1.Result, tasks.Item2.Result, tasks.Item3.Result,
            tasks.Item4.Result, tasks.Item5.Result, tasks.Item6.Result, tasks.Item7.Result);
    }
}
