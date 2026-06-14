namespace SqlMcpServer.Infrastructure.Configuration;

public sealed class CacheSettings
{
    public string Provider { get; set; } = "Memory";
    public string? RedisConnectionString { get; set; }
    public int DefaultTtlSeconds { get; set; } = 300;
    public int SchemasTtlSeconds { get; set; } = 600;
    public int DefinitionsTtlSeconds { get; set; } = 900;
    public int StatisticsTtlSeconds { get; set; } = 120;
    public int DatabaseListTtlSeconds { get; set; } = 1800;
}
