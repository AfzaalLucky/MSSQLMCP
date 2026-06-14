using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.ValueObjects;
using SqlMcpServer.Infrastructure.Configuration;

namespace SqlMcpServer.Infrastructure.Caching;

internal sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly CacheSettings _settings;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly ConcurrentDictionary<string, bool> _keys = new(StringComparer.OrdinalIgnoreCase);

    private long _hits;
    private long _misses;

    public MemoryCacheService(
        IMemoryCache cache,
        IOptions<CacheSettings> settings,
        ILogger<MemoryCacheService> logger)
    {
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key, Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            Interlocked.Increment(ref _hits);
            _logger.LogTrace("Cache HIT: {Key}", key);
            return cached;
        }

        Interlocked.Increment(ref _misses);
        _logger.LogTrace("Cache MISS: {Key}", key);

        var value = await factory(cancellationToken);
        await SetAsync(key, value, ttl, cancellationToken);
        return value;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(
        string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var expiry = ttl ?? TimeSpan.FromSeconds(_settings.DefaultTtlSeconds);
        _cache.Set(key, value, expiry);
        _keys.TryAdd(key, true);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var toRemove = _keys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in toRemove)
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }

        _logger.LogDebug("Removed {Count} cache entries with prefix '{Prefix}'", toRemove.Count, prefix);
        return Task.CompletedTask;
    }

    public Task<CacheStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);
        return Task.FromResult(new CacheStats(hits, misses, _keys.Count, "Memory", true));
    }
}
