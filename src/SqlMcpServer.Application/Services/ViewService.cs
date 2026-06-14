using Microsoft.Extensions.Options;
using SqlMcpServer.Application.Configuration;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Application.Services;

public sealed class ViewService
{
    private readonly IViewRepository _repo;
    private readonly ICacheService _cache;
    private readonly ToolSettings _settings;

    public ViewService(IViewRepository repo, ICacheService cache, IOptions<ToolSettings> settings)
    {
        _repo = repo;
        _cache = cache;
        _settings = settings.Value;
    }

    public Task<ViewInfo?> DescribeViewAsync(string schema, string view, CancellationToken ct = default) =>
        _repo.DescribeViewAsync(schema, view, ct);

    public async Task<string?> GetViewDefinitionAsync(string schema, string view, CancellationToken ct = default) =>
        await _cache.GetOrSetAsync(
            $"def:view:{schema}:{view}",
            _ => _repo.GetViewDefinitionAsync(schema, view, ct),
            TimeSpan.FromSeconds(_settings.DefinitionsCacheTtl), ct);

    public Task<IReadOnlyList<DependencyInfo>> GetViewDependenciesAsync(
        string schema, string view, CancellationToken ct = default) =>
        _repo.GetViewDependenciesAsync(schema, view, ct);

    public Task<IReadOnlyList<ColumnInfo>> GetViewColumnsAsync(
        string schema, string view, CancellationToken ct = default) =>
        _repo.GetViewColumnsAsync(schema, view, ct);
}
