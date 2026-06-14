namespace SqlMcpServer.Domain.Entities;

public sealed record ProcedureParameter(
    string Name,
    string DataType,
    int OrdinalPosition,
    string ParameterMode,
    int? MaxLength,
    int? Precision,
    int? Scale,
    bool HasDefault,
    string? DefaultValue,
    bool IsOutput,
    bool IsReadOnly);
