using Microsoft.Extensions.Logging;
using SqlMcpServer.Domain.Contracts.Infrastructure;

namespace SqlMcpServer.Infrastructure.Audit;

internal sealed class SerilogAuditLogger : IAuditLogger
{
    private readonly ILogger<SerilogAuditLogger> _logger;

    public SerilogAuditLogger(ILogger<SerilogAuditLogger> logger)
    {
        _logger = logger;
    }

    public Task LogToolExecutionAsync(
        string tool, string? user, object? parameters, object? result,
        long durationMs, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[AUDIT] Tool={Tool} User={User} DurationMs={DurationMs} HasResult={HasResult}",
            tool, user ?? "anonymous", durationMs, result is not null);
        return Task.CompletedTask;
    }

    public Task LogQueryExecutionAsync(
        string sql, string? user, string? database, long rowCount,
        long durationMs, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[AUDIT] Query User={User} Database={Database} RowCount={RowCount} DurationMs={DurationMs} SqlPreview={SqlPreview}",
            user ?? "anonymous", database, rowCount, durationMs,
            sql.Length > 200 ? sql[..200] + "..." : sql);
        return Task.CompletedTask;
    }

    public Task LogSecurityViolationAsync(
        string? user, string tool, string reason, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[AUDIT][SECURITY] User={User} Tool={Tool} ViolationReason={Reason}",
            user ?? "anonymous", tool, reason);
        return Task.CompletedTask;
    }
}
