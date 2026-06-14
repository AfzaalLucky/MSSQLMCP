using SqlMcpServer.Domain.Entities;

namespace SqlMcpServer.Domain.Contracts.Repositories;

public interface ISchemaRepository
{
    Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchemaInfo>> GetSchemasAsync(string database, CancellationToken cancellationToken = default);
}
