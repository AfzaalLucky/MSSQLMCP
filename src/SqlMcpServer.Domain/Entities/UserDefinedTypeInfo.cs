namespace SqlMcpServer.Domain.Entities;

public sealed record UserDefinedTypeInfo(
    string Schema,
    string Name,
    string BaseType,
    int? MaxLength,
    int? Precision,
    int? Scale,
    bool IsNullable,
    bool IsAssemblyType,
    string? AssemblyName);
