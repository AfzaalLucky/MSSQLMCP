using Microsoft.Extensions.Logging;
using SqlMcpServer.Application.Models.Responses;
using SqlMcpServer.Domain.Contracts.Infrastructure;

namespace SqlMcpServer.Application.Services;

public sealed class HealthService
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ICacheService _cacheService;
    private readonly ILogger<HealthService> _logger;

    public HealthService(
        IConnectionFactory connectionFactory,
        ICacheService cacheService,
        ILogger<HealthService> logger)
    {
        _connectionFactory = connectionFactory;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<HealthResponse> HealthCheckAsync(CancellationToken ct = default)
    {
        var (dbOk, dbError) = await TestDatabaseAsync(ct);
        var cacheStats = await GetCacheStatsAsync(ct);

        if (dbOk)
            return HealthResponse.Healthy(cacheStats?.IsHealthy ?? false, cacheStats);

        _logger.LogWarning("Health check: database connectivity failed — {Error}", dbError);
        return HealthResponse.Degraded(dbError!, cacheStats?.IsHealthy ?? false, cacheStats);
    }

    public async Task<bool> DatabaseConnectivityTestAsync(CancellationToken ct = default)
    {
        var (ok, _) = await TestDatabaseAsync(ct);
        return ok;
    }

    public async Task<Domain.ValueObjects.CacheStats?> CacheStatusAsync(CancellationToken ct = default) =>
        await GetCacheStatsAsync(ct);

    private async Task<(bool Ok, string? Error)> TestDatabaseAsync(CancellationToken ct)
    {
        try
        {
            var ok = await _connectionFactory.TestConnectionAsync(ct);
            return (ok, ok ? null : "Connection test returned false");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<Domain.ValueObjects.CacheStats?> GetCacheStatsAsync(CancellationToken ct)
    {
        try { return await _cacheService.GetStatsAsync(ct); }
        catch { return null; }
    }
}
