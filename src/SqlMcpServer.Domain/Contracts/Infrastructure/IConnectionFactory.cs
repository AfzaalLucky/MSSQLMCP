using System.Data.Common;

namespace SqlMcpServer.Domain.Contracts.Infrastructure;

public interface IConnectionFactory
{
    Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
