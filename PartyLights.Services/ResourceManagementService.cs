using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;

namespace PartyLights.Services;

/// <summary>
/// Resource management service for efficient memory and CPU usage
/// </summary>
public class ResourceManagementService : IDisposable
{
    private readonly ILogger<ResourceManagementService> _logger;
    private readonly ConcurrentDictionary<string, ManagedResource> _managedResources = new();
    private readonly Timer _cleanupTimer;
    private readonly Timer _optimizationTimer;
    private readonly object _lockObject = new();

    private const int CleanupIntervalMs = 30000; // 30 seconds
    private const int OptimizationIntervalMs = 60000; // 1 minute
    private bool _isManaging;

    // Resource limits
    private const long MaxMemoryUsageBytes = 1024L * 1024L * 1024L; // 1 GB
    private const int MaxConcurrentResources = 1000;
    private const int MaxCacheSize = 100;

    // Resource pools
    private readonly ConcurrentQueue<byte[]> _byteArrayPool = new();
    private readonly ConcurrentQueue<float[]> _floatArrayPool = new();
    private readonly ConcurrentQueue<Complex[]> _complexArrayPool = new();
    private readonly ConcurrentDictionary<string, object> _objectCache = new();

    public event EventHandler<ResourceEventArgs>? ResourceAllocated;
    public event EventHandler<ResourceEventArgs>? ResourceReleased;
    public event EventHandler<ResourceEventArgs>? ResourceOptimized;

    public ResourceManagementService(ILogger<ResourceManagementService> logger)
    {
        _logger = logger;

        _cleanupTimer = new Timer(CleanupResources, null, CleanupIntervalMs, CleanupIntervalMs);
        _optimizationTimer = new Timer(OptimizeResources, null, OptimizationIntervalMs, OptimizationIntervalMs);

        _isManaging = true;

        InitializeResourcePools();

        _logger.LogInformation("Resource management service initialized");
    }

    /// <summary>
    /// Allocates a managed resource
    /// </summary>
    public T AllocateResource<T>(string name, Func<T> factory, ResourceType type = ResourceType.Custom) where T : class
    {
        try
        {
            var resource = factory();
            var managedResource = new ManagedResource
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Resource = resource,
                Type = type,
                AllocatedTime = DateTime.UtcNow,
                LastAccessedTime = DateTime.UtcNow,
                AccessCount = 0,
                SizeBytes = EstimateSize(resource)
            };

            _managedResources.TryAdd(managedResource.Id, managedResource);

            ResourceAllocated?.Invoke(this, new ResourceEventArgs(managedResource.Id, name, ResourceAction.Allocated));
            _logger.LogDebug("Allocated resource: {ResourceName}", name);

            return resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating resource: {ResourceName}", name);
            throw;
        }
    }

    /// <summary>
    /// Releases a managed resource
    /// </summary>
    public bool ReleaseResource(string resourceId)
    {
        try
        {
            if (_managedResources.TryRemove(resourceId, out var resource))
            {
                // Dispose if disposable
                if (resource.Resource is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                ResourceReleased?.Invoke(this, new ResourceEventArgs(resourceId, resource.Name, ResourceAction.Released));
                _logger.LogDebug("Released resource: {ResourceName}", resource.Name);

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing resource: {ResourceId}", resourceId);
            return false;
        }
    }

    /// <summary>
    /// Gets a cached object or creates a new one
    /// </summary>
    public T GetOrCreateCachedObject<T>(string key, Func<T> factory) where T : class
    {
        try
        {
            if (_objectCache.TryGetValue(key, out var cachedObject) && cachedObject is T typedObject)
            {
                return typedObject;
            }

            var newObject = factory();
            _objectCache[key] = newObject;

            // Limit cache size
            if (_objectCache.Count > MaxCacheSize)
            {
                var oldestKey = _objectCache.Keys.First();
                _objectCache.TryRemove(oldestKey, out _);
            }

            return newObject;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting or creating cached object: {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Gets a byte array from the pool or creates a new one
    /// </summary>
    public byte[] GetByteArray(int size)
    {
        try
        {
            if (_byteArrayPool.TryDequeue(out var pooledArray) && pooledArray.Length >= size)
            {
                return pooledArray;
            }

            return new byte[size];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting byte array of size: {Size}", size);
            return new byte[size];
        }
    }

    /// <summary>
    /// Returns a byte array to the pool
    /// </summary>
    public void ReturnByteArray(byte[] array)
    {
        try
        {
            if (array != null && _byteArrayPool.Count < 50)
            {
                Array.Clear(array, 0, array.Length);
                _byteArrayPool.Enqueue(array);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning byte array to pool");
        }
    }

    /// <summary>
    /// Gets a float array from the pool or creates a new one
    /// </summary>
    public float[] GetFloatArray(int size)
    {
        try
        {
            if (_floatArrayPool.TryDequeue(out var pooledArray) && pooledArray.Length >= size)
            {
                return pooledArray;
            }

            return new float[size];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting float array of size: {Size}", size);
            return new float[size];
        }
    }

    /// <summary>
    /// Returns a float array to the pool
    /// </summary>
    public void ReturnFloatArray(float[] array)
    {
        try
        {
            if (array != null && _floatArrayPool.Count < 50)
            {
                Array.Clear(array, 0, array.Length);
                _floatArrayPool.Enqueue(array);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning float array to pool");
        }
    }

    /// <summary>
    /// Gets a complex array from the pool or creates a new one
    /// </summary>
    public Complex[] GetComplexArray(int size)
    {
        try
        {
            if (_complexArrayPool.TryDequeue(out var pooledArray) && pooledArray.Length >= size)
            {
                return pooledArray;
            }

            return new Complex[size];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting complex array of size: {Size}", size);
            return new Complex[size];
        }
    }

    /// <summary>
    /// Returns a complex array to the pool
    /// </summary>
    public void ReturnComplexArray(Complex[] array)
    {
        try
        {
            if (array != null && _complexArrayPool.Count < 50)
            {
                Array.Clear(array, 0, array.Length);
                _complexArrayPool.Enqueue(array);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning complex array to pool");
        }
    }

    /// <summary>
    /// Gets resource usage statistics
    /// </summary>
    public ResourceUsageStatistics GetResourceUsageStatistics()
    {
        try
        {
            var totalMemory = GC.GetTotalMemory(false);
            var managedResources = _managedResources.Values.ToList();

            var statistics = new ResourceUsageStatistics
            {
                Timestamp = DateTime.UtcNow,
                TotalManagedMemoryBytes = totalMemory,
                ManagedResourceCount = managedResources.Count,
                ByteArrayPoolSize = _byteArrayPool.Count,
                FloatArrayPoolSize = _floatArrayPool.Count,
                ComplexArrayPoolSize = _complexArrayPool.Count,
                ObjectCacheSize = _objectCache.Count,
                ResourceTypes = managedResources.GroupBy(r => r.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                TotalResourceSizeBytes = managedResources.Sum(r => r.SizeBytes),
                AverageResourceSizeBytes = managedResources.Any() ? managedResources.Average(r => r.SizeBytes) : 0,
                OldestResourceAge = managedResources.Any() ?
                    (DateTime.UtcNow - managedResources.Min(r => r.AllocatedTime)).TotalMinutes : 0
            };

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource usage statistics");
            return new ResourceUsageStatistics { Timestamp = DateTime.UtcNow };
        }
    }

    /// <summary>
    /// Forces garbage collection and cleanup
    /// </summary>
    public async Task ForceCleanupAsync()
    {
        try
        {
            // Clean up unused resources
            await CleanupUnusedResources();

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            _logger.LogInformation("Forced cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during forced cleanup");
        }
    }

    #region Private Methods

    private async void CleanupResources(object? state)
    {
        if (!_isManaging)
        {
            return;
        }

        try
        {
            await CleanupUnusedResources();
            await OptimizeResourcePools();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in resource cleanup");
        }
    }

    private async void OptimizeResources(object? state)
    {
        if (!_isManaging)
        {
            return;
        }

        try
        {
            await OptimizeResourceAllocation();
            await OptimizeMemoryUsage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in resource optimization");
        }
    }

    private async Task CleanupUnusedResources()
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-5); // Resources unused for 5 minutes
            var resourcesToRemove = _managedResources.Values
                .Where(r => r.LastAccessedTime < cutoffTime)
                .ToList();

            foreach (var resource in resourcesToRemove)
            {
                await ReleaseResource(resource.Id);
            }

            if (resourcesToRemove.Any())
            {
                _logger.LogInformation("Cleaned up {Count} unused resources", resourcesToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up unused resources");
        }
    }

    private async Task OptimizeResourcePools()
    {
        try
        {
            // Limit pool sizes to prevent excessive memory usage
            while (_byteArrayPool.Count > 100)
            {
                _byteArrayPool.TryDequeue(out _);
            }

            while (_floatArrayPool.Count > 100)
            {
                _floatArrayPool.TryDequeue(out _);
            }

            while (_complexArrayPool.Count > 100)
            {
                _complexArrayPool.TryDequeue(out _);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing resource pools");
        }
    }

    private async Task OptimizeResourceAllocation()
    {
        try
        {
            // Check if we're approaching resource limits
            var totalMemory = GC.GetTotalMemory(false);
            var resourceCount = _managedResources.Count;

            if (totalMemory > MaxMemoryUsageBytes * 0.8f)
            {
                // Force cleanup if memory usage is high
                await ForceCleanupAsync();
            }

            if (resourceCount > MaxConcurrentResources * 0.8f)
            {
                // Clean up oldest resources
                var oldestResources = _managedResources.Values
                    .OrderBy(r => r.AllocatedTime)
                    .Take(resourceCount / 4)
                    .ToList();

                foreach (var resource in oldestResources)
                {
                    await ReleaseResource(resource.Id);
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing resource allocation");
        }
    }

    private async Task OptimizeMemoryUsage()
    {
        try
        {
            // Compact object cache
            if (_objectCache.Count > MaxCacheSize * 0.8f)
            {
                var keysToRemove = _objectCache.Keys.Take(_objectCache.Count / 4).ToList();
                foreach (var key in keysToRemove)
                {
                    _objectCache.TryRemove(key, out _);
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing memory usage");
        }
    }

    private void InitializeResourcePools()
    {
        // Pre-allocate some arrays for common sizes
        for (int i = 0; i < 10; i++)
        {
            _byteArrayPool.Enqueue(new byte[1024]);
            _floatArrayPool.Enqueue(new float[1024]);
            _complexArrayPool.Enqueue(new Complex[1024]);
        }
    }

    private long EstimateSize(object obj)
    {
        try
        {
            if (obj is byte[] byteArray)
            {
                return byteArray.Length;
            }
            else if (obj is float[] floatArray)
            {
                return floatArray.Length * sizeof(float);
            }
            else if (obj is Complex[] complexArray)
            {
                return complexArray.Length * sizeof(double) * 2; // Complex is 2 doubles
            }
            else if (obj is string str)
            {
                return str.Length * sizeof(char);
            }
            else
            {
                // Rough estimate for other objects
                return 100; // Default size estimate
            }
        }
        catch
        {
            return 100; // Default size estimate
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isManaging = false;

            _cleanupTimer?.Dispose();
            _optimizationTimer?.Dispose();

            // Release all managed resources
            var resourceIds = _managedResources.Keys.ToList();
            foreach (var resourceId in resourceIds)
            {
                ReleaseResource(resourceId);
            }

            // Clear pools
            while (_byteArrayPool.TryDequeue(out _)) { }
            while (_floatArrayPool.TryDequeue(out _)) { }
            while (_complexArrayPool.TryDequeue(out _)) { }

            _objectCache.Clear();

            _logger.LogInformation("Resource management service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing resource management service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Managed resource
/// </summary>
public class ManagedResource
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public object Resource { get; set; } = new();
    public ResourceType Type { get; set; }
    public DateTime AllocatedTime { get; set; }
    public DateTime LastAccessedTime { get; set; }
    public int AccessCount { get; set; }
    public long SizeBytes { get; set; }
}

/// <summary>
/// Resource usage statistics
/// </summary>
public class ResourceUsageStatistics
{
    public DateTime Timestamp { get; set; }
    public long TotalManagedMemoryBytes { get; set; }
    public int ManagedResourceCount { get; set; }
    public int ByteArrayPoolSize { get; set; }
    public int FloatArrayPoolSize { get; set; }
    public int ComplexArrayPoolSize { get; set; }
    public int ObjectCacheSize { get; set; }
    public Dictionary<ResourceType, int> ResourceTypes { get; set; } = new();
    public long TotalResourceSizeBytes { get; set; }
    public double AverageResourceSizeBytes { get; set; }
    public double OldestResourceAge { get; set; }
}

/// <summary>
/// Resource event arguments
/// </summary>
public class ResourceEventArgs : EventArgs
{
    public string ResourceId { get; }
    public string ResourceName { get; }
    public ResourceAction Action { get; }
    public DateTime Timestamp { get; }

    public ResourceEventArgs(string resourceId, string resourceName, ResourceAction action)
    {
        ResourceId = resourceId;
        ResourceName = resourceName;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Resource types
/// </summary>
public enum ResourceType
{
    Audio,
    Effect,
    Device,
    Cache,
    Buffer,
    Custom
}

/// <summary>
/// Resource actions
/// </summary>
public enum ResourceAction
{
    Allocated,
    Released,
    Optimized,
    Cleaned
}
