using System.Data.Common;
using Azure.Identity;
using Azure.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.Enums;
using SqlMcpServer.Infrastructure.Configuration;

namespace SqlMcpServer.Infrastructure.Connection;

internal sealed class SqlConnectionFactory : IConnectionFactory
{
    private readonly SqlServerSettings _settings;
    private readonly ILogger<SqlConnectionFactory> _logger;
    private readonly DefaultAzureCredential? _credential;

    public SqlConnectionFactory(IOptions<SqlServerSettings> settings, ILogger<SqlConnectionFactory> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        if (_settings.AuthMode is ConnectionAuthMode.AzureManagedIdentity or ConnectionAuthMode.AzureAD)
            _credential = new DefaultAzureCredential();
    }

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = BuildConnectionString();
        var connection = new SqlConnection(connectionString);

        if (_settings.AuthMode == ConnectionAuthMode.AzureManagedIdentity && _credential is not null)
        {
            var tokenContext = new TokenRequestContext([_settings.AzureScope]);
            var token = await _credential.GetTokenAsync(tokenContext, cancellationToken);
            connection.AccessToken = token.Token;
        }

        await connection.OpenAsync(cancellationToken);
        _logger.LogDebug("SQL connection opened to {Server}", connection.DataSource);
        return connection;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await CreateConnectionAsync(cancellationToken);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQL connection health check failed");
            return false;
        }
    }

    private string BuildConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(_settings.ConnectionString)
        {
            MaxPoolSize = _settings.MaxPoolSize,
            MinPoolSize = _settings.MinPoolSize,
            ConnectTimeout = _settings.ConnectTimeoutSeconds,
        };

        if (_settings.Encrypt.HasValue)
            builder.Encrypt = _settings.Encrypt.Value;
        if (_settings.TrustServerCertificate.HasValue)
            builder.TrustServerCertificate = _settings.TrustServerCertificate.Value;

        switch (_settings.AuthMode)
        {
            case ConnectionAuthMode.WindowsAuth:
                builder.IntegratedSecurity = true;
                break;
            case ConnectionAuthMode.AzureAD:
                builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
                break;
            case ConnectionAuthMode.AzureManagedIdentity:
                // Token is set on the connection object, not in the connection string
                builder.IntegratedSecurity = false;
                break;
        }

        return builder.ConnectionString;
    }
}
