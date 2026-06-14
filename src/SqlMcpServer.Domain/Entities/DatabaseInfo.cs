namespace SqlMcpServer.Domain.Entities;

public sealed record DatabaseInfo(
    string Name,
    string State,
    int CompatibilityLevel,
    string Collation,
    DateTime CreateDate,
    bool IsReadOnly,
    string RecoveryModel);
