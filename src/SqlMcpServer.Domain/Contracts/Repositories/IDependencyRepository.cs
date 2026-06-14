using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Domain.Contracts.Repositories;

public interface IDependencyRepository
{
    Task<IReadOnlyList<DependencyInfo>> FindObjectDependenciesAsync(string schema, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DependencyInfo>> FindReferencingObjectsAsync(string schema, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DependencyInfo>> GenerateDependencyGraphAsync(string? schema, CancellationToken cancellationToken = default);
}
