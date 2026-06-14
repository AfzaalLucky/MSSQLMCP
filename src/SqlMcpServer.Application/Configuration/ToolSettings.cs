namespace SqlMcpServer.Application.Configuration;

public sealed class ToolSettings
{
    public IList<string> EnabledTools { get; set; } = [];
    public int DefaultPageSize { get; set; } = 25;
    public int MaxPageSize { get; set; } = 500;
    public int SampleDataRowCount { get; set; } = 100;

    // Cache TTLs (in seconds) for application-layer caching
    public int DatabaseListCacheTtl { get; set; } = 1800;
    public int SchemasCacheTtl { get; set; } = 600;
    public int ObjectsCacheTtl { get; set; } = 300;
    public int DefinitionsCacheTtl { get; set; } = 900;
    public int StatisticsCacheTtl { get; set; } = 120;
}
