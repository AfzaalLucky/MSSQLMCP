namespace SqlMcpServer.Domain.ValueObjects;

public sealed record CacheStats(
    long Hits,
    long Misses,
    long EntryCount,
    string Provider,
    bool IsHealthy)
{
    public double HitRatio => Hits + Misses == 0 ? 0 : (double)Hits / (Hits + Misses);
}
