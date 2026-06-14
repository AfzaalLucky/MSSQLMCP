namespace SqlMcpServer.Application.Models.Requests;

public sealed record GetObjectsRequest(
    string Database,
    string? Schema = null,
    int Page = 1,
    int PageSize = 25);
