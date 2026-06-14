using Microsoft.Extensions.Options;
using SqlMcpServer.Application.Configuration;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Application.Services;

public sealed class ProcedureService
{
    private readonly IProcedureRepository _repo;
    private readonly ICacheService _cache;
    private readonly ToolSettings _settings;

    public ProcedureService(IProcedureRepository repo, ICacheService cache, IOptions<ToolSettings> settings)
    {
        _repo = repo;
        _cache = cache;
        _settings = settings.Value;
    }

    public Task<ProcedureInfo?> DescribeProcedureAsync(
        string schema, string name, CancellationToken ct = default) =>
        _repo.DescribeProcedureAsync(schema, name, ct);

    public async Task<string?> GetProcedureDefinitionAsync(
        string schema, string name, CancellationToken ct = default) =>
        await _cache.GetOrSetAsync(
            $"def:proc:{schema}:{name}",
            _ => _repo.GetProcedureDefinitionAsync(schema, name, ct),
            TimeSpan.FromSeconds(_settings.DefinitionsCacheTtl), ct);

    public Task<IReadOnlyList<ProcedureParameter>> GetProcedureParametersAsync(
        string schema, string name, CancellationToken ct = default) =>
        _repo.GetProcedureParametersAsync(schema, name, ct);

    public Task<IReadOnlyList<DependencyInfo>> AnalyzeProcedureDependenciesAsync(
        string schema, string name, CancellationToken ct = default) =>
        _repo.AnalyzeProcedureDependenciesAsync(schema, name, ct);
}
