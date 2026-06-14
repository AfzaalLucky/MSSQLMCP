using SqlMcpServer.Domain.Enums;

namespace SqlMcpServer.Domain.Entities;

public sealed record ExecutionPlanInfo(
    string QueryText,
    string? PlanXml,
    double EstimatedCost,
    StatementType StatementType,
    long EstimatedRows,
    double EstimatedIo,
    double EstimatedCpu);
