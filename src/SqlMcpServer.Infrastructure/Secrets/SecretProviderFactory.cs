using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SqlMcpServer.Domain.Contracts.Infrastructure;

namespace SqlMcpServer.Infrastructure.Secrets;

public sealed class SecretProviderFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;

    public SecretProviderFactory(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory;
    }

    public ISecretProvider Create()
    {
        var keyVaultUri = _configuration["KeyVault:Uri"];
        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            var logger = _loggerFactory.CreateLogger<AzureKeyVaultSecretProvider>();
            return new AzureKeyVaultSecretProvider(keyVaultUri, logger);
        }

        return new EnvironmentVariableSecretProvider();
    }
}
