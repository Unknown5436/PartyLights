using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;

namespace PartyLights.Services;

/// <summary>
/// Adaptive quality manager for dynamic performance adjustment
/// </summary>
public class AdaptiveQualityManager : IDisposable
{
    private readonly ILogger<AdaptiveQualityManager> _logger;
    private readonly PerformanceOptimizationService _performanceService;
    private readonly Dictionary<string, QualityProfile> _qualityProfiles = new();
    private readonly Timer _adaptationTimer;
    private readonly object _lockObject = new();

    private const int AdaptationIntervalMs = 2000; // 2 seconds
    private bool _isAdapting;
    private QualityLevel _currentQualityLevel = QualityLevel.High;
    private QualityProfile _currentProfile = new();

    // Adaptation thresholds
    private const float PerformanceImprovementThreshold = 0.1f;
    private const float PerformanceDegradationThreshold = 0.15f;
    private const int MinTimeBetweenChanges = 5000; // 5 seconds

    private DateTime _lastQualityChange = DateTime.MinValue;
    private readonly Queue<PerformanceSnapshot> _performanceHistory = new();
    private const int HistorySize = 10;

    public event EventHandler<QualityChangeEventArgs>? QualityChanged;
    public event EventHandler<AdaptationEventArgs>? AdaptationApplied;

    public AdaptiveQualityManager(
        ILogger<AdaptiveQualityManager> logger,
        PerformanceOptimizationService performanceService)
    {
        _logger = logger;
        _performanceService = performanceService;

        _adaptationTimer = new Timer(AdaptQuality, null, AdaptationIntervalMs, AdaptationIntervalMs);
        _isAdapting = true;

        InitializeQualityProfiles();

        _logger.LogInformation("Adaptive quality manager initialized");
    }

    /// <summary>
    /// Gets current quality level
    /// </summary>
    public QualityLevel GetCurrentQualityLevel()
    {
        lock (_lockObject)
        {
            return _currentQualityLevel;
        }
    }

    /// <summary>
    /// Gets current quality profile
    /// </summary>
    public QualityProfile GetCurrentProfile()
    {
        lock (_lockObject)
        {
            return _currentProfile;
        }
    }

    /// <summary>
    /// Sets quality level manually
    /// </summary>
    public async Task<bool> SetQualityLevelAsync(QualityLevel level)
    {
        try
        {
            lock (_lockObject)
            {
                if (_currentQualityLevel == level)
                {
                    return true; // Already at desired level
                }

                var oldLevel = _currentQualityLevel;
                _currentQualityLevel = level;
                _currentProfile = _qualityProfiles[level.ToString()];
                _lastQualityChange = DateTime.UtcNow;
            }

            await ApplyQualityProfile(_currentProfile);

            QualityChanged?.Invoke(this, new QualityChangeEventArgs(_currentQualityLevel, QualityChangeReason.Manual));
            _logger.LogInformation("Quality level changed to: {QualityLevel}", level);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting quality level: {QualityLevel}", level);
            return false;
        }
    }

    /// <summary>
    /// Enables adaptive quality management
    /// </summary>
    public async Task<bool> EnableAdaptiveQualityAsync()
    {
        try
        {
            _isAdapting = true;
            _logger.LogInformation("Adaptive quality management enabled");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling adaptive quality");
            return false;
        }
    }

    /// <summary>
    /// Disables adaptive quality management
    /// </summary>
    public async Task DisableAdaptiveQualityAsync()
    {
        try
        {
            _isAdapting = false;
            _logger.LogInformation("Adaptive quality management disabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling adaptive quality");
        }
    }

    /// <summary>
    /// Gets quality recommendations based on current performance
    /// </summary>
    public IEnumerable<QualityRecommendation> GetQualityRecommendations()
    {
        var recommendations = new List<QualityRecommendation>();
        var report = _performanceService.GetPerformanceReport();

        // CPU-based recommendations
        if (report.CpuUsagePercent > 85f)
        {
            recommendations.Add(new QualityRecommendation
            {
                TargetLevel = QualityLevel.Low,
                Reason = "High CPU usage",
                Priority = Priority.High,
                ExpectedImprovement = "Reduce CPU usage by 25-35%"
            });
        }
        else if (report.CpuUsagePercent > 70f)
        {
            recommendations.Add(new QualityRecommendation
            {
                TargetLevel = QualityLevel.Medium,
                Reason = "Moderate CPU usage",
                Priority = Priority.Medium,
                ExpectedImprovement = "Reduce CPU usage by 15-20%"
            });
        }
        else if (report.CpuUsagePercent < 30f && report.FrameRate > 55f)
        {
            recommendations.Add(new QualityRecommendation
            {
                TargetLevel = QualityLevel.High,
                Reason = "Low CPU usage with good performance",
                Priority = Priority.Low,
                ExpectedImprovement = "Improve visual quality"
            });
        }

        // Memory-based recommendations
        if (report.MemoryUsageMB > 1536f)
        {
            recommendations.Add(new QualityRecommendation
            {
                TargetLevel = QualityLevel.Low,
                Reason = "High memory usage",
                Priority = Priority.High,
                ExpectedImprovement = "Reduce memory usage by 20-30%"
            });
        }

        // Latency-based recommendations
        if (report.AverageLatencyMs > 75f)
        {
            recommendations.Add(new QualityRecommendation
            {
                TargetLevel = QualityLevel.Medium,
                Reason = "High latency",
                Priority = Priority.Medium,
                ExpectedImprovement = "Reduce latency by 10-15ms"
            });
        }

        // Frame rate-based recommendations
        if (report.FrameRate < 45f)
        {
            recommendations.Add(new QualityRecommendation
            {
                TargetLevel = QualityLevel.Low,
                Reason = "Low frame rate",
                Priority = Priority.High,
                ExpectedImprovement = "Improve frame rate by 10-15 FPS"
            });
        }

        return recommendations.OrderByDescending(r => r.Priority);
    }

    #region Private Methods

    private async void AdaptQuality(object? state)
    {
        if (!_isAdapting)
        {
            return;
        }

        try
        {
            // Check if enough time has passed since last change
            if ((DateTime.UtcNow - _lastQualityChange).TotalMilliseconds < MinTimeBetweenChanges)
            {
                return;
            }

            // Get current performance report
            var report = _performanceService.GetPerformanceReport();

            // Record performance snapshot
            RecordPerformanceSnapshot(report);

            // Analyze performance trend
            var trend = AnalyzePerformanceTrend();

            // Determine if quality adjustment is needed
            var recommendation = DetermineQualityAdjustment(trend, report);

            if (recommendation != null)
            {
                await ApplyQualityRecommendation(recommendation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in adaptive quality management");
        }
    }

    private void RecordPerformanceSnapshot(PerformanceReport report)
    {
        var snapshot = new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            CpuUsage = report.CpuUsagePercent,
            MemoryUsage = report.MemoryUsageMB,
            Latency = report.AverageLatencyMs,
            FrameRate = report.FrameRate
        };

        _performanceHistory.Enqueue(snapshot);

        // Keep only recent snapshots
        while (_performanceHistory.Count > HistorySize)
        {
            _performanceHistory.Dequeue();
        }
    }

    private PerformanceTrend AnalyzePerformanceTrend()
    {
        if (_performanceHistory.Count < 3)
        {
            return PerformanceTrend.Stable;
        }

        var snapshots = _performanceHistory.ToList();
        var recent = snapshots.TakeLast(3).ToList();
        var older = snapshots.Take(snapshots.Count - 3).ToList();

        if (!recent.Any() || !older.Any())
        {
            return PerformanceTrend.Stable;
        }

        // Calculate average performance metrics
        var recentCpu = recent.Average(s => s.CpuUsage);
        var recentMemory = recent.Average(s => s.MemoryUsage);
        var recentLatency = recent.Average(s => s.Latency);
        var recentFrameRate = recent.Average(s => s.FrameRate);

        var olderCpu = older.Average(s => s.CpuUsage);
        var olderMemory = older.Average(s => s.MemoryUsage);
        var olderLatency = older.Average(s => s.Latency);
        var olderFrameRate = older.Average(s => s.FrameRate);

        // Determine trend based on performance changes
        var cpuChange = (recentCpu - olderCpu) / olderCpu;
        var memoryChange = (recentMemory - olderMemory) / olderMemory;
        var latencyChange = (recentLatency - olderLatency) / olderLatency;
        var frameRateChange = (recentFrameRate - olderFrameRate) / olderFrameRate;

        // Overall performance change (negative is improvement)
        var overallChange = (cpuChange + memoryChange + latencyChange - frameRateChange) / 4;

        if (overallChange > PerformanceDegradationThreshold)
        {
            return PerformanceTrend.Degrading;
        }
        else if (overallChange < -PerformanceImprovementThreshold)
        {
            return PerformanceTrend.Improving;
        }
        else
        {
            return PerformanceTrend.Stable;
        }
    }

    private QualityRecommendation? DetermineQualityAdjustment(PerformanceTrend trend, PerformanceReport report)
    {
        lock (_lockObject)
        {
            switch (trend)
            {
                case PerformanceTrend.Degrading:
                    // Performance is getting worse, consider reducing quality
                    if (_currentQualityLevel > QualityLevel.Low)
                    {
                        var newLevel = _currentQualityLevel - 1;
                        return new QualityRecommendation
                        {
                            TargetLevel = newLevel,
                            Reason = "Performance degrading",
                            Priority = Priority.High,
                            ExpectedImprovement = "Improve performance"
                        };
                    }
                    break;

                case PerformanceTrend.Improving:
                    // Performance is improving, consider increasing quality
                    if (_currentQualityLevel < QualityLevel.Maximum &&
                        report.CpuUsagePercent < 50f &&
                        report.MemoryUsageMB < 1024f &&
                        report.FrameRate > 55f)
                    {
                        var newLevel = _currentQualityLevel + 1;
                        return new QualityRecommendation
                        {
                            TargetLevel = newLevel,
                            Reason = "Performance improving",
                            Priority = Priority.Low,
                            ExpectedImprovement = "Improve visual quality"
                        };
                    }
                    break;

                case PerformanceTrend.Stable:
                    // Check if current performance is acceptable
                    if (report.CpuUsagePercent > 80f || report.MemoryUsageMB > 1536f || report.FrameRate < 45f)
                    {
                        if (_currentQualityLevel > QualityLevel.Low)
                        {
                            var newLevel = _currentQualityLevel - 1;
                            return new QualityRecommendation
                            {
                                TargetLevel = newLevel,
                                Reason = "Poor performance",
                                Priority = Priority.Medium,
                                ExpectedImprovement = "Improve performance"
                            };
                        }
                    }
                    break;
            }
        }

        return null;
    }

    private async Task ApplyQualityRecommendation(QualityRecommendation recommendation)
    {
        try
        {
            var success = await SetQualityLevelAsync(recommendation.TargetLevel);

            if (success)
            {
                AdaptationApplied?.Invoke(this, new AdaptationEventArgs(recommendation, true));
                _logger.LogInformation("Applied quality recommendation: {Reason}", recommendation.Reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying quality recommendation");
            AdaptationApplied?.Invoke(this, new AdaptationEventArgs(recommendation, false));
        }
    }

    private void InitializeQualityProfiles()
    {
        // Low quality profile
        _qualityProfiles["Low"] = new QualityProfile
        {
            Level = QualityLevel.Low,
            AudioProcessingFrequency = 0.5f,
            EffectUpdateFrequency = 0.5f,
            BufferSizeMultiplier = 0.7f,
            EffectComplexity = 0.6f,
            ColorDepth = 8,
            MaxConcurrentEffects = 2,
            EnableAdvancedFeatures = false
        };

        // Medium quality profile
        _qualityProfiles["Medium"] = new QualityProfile
        {
            Level = QualityLevel.Medium,
            AudioProcessingFrequency = 0.75f,
            EffectUpdateFrequency = 0.75f,
            BufferSizeMultiplier = 0.85f,
            EffectComplexity = 0.8f,
            ColorDepth = 16,
            MaxConcurrentEffects = 4,
            EnableAdvancedFeatures = true
        };

        // High quality profile
        _qualityProfiles["High"] = new QualityProfile
        {
            Level = QualityLevel.High,
            AudioProcessingFrequency = 1.0f,
            EffectUpdateFrequency = 1.0f,
            BufferSizeMultiplier = 1.0f,
            EffectComplexity = 1.0f,
            ColorDepth = 24,
            MaxConcurrentEffects = 6,
            EnableAdvancedFeatures = true
        };

        // Maximum quality profile
        _qualityProfiles["Maximum"] = new QualityProfile
        {
            Level = QualityLevel.Maximum,
            AudioProcessingFrequency = 1.2f,
            EffectUpdateFrequency = 1.2f,
            BufferSizeMultiplier = 1.2f,
            EffectComplexity = 1.2f,
            ColorDepth = 32,
            MaxConcurrentEffects = 8,
            EnableAdvancedFeatures = true
        };

        _currentProfile = _qualityProfiles["High"];
    }

    private async Task ApplyQualityProfile(QualityProfile profile)
    {
        try
        {
            // Apply quality profile settings to various services
            // This would typically involve updating configuration in other services

            // For now, it's a placeholder implementation
            await Task.Delay(100);

            _logger.LogInformation("Applied quality profile: {Level}", profile.Level);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying quality profile: {Level}", profile.Level);
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isAdapting = false;
            _adaptationTimer?.Dispose();

            _logger.LogInformation("Adaptive quality manager disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing adaptive quality manager");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Quality profile defining performance characteristics
/// </summary>
public class QualityProfile
{
    public QualityLevel Level { get; set; }
    public float AudioProcessingFrequency { get; set; } = 1.0f;
    public float EffectUpdateFrequency { get; set; } = 1.0f;
    public float BufferSizeMultiplier { get; set; } = 1.0f;
    public float EffectComplexity { get; set; } = 1.0f;
    public int ColorDepth { get; set; } = 24;
    public int MaxConcurrentEffects { get; set; } = 6;
    public bool EnableAdvancedFeatures { get; set; } = true;
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// Quality recommendation
/// </summary>
public class QualityRecommendation
{
    public QualityLevel TargetLevel { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Priority Priority { get; set; }
    public string ExpectedImprovement { get; set; } = string.Empty;
}

/// <summary>
/// Performance snapshot
/// </summary>
public class PerformanceSnapshot
{
    public DateTime Timestamp { get; set; }
    public float CpuUsage { get; set; }
    public float MemoryUsage { get; set; }
    public float Latency { get; set; }
    public float FrameRate { get; set; }
}

/// <summary>
/// Quality change event arguments
/// </summary>
public class QualityChangeEventArgs : EventArgs
{
    public QualityLevel NewLevel { get; }
    public QualityChangeReason Reason { get; }
    public DateTime Timestamp { get; }

    public QualityChangeEventArgs(QualityLevel newLevel, QualityChangeReason reason)
    {
        NewLevel = newLevel;
        Reason = reason;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Adaptation event arguments
/// </summary>
public class AdaptationEventArgs : EventArgs
{
    public QualityRecommendation Recommendation { get; }
    public bool Success { get; }
    public DateTime Timestamp { get; }

    public AdaptationEventArgs(QualityRecommendation recommendation, bool success)
    {
        Recommendation = recommendation;
        Success = success;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Quality levels
/// </summary>
public enum QualityLevel
{
    Low,
    Medium,
    High,
    Maximum
}

/// <summary>
/// Performance trends
/// </summary>
public enum PerformanceTrend
{
    Improving,
    Stable,
    Degrading
}

/// <summary>
/// Quality change reasons
/// </summary>
public enum QualityChangeReason
{
    Manual,
    Automatic,
    PerformanceBased,
    UserPreference
}
