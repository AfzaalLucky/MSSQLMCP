using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Contracts.Repositories;
using SqlMcpServer.Domain.Entities;
using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Application.Services;

public sealed class DependencyService
{
    private readonly IDependencyRepository _dependencyRepo;
    private readonly IConstraintRepository _constraintRepo;
    private readonly ITableRepository _tableRepo;
    private readonly ICacheService _cache;

    public DependencyService(
        IDependencyRepository dependencyRepo,
        IConstraintRepository constraintRepo,
        ITableRepository tableRepo,
        ICacheService cache)
    {
        _dependencyRepo = dependencyRepo;
        _constraintRepo = constraintRepo;
        _tableRepo = tableRepo;
        _cache = cache;
    }

    public Task<IReadOnlyList<DependencyInfo>> FindObjectDependenciesAsync(
        string schema, string name, CancellationToken ct = default) =>
        _dependencyRepo.FindObjectDependenciesAsync(schema, name, ct);

    public Task<IReadOnlyList<DependencyInfo>> FindReferencingObjectsAsync(
        string schema, string name, CancellationToken ct = default) =>
        _dependencyRepo.FindReferencingObjectsAsync(schema, name, ct);

    public Task<IReadOnlyList<DependencyInfo>> GenerateDependencyGraphAsync(
        string? schema, CancellationToken ct = default) =>
        _dependencyRepo.GenerateDependencyGraphAsync(schema, ct);

    public async Task<string> GenerateErdAsync(
        string database, string? schema, CancellationToken ct = default)
    {
        var tables = await _tableRepo.GetTablesAsync(database, schema, ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("erDiagram");

        foreach (var table in tables)
        {
            var fks = await _constraintRepo.GetForeignKeysAsync(table.Schema, table.Name, ct);
            foreach (var fk in fks)
            {
                sb.AppendLine(
                    $"    {Mermaid(table.Name)} ||--o{{  {Mermaid(fk.ReferencedTable)} : \"{fk.Name}\"");
            }
        }

        if (!tables.Any()) sb.AppendLine("    %% No tables found");
        return sb.ToString();
    }

    private static string Mermaid(string name) =>
        name.Replace(" ", "_").Replace("-", "_");
}
