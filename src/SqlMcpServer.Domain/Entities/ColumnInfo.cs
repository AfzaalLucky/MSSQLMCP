namespace SqlMcpServer.Domain.Entities;

public sealed record ColumnInfo(
    string TableSchema,
    string TableName,
    string ColumnName,
    int OrdinalPosition,
    string DataType,
    int? MaxLength,
    int? Precision,
    int? Scale,
    bool IsNullable,
    bool HasDefault,
    string? DefaultValue,
    bool IsComputed,
    bool IsIdentity,
    string? ComputedDefinition);
