namespace SqlMcpServer.Application.Models.Requests;

public sealed record CompareSchemasRequest(
    string SourceDatabase,
    string SourceSchema,
    string TargetDatabase,
    string TargetSchema);
