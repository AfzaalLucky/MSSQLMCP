using Microsoft.Extensions.Options;
using SqlMcpServer.Application.Configuration;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Application.Services;

public sealed class TriggerService
{
    private readonly ITriggerRepository _repo;
    private readonly ICacheService _cache;
    private readonly ToolSettings _settings;

    public TriggerService(ITriggerRepository repo, ICacheService cache, IOptions<ToolSettings> settings)
    {
        _repo = repo;
        _cache = cache;
        _settings = settings.Value;
    }

    public Task<TriggerInfo?> DescribeTriggerAsync(
        string schema, string name, CancellationToken ct = default) =>
        _repo.DescribeTriggerAsync(schema, name, ct);

    public async Task<string?> GetTriggerDefinitionAsync(
        string schema, string name, CancellationToken ct = default) =>
        await _cache.GetOrSetAsync(
            $"def:trigger:{schema}:{name}",
            _ => _repo.GetTriggerDefinitionAsync(schema, name, ct),
            TimeSpan.FromSeconds(_settings.DefinitionsCacheTtl), ct);

    public Task<IReadOnlyList<DependencyInfo>> GetTriggerDependenciesAsync(
        string schema, string name, CancellationToken ct = default) =>
        _repo.GetTriggerDependenciesAsync(schema, name, ct);
}
