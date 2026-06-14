namespace SqlMcpServer.Domain.Contracts.Infrastructure;

public interface ISecretProvider
{
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
}
