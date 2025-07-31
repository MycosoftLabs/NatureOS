using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Advanced caching service with multi-level caching strategy
/// Combines in-memory, distributed Redis, and intelligent cache invalidation
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default);
    Task WarmCacheAsync(CancellationToken cancellationToken = default);
    CacheStatistics GetStatistics();
}

public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly IDatabase _redisDatabase;
    private readonly ILogger<CacheService> _logger;
    private readonly CacheStatistics _statistics;
    
    // Cache configuration
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _memoryExpiry = TimeSpan.FromMinutes(5);
    private readonly JsonSerializerOptions _jsonOptions;

    public CacheService(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        IConnectionMultiplexer redis,
        ILogger<CacheService> logger)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _redisDatabase = redis.GetDatabase();
        _logger = logger;
        _statistics = new CacheStatistics();
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            _statistics.TotalRequests++;
            
            // Level 1: Check memory cache first (fastest)
            if (_memoryCache.TryGetValue(key, out T? memoryValue))
            {
                _statistics.MemoryHits++;
                _logger.LogDebug("Cache hit (memory): {Key}", key);
                return memoryValue;
            }

            // Level 2: Check Redis distributed cache
            var distributedValue = await _distributedCache.GetStringAsync(key, cancellationToken);
            if (!string.IsNullOrEmpty(distributedValue))
            {
                _statistics.DistributedHits++;
                _logger.LogDebug("Cache hit (distributed): {Key}", key);
                
                var deserializedValue = JsonSerializer.Deserialize<T>(distributedValue, _jsonOptions);
                
                // Store in memory cache for faster subsequent access
                _memoryCache.Set(key, deserializedValue, _memoryExpiry);
                
                return deserializedValue;
            }

            _statistics.Misses++;
            _logger.LogDebug("Cache miss: {Key}", key);
            return default(T);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
            _statistics.Errors++;
            return default(T);
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var actualExpiry = expiry ?? _defaultExpiry;
            var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);

            // Set in both caches
            _memoryCache.Set(key, value, _memoryExpiry);
            
            var distributedCacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = actualExpiry
            };
            
            await _distributedCache.SetStringAsync(key, serializedValue, distributedCacheOptions, cancellationToken);
            
            _statistics.Sets++;
            _logger.LogDebug("Cache set: {Key} (expiry: {Expiry})", key, actualExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
            _statistics.Errors++;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            _memoryCache.Remove(key);
            await _distributedCache.RemoveAsync(key, cancellationToken);
            
            _statistics.Removals++;
            _logger.LogDebug("Cache removed: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
            _statistics.Errors++;
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            // Redis pattern-based deletion
            var server = _redisDatabase.Multiplexer.GetServer(_redisDatabase.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern);
            
            foreach (var key in keys)
            {
                await _redisDatabase.KeyDeleteAsync(key);
                _memoryCache.Remove(key.ToString());
            }
            
            _logger.LogDebug("Cache pattern removed: {Pattern}", pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache pattern: {Pattern}", pattern);
            _statistics.Errors++;
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var cachedValue = await GetAsync<T>(key, cancellationToken);
        if (cachedValue != null)
        {
            return cachedValue;
        }

        // Generate value and cache it
        var value = await factory();
        await SetAsync(key, value, expiry, cancellationToken);
        
        return value;
    }

    public async Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        // Implement tag-based cache invalidation
        await RemoveByPatternAsync($"*:{tag}:*", cancellationToken);
        _logger.LogDebug("Cache tag invalidated: {Tag}", tag);
    }

    public async Task WarmCacheAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting cache warm-up...");
        
        try
        {
            // Warm up commonly accessed data
            var warmUpTasks = new List<Task>
            {
                WarmUpSystemStatus(cancellationToken),
                WarmUpDeviceStatistics(cancellationToken),
                WarmUpEventStatistics(cancellationToken),
                WarmUpCommonQueries(cancellationToken)
            };
            
            await Task.WhenAll(warmUpTasks);
            _logger.LogInformation("Cache warm-up completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache warm-up");
        }
    }

    public CacheStatistics GetStatistics()
    {
        var hitRate = _statistics.TotalRequests > 0 
            ? (double)(_statistics.MemoryHits + _statistics.DistributedHits) / _statistics.TotalRequests * 100 
            : 0;
            
        return new CacheStatistics
        {
            TotalRequests = _statistics.TotalRequests,
            MemoryHits = _statistics.MemoryHits,
            DistributedHits = _statistics.DistributedHits,
            Misses = _statistics.Misses,
            Sets = _statistics.Sets,
            Removals = _statistics.Removals,
            Errors = _statistics.Errors,
            HitRate = Math.Round(hitRate, 2)
        };
    }

    private async Task WarmUpSystemStatus(CancellationToken cancellationToken)
    {
        // Pre-load system status data
        await SetAsync("system:status", new { Status = "Warming up..." }, TimeSpan.FromMinutes(5), cancellationToken);
    }

    private async Task WarmUpDeviceStatistics(CancellationToken cancellationToken)
    {
        // Pre-load device statistics
        await SetAsync("devices:statistics", new { TotalDevices = 0, OnlineDevices = 0 }, TimeSpan.FromMinutes(10), cancellationToken);
    }

    private async Task WarmUpEventStatistics(CancellationToken cancellationToken)
    {
        // Pre-load event statistics
        await SetAsync("events:statistics", new { TotalEvents = 0, TodayEvents = 0 }, TimeSpan.FromMinutes(10), cancellationToken);
    }

    private async Task WarmUpCommonQueries(CancellationToken cancellationToken)
    {
        // Pre-load common MYCA responses
        var commonQueries = new[]
        {
            "What is the current system status?",
            "How many devices are online?",
            "Show me recent events"
        };

        foreach (var query in commonQueries)
        {
            var cacheKey = $"myca:query:{query.GetHashCode()}";
            await SetAsync(cacheKey, new { Answer = "Loading...", Cached = true }, TimeSpan.FromHours(1), cancellationToken);
        }
    }
}

public class CacheStatistics
{
    public long TotalRequests { get; set; }
    public long MemoryHits { get; set; }
    public long DistributedHits { get; set; }
    public long Misses { get; set; }
    public long Sets { get; set; }
    public long Removals { get; set; }
    public long Errors { get; set; }
    public double HitRate { get; set; }
}

// Cache key generators for consistent naming
public static class CacheKeys
{
    public static string SystemStatus() => "system:status";
    public static string DeviceStatistics() => "devices:statistics";
    public static string DeviceList() => "devices:list";
    public static string Device(string deviceId) => $"device:{deviceId}";
    public static string EventStatistics() => "events:statistics";
    public static string Events(string query) => $"events:query:{query.GetHashCode()}";
    public static string MycaQuery(string question) => $"myca:query:{question.GetHashCode()}";
    public static string ExternalData(string source) => $"external:{source}:data";
    public static string WebsiteDashboard() => "website:dashboard";
}

// Cache warming background service
public class CacheWarmupService : BackgroundService
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<CacheWarmupService> _logger;

    public CacheWarmupService(ICacheService cacheService, ILogger<CacheWarmupService> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial warm-up
        await _cacheService.WarmCacheAsync(stoppingToken);

        // Periodic cache refresh every hour
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                await _cacheService.WarmCacheAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic cache warm-up");
            }
        }
    }
} 