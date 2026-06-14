using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Domain.Contracts.Repositories;

public interface IIndexRepository
{
    Task<IReadOnlyList<IndexInfo>> GetIndexesAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IndexInfo>> GetMissingIndexesAsync(CancellationToken cancellationToken = default);
}
