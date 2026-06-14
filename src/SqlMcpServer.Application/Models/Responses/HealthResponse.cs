using SqlMcpServer.Domain.ValueObjects;

namespace SqlMcpServer.Application.Models.Responses;

public sealed record HealthResponse(
    bool IsHealthy,
    bool DatabaseConnected,
    bool CacheHealthy,
    CacheStats? CacheStats,
    string? DatabaseError,
    DateTimeOffset CheckedAt)
{
    public static HealthResponse Healthy(bool cacheHealthy, CacheStats? cacheStats) =>
        new(true, true, cacheHealthy, cacheStats, null, DateTimeOffset.UtcNow);

    public static HealthResponse Degraded(string dbError, bool cacheHealthy, CacheStats? cacheStats) =>
        new(false, false, cacheHealthy, cacheStats, dbError, DateTimeOffset.UtcNow);
}
