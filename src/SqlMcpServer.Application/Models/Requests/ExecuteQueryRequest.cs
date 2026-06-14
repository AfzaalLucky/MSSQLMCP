namespace SqlMcpServer.Application.Models.Requests;

public sealed record ExecuteQueryRequest(
    string Sql,
    Dictionary<string, object?>? Parameters = null,
    int TimeoutSeconds = 30,
    int MaxRows = 1000);
