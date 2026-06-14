namespace SqlMcpServer.Domain.Contracts.Infrastructure;

public interface IAuditLogger
{
    Task LogToolExecutionAsync(string tool, string? user, object? parameters, object? result, long durationMs, CancellationToken cancellationToken = default);
    Task LogQueryExecutionAsync(string sql, string? user, string? database, long rowCount, long durationMs, CancellationToken cancellationToken = default);
    Task LogSecurityViolationAsync(string? user, string tool, string reason, CancellationToken cancellationToken = default);
}
