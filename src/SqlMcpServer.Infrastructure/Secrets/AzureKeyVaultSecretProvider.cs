using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using SqlMcpServer.Domain.Contracts.Infrastructure;

namespace SqlMcpServer.Infrastructure.Secrets;

internal sealed class AzureKeyVaultSecretProvider : ISecretProvider
{
    private readonly SecretClient _client;
    private readonly ILogger<AzureKeyVaultSecretProvider> _logger;

    public AzureKeyVaultSecretProvider(string keyVaultUri, ILogger<AzureKeyVaultSecretProvider> logger)
    {
        _client = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());
        _logger = logger;
    }

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetSecretAsync(name, cancellationToken: cancellationToken);
            return response.Value.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve secret '{Name}' from Key Vault", name);
            return null;
        }
    }
}
