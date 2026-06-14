using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Domain.Exceptions;

public sealed class QuerySafetyViolationException : SecurityException
{
    public string ViolatedPattern { get; }
    public StatementType DetectedStatementType { get; }

    public QuerySafetyViolationException(string violatedPattern, StatementType detectedType)
        : base($"Query blocked by safety policy. Violation: {violatedPattern}")
    {
        ViolatedPattern = violatedPattern;
        DetectedStatementType = detectedType;
    }
}
