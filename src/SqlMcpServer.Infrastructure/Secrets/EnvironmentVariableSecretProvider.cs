using SqlMcpServer.Domain.Contracts.Infrastructure;

namespace SqlMcpServer.Infrastructure.Secrets;

internal sealed class EnvironmentVariableSecretProvider : ISecretProvider
{
    private const string Prefix = "SQLMCP_SECRET_";

    public Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        var envKey = Prefix + name.ToUpperInvariant().Replace('-', '_').Replace('/', '_');
        var value = Environment.GetEnvironmentVariable(envKey);
        return Task.FromResult(value);
    }
}
