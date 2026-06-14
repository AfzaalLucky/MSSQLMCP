namespace SqlMcpServer.Domain.Exceptions;

public sealed class ToolException : McpDomainException
{
    public string ToolName { get; }

    public ToolException(string toolName, string message)
        : base("TOOL_ERROR", message)
    {
        ToolName = toolName;
    }

    public ToolException(string toolName, string message, Exception inner)
        : base("TOOL_ERROR", message, inner)
    {
        ToolName = toolName;
    }
}
