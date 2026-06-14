using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Domain.ValueObjects;

public sealed record QuerySafetyResult(
    bool IsAllowed,
    string? ViolationReason,
    StatementType DetectedStatementType,
    QuerySafetyLevel RequiredLevel)
{
    public static QuerySafetyResult Allow(StatementType statementType) =>
        new(true, null, statementType, QuerySafetyLevel.ReadOnly);

    public static QuerySafetyResult Deny(string reason, StatementType statementType) =>
        new(false, reason, statementType, QuerySafetyLevel.AdminOnly);
}
