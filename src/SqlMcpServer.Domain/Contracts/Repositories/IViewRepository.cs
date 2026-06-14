using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Domain.Contracts.Repositories;

public interface IViewRepository
{
    Task<IReadOnlyList<ViewInfo>> GetViewsAsync(string database, string? schema, CancellationToken cancellationToken = default);
    Task<ViewInfo?> DescribeViewAsync(string schema, string view, CancellationToken cancellationToken = default);
    Task<string?> GetViewDefinitionAsync(string schema, string view, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DependencyInfo>> GetViewDependenciesAsync(string schema, string view, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ColumnInfo>> GetViewColumnsAsync(string schema, string view, CancellationToken cancellationToken = default);
}
