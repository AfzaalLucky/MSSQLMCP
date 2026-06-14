namespace SqlMcpServer.Application.Models.Requests;

public sealed record ExecuteProcedureRequest(
    string Schema,
    string Name,
    Dictionary<string, object?>? Parameters = null);
