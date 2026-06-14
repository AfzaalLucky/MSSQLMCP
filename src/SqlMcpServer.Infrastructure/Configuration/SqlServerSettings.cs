using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Infrastructure.Configuration;

public sealed class SqlServerSettings
{
    public string ConnectionString { get; set; } = "";
    public ConnectionAuthMode AuthMode { get; set; } = ConnectionAuthMode.WindowsAuth;
    public int CommandTimeoutSeconds { get; set; } = 30;
    public int MaxPoolSize { get; set; } = 100;
    public int MinPoolSize { get; set; } = 5;
    public int ConnectTimeoutSeconds { get; set; } = 15;
    public bool? Encrypt { get; set; }
    public bool? TrustServerCertificate { get; set; }
    public string AzureScope { get; set; } = "https://database.windows.net/.default";
    public int MaxRowsPerQuery { get; set; } = 10_000;
}
