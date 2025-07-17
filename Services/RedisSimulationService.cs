using System.Collections.Concurrent;
using System.Text.Json;

namespace ThreadPoolDemo.Services;

public interface IRedisSimulationService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
    Task<bool> DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task<long> IncrementAsync(string key);
    Task<long> DecrementAsync(string key);
    Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
    Task<List<string>> GetKeysAsync(string pattern);
}

public class RedisSimulationService : IRedisSimulationService
{
    private readonly ConcurrentDictionary<string, CacheItem> _cache = new();
    private readonly ILogger<RedisSimulationService> _logger;

    public RedisSimulationService(ILogger<RedisSimulationService> logger)
    {
        _logger = logger;
        
        // Start background cleanup task
        _ = Task.Run(CleanupExpiredItemsAsync);
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        // Simulate Redis network latency
        await Task.Delay(Random.Shared.Next(1, 5));
        
        if (_cache.TryGetValue(key, out var item))
        {
            if (item.ExpiresAt == null || item.ExpiresAt > DateTime.UtcNow)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(item.Value);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize cached value for key {Key}", key);
                    _cache.TryRemove(key, out _);
                }
            }
            else
            {
                // Item expired, remove it
                _cache.TryRemove(key, out _);
            }
        }
        
        return null;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        // Simulate Redis network latency
        await Task.Delay(Random.Shared.Next(2, 8));
        
        var serializedValue = JsonSerializer.Serialize(value);
        var expiresAt = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
        
        _cache.AddOrUpdate(key, 
            new CacheItem { Value = serializedValue, ExpiresAt = expiresAt },
            (k, existing) => new CacheItem { Value = serializedValue, ExpiresAt = expiresAt });
    }

    public async Task<bool> DeleteAsync(string key)
    {
        // Simulate Redis network latency
        await Task.Delay(Random.Shared.Next(1, 4));
        
        return _cache.TryRemove(key, out _);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        // Simulate Redis network latency
        await Task.Delay(Random.Shared.Next(1, 3));
        
        if (_cache.TryGetValue(key, out var item))
        {
            if (!item.ExpiresAt.HasValue || item.ExpiresAt.Value > DateTime.UtcNow)
            {
                return true;
            }
            else
            {
                // Item expired, remove it
                _cache.TryRemove(key, out _);
            }
        }
        
        return false;
    }

    public async Task<long> IncrementAsync(string key)
    {
        // Simulate Redis network latency
        await Task.Delay(Random.Shared.Next(2, 6));
        
        var newValue = _cache.AddOrUpdate(key,
            new CacheItem { Value = "1", ExpiresAt = null },
            (k, existing) =>
            {
                if (long.TryParse(existing.Value, out var currentValue))
                {
                    return new CacheItem { Value = (currentValue + 1).ToString(), ExpiresAt = existing.ExpiresAt };
                }
                return new CacheItem { Value = "1", ExpiresAt = existing.ExpiresAt };
            });
        
        return long.TryParse(newValue.Value, out var result) ? result : 1;
    }

    public async Task<long> DecrementAsync(string key)
    {
        // Simulate Redis network latency
        await Task.Delay(Random.Shared.Next(2, 6));
        
        var newValue = _cache.AddOrUpdate(key,
            new CacheItem { Value = "-1", ExpiresAt = null },
            (k, existing) =>
            {
                if (long.TryParse(existing.Value, out var currentValue))
                {
                    return new CacheItem { Value = (currentValue - 1).ToString(), ExpiresAt = existing.ExpiresAt };
                }
                return new CacheItem { Value = "-1", ExpiresAt = existing.ExpiresAt };
            });
        
        return long.TryParse(newValue.Value, out var result) ? result : -1;
    }

    public async Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        // Simulate Redis network latency
        await Task.Delay(Random.Shared.Next(2, 8));
        
        var serializedValue = JsonSerializer.Serialize(value);
        var expiresAt = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
        
        return _cache.TryAdd(key, new CacheItem { Value = serializedValue, ExpiresAt = expiresAt });
    }

    public async Task<List<string>> GetKeysAsync(string pattern)
    {
        // Simulate Redis network latency for key scanning
        await Task.Delay(Random.Shared.Next(5, 15));
        
        var keys = new List<string>();
        
        foreach (var kvp in _cache)
        {
            // Simple pattern matching (just contains for now)
            if (pattern == "*" || kvp.Key.Contains(pattern.Replace("*", "")))
            {
                // Check if not expired
                if (!kvp.Value.ExpiresAt.HasValue || kvp.Value.ExpiresAt.Value > DateTime.UtcNow)
                {
                    keys.Add(kvp.Key);
                }
            }
        }
        
        return keys;
    }

    private async Task CleanupExpiredItemsAsync()
    {
        while (true)
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredKeys = new List<string>();
                
                foreach (var kvp in _cache)
                {
                    if (kvp.Value.ExpiresAt.HasValue && kvp.Value.ExpiresAt <= now)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }
                
                foreach (var key in expiredKeys)
                {
                    _cache.TryRemove(key, out _);
                }
                
                if (expiredKeys.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {Count} expired cache items", expiredKeys.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
            
            // Run cleanup every 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
    }

    private class CacheItem
    {
        public string Value { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
    }
}
