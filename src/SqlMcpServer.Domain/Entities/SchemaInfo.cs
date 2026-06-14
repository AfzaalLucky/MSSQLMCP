namespace SqlMcpServer.Domain.Entities;

public sealed record SchemaInfo(
    string Name,
    string Owner,
    int SchemaId);
