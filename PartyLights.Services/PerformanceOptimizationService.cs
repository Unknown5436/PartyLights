using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PartyLights.Services;

/// <summary>
/// Comprehensive performance monitoring and optimization service
/// </summary>
public class PerformanceOptimizationService : IDisposable
{
    private readonly ILogger<PerformanceOptimizationService> _logger;
    private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics = new();
    private readonly Timer _monitoringTimer;
    private readonly Timer _optimizationTimer;
    private readonly object _lockObject = new();

    private const int MonitoringIntervalMs = 1000; // 1 second
    private const int OptimizationIntervalMs = 5000; // 5 seconds
    private bool _isMonitoring;
    private bool _isOptimizing;

    // Performance thresholds
    private const float CpuThresholdPercent = 80f;
    private const float MemoryThresholdMB = 1024f;
    private const float LatencyThresholdMs = 50f;
    private const float FrameDropThresholdPercent = 5f;

    // Performance counters
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;
    private readonly Process _currentProcess;

    // Optimization state
    private PerformanceLevel _currentPerformanceLevel = PerformanceLevel.High;
    private readonly Dictionary<string, OptimizationRule> _optimizationRules = new();
    private readonly Queue<PerformanceAlert> _performanceAlerts = new();

    public event EventHandler<PerformanceAlertEventArgs>? PerformanceAlert;
    public event EventHandler<OptimizationEventArgs>? OptimizationApplied;
    public event EventHandler<PerformanceReportEventArgs>? PerformanceReport;

    public PerformanceOptimizationService(ILogger<PerformanceOptimizationService> logger)
    {
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();

        // Initialize performance counters
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");

        // Initialize monitoring timers
        _monitoringTimer = new Timer(MonitorPerformance, null, MonitoringIntervalMs, MonitoringIntervalMs);
        _optimizationTimer = new Timer(OptimizePerformance, null, OptimizationIntervalMs, OptimizationIntervalMs);

        _isMonitoring = true;
        _isOptimizing = true;

        InitializeOptimizationRules();

        _logger.LogInformation("Performance optimization service initialized");
    }

    /// <summary>
    /// Starts performance monitoring
    /// </summary>
    public async Task<bool> StartMonitoringAsync()
    {
        try
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("Performance monitoring is already running");
                return true;
            }

            _isMonitoring = true;
            _logger.LogInformation("Started performance monitoring");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting performance monitoring");
            return false;
        }
    }

    /// <summary>
    /// Stops performance monitoring
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        try
        {
            _isMonitoring = false;
            _logger.LogInformation("Stopped performance monitoring");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping performance monitoring");
        }
    }

    /// <summary>
    /// Records a performance metric
    /// </summary>
    public void RecordMetric(string name, float value, MetricType type = MetricType.Custom)
    {
        try
        {
            var metric = new PerformanceMetric
            {
                Name = name,
                Value = value,
                Type = type,
                Timestamp = DateTime.UtcNow
            };

            _metrics.AddOrUpdate(name, metric, (key, existing) => metric);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording metric: {MetricName}", name);
        }
    }

    /// <summary>
    /// Gets current performance report
    /// </summary>
    public PerformanceReport GetPerformanceReport()
    {
        try
        {
            var cpuUsage = GetCpuUsage();
            var memoryUsage = GetMemoryUsage();
            var latency = GetAverageLatency();
            var frameRate = GetFrameRate();

            var report = new PerformanceReport
            {
                Timestamp = DateTime.UtcNow,
                CpuUsagePercent = cpuUsage,
                MemoryUsageMB = memoryUsage,
                AverageLatencyMs = latency,
                FrameRate = frameRate,
                PerformanceLevel = _currentPerformanceLevel,
                Metrics = _metrics.Values.ToList(),
                Alerts = _performanceAlerts.ToList()
            };

            PerformanceReport?.Invoke(this, new PerformanceReportEventArgs(report));
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance report");
            return new PerformanceReport { Timestamp = DateTime.UtcNow };
        }
    }

    /// <summary>
    /// Applies performance optimization
    /// </summary>
    public async Task<bool> ApplyOptimizationAsync(OptimizationType type, OptimizationParameters parameters)
    {
        try
        {
            var optimization = new Optimization
            {
                Type = type,
                Parameters = parameters,
                AppliedTime = DateTime.UtcNow,
                IsActive = true
            };

            var success = await ExecuteOptimization(optimization);

            if (success)
            {
                OptimizationApplied?.Invoke(this, new OptimizationEventArgs(type, true));
                _logger.LogInformation("Applied optimization: {OptimizationType}", type);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying optimization: {OptimizationType}", type);
            OptimizationApplied?.Invoke(this, new OptimizationEventArgs(type, false));
            return false;
        }
    }

    /// <summary>
    /// Gets recommended optimizations based on current performance
    /// </summary>
    public IEnumerable<OptimizationRecommendation> GetOptimizationRecommendations()
    {
        var recommendations = new List<OptimizationRecommendation>();
        var report = GetPerformanceReport();

        // CPU optimization recommendations
        if (report.CpuUsagePercent > CpuThresholdPercent)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Type = OptimizationType.ReduceProcessingFrequency,
                Priority = Priority.High,
                Description = "High CPU usage detected. Consider reducing processing frequency.",
                ExpectedImprovement = "Reduce CPU usage by 20-30%"
            });
        }

        // Memory optimization recommendations
        if (report.MemoryUsageMB > MemoryThresholdMB)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Type = OptimizationType.ReduceBufferSizes,
                Priority = Priority.High,
                Description = "High memory usage detected. Consider reducing buffer sizes.",
                ExpectedImprovement = "Reduce memory usage by 15-25%"
            });
        }

        // Latency optimization recommendations
        if (report.AverageLatencyMs > LatencyThresholdMs)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Type = OptimizationType.OptimizeAudioProcessing,
                Priority = Priority.Medium,
                Description = "High latency detected. Consider optimizing audio processing.",
                ExpectedImprovement = "Reduce latency by 10-20ms"
            });
        }

        // Frame rate optimization recommendations
        if (report.FrameRate < 50f)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Type = OptimizationType.ReduceEffectComplexity,
                Priority = Priority.Medium,
                Description = "Low frame rate detected. Consider reducing effect complexity.",
                ExpectedImprovement = "Improve frame rate by 10-15 FPS"
            });
        }

        return recommendations;
    }

    #region Private Methods

    private async void MonitorPerformance(object? state)
    {
        if (!_isMonitoring)
        {
            return;
        }

        try
        {
            // Collect system metrics
            var cpuUsage = GetCpuUsage();
            var memoryUsage = GetMemoryUsage();
            var latency = GetAverageLatency();
            var frameRate = GetFrameRate();

            // Record metrics
            RecordMetric("CPU_Usage", cpuUsage, MetricType.System);
            RecordMetric("Memory_Usage", memoryUsage, MetricType.System);
            RecordMetric("Latency", latency, MetricType.Performance);
            RecordMetric("Frame_Rate", frameRate, MetricType.Performance);

            // Check for performance alerts
            CheckPerformanceAlerts(cpuUsage, memoryUsage, latency, frameRate);

            // Update performance level
            UpdatePerformanceLevel(cpuUsage, memoryUsage, latency, frameRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in performance monitoring");
        }
    }

    private async void OptimizePerformance(object? state)
    {
        if (!_isOptimizing)
        {
            return;
        }

        try
        {
            var recommendations = GetOptimizationRecommendations();

            foreach (var recommendation in recommendations.Where(r => r.Priority == Priority.High))
            {
                await ApplyAutomaticOptimization(recommendation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in performance optimization");
        }
    }

    private float GetCpuUsage()
    {
        try
        {
            return _cpuCounter.NextValue();
        }
        catch
        {
            return 0f;
        }
    }

    private float GetMemoryUsage()
    {
        try
        {
            var totalMemory = GC.GetTotalMemory(false) / 1024f / 1024f; // Convert to MB
            return totalMemory;
        }
        catch
        {
            return 0f;
        }
    }

    private float GetAverageLatency()
    {
        try
        {
            var latencyMetrics = _metrics.Values
                .Where(m => m.Name.Contains("Latency") || m.Name.Contains("Processing_Time"))
                .Select(m => m.Value)
                .ToList();

            return latencyMetrics.Any() ? latencyMetrics.Average() : 0f;
        }
        catch
        {
            return 0f;
        }
    }

    private float GetFrameRate()
    {
        try
        {
            var frameRateMetric = _metrics.GetValueOrDefault("Frame_Rate");
            return frameRateMetric?.Value ?? 60f;
        }
        catch
        {
            return 60f;
        }
    }

    private void CheckPerformanceAlerts(float cpuUsage, float memoryUsage, float latency, float frameRate)
    {
        var alerts = new List<PerformanceAlert>();

        if (cpuUsage > CpuThresholdPercent)
        {
            alerts.Add(new PerformanceAlert
            {
                Type = AlertType.CpuHigh,
                Severity = Severity.Warning,
                Message = $"High CPU usage: {cpuUsage:F1}%",
                Timestamp = DateTime.UtcNow
            });
        }

        if (memoryUsage > MemoryThresholdMB)
        {
            alerts.Add(new PerformanceAlert
            {
                Type = AlertType.MemoryHigh,
                Severity = Severity.Warning,
                Message = $"High memory usage: {memoryUsage:F1} MB",
                Timestamp = DateTime.UtcNow
            });
        }

        if (latency > LatencyThresholdMs)
        {
            alerts.Add(new PerformanceAlert
            {
                Type = AlertType.LatencyHigh,
                Severity = Severity.Warning,
                Message = $"High latency: {latency:F1} ms",
                Timestamp = DateTime.UtcNow
            });
        }

        if (frameRate < 50f)
        {
            alerts.Add(new PerformanceAlert
            {
                Type = AlertType.FrameRateLow,
                Severity = Severity.Warning,
                Message = $"Low frame rate: {frameRate:F1} FPS",
                Timestamp = DateTime.UtcNow
            });
        }

        // Add alerts to queue and notify
        foreach (var alert in alerts)
        {
            _performanceAlerts.Enqueue(alert);
            PerformanceAlert?.Invoke(this, new PerformanceAlertEventArgs(alert));
        }

        // Keep only recent alerts
        while (_performanceAlerts.Count > 100)
        {
            _performanceAlerts.Dequeue();
        }
    }

    private void UpdatePerformanceLevel(float cpuUsage, float memoryUsage, float latency, float frameRate)
    {
        var newLevel = DeterminePerformanceLevel(cpuUsage, memoryUsage, latency, frameRate);

        if (newLevel != _currentPerformanceLevel)
        {
            var oldLevel = _currentPerformanceLevel;
            _currentPerformanceLevel = newLevel;

            _logger.LogInformation("Performance level changed from {OldLevel} to {NewLevel}", oldLevel, newLevel);
        }
    }

    private PerformanceLevel DeterminePerformanceLevel(float cpuUsage, float memoryUsage, float latency, float frameRate)
    {
        // Determine performance level based on metrics
        if (cpuUsage > 90f || memoryUsage > 2048f || latency > 100f || frameRate < 30f)
        {
            return PerformanceLevel.Low;
        }
        else if (cpuUsage > 70f || memoryUsage > 1536f || latency > 75f || frameRate < 45f)
        {
            return PerformanceLevel.Medium;
        }
        else if (cpuUsage > 50f || memoryUsage > 1024f || latency > 50f || frameRate < 55f)
        {
            return PerformanceLevel.High;
        }
        else
        {
            return PerformanceLevel.Maximum;
        }
    }

    private void InitializeOptimizationRules()
    {
        // CPU optimization rules
        _optimizationRules["ReduceProcessingFrequency"] = new OptimizationRule
        {
            Name = "ReduceProcessingFrequency",
            Condition = (report) => report.CpuUsagePercent > CpuThresholdPercent,
            Action = async (report) => await ApplyOptimizationAsync(OptimizationType.ReduceProcessingFrequency, new OptimizationParameters())
        };

        // Memory optimization rules
        _optimizationRules["ReduceBufferSizes"] = new OptimizationRule
        {
            Name = "ReduceBufferSizes",
            Condition = (report) => report.MemoryUsageMB > MemoryThresholdMB,
            Action = async (report) => await ApplyOptimizationAsync(OptimizationType.ReduceBufferSizes, new OptimizationParameters())
        };

        // Latency optimization rules
        _optimizationRules["OptimizeAudioProcessing"] = new OptimizationRule
        {
            Name = "OptimizeAudioProcessing",
            Condition = (report) => report.AverageLatencyMs > LatencyThresholdMs,
            Action = async (report) => await ApplyOptimizationAsync(OptimizationType.OptimizeAudioProcessing, new OptimizationParameters())
        };

        // Frame rate optimization rules
        _optimizationRules["ReduceEffectComplexity"] = new OptimizationRule
        {
            Name = "ReduceEffectComplexity",
            Condition = (report) => report.FrameRate < 50f,
            Action = async (report) => await ApplyOptimizationAsync(OptimizationType.ReduceEffectComplexity, new OptimizationParameters())
        };
    }

    private async Task<bool> ApplyAutomaticOptimization(OptimizationRecommendation recommendation)
    {
        try
        {
            var parameters = new OptimizationParameters();

            switch (recommendation.Type)
            {
                case OptimizationType.ReduceProcessingFrequency:
                    parameters.ProcessingFrequencyMultiplier = 0.8f;
                    break;
                case OptimizationType.ReduceBufferSizes:
                    parameters.BufferSizeMultiplier = 0.7f;
                    break;
                case OptimizationType.OptimizeAudioProcessing:
                    parameters.AudioProcessingOptimization = true;
                    break;
                case OptimizationType.ReduceEffectComplexity:
                    parameters.EffectComplexityMultiplier = 0.6f;
                    break;
            }

            return await ApplyOptimizationAsync(recommendation.Type, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying automatic optimization: {Type}", recommendation.Type);
            return false;
        }
    }

    private async Task<bool> ExecuteOptimization(Optimization optimization)
    {
        try
        {
            switch (optimization.Type)
            {
                case OptimizationType.ReduceProcessingFrequency:
                    return await ExecuteReduceProcessingFrequency(optimization.Parameters);
                case OptimizationType.ReduceBufferSizes:
                    return await ExecuteReduceBufferSizes(optimization.Parameters);
                case OptimizationType.OptimizeAudioProcessing:
                    return await ExecuteOptimizeAudioProcessing(optimization.Parameters);
                case OptimizationType.ReduceEffectComplexity:
                    return await ExecuteReduceEffectComplexity(optimization.Parameters);
                case OptimizationType.EnableAdaptiveQuality:
                    return await ExecuteEnableAdaptiveQuality(optimization.Parameters);
                case OptimizationType.OptimizeMemoryUsage:
                    return await ExecuteOptimizeMemoryUsage(optimization.Parameters);
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing optimization: {Type}", optimization.Type);
            return false;
        }
    }

    private async Task<bool> ExecuteReduceProcessingFrequency(OptimizationParameters parameters)
    {
        // This would typically adjust processing intervals in other services
        // For now, it's a placeholder implementation
        await Task.Delay(100);
        return true;
    }

    private async Task<bool> ExecuteReduceBufferSizes(OptimizationParameters parameters)
    {
        // This would typically reduce buffer sizes in audio processing services
        await Task.Delay(100);
        return true;
    }

    private async Task<bool> ExecuteOptimizeAudioProcessing(OptimizationParameters parameters)
    {
        // This would typically optimize audio processing algorithms
        await Task.Delay(100);
        return true;
    }

    private async Task<bool> ExecuteReduceEffectComplexity(OptimizationParameters parameters)
    {
        // This would typically reduce effect complexity in real-time processing
        await Task.Delay(100);
        return true;
    }

    private async Task<bool> ExecuteEnableAdaptiveQuality(OptimizationParameters parameters)
    {
        // This would typically enable adaptive quality based on performance
        await Task.Delay(100);
        return true;
    }

    private async Task<bool> ExecuteOptimizeMemoryUsage(OptimizationParameters parameters)
    {
        // Force garbage collection and optimize memory usage
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Task.Delay(100);
        return true;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isMonitoring = false;
            _isOptimizing = false;

            _monitoringTimer?.Dispose();
            _optimizationTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();

            _logger.LogInformation("Performance optimization service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing performance optimization service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Performance metric
/// </summary>
public class PerformanceMetric
{
    public string Name { get; set; } = string.Empty;
    public float Value { get; set; }
    public MetricType Type { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Performance report
/// </summary>
public class PerformanceReport
{
    public DateTime Timestamp { get; set; }
    public float CpuUsagePercent { get; set; }
    public float MemoryUsageMB { get; set; }
    public float AverageLatencyMs { get; set; }
    public float FrameRate { get; set; }
    public PerformanceLevel PerformanceLevel { get; set; }
    public List<PerformanceMetric> Metrics { get; set; } = new();
    public List<PerformanceAlert> Alerts { get; set; } = new();
}

/// <summary>
/// Performance alert
/// </summary>
public class PerformanceAlert
{
    public AlertType Type { get; set; }
    public Severity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Optimization recommendation
/// </summary>
public class OptimizationRecommendation
{
    public OptimizationType Type { get; set; }
    public Priority Priority { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ExpectedImprovement { get; set; } = string.Empty;
}

/// <summary>
/// Optimization rule
/// </summary>
public class OptimizationRule
{
    public string Name { get; set; } = string.Empty;
    public Func<PerformanceReport, bool> Condition { get; set; } = _ => false;
    public Func<PerformanceReport, Task> Action { get; set; } = _ => Task.CompletedTask;
}

/// <summary>
/// Optimization
/// </summary>
public class Optimization
{
    public OptimizationType Type { get; set; }
    public OptimizationParameters Parameters { get; set; } = new();
    public DateTime AppliedTime { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Optimization parameters
/// </summary>
public class OptimizationParameters
{
    public float ProcessingFrequencyMultiplier { get; set; } = 1f;
    public float BufferSizeMultiplier { get; set; } = 1f;
    public float EffectComplexityMultiplier { get; set; } = 1f;
    public bool AudioProcessingOptimization { get; set; }
    public bool AdaptiveQualityEnabled { get; set; }
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Performance alert event arguments
/// </summary>
public class PerformanceAlertEventArgs : EventArgs
{
    public PerformanceAlert Alert { get; }
    public DateTime Timestamp { get; }

    public PerformanceAlertEventArgs(PerformanceAlert alert)
    {
        Alert = alert;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Optimization event arguments
/// </summary>
public class OptimizationEventArgs : EventArgs
{
    public OptimizationType Type { get; }
    public bool Success { get; }
    public DateTime Timestamp { get; }

    public OptimizationEventArgs(OptimizationType type, bool success)
    {
        Type = type;
        Success = success;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Performance report event arguments
/// </summary>
public class PerformanceReportEventArgs : EventArgs
{
    public PerformanceReport Report { get; }
    public DateTime Timestamp { get; }

    public PerformanceReportEventArgs(PerformanceReport report)
    {
        Report = report;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Metric types
/// </summary>
public enum MetricType
{
    System,
    Performance,
    Audio,
    Device,
    Custom
}

/// <summary>
/// Performance levels
/// </summary>
public enum PerformanceLevel
{
    Low,
    Medium,
    High,
    Maximum
}

/// <summary>
/// Alert types
/// </summary>
public enum AlertType
{
    CpuHigh,
    MemoryHigh,
    LatencyHigh,
    FrameRateLow,
    DeviceError,
    AudioError,
    Custom
}

/// <summary>
/// Severity levels
/// </summary>
public enum Severity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Priority levels
/// </summary>
public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Optimization types
/// </summary>
public enum OptimizationType
{
    ReduceProcessingFrequency,
    ReduceBufferSizes,
    OptimizeAudioProcessing,
    ReduceEffectComplexity,
    EnableAdaptiveQuality,
    OptimizeMemoryUsage,
    Custom
}
