using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Domain.Contracts.Repositories;

public interface ITriggerRepository
{
    Task<IReadOnlyList<TriggerInfo>> GetTriggersAsync(string database, string? schema, CancellationToken cancellationToken = default);
    Task<TriggerInfo?> DescribeTriggerAsync(string schema, string name, CancellationToken cancellationToken = default);
    Task<string?> GetTriggerDefinitionAsync(string schema, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DependencyInfo>> GetTriggerDependenciesAsync(string schema, string name, CancellationToken cancellationToken = default);
}
