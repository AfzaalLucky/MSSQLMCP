using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Domain.Contracts.Repositories;

public interface IConstraintRepository
{
    Task<IReadOnlyList<ConstraintInfo>> GetConstraintsAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ForeignKeyInfo>> GetForeignKeysAsync(string schema, string table, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SequenceInfo>> GetSequencesAsync(string? schema, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SynonymInfo>> GetSynonymsAsync(string? schema, CancellationToken cancellationToken = default);
}
