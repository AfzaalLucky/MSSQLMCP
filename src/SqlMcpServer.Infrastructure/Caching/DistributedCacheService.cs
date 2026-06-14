using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlMcpServer.Domain.Contracts.Infrastructure;
using SqlMcpServer.Domain.ValueObjects;
using SqlMcpServer.Infrastructure.Configuration;

namespace SqlMcpServer.Infrastructure.Caching;

internal sealed class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly CacheSettings _settings;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly ConcurrentDictionary<string, bool> _keyTracker = new(StringComparer.OrdinalIgnoreCase);

    private long _hits;
    private long _misses;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DistributedCacheService(
        IDistributedCache cache,
        IOptions<CacheSettings> settings,
        ILogger<DistributedCacheService> logger)
    {
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key, Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync<T>(key, cancellationToken);
        if (existing is not null)
        {
            Interlocked.Increment(ref _hits);
            return existing;
        }

        Interlocked.Increment(ref _misses);
        var value = await factory(cancellationToken);
        await SetAsync(key, value, ttl, cancellationToken);
        return value;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await _cache.GetAsync(key, cancellationToken);
            if (bytes is null) return default;
            return JsonSerializer.Deserialize<T>(bytes, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache GET failed for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var expiry = ttl ?? TimeSpan.FromSeconds(_settings.DefaultTtlSeconds);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry
            };
            await _cache.SetAsync(key, bytes, options, cancellationToken);
            _keyTracker.TryAdd(key, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache SET failed for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
            _keyTracker.TryRemove(key, out _);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache REMOVE failed for key {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var toRemove = _keyTracker.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in toRemove)
        {
            await RemoveAsync(key, cancellationToken);
        }
    }

    public Task<CacheStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);
        return Task.FromResult(new CacheStats(hits, misses, _keyTracker.Count, "Redis", true));
    }
}
