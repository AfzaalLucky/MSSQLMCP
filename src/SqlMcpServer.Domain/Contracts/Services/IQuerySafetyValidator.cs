using SqlMcpServer.Domain.Enums;
using SqlMcpServer.Domain.ValueObjects;

namespace SqlMcpServer.Domain.Contracts.Services;

public interface IQuerySafetyValidator
{
    QuerySafetyResult Validate(string sql, UserRole userRole);
    StatementType DetectStatementType(string sql);
}
