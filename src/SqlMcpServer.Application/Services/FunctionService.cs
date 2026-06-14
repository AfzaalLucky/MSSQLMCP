using Microsoft.Extensions.Options;
using SqlMcpServer.Application.Configuration;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Application.Services;

public sealed class FunctionService
{
    private readonly IFunctionRepository _repo;
    private readonly ICacheService _cache;
    private readonly ToolSettings _settings;

    public FunctionService(IFunctionRepository repo, ICacheService cache, IOptions<ToolSettings> settings)
    {
        _repo = repo;
        _cache = cache;
        _settings = settings.Value;
    }

    public Task<IReadOnlyList<FunctionInfo>> GetScalarFunctionsAsync(
        string? schema, CancellationToken ct = default) =>
        _repo.GetScalarFunctionsAsync(schema, ct);

    public Task<IReadOnlyList<FunctionInfo>> GetTableValuedFunctionsAsync(
        string? schema, CancellationToken ct = default) =>
        _repo.GetTableValuedFunctionsAsync(schema, ct);

    public Task<FunctionInfo?> DescribeFunctionAsync(
        string schema, string name, CancellationToken ct = default) =>
        _repo.DescribeFunctionAsync(schema, name, ct);

    public async Task<string?> GetFunctionDefinitionAsync(
        string schema, string name, CancellationToken ct = default) =>
        await _cache.GetOrSetAsync(
            $"def:fn:{schema}:{name}",
            _ => _repo.GetFunctionDefinitionAsync(schema, name, ct),
            TimeSpan.FromSeconds(_settings.DefinitionsCacheTtl), ct);

    public Task<IReadOnlyList<DependencyInfo>> AnalyzeFunctionDependenciesAsync(
        string schema, string name, CancellationToken ct = default) =>
        _repo.AnalyzeFunctionDependenciesAsync(schema, name, ct);
}
