namespace SqlMcpServer.Application.Models.Requests;

public sealed record SearchDataRequest(
    string Schema,
    string Table,
    string SearchTerm,
    IEnumerable<string>? Columns = null);
