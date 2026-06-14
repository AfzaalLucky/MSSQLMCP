namespace SqlMcpServer.Application.Models.Requests;

public sealed record DescribeObjectRequest(string Schema, string Name);
