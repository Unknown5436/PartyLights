using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PartyLights.Services;

/// <summary>
/// Dynamic performance optimization service for real-time performance tuning
/// </summary>
public class DynamicPerformanceOptimizationService : IDisposable
{
    private readonly ILogger<DynamicPerformanceOptimizationService> _logger;
    private readonly ConcurrentDictionary<string, OptimizationRule> _optimizationRules = new();
    private readonly ConcurrentDictionary<string, PerformanceTarget> _performanceTargets = new();
    private readonly Timer _optimizationTimer;
    private readonly object _lockObject = new();

    private const int OptimizationIntervalMs = 2000; // 2 seconds
    private bool _isOptimizing;

    // Performance optimization
    private readonly Dictionary<string, OptimizationStrategy> _optimizationStrategies = new();
    private readonly Dictionary<string, PerformanceBaseline> _performanceBaselines = new();
    private readonly Dictionary<string, OptimizationHistory> _optimizationHistory = new();

    public event EventHandler<OptimizationEventArgs>? OptimizationApplied;
    public event EventHandler<PerformanceTargetEventArgs>? PerformanceTargetUpdated;
    public event EventHandler<OptimizationRuleEventArgs>? OptimizationRuleTriggered;

    public DynamicPerformanceOptimizationService(ILogger<DynamicPerformanceOptimizationService> logger)
    {
        _logger = logger;

        _optimizationTimer = new Timer(ProcessOptimization, null, OptimizationIntervalMs, OptimizationIntervalMs);
        _isOptimizing = true;

        InitializeOptimizationStrategies();
        InitializeDefaultRules();

        _logger.LogInformation("Dynamic performance optimization service initialized");
    }

    /// <summary>
    /// Registers an optimization rule
    /// </summary>
    public async Task<bool> RegisterOptimizationRuleAsync(OptimizationRuleRequest request)
    {
        try
        {
            var rule = new OptimizationRule
            {
                Id = request.Id,
                Name = request.Name,
                Description = request.Description,
                ComponentName = request.ComponentName,
                MetricName = request.MetricName,
                Condition = request.Condition,
                Threshold = request.Threshold,
                Strategy = request.Strategy,
                IsEnabled = request.IsEnabled,
                Priority = request.Priority,
                CooldownPeriod = request.CooldownPeriod,
                LastTriggered = DateTime.MinValue,
                Parameters = request.Parameters ?? new Dictionary<string, object>(),
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _optimizationRules[request.Id] = rule;

            _logger.LogInformation("Registered optimization rule: {RuleName} ({RuleId})", request.Name, request.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering optimization rule: {RuleName}", request.Name);
            return false;
        }
    }

    /// <summary>
    /// Sets a performance target
    /// </summary>
    public async Task<bool> SetPerformanceTargetAsync(PerformanceTargetRequest request)
    {
        try
        {
            var target = new PerformanceTarget
            {
                Id = request.Id,
                Name = request.Name,
                ComponentName = request.ComponentName,
                MetricName = request.MetricName,
                TargetValue = request.TargetValue,
                Tolerance = request.Tolerance,
                IsEnabled = request.IsEnabled,
                Priority = request.Priority,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                Parameters = request.Parameters ?? new Dictionary<string, object>(),
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _performanceTargets[request.Id] = target;

            PerformanceTargetUpdated?.Invoke(this, new PerformanceTargetEventArgs(target.Id, target, PerformanceTargetAction.Set));
            _logger.LogInformation("Set performance target: {TargetName} ({TargetId})", request.Name, request.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting performance target: {TargetName}", request.Name);
            return false;
        }
    }

    /// <summary>
    /// Updates a performance metric and triggers optimization
    /// </summary>
    public async Task<bool> UpdatePerformanceMetricAsync(string metricName, double value, Dictionary<string, object>? additionalData = null)
    {
        try
        {
            // Check optimization rules
            foreach (var rule in _optimizationRules.Values.Where(r => r.IsEnabled && r.MetricName == metricName))
            {
                if (await CheckOptimizationRule(rule, value))
                {
                    await ApplyOptimization(rule, value, additionalData);
                }
            }

            // Check performance targets
            foreach (var target in _performanceTargets.Values.Where(t => t.IsEnabled && t.MetricName == metricName))
            {
                await CheckPerformanceTarget(target, value);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance metric: {MetricName}", metricName);
            return false;
        }
    }

    /// <summary>
    /// Applies a specific optimization strategy
    /// </summary>
    public async Task<OptimizationResult> ApplyOptimizationStrategyAsync(string strategyName, Dictionary<string, object>? parameters = null)
    {
        try
        {
            if (!_optimizationStrategies.TryGetValue(strategyName, out var strategy))
            {
                _logger.LogWarning("Optimization strategy not found: {StrategyName}", strategyName);
                return new OptimizationResult
                {
                    StrategyName = strategyName,
                    Success = false,
                    ErrorMessage = "Strategy not found"
                };
            }

            var result = new OptimizationResult
            {
                StrategyName = strategyName,
                StartTime = DateTime.UtcNow,
                Parameters = parameters ?? new Dictionary<string, object>()
            };

            try
            {
                var optimizationResult = await ExecuteOptimizationStrategy(strategy, parameters);
                result.Success = optimizationResult.Success;
                result.Message = optimizationResult.Message;
                result.Changes = optimizationResult.Changes;
                result.Metrics = optimizationResult.Metrics;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
            }

            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            // Record optimization history
            await RecordOptimizationHistory(strategyName, result);

            OptimizationApplied?.Invoke(this, new OptimizationEventArgs(strategyName, result));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying optimization strategy: {StrategyName}", strategyName);
            return new OptimizationResult
            {
                StrategyName = strategyName,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets optimization recommendations
    /// </summary>
    public async Task<List<OptimizationRecommendation>> GetOptimizationRecommendationsAsync(string? componentName = null)
    {
        try
        {
            var recommendations = new List<OptimizationRecommendation>();

            // Analyze current performance against targets
            foreach (var target in _performanceTargets.Values.Where(t => componentName == null || t.ComponentName == componentName))
            {
                var recommendation = await AnalyzePerformanceTarget(target);
                if (recommendation != null)
                {
                    recommendations.Add(recommendation);
                }
            }

            // Analyze optimization rules
            foreach (var rule in _optimizationRules.Values.Where(r => componentName == null || r.ComponentName == componentName))
            {
                var recommendation = await AnalyzeOptimizationRule(rule);
                if (recommendation != null)
                {
                    recommendations.Add(recommendation);
                }
            }

            return recommendations.OrderByDescending(r => r.Priority).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting optimization recommendations");
            return new List<OptimizationRecommendation>();
        }
    }

    /// <summary>
    /// Gets optimization history
    /// </summary>
    public IEnumerable<OptimizationHistory> GetOptimizationHistory(string? strategyName = null)
    {
        return _optimizationHistory.Values.Where(h => strategyName == null || h.StrategyName == strategyName);
    }

    /// <summary>
    /// Gets performance targets
    /// </summary>
    public IEnumerable<PerformanceTarget> GetPerformanceTargets(string? componentName = null)
    {
        return _performanceTargets.Values.Where(t => componentName == null || t.ComponentName == componentName);
    }

    /// <summary>
    /// Gets optimization rules
    /// </summary>
    public IEnumerable<OptimizationRule> GetOptimizationRules(string? componentName = null)
    {
        return _optimizationRules.Values.Where(r => componentName == null || r.ComponentName == componentName);
    }

    #region Private Methods

    private async void ProcessOptimization(object? state)
    {
        if (!_isOptimizing)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;

            // Process optimization rules
            foreach (var rule in _optimizationRules.Values.Where(r => r.IsEnabled))
            {
                if (currentTime - rule.LastTriggered > rule.CooldownPeriod)
                {
                    await ProcessOptimizationRule(rule);
                }
            }

            // Process performance targets
            foreach (var target in _performanceTargets.Values.Where(t => t.IsEnabled))
            {
                await ProcessPerformanceTarget(target);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in optimization processing");
        }
    }

    private async Task<bool> CheckOptimizationRule(OptimizationRule rule, double value)
    {
        try
        {
            bool conditionMet = rule.Condition switch
            {
                OptimizationCondition.GreaterThan => value > rule.Threshold,
                OptimizationCondition.LessThan => value < rule.Threshold,
                OptimizationCondition.EqualTo => Math.Abs(value - rule.Threshold) < 0.001,
                OptimizationCondition.NotEqualTo => Math.Abs(value - rule.Threshold) >= 0.001,
                _ => false
            };

            if (conditionMet)
            {
                rule.LastTriggered = DateTime.UtcNow;
                OptimizationRuleTriggered?.Invoke(this, new OptimizationRuleEventArgs(rule.Id, rule, OptimizationRuleAction.Triggered));
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking optimization rule: {RuleId}", rule.Id);
            return false;
        }
    }

    private async Task ApplyOptimization(OptimizationRule rule, double value, Dictionary<string, object>? additionalData)
    {
        try
        {
            var result = await ApplyOptimizationStrategyAsync(rule.Strategy, rule.Parameters);

            if (result.Success)
            {
                _logger.LogInformation("Applied optimization for rule {RuleName}: {Message}", rule.Name, result.Message);
            }
            else
            {
                _logger.LogWarning("Failed to apply optimization for rule {RuleName}: {ErrorMessage}", rule.Name, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying optimization for rule: {RuleId}", rule.Id);
        }
    }

    private async Task CheckPerformanceTarget(PerformanceTarget target, double value)
    {
        try
        {
            var deviation = Math.Abs(value - target.TargetValue);
            var isWithinTarget = deviation <= target.Tolerance;

            if (!isWithinTarget)
            {
                // Target not met, consider optimization
                var recommendation = await AnalyzePerformanceTarget(target);
                if (recommendation != null)
                {
                    _logger.LogInformation("Performance target not met for {TargetName}: Current {CurrentValue:F2}, Target {TargetValue:F2}",
                        target.Name, value, target.TargetValue);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking performance target: {TargetId}", target.Id);
        }
    }

    private async Task<OptimizationResult> ExecuteOptimizationStrategy(OptimizationStrategy strategy, Dictionary<string, object>? parameters)
    {
        try
        {
            switch (strategy.Type)
            {
                case OptimizationType.CacheOptimization:
                    return await ExecuteCacheOptimization(strategy, parameters);
                case OptimizationType.MemoryOptimization:
                    return await ExecuteMemoryOptimization(strategy, parameters);
                case OptimizationType.CpuOptimization:
                    return await ExecuteCpuOptimization(strategy, parameters);
                case OptimizationType.NetworkOptimization:
                    return await ExecuteNetworkOptimization(strategy, parameters);
                case OptimizationType.ThreadOptimization:
                    return await ExecuteThreadOptimization(strategy, parameters);
                case OptimizationType.Custom:
                    return await ExecuteCustomOptimization(strategy, parameters);
                default:
                    return new OptimizationResult
                    {
                        StrategyName = strategy.Name,
                        Success = false,
                        ErrorMessage = "Unknown optimization type"
                    };
            }
        }
        catch (Exception ex)
        {
            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = false,
                ErrorMessage = ex.Message,
                Exception = ex
            };
        }
    }

    private async Task<OptimizationResult> ExecuteCacheOptimization(OptimizationStrategy strategy, Dictionary<string, object>? parameters)
    {
        try
        {
            // Simulate cache optimization
            await Task.Delay(100);

            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = true,
                Message = "Cache optimization applied successfully",
                Changes = new Dictionary<string, object>
                {
                    ["CacheSize"] = "Increased",
                    ["CacheHitRate"] = "Improved"
                },
                Metrics = new Dictionary<string, double>
                {
                    ["CacheHitRate"] = 0.95,
                    ["CacheSize"] = 1024
                }
            };
        }
        catch (Exception ex)
        {
            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<OptimizationResult> ExecuteMemoryOptimization(OptimizationStrategy strategy, Dictionary<string, object>? parameters)
    {
        try
        {
            // Simulate memory optimization
            await Task.Delay(150);

            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = true,
                Message = "Memory optimization applied successfully",
                Changes = new Dictionary<string, object>
                {
                    ["MemoryUsage"] = "Reduced",
                    ["GarbageCollection"] = "Optimized"
                },
                Metrics = new Dictionary<string, double>
                {
                    ["MemoryUsage"] = 512,
                    ["GcFrequency"] = 0.1
                }
            };
        }
        catch (Exception ex)
        {
            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<OptimizationResult> ExecuteCpuOptimization(OptimizationStrategy strategy, Dictionary<string, object>? parameters)
    {
        try
        {
            // Simulate CPU optimization
            await Task.Delay(200);

            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = true,
                Message = "CPU optimization applied successfully",
                Changes = new Dictionary<string, object>
                {
                    ["CpuUsage"] = "Reduced",
                    ["ThreadCount"] = "Optimized"
                },
                Metrics = new Dictionary<string, double>
                {
                    ["CpuUsage"] = 45.0,
                    ["ThreadCount"] = 8
                }
            };
        }
        catch (Exception ex)
        {
            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<OptimizationResult> ExecuteNetworkOptimization(OptimizationStrategy strategy, Dictionary<string, object>? parameters)
    {
        try
        {
            // Simulate network optimization
            await Task.Delay(120);

            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = true,
                Message = "Network optimization applied successfully",
                Changes = new Dictionary<string, object>
                {
                    ["NetworkLatency"] = "Reduced",
                    ["Bandwidth"] = "Optimized"
                },
                Metrics = new Dictionary<string, double>
                {
                    ["NetworkLatency"] = 50.0,
                    ["Bandwidth"] = 100.0
                }
            };
        }
        catch (Exception ex)
        {
            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<OptimizationResult> ExecuteThreadOptimization(OptimizationStrategy strategy, Dictionary<string, object>? parameters)
    {
        try
        {
            // Simulate thread optimization
            await Task.Delay(80);

            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = true,
                Message = "Thread optimization applied successfully",
                Changes = new Dictionary<string, object>
                {
                    ["ThreadCount"] = "Optimized",
                    ["ThreadPool"] = "Tuned"
                },
                Metrics = new Dictionary<string, double>
                {
                    ["ThreadCount"] = 16,
                    ["ThreadPoolUtilization"] = 0.8
                }
            };
        }
        catch (Exception ex)
        {
            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<OptimizationResult> ExecuteCustomOptimization(OptimizationStrategy strategy, Dictionary<string, object>? parameters)
    {
        try
        {
            // Simulate custom optimization
            await Task.Delay(100);

            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = true,
                Message = "Custom optimization applied successfully",
                Changes = new Dictionary<string, object>
                {
                    ["CustomParameter"] = "Optimized"
                },
                Metrics = new Dictionary<string, double>
                {
                    ["CustomMetric"] = 100.0
                }
            };
        }
        catch (Exception ex)
        {
            return new OptimizationResult
            {
                StrategyName = strategy.Name,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task ProcessOptimizationRule(OptimizationRule rule)
    {
        try
        {
            // Process optimization rule logic
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing optimization rule: {RuleId}", rule.Id);
        }
    }

    private async Task ProcessPerformanceTarget(PerformanceTarget target)
    {
        try
        {
            // Process performance target logic
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing performance target: {TargetId}", target.Id);
        }
    }

    private async Task RecordOptimizationHistory(string strategyName, OptimizationResult result)
    {
        try
        {
            var history = new OptimizationHistory
            {
                Id = Guid.NewGuid().ToString(),
                StrategyName = strategyName,
                StartTime = result.StartTime,
                EndTime = result.EndTime,
                Duration = result.Duration,
                Success = result.Success,
                Message = result.Message,
                ErrorMessage = result.ErrorMessage,
                Changes = result.Changes ?? new Dictionary<string, object>(),
                Metrics = result.Metrics ?? new Dictionary<string, double>(),
                Parameters = result.Parameters ?? new Dictionary<string, object>()
            };

            _optimizationHistory[history.Id] = history;

            // Keep only recent history
            if (_optimizationHistory.Count > 1000)
            {
                var oldestKey = _optimizationHistory.Keys.First();
                _optimizationHistory.TryRemove(oldestKey, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording optimization history");
        }
    }

    private async Task<OptimizationRecommendation?> AnalyzePerformanceTarget(PerformanceTarget target)
    {
        try
        {
            // Analyze performance target and generate recommendation
            var recommendation = new OptimizationRecommendation
            {
                Id = Guid.NewGuid().ToString(),
                ComponentName = target.ComponentName,
                MetricName = target.MetricName,
                Priority = target.Priority,
                RecommendationType = RecommendationType.PerformanceTarget,
                Title = $"Optimize {target.MetricName} for {target.ComponentName}",
                Description = $"Current performance for {target.MetricName} may not meet target of {target.TargetValue}",
                SuggestedActions = new List<string>
                {
                    $"Review {target.ComponentName} configuration",
                    $"Consider optimization strategies for {target.MetricName}",
                    $"Monitor performance trends"
                },
                EstimatedImpact = "Medium",
                CreatedAt = DateTime.UtcNow
            };

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing performance target: {TargetId}", target.Id);
            return null;
        }
    }

    private async Task<OptimizationRecommendation?> AnalyzeOptimizationRule(OptimizationRule rule)
    {
        try
        {
            // Analyze optimization rule and generate recommendation
            var recommendation = new OptimizationRecommendation
            {
                Id = Guid.NewGuid().ToString(),
                ComponentName = rule.ComponentName,
                MetricName = rule.MetricName,
                Priority = rule.Priority,
                RecommendationType = RecommendationType.OptimizationRule,
                Title = $"Apply {rule.Strategy} for {rule.ComponentName}",
                Description = $"Rule '{rule.Name}' suggests applying {rule.Strategy} when {rule.MetricName} {rule.Condition} {rule.Threshold}",
                SuggestedActions = new List<string>
                {
                    $"Apply {rule.Strategy} strategy",
                    $"Monitor {rule.MetricName} after optimization",
                    $"Adjust rule parameters if needed"
                },
                EstimatedImpact = "High",
                CreatedAt = DateTime.UtcNow
            };

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing optimization rule: {RuleId}", rule.Id);
            return null;
        }
    }

    private void InitializeOptimizationStrategies()
    {
        try
        {
            // Initialize optimization strategies
            _optimizationStrategies["cache_optimization"] = new OptimizationStrategy
            {
                Name = "cache_optimization",
                Type = OptimizationType.CacheOptimization,
                Description = "Optimizes cache usage and hit rates"
            };

            _optimizationStrategies["memory_optimization"] = new OptimizationStrategy
            {
                Name = "memory_optimization",
                Type = OptimizationType.MemoryOptimization,
                Description = "Optimizes memory usage and garbage collection"
            };

            _optimizationStrategies["cpu_optimization"] = new OptimizationStrategy
            {
                Name = "cpu_optimization",
                Type = OptimizationType.CpuOptimization,
                Description = "Optimizes CPU usage and thread management"
            };

            _optimizationStrategies["network_optimization"] = new OptimizationStrategy
            {
                Name = "network_optimization",
                Type = OptimizationType.NetworkOptimization,
                Description = "Optimizes network performance and bandwidth usage"
            };

            _optimizationStrategies["thread_optimization"] = new OptimizationStrategy
            {
                Name = "thread_optimization",
                Type = OptimizationType.ThreadOptimization,
                Description = "Optimizes thread usage and thread pool management"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing optimization strategies");
        }
    }

    private void InitializeDefaultRules()
    {
        try
        {
            // Initialize default optimization rules
            RegisterOptimizationRuleAsync(new OptimizationRuleRequest
            {
                Id = "high_cpu_optimization",
                Name = "High CPU Optimization",
                Description = "Optimizes when CPU usage is high",
                ComponentName = "System",
                MetricName = "cpu_usage",
                Condition = OptimizationCondition.GreaterThan,
                Threshold = 80,
                Strategy = "cpu_optimization",
                IsEnabled = true,
                Priority = 1,
                CooldownPeriod = TimeSpan.FromMinutes(5)
            }).Wait();

            RegisterOptimizationRuleAsync(new OptimizationRuleRequest
            {
                Id = "high_memory_optimization",
                Name = "High Memory Optimization",
                Description = "Optimizes when memory usage is high",
                ComponentName = "System",
                MetricName = "memory_usage",
                Condition = OptimizationCondition.GreaterThan,
                Threshold = 85,
                Strategy = "memory_optimization",
                IsEnabled = true,
                Priority = 1,
                CooldownPeriod = TimeSpan.FromMinutes(5)
            }).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing default optimization rules");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isOptimizing = false;
            _optimizationTimer?.Dispose();

            _logger.LogInformation("Dynamic performance optimization service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing dynamic performance optimization service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Optimization rule request
/// </summary>
public class OptimizationRuleRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public OptimizationCondition Condition { get; set; } = OptimizationCondition.GreaterThan;
    public double Threshold { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 1;
    public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);
    public Dictionary<string, object>? Parameters { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Performance target request
/// </summary>
public class PerformanceTargetRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double TargetValue { get; set; }
    public double Tolerance { get; set; } = 0.1;
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 1;
    public Dictionary<string, object>? Parameters { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Optimization rule
/// </summary>
public class OptimizationRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public OptimizationCondition Condition { get; set; }
    public double Threshold { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public TimeSpan CooldownPeriod { get; set; }
    public DateTime LastTriggered { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Performance target
/// </summary>
public class PerformanceTarget
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double TargetValue { get; set; }
    public double Tolerance { get; set; }
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Optimization strategy
/// </summary>
public class OptimizationStrategy
{
    public string Name { get; set; } = string.Empty;
    public OptimizationType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Optimization result
/// </summary>
public class OptimizationResult
{
    public string StrategyName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public Dictionary<string, object> Changes { get; set; } = new();
    public Dictionary<string, double> Metrics { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Optimization history
/// </summary>
public class OptimizationHistory
{
    public string Id { get; set; } = string.Empty;
    public string StrategyName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public Dictionary<string, object> Changes { get; set; } = new();
    public Dictionary<string, double> Metrics { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Optimization recommendation
/// </summary>
public class OptimizationRecommendation
{
    public string Id { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public RecommendationType RecommendationType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> SuggestedActions { get; set; } = new();
    public string EstimatedImpact { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Optimization event arguments
/// </summary>
public class OptimizationEventArgs : EventArgs
{
    public string StrategyName { get; }
    public OptimizationResult Result { get; }
    public DateTime Timestamp { get; }

    public OptimizationEventArgs(string strategyName, OptimizationResult result)
    {
        StrategyName = strategyName;
        Result = result;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Performance target event arguments
/// </summary>
public class PerformanceTargetEventArgs : EventArgs
{
    public string TargetId { get; }
    public PerformanceTarget Target { get; }
    public PerformanceTargetAction Action { get; }
    public DateTime Timestamp { get; }

    public PerformanceTargetEventArgs(string targetId, PerformanceTarget target, PerformanceTargetAction action)
    {
        TargetId = targetId;
        Target = target;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Optimization rule event arguments
/// </summary>
public class OptimizationRuleEventArgs : EventArgs
{
    public string RuleId { get; }
    public OptimizationRule Rule { get; }
    public OptimizationRuleAction Action { get; }
    public DateTime Timestamp { get; }

    public OptimizationRuleEventArgs(string ruleId, OptimizationRule rule, OptimizationRuleAction action)
    {
        RuleId = ruleId;
        Rule = rule;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Optimization types
/// </summary>
public enum OptimizationType
{
    CacheOptimization,
    MemoryOptimization,
    CpuOptimization,
    NetworkOptimization,
    ThreadOptimization,
    Custom
}

/// <summary>
/// Optimization conditions
/// </summary>
public enum OptimizationCondition
{
    GreaterThan,
    LessThan,
    EqualTo,
    NotEqualTo
}

/// <summary>
/// Recommendation types
/// </summary>
public enum RecommendationType
{
    PerformanceTarget,
    OptimizationRule,
    SystemHealth,
    ResourceUsage
}

/// <summary>
/// Performance target actions
/// </summary>
public enum PerformanceTargetAction
{
    Set,
    Updated,
    Removed
}

/// <summary>
/// Optimization rule actions
/// </summary>
public enum OptimizationRuleAction
{
    Triggered,
    Applied,
    Failed
}
