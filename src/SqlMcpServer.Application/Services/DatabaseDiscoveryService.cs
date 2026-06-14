using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlMcpServer.Application.Configuration;
using SqlMcpServer.Application.Models.Requests;
using SqlMcpServer.Application.Models.Responses;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Application.Services;

public sealed class DatabaseDiscoveryService
{
    private readonly ISchemaRepository _schemaRepo;
    private readonly ITableRepository _tableRepo;
    private readonly IViewRepository _viewRepo;
    private readonly IFunctionRepository _functionRepo;
    private readonly IProcedureRepository _procedureRepo;
    private readonly ITriggerRepository _triggerRepo;
    private readonly ITypeRepository _typeRepo;
    private readonly IIndexRepository _indexRepo;
    private readonly IConstraintRepository _constraintRepo;
    private readonly ICacheService _cache;
    private readonly ToolSettings _settings;
    private readonly ILogger<DatabaseDiscoveryService> _logger;

    public DatabaseDiscoveryService(
        ISchemaRepository schemaRepo,
        ITableRepository tableRepo,
        IViewRepository viewRepo,
        IFunctionRepository functionRepo,
        IProcedureRepository procedureRepo,
        ITriggerRepository triggerRepo,
        ITypeRepository typeRepo,
        IIndexRepository indexRepo,
        IConstraintRepository constraintRepo,
        ICacheService cache,
        IOptions<ToolSettings> settings,
        ILogger<DatabaseDiscoveryService> logger)
    {
        _schemaRepo = schemaRepo;
        _tableRepo = tableRepo;
        _viewRepo = viewRepo;
        _functionRepo = functionRepo;
        _procedureRepo = procedureRepo;
        _triggerRepo = triggerRepo;
        _typeRepo = typeRepo;
        _indexRepo = indexRepo;
        _constraintRepo = constraintRepo;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("GetDatabases");
        return await _cache.GetOrSetAsync(
            "databases",
            _ => _schemaRepo.GetDatabasesAsync(ct),
            TimeSpan.FromSeconds(_settings.DatabaseListCacheTtl), ct);
    }

    public async Task<IReadOnlyList<SchemaInfo>> GetSchemasAsync(string database, CancellationToken ct = default)
    {
        _logger.LogDebug("GetSchemas: {Database}", database);
        return await _cache.GetOrSetAsync(
            $"schemas:{database}",
            _ => _schemaRepo.GetSchemasAsync(database, ct),
            TimeSpan.FromSeconds(_settings.SchemasCacheTtl), ct);
    }

    public async Task<PagedResponse<TableInfo>> GetTablesAsync(GetObjectsRequest request, CancellationToken ct = default)
    {
        var all = await _cache.GetOrSetAsync(
            $"tables:{request.Database}:{request.Schema}",
            _ => _tableRepo.GetTablesAsync(request.Database, request.Schema, ct),
            TimeSpan.FromSeconds(_settings.SchemasCacheTtl), ct);

        return Page(all, request.Page, request.PageSize);
    }

    public async Task<IReadOnlyList<Domain.Entities.ViewInfo>> GetViewsAsync(
        string database, string? schema, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            $"views:{database}:{schema}",
            _ => _viewRepo.GetViewsAsync(database, schema, ct),
            TimeSpan.FromSeconds(_settings.SchemasCacheTtl), ct);
    }

    public async Task<IReadOnlyList<FunctionInfo>> GetFunctionsAsync(
        string database, string? schema, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            $"functions:{database}:{schema}",
            _ => _functionRepo.GetFunctionsAsync(database, schema, ct),
            TimeSpan.FromSeconds(_settings.SchemasCacheTtl), ct);
    }

    public async Task<IReadOnlyList<ProcedureInfo>> GetProceduresAsync(
        string database, string? schema, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            $"procedures:{database}:{schema}",
            _ => _procedureRepo.GetProceduresAsync(database, schema, ct),
            TimeSpan.FromSeconds(_settings.SchemasCacheTtl), ct);
    }

    public async Task<IReadOnlyList<TriggerInfo>> GetTriggersAsync(
        string database, string? schema, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            $"triggers:{database}:{schema}",
            _ => _triggerRepo.GetTriggersAsync(database, schema, ct),
            TimeSpan.FromSeconds(_settings.SchemasCacheTtl), ct);
    }

    public async Task<IReadOnlyList<UserDefinedTypeInfo>> GetUserDefinedTypesAsync(
        string? schema, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            $"udts:{schema}",
            _ => _typeRepo.GetUserDefinedTypesAsync(schema, ct),
            TimeSpan.FromSeconds(_settings.ObjectsCacheTtl), ct);
    }

    public async Task<IReadOnlyList<TableTypeInfo>> GetTableTypesAsync(
        string? schema, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            $"tabletypes:{schema}",
            _ => _typeRepo.GetTableTypesAsync(schema, ct),
            TimeSpan.FromSeconds(_settings.ObjectsCacheTtl), ct);
    }

    public async Task<IReadOnlyList<IndexInfo>> GetIndexesAsync(
        string schema, string table, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            $"indexes:{schema}:{table}",
            _ => _indexRepo.GetIndexesAsync(schema, table, ct),
            TimeSpan.FromSeconds(_settings.ObjectsCacheTtl), ct);
    }

    public async Task<IReadOnlyList<ConstraintInfo>> GetConstraintsAsync(
        string schema, string table, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            $"constraints:{schema}:{table}",
            _ => _constraintRepo.GetConstraintsAsync(schema, table, ct),
            TimeSpan.FromSeconds(_settings.ObjectsCacheTtl), ct);
    }

    public async Task<IReadOnlyList<ForeignKeyInfo>> GetForeignKeysAsync(
        string schema, string table, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            $"fk:{schema}:{table}",
            _ => _constraintRepo.GetForeignKeysAsync(schema, table, ct),
            TimeSpan.FromSeconds(_settings.ObjectsCacheTtl), ct);
    }

    public async Task<IReadOnlyList<SequenceInfo>> GetSequencesAsync(
        string? schema, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            $"sequences:{schema}",
            _ => _constraintRepo.GetSequencesAsync(schema, ct),
            TimeSpan.FromSeconds(_settings.ObjectsCacheTtl), ct);
    }

    public async Task<IReadOnlyList<SynonymInfo>> GetSynonymsAsync(
        string? schema, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync(
            $"synonyms:{schema}",
            _ => _constraintRepo.GetSynonymsAsync(schema, ct),
            TimeSpan.FromSeconds(_settings.ObjectsCacheTtl), ct);
    }

    private static PagedResponse<T> Page<T>(IReadOnlyList<T> all, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList().AsReadOnly();
        return new PagedResponse<T>(items, all.Count, page, pageSize);
    }
}
