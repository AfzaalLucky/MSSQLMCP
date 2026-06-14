namespace SqlMcpServer.Application.Models.Requests;

public sealed record SampleDataRequest(string Schema, string Table, int RowCount = 100);
