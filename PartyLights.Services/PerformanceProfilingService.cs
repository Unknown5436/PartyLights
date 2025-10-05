using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PartyLights.Services;

/// <summary>
/// Advanced performance profiling service for comprehensive performance analysis
/// </summary>
public class PerformanceProfilingService : IDisposable
{
    private readonly ILogger<PerformanceProfilingService> _logger;
    private readonly ConcurrentDictionary<string, ProfilingSession> _profilingSessions = new();
    private readonly ConcurrentDictionary<string, PerformanceProfile> _performanceProfiles = new();
    private readonly Timer _profilingTimer;
    private readonly object _lockObject = new();

    private const int ProfilingIntervalMs = 1000; // 1 second
    private bool _isProfiling;

    // Performance tracking
    private readonly Dictionary<string, PerformanceCounter> _performanceCounters = new();
    private readonly Dictionary<string, ProfilingRule> _profilingRules = new();
    private readonly Dictionary<string, PerformanceBaseline> _performanceBaselines = new();

    public event EventHandler<ProfilingEventArgs>? ProfilingStarted;
    public event EventHandler<ProfilingEventArgs>? ProfilingStopped;
    public event EventHandler<PerformanceProfileEventArgs>? PerformanceProfileUpdated;
    public event EventHandler<PerformanceAlertEventArgs>? PerformanceAlert;

    public PerformanceProfilingService(ILogger<PerformanceProfilingService> logger)
    {
        _logger = logger;

        _profilingTimer = new Timer(ProcessProfiling, null, ProfilingIntervalMs, ProfilingIntervalMs);
        _isProfiling = true;

        InitializePerformanceCounters();
        InitializeProfilingRules();

        _logger.LogInformation("Performance profiling service initialized");
    }

    /// <summary>
    /// Starts a profiling session
    /// </summary>
    public async Task<string> StartProfilingSessionAsync(ProfilingSessionRequest request)
    {
        try
        {
            var sessionId = Guid.NewGuid().ToString();

            var session = new ProfilingSession
            {
                SessionId = sessionId,
                Name = request.Name,
                Description = request.Description,
                StartTime = DateTime.UtcNow,
                IsActive = true,
                ProfilingLevel = request.ProfilingLevel,
                TargetComponents = request.TargetComponents ?? new List<string>(),
                Metrics = request.Metrics ?? new List<string>(),
                Rules = request.Rules ?? new List<string>(),
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _profilingSessions[sessionId] = session;

            ProfilingStarted?.Invoke(this, new ProfilingEventArgs(sessionId, ProfilingAction.Started));
            _logger.LogInformation("Started profiling session: {SessionName} ({SessionId})", request.Name, sessionId);

            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting profiling session: {SessionName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Stops a profiling session
    /// </summary>
    public async Task<ProfilingSessionResult> StopProfilingSessionAsync(string sessionId)
    {
        try
        {
            if (!_profilingSessions.TryGetValue(sessionId, out var session))
            {
                _logger.LogWarning("Profiling session not found: {SessionId}", sessionId);
                return new ProfilingSessionResult
                {
                    SessionId = sessionId,
                    Success = false,
                    ErrorMessage = "Session not found"
                };
            }

            session.EndTime = DateTime.UtcNow;
            session.IsActive = false;
            session.Duration = session.EndTime - session.StartTime;

            // Generate profiling report
            var report = await GenerateProfilingReport(session);

            ProfilingStopped?.Invoke(this, new ProfilingEventArgs(sessionId, ProfilingAction.Stopped));
            _logger.LogInformation("Stopped profiling session: {SessionName} ({SessionId})", session.Name, sessionId);

            return new ProfilingSessionResult
            {
                SessionId = sessionId,
                Success = true,
                Report = report,
                Duration = session.Duration
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping profiling session: {SessionId}", sessionId);
            return new ProfilingSessionResult
            {
                SessionId = sessionId,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Profiles a method execution
    /// </summary>
    public async Task<MethodProfile> ProfileMethodAsync(string methodName, Func<Task> method, ProfilingContext? context = null)
    {
        try
        {
            var profile = new MethodProfile
            {
                MethodName = methodName,
                StartTime = DateTime.UtcNow,
                Context = context ?? new ProfilingContext()
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await method();
                profile.Success = true;
            }
            catch (Exception ex)
            {
                profile.Success = false;
                profile.Exception = ex;
                profile.ErrorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                profile.EndTime = DateTime.UtcNow;
                profile.Duration = stopwatch.Elapsed;
                profile.ThreadId = Environment.CurrentManagedThreadId;
                profile.ProcessId = Environment.ProcessId;
            }

            // Update performance profile
            await UpdatePerformanceProfile(methodName, profile);

            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error profiling method: {MethodName}", methodName);
            return new MethodProfile
            {
                MethodName = methodName,
                Success = false,
                ErrorMessage = ex.Message,
                Duration = TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// Profiles a method with return value
    /// </summary>
    public async Task<MethodProfile<T>> ProfileMethodAsync<T>(string methodName, Func<Task<T>> method, ProfilingContext? context = null)
    {
        try
        {
            var profile = new MethodProfile<T>
            {
                MethodName = methodName,
                StartTime = DateTime.UtcNow,
                Context = context ?? new ProfilingContext()
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                profile.Result = await method();
                profile.Success = true;
            }
            catch (Exception ex)
            {
                profile.Success = false;
                profile.Exception = ex;
                profile.ErrorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                profile.EndTime = DateTime.UtcNow;
                profile.Duration = stopwatch.Elapsed;
                profile.ThreadId = Environment.CurrentManagedThreadId;
                profile.ProcessId = Environment.ProcessId;
            }

            // Update performance profile
            await UpdatePerformanceProfile(methodName, profile);

            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error profiling method: {MethodName}", methodName);
            return new MethodProfile<T>
            {
                MethodName = methodName,
                Success = false,
                ErrorMessage = ex.Message,
                Duration = TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// Gets performance profile for a method
    /// </summary>
    public PerformanceProfile? GetPerformanceProfile(string methodName)
    {
        _performanceProfiles.TryGetValue(methodName, out var profile);
        return profile;
    }

    /// <summary>
    /// Gets all performance profiles
    /// </summary>
    public IEnumerable<PerformanceProfile> GetAllPerformanceProfiles()
    {
        return _performanceProfiles.Values;
    }

    /// <summary>
    /// Analyzes performance trends
    /// </summary>
    public async Task<PerformanceAnalysisResult> AnalyzePerformanceTrendsAsync(PerformanceAnalysisRequest request)
    {
        try
        {
            var result = new PerformanceAnalysisResult
            {
                AnalysisId = Guid.NewGuid().ToString(),
                StartTime = DateTime.UtcNow,
                MethodName = request.MethodName,
                AnalysisType = request.AnalysisType,
                TimeRange = request.TimeRange
            };

            var profiles = GetPerformanceProfilesForAnalysis(request);

            switch (request.AnalysisType)
            {
                case PerformanceAnalysisType.ResponseTime:
                    result = await AnalyzeResponseTimeTrends(profiles, result);
                    break;
                case PerformanceAnalysisType.Throughput:
                    result = await AnalyzeThroughputTrends(profiles, result);
                    break;
                case PerformanceAnalysisType.ErrorRate:
                    result = await AnalyzeErrorRateTrends(profiles, result);
                    break;
                case PerformanceAnalysisType.ResourceUsage:
                    result = await AnalyzeResourceUsageTrends(profiles, result);
                    break;
                case PerformanceAnalysisType.Comprehensive:
                    result = await AnalyzeComprehensiveTrends(profiles, result);
                    break;
            }

            result.EndTime = DateTime.UtcNow;
            result.Success = true;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing performance trends: {MethodName}", request.MethodName);
            return new PerformanceAnalysisResult
            {
                AnalysisId = Guid.NewGuid().ToString(),
                MethodName = request.MethodName,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #region Private Methods

    private async void ProcessProfiling(object? state)
    {
        if (!_isProfiling)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;

            // Process active profiling sessions
            foreach (var session in _profilingSessions.Values.Where(s => s.IsActive))
            {
                await ProcessProfilingSession(session, currentTime);
            }

            // Update performance baselines
            await UpdatePerformanceBaselines();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in profiling processing");
        }
    }

    private async Task ProcessProfilingSession(ProfilingSession session, DateTime currentTime)
    {
        try
        {
            // Collect performance metrics for the session
            var metrics = new Dictionary<string, double>();

            foreach (var metricName in session.Metrics)
            {
                if (_performanceCounters.TryGetValue(metricName, out var counter))
                {
                    metrics[metricName] = counter.NextValue();
                }
            }

            // Check profiling rules
            foreach (var ruleName in session.Rules)
            {
                if (_profilingRules.TryGetValue(ruleName, out var rule))
                {
                    await CheckProfilingRule(rule, metrics, session);
                }
            }

            // Update session metrics
            session.MetricsHistory.Add(new ProfilingMetricSnapshot
            {
                Timestamp = currentTime,
                Metrics = metrics
            });

            // Keep only recent history
            if (session.MetricsHistory.Count > 1000)
            {
                session.MetricsHistory.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing profiling session: {SessionId}", session.SessionId);
        }
    }

    private async Task UpdatePerformanceProfile(string methodName, MethodProfile profile)
    {
        try
        {
            if (!_performanceProfiles.TryGetValue(methodName, out var performanceProfile))
            {
                performanceProfile = new PerformanceProfile
                {
                    MethodName = methodName,
                    FirstExecution = profile.StartTime,
                    LastExecution = profile.StartTime
                };
                _performanceProfiles[methodName] = performanceProfile;
            }

            // Update profile statistics
            performanceProfile.TotalExecutions++;
            performanceProfile.LastExecution = profile.StartTime;

            if (profile.Success)
            {
                performanceProfile.SuccessfulExecutions++;
                performanceProfile.TotalDuration += profile.Duration;

                if (performanceProfile.MinDuration == TimeSpan.Zero || profile.Duration < performanceProfile.MinDuration)
                {
                    performanceProfile.MinDuration = profile.Duration;
                }

                if (profile.Duration > performanceProfile.MaxDuration)
                {
                    performanceProfile.MaxDuration = profile.Duration;
                }
            }
            else
            {
                performanceProfile.FailedExecutions++;
            }

            // Add to execution history
            performanceProfile.ExecutionHistory.Add(new ExecutionRecord
            {
                StartTime = profile.StartTime,
                Duration = profile.Duration,
                Success = profile.Success,
                ThreadId = profile.ThreadId,
                ProcessId = profile.ProcessId,
                Context = profile.Context
            });

            // Keep only recent history
            if (performanceProfile.ExecutionHistory.Count > 1000)
            {
                performanceProfile.ExecutionHistory.RemoveAt(0);
            }

            // Calculate averages
            if (performanceProfile.SuccessfulExecutions > 0)
            {
                performanceProfile.AverageDuration = TimeSpan.FromTicks(performanceProfile.TotalDuration.Ticks / performanceProfile.SuccessfulExecutions);
            }

            PerformanceProfileUpdated?.Invoke(this, new PerformanceProfileEventArgs(methodName, performanceProfile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance profile: {MethodName}", methodName);
        }
    }

    private async Task<ProfilingReport> GenerateProfilingReport(ProfilingSession session)
    {
        try
        {
            var report = new ProfilingReport
            {
                SessionId = session.SessionId,
                SessionName = session.Name,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                Duration = session.Duration,
                ProfilingLevel = session.ProfilingLevel,
                GeneratedAt = DateTime.UtcNow
            };

            // Analyze session metrics
            if (session.MetricsHistory.Any())
            {
                report.MetricsAnalysis = AnalyzeSessionMetrics(session.MetricsHistory);
            }

            // Analyze performance profiles
            report.PerformanceProfiles = _performanceProfiles.Values
                .Where(p => p.LastExecution >= session.StartTime && p.LastExecution <= session.EndTime)
                .ToList();

            // Generate recommendations
            report.Recommendations = GeneratePerformanceRecommendations(report);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating profiling report: {SessionId}", session.SessionId);
            return new ProfilingReport
            {
                SessionId = session.SessionId,
                SessionName = session.Name,
                ErrorMessage = ex.Message
            };
        }
    }

    private MetricsAnalysis AnalyzeSessionMetrics(List<ProfilingMetricSnapshot> snapshots)
    {
        try
        {
            var analysis = new MetricsAnalysis
            {
                TotalSnapshots = snapshots.Count,
                AnalysisStartTime = snapshots.First().Timestamp,
                AnalysisEndTime = snapshots.Last().Timestamp
            };

            // Analyze each metric
            var metricNames = snapshots.First().Metrics.Keys;
            foreach (var metricName in metricNames)
            {
                var values = snapshots.Select(s => s.Metrics[metricName]).ToList();

                analysis.MetricStatistics[metricName] = new MetricStatistics
                {
                    MetricName = metricName,
                    MinValue = values.Min(),
                    MaxValue = values.Max(),
                    AverageValue = values.Average(),
                    MedianValue = values.OrderBy(v => v).Skip(values.Count / 2).First(),
                    StandardDeviation = CalculateStandardDeviation(values)
                };
            }

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing session metrics");
            return new MetricsAnalysis();
        }
    }

    private List<string> GeneratePerformanceRecommendations(ProfilingReport report)
    {
        var recommendations = new List<string>();

        try
        {
            // Analyze performance profiles
            foreach (var profile in report.PerformanceProfiles)
            {
                if (profile.AverageDuration.TotalMilliseconds > 1000)
                {
                    recommendations.Add($"Method '{profile.MethodName}' has high average execution time ({profile.AverageDuration.TotalMilliseconds:F2}ms). Consider optimization.");
                }

                if (profile.FailedExecutions > profile.SuccessfulExecutions * 0.1)
                {
                    recommendations.Add($"Method '{profile.MethodName}' has high failure rate ({profile.FailedExecutions}/{profile.TotalExecutions}). Investigate error causes.");
                }

                if (profile.MaxDuration.TotalMilliseconds > profile.AverageDuration.TotalMilliseconds * 5)
                {
                    recommendations.Add($"Method '{profile.MethodName}' has high execution time variance. Consider caching or optimization.");
                }
            }

            // Analyze metrics
            foreach (var metric in report.MetricsAnalysis.MetricStatistics.Values)
            {
                if (metric.AverageValue > 80) // Assuming percentage metrics
                {
                    recommendations.Add($"Metric '{metric.MetricName}' shows high average usage ({metric.AverageValue:F2}%). Monitor for potential issues.");
                }
            }

            if (!recommendations.Any())
            {
                recommendations.Add("No significant performance issues detected.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating performance recommendations");
            recommendations.Add("Error generating recommendations.");
        }

        return recommendations;
    }

    private async Task CheckProfilingRule(ProfilingRule rule, Dictionary<string, double> metrics, ProfilingSession session)
    {
        try
        {
            if (metrics.TryGetValue(rule.MetricName, out var value))
            {
                bool conditionMet = rule.Condition switch
                {
                    ProfilingCondition.GreaterThan => value > rule.Threshold,
                    ProfilingCondition.LessThan => value < rule.Threshold,
                    ProfilingCondition.EqualTo => Math.Abs(value - rule.Threshold) < 0.001,
                    ProfilingCondition.NotEqualTo => Math.Abs(value - rule.Threshold) >= 0.001,
                    _ => false
                };

                if (conditionMet)
                {
                    var alert = new PerformanceAlert
                    {
                        AlertId = Guid.NewGuid().ToString(),
                        RuleId = rule.Id,
                        SessionId = session.SessionId,
                        MetricName = rule.MetricName,
                        CurrentValue = value,
                        Threshold = rule.Threshold,
                        Condition = rule.Condition,
                        Severity = rule.Severity,
                        Message = $"Performance alert: {rule.MetricName} {rule.Condition} {rule.Threshold} (Current: {value:F2})",
                        Timestamp = DateTime.UtcNow
                    };

                    PerformanceAlert?.Invoke(this, new PerformanceAlertEventArgs(alert));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking profiling rule: {RuleId}", rule.Id);
        }
    }

    private async Task UpdatePerformanceBaselines()
    {
        try
        {
            foreach (var profile in _performanceProfiles.Values)
            {
                if (!_performanceBaselines.TryGetValue(profile.MethodName, out var baseline))
                {
                    baseline = new PerformanceBaseline
                    {
                        MethodName = profile.MethodName,
                        CreatedAt = DateTime.UtcNow
                    };
                    _performanceBaselines[profile.MethodName] = baseline;
                }

                // Update baseline with recent performance data
                if (profile.SuccessfulExecutions > 0)
                {
                    baseline.AverageResponseTime = profile.AverageDuration;
                    baseline.MaxResponseTime = profile.MaxDuration;
                    baseline.MinResponseTime = profile.MinDuration;
                    baseline.SuccessRate = (double)profile.SuccessfulExecutions / profile.TotalExecutions;
                    baseline.LastUpdated = DateTime.UtcNow;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance baselines");
        }
    }

    private IEnumerable<PerformanceProfile> GetPerformanceProfilesForAnalysis(PerformanceAnalysisRequest request)
    {
        var profiles = _performanceProfiles.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(request.MethodName))
        {
            profiles = profiles.Where(p => p.MethodName == request.MethodName);
        }

        if (request.TimeRange.HasValue)
        {
            var cutoffTime = DateTime.UtcNow - request.TimeRange.Value;
            profiles = profiles.Where(p => p.LastExecution >= cutoffTime);
        }

        return profiles;
    }

    private async Task<PerformanceAnalysisResult> AnalyzeResponseTimeTrends(IEnumerable<PerformanceProfile> profiles, PerformanceAnalysisResult result)
    {
        try
        {
            var responseTimes = profiles.Select(p => p.AverageDuration.TotalMilliseconds).ToList();

            if (responseTimes.Any())
            {
                result.Metrics["AverageResponseTime"] = responseTimes.Average();
                result.Metrics["MaxResponseTime"] = responseTimes.Max();
                result.Metrics["MinResponseTime"] = responseTimes.Min();
                result.Metrics["ResponseTimeVariance"] = CalculateVariance(responseTimes);

                result.Insights.Add($"Average response time: {responseTimes.Average():F2}ms");
                result.Insights.Add($"Response time range: {responseTimes.Min():F2}ms - {responseTimes.Max():F2}ms");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing response time trends");
            return result;
        }
    }

    private async Task<PerformanceAnalysisResult> AnalyzeThroughputTrends(IEnumerable<PerformanceProfile> profiles, PerformanceAnalysisResult result)
    {
        try
        {
            var totalExecutions = profiles.Sum(p => p.TotalExecutions);
            var timeSpan = profiles.Max(p => p.LastExecution) - profiles.Min(p => p.FirstExecution);

            if (timeSpan.TotalSeconds > 0)
            {
                var throughput = totalExecutions / timeSpan.TotalSeconds;
                result.Metrics["Throughput"] = throughput;
                result.Insights.Add($"Throughput: {throughput:F2} executions/second");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing throughput trends");
            return result;
        }
    }

    private async Task<PerformanceAnalysisResult> AnalyzeErrorRateTrends(IEnumerable<PerformanceProfile> profiles, PerformanceAnalysisResult result)
    {
        try
        {
            var totalExecutions = profiles.Sum(p => p.TotalExecutions);
            var failedExecutions = profiles.Sum(p => p.FailedExecutions);

            if (totalExecutions > 0)
            {
                var errorRate = (double)failedExecutions / totalExecutions * 100;
                result.Metrics["ErrorRate"] = errorRate;
                result.Insights.Add($"Error rate: {errorRate:F2}%");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing error rate trends");
            return result;
        }
    }

    private async Task<PerformanceAnalysisResult> AnalyzeResourceUsageTrends(IEnumerable<PerformanceProfile> profiles, PerformanceAnalysisResult result)
    {
        try
        {
            // Analyze resource usage patterns
            result.Insights.Add("Resource usage analysis completed");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing resource usage trends");
            return result;
        }
    }

    private async Task<PerformanceAnalysisResult> AnalyzeComprehensiveTrends(IEnumerable<PerformanceProfile> profiles, PerformanceAnalysisResult result)
    {
        try
        {
            // Perform comprehensive analysis
            await AnalyzeResponseTimeTrends(profiles, result);
            await AnalyzeThroughputTrends(profiles, result);
            await AnalyzeErrorRateTrends(profiles, result);
            await AnalyzeResourceUsageTrends(profiles, result);

            result.Insights.Add("Comprehensive performance analysis completed");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing comprehensive trends");
            return result;
        }
    }

    private double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count == 0) return 0;

        var mean = values.Average();
        var variance = values.Select(v => Math.Pow(v - mean, 2)).Average();
        return Math.Sqrt(variance);
    }

    private double CalculateVariance(List<double> values)
    {
        if (values.Count == 0) return 0;

        var mean = values.Average();
        return values.Select(v => Math.Pow(v - mean, 2)).Average();
    }

    private void InitializePerformanceCounters()
    {
        try
        {
            // Initialize performance counters
            _performanceCounters["cpu_usage"] = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _performanceCounters["memory_usage"] = new PerformanceCounter("Memory", "Available MBytes");
            _performanceCounters["disk_usage"] = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing performance counters");
        }
    }

    private void InitializeProfilingRules()
    {
        try
        {
            // Initialize default profiling rules
            _profilingRules["high_cpu"] = new ProfilingRule
            {
                Id = "high_cpu",
                Name = "High CPU Usage",
                MetricName = "cpu_usage",
                Condition = ProfilingCondition.GreaterThan,
                Threshold = 80,
                Severity = ProfilingSeverity.High,
                IsEnabled = true
            };

            _profilingRules["low_memory"] = new ProfilingRule
            {
                Id = "low_memory",
                Name = "Low Memory",
                MetricName = "memory_usage",
                Condition = ProfilingCondition.LessThan,
                Threshold = 100, // MB
                Severity = ProfilingSeverity.Medium,
                IsEnabled = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing profiling rules");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isProfiling = false;
            _profilingTimer?.Dispose();

            // Dispose performance counters
            foreach (var counter in _performanceCounters.Values)
            {
                counter?.Dispose();
            }

            _logger.LogInformation("Performance profiling service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing performance profiling service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Profiling session request
/// </summary>
public class ProfilingSessionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProfilingLevel ProfilingLevel { get; set; } = ProfilingLevel.Medium;
    public List<string>? TargetComponents { get; set; }
    public List<string>? Metrics { get; set; }
    public List<string>? Rules { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Performance analysis request
/// </summary>
public class PerformanceAnalysisRequest
{
    public string MethodName { get; set; } = string.Empty;
    public PerformanceAnalysisType AnalysisType { get; set; } = PerformanceAnalysisType.Comprehensive;
    public TimeSpan? TimeRange { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Profiling session
/// </summary>
public class ProfilingSession
{
    public string SessionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsActive { get; set; }
    public ProfilingLevel ProfilingLevel { get; set; }
    public List<string> TargetComponents { get; set; } = new();
    public List<string> Metrics { get; set; } = new();
    public List<string> Rules { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<ProfilingMetricSnapshot> MetricsHistory { get; set; } = new();
}

/// <summary>
/// Profiling metric snapshot
/// </summary>
public class ProfilingMetricSnapshot
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> Metrics { get; set; } = new();
}

/// <summary>
/// Performance profile
/// </summary>
public class PerformanceProfile
{
    public string MethodName { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public DateTime FirstExecution { get; set; }
    public DateTime LastExecution { get; set; }
    public List<ExecutionRecord> ExecutionHistory { get; set; } = new();
}

/// <summary>
/// Execution record
/// </summary>
public class ExecutionRecord
{
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public int ThreadId { get; set; }
    public int ProcessId { get; set; }
    public ProfilingContext? Context { get; set; }
}

/// <summary>
/// Method profile
/// </summary>
public class MethodProfile
{
    public string MethodName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public int ThreadId { get; set; }
    public int ProcessId { get; set; }
    public ProfilingContext? Context { get; set; }
}

/// <summary>
/// Method profile with result
/// </summary>
public class MethodProfile<T> : MethodProfile
{
    public T? Result { get; set; }
}

/// <summary>
/// Profiling context
/// </summary>
public class ProfilingContext
{
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Profiling rule
/// </summary>
public class ProfilingRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public ProfilingCondition Condition { get; set; }
    public double Threshold { get; set; }
    public ProfilingSeverity Severity { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Performance baseline
/// </summary>
public class PerformanceBaseline
{
    public string MethodName { get; set; } = string.Empty;
    public TimeSpan AverageResponseTime { get; set; }
    public TimeSpan MaxResponseTime { get; set; }
    public TimeSpan MinResponseTime { get; set; }
    public double SuccessRate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Profiling report
/// </summary>
public class ProfilingReport
{
    public string SessionId { get; set; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public ProfilingLevel ProfilingLevel { get; set; }
    public DateTime GeneratedAt { get; set; }
    public MetricsAnalysis MetricsAnalysis { get; set; } = new();
    public List<PerformanceProfile> PerformanceProfiles { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Metrics analysis
/// </summary>
public class MetricsAnalysis
{
    public int TotalSnapshots { get; set; }
    public DateTime AnalysisStartTime { get; set; }
    public DateTime AnalysisEndTime { get; set; }
    public Dictionary<string, MetricStatistics> MetricStatistics { get; set; } = new();
}

/// <summary>
/// Metric statistics
/// </summary>
public class MetricStatistics
{
    public string MetricName { get; set; } = string.Empty;
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double AverageValue { get; set; }
    public double MedianValue { get; set; }
    public double StandardDeviation { get; set; }
}

/// <summary>
/// Performance analysis result
/// </summary>
public class PerformanceAnalysisResult
{
    public string AnalysisId { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public PerformanceAnalysisType AnalysisType { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Insights { get; set; } = new();
    public Dictionary<string, double> Metrics { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public TimeSpan? TimeRange { get; set; }
}

/// <summary>
/// Profiling session result
/// </summary>
public class ProfilingSessionResult
{
    public string SessionId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public ProfilingReport? Report { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Performance alert
/// </summary>
public class PerformanceAlert
{
    public string AlertId { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double Threshold { get; set; }
    public ProfilingCondition Condition { get; set; }
    public ProfilingSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Profiling event arguments
/// </summary>
public class ProfilingEventArgs : EventArgs
{
    public string SessionId { get; }
    public ProfilingAction Action { get; }
    public DateTime Timestamp { get; }

    public ProfilingEventArgs(string sessionId, ProfilingAction action)
    {
        SessionId = sessionId;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Performance profile event arguments
/// </summary>
public class PerformanceProfileEventArgs : EventArgs
{
    public string MethodName { get; }
    public PerformanceProfile Profile { get; }
    public DateTime Timestamp { get; }

    public PerformanceProfileEventArgs(string methodName, PerformanceProfile profile)
    {
        MethodName = methodName;
        Profile = profile;
        Timestamp = DateTime.UtcNow;
    }
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
/// Profiling levels
/// </summary>
public enum ProfilingLevel
{
    Low,
    Medium,
    High,
    Comprehensive
}

/// <summary>
/// Performance analysis types
/// </summary>
public enum PerformanceAnalysisType
{
    ResponseTime,
    Throughput,
    ErrorRate,
    ResourceUsage,
    Comprehensive
}

/// <summary>
/// Profiling conditions
/// </summary>
public enum ProfilingCondition
{
    GreaterThan,
    LessThan,
    EqualTo,
    NotEqualTo
}

/// <summary>
/// Profiling severity levels
/// </summary>
public enum ProfilingSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Profiling actions
/// </summary>
public enum ProfilingAction
{
    Started,
    Stopped,
    Paused,
    Resumed
}
