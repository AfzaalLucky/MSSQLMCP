namespace SqlMcpServer.Application.Configuration;

public sealed class McpSettings
{
    public string ServerName { get; set; } = "SQL MCP Server";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "Enterprise MCP Server for Microsoft SQL Server";
    public int MaxConcurrentTools { get; set; } = 10;
}
