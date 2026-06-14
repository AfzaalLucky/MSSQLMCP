using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Domain.Entities;

public sealed record DependencyInfo(
    string ObjectSchema,
    string ObjectName,
    ObjectType ObjectType,
    string? ReferencedSchema,
    string ReferencedName,
    ObjectType ReferencedType,
    bool IsCallerDependent,
    bool IsAmbiguous);
