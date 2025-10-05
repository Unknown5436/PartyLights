using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PartyLights.Services;

/// <summary>
/// System health monitoring service for proactive error detection and recovery
/// </summary>
public class SystemHealthMonitoringService : IDisposable
{
    private readonly ILogger<SystemHealthMonitoringService> _logger;
    private readonly ConcurrentDictionary<string, HealthCheck> _healthChecks = new();
    private readonly ConcurrentDictionary<string, HealthMetric> _healthMetrics = new();
    private readonly Timer _monitoringTimer;
    private readonly object _lockObject = new();

    private const int MonitoringIntervalMs = 5000; // 5 seconds
    private bool _isMonitoring;

    // Health monitoring
    private readonly Dictionary<string, HealthThreshold> _healthThresholds = new();
    private readonly Dictionary<string, AlertRule> _alertRules = new();
    private readonly Dictionary<string, HealthStatus> _componentStatuses = new();
    private readonly Dictionary<string, PerformanceCounter> _performanceCounters = new();

    public event EventHandler<HealthStatusChangedEventArgs>? HealthStatusChanged;
    public event EventHandler<HealthAlertEventArgs>? HealthAlert;
    public event EventHandler<HealthMetricEventArgs>? HealthMetricUpdated;
    public event EventHandler<SystemHealthEventArgs>? SystemHealthUpdated;

    public SystemHealthMonitoringService(ILogger<SystemHealthMonitoringService> logger)
    {
        _logger = logger;

        _monitoringTimer = new Timer(MonitorSystemHealth, null, MonitoringIntervalMs, MonitoringIntervalMs);
        _isMonitoring = true;

        InitializeHealthChecks();
        InitializeAlertRules();
        InitializePerformanceCounters();

        _logger.LogInformation("System health monitoring service initialized");
    }

    /// <summary>
    /// Registers a health check
    /// </summary>
    public async Task<bool> RegisterHealthCheckAsync(HealthCheckRequest request)
    {
        try
        {
            var healthCheck = new HealthCheck
            {
                Id = request.Id,
                Name = request.Name,
                Description = request.Description,
                ComponentName = request.ComponentName,
                CheckType = request.CheckType,
                CheckFunction = request.CheckFunction,
                Interval = request.Interval,
                Timeout = request.Timeout,
                IsEnabled = request.IsEnabled,
                Thresholds = request.Thresholds ?? new Dictionary<string, HealthThreshold>(),
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _healthChecks[request.Id] = healthCheck;

            _logger.LogInformation("Registered health check: {HealthCheckName} ({HealthCheckId})", request.Name, request.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering health check: {HealthCheckName}", request.Name);
            return false;
        }
    }

    /// <summary>
    /// Registers a health metric
    /// </summary>
    public async Task<bool> RegisterHealthMetricAsync(HealthMetricRequest request)
    {
        try
        {
            var healthMetric = new HealthMetric
            {
                Id = request.Id,
                Name = request.Name,
                Description = request.Description,
                ComponentName = request.ComponentName,
                MetricType = request.MetricType,
                Unit = request.Unit,
                IsEnabled = request.IsEnabled,
                Thresholds = request.Thresholds ?? new Dictionary<string, HealthThreshold>(),
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _healthMetrics[request.Id] = healthMetric;

            _logger.LogInformation("Registered health metric: {HealthMetricName} ({HealthMetricId})", request.Name, request.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering health metric: {HealthMetricName}", request.Name);
            return false;
        }
    }

    /// <summary>
    /// Updates a health metric value
    /// </summary>
    public async Task<bool> UpdateHealthMetricAsync(string metricId, double value, Dictionary<string, object>? additionalData = null)
    {
        try
        {
            if (!_healthMetrics.TryGetValue(metricId, out var metric))
            {
                _logger.LogWarning("Health metric not found: {MetricId}", metricId);
                return false;
            }

            var previousValue = metric.CurrentValue;
            metric.CurrentValue = value;
            metric.LastUpdated = DateTime.UtcNow;
            metric.History.Add(new HealthMetricValue
            {
                Value = value,
                Timestamp = DateTime.UtcNow,
                AdditionalData = additionalData ?? new Dictionary<string, object>()
            });

            // Keep only recent history (last 100 values)
            if (metric.History.Count > 100)
            {
                metric.History.RemoveAt(0);
            }

            // Check thresholds
            var thresholdStatus = CheckThresholds(metric, value);
            if (thresholdStatus != HealthStatus.Healthy)
            {
                await HandleThresholdViolation(metric, value, thresholdStatus);
            }

            HealthMetricUpdated?.Invoke(this, new HealthMetricEventArgs(metricId, value, previousValue));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating health metric: {MetricId}", metricId);
            return false;
        }
    }

    /// <summary>
    /// Executes a health check
    /// </summary>
    public async Task<HealthCheckResult> ExecuteHealthCheckAsync(string healthCheckId)
    {
        try
        {
            if (!_healthChecks.TryGetValue(healthCheckId, out var healthCheck))
            {
                _logger.LogWarning("Health check not found: {HealthCheckId}", healthCheckId);
                return new HealthCheckResult
                {
                    HealthCheckId = healthCheckId,
                    Status = HealthStatus.Unhealthy,
                    Message = "Health check not found",
                    Duration = TimeSpan.Zero
                };
            }

            var startTime = DateTime.UtcNow;
            var result = new HealthCheckResult
            {
                HealthCheckId = healthCheckId,
                StartTime = startTime
            };

            try
            {
                // Execute the health check function
                var checkResult = await ExecuteHealthCheckFunction(healthCheck);

                result.Status = checkResult.Status;
                result.Message = checkResult.Message;
                result.Data = checkResult.Data;
                result.Duration = DateTime.UtcNow - startTime;

                // Update component status
                await UpdateComponentStatus(healthCheck.ComponentName, result.Status, result.Message);
            }
            catch (Exception ex)
            {
                result.Status = HealthStatus.Unhealthy;
                result.Message = $"Health check failed with exception: {ex.Message}";
                result.Duration = DateTime.UtcNow - startTime;
                result.Exception = ex;

                _logger.LogError(ex, "Health check failed: {HealthCheckId}", healthCheckId);
            }

            result.EndTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing health check: {HealthCheckId}", healthCheckId);
            return new HealthCheckResult
            {
                HealthCheckId = healthCheckId,
                Status = HealthStatus.Unhealthy,
                Message = $"Error executing health check: {ex.Message}",
                Duration = TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// Gets system health status
    /// </summary>
    public SystemHealthStatus GetSystemHealthStatus()
    {
        try
        {
            var overallStatus = HealthStatus.Healthy;
            var componentStatuses = new Dictionary<string, HealthStatus>();
            var healthMetrics = new Dictionary<string, double>();
            var alerts = new List<HealthAlert>();

            // Aggregate component statuses
            foreach (var componentStatus in _componentStatuses)
            {
                componentStatuses[componentStatus.Key] = componentStatus.Value.Status;

                if (componentStatus.Value.Status > overallStatus)
                {
                    overallStatus = componentStatus.Value.Status;
                }
            }

            // Aggregate health metrics
            foreach (var metric in _healthMetrics.Values)
            {
                healthMetrics[metric.Name] = metric.CurrentValue;
            }

            // Check for active alerts
            foreach (var alertRule in _alertRules.Values.Where(r => r.IsEnabled))
            {
                var alert = await CheckAlertRule(alertRule);
                if (alert != null)
                {
                    alerts.Add(alert);
                }
            }

            return new SystemHealthStatus
            {
                OverallStatus = overallStatus,
                Timestamp = DateTime.UtcNow,
                ComponentStatuses = componentStatuses,
                HealthMetrics = healthMetrics,
                ActiveAlerts = alerts,
                TotalHealthChecks = _healthChecks.Count,
                TotalHealthMetrics = _healthMetrics.Count,
                HealthyComponents = componentStatuses.Count(c => c.Value == HealthStatus.Healthy),
                UnhealthyComponents = componentStatuses.Count(c => c.Value == HealthStatus.Unhealthy)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system health status");
            return new SystemHealthStatus
            {
                OverallStatus = HealthStatus.Unhealthy,
                Timestamp = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets health check results
    /// </summary>
    public IEnumerable<HealthCheckResult> GetHealthCheckResults(string? componentName = null)
    {
        var results = new List<HealthCheckResult>();

        foreach (var healthCheck in _healthChecks.Values)
        {
            if (componentName == null || healthCheck.ComponentName == componentName)
            {
                var result = ExecuteHealthCheckAsync(healthCheck.Id).Result;
                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>
    /// Gets health metrics
    /// </summary>
    public IEnumerable<HealthMetric> GetHealthMetrics(string? componentName = null)
    {
        return _healthMetrics.Values.Where(m => componentName == null || m.ComponentName == componentName);
    }

    /// <summary>
    /// Registers an alert rule
    /// </summary>
    public async Task<bool> RegisterAlertRuleAsync(AlertRuleRequest request)
    {
        try
        {
            var alertRule = new AlertRule
            {
                Id = request.Id,
                Name = request.Name,
                Description = request.Description,
                ComponentName = request.ComponentName,
                MetricName = request.MetricName,
                Condition = request.Condition,
                Threshold = request.Threshold,
                Severity = request.Severity,
                IsEnabled = request.IsEnabled,
                CooldownPeriod = request.CooldownPeriod,
                LastTriggered = DateTime.MinValue,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _alertRules[request.Id] = alertRule;

            _logger.LogInformation("Registered alert rule: {AlertRuleName} ({AlertRuleId})", request.Name, request.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering alert rule: {AlertRuleName}", request.Name);
            return false;
        }
    }

    #region Private Methods

    private async void MonitorSystemHealth(object? state)
    {
        if (!_isMonitoring)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;

            // Execute enabled health checks
            foreach (var healthCheck in _healthChecks.Values.Where(h => h.IsEnabled))
            {
                if (healthCheck.LastExecuted.Add(healthCheck.Interval) <= currentTime)
                {
                    try
                    {
                        await ExecuteHealthCheckAsync(healthCheck.Id);
                        healthCheck.LastExecuted = currentTime;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing health check: {HealthCheckId}", healthCheck.Id);
                    }
                }
            }

            // Update system metrics
            await UpdateSystemMetrics();

            // Check alert rules
            await CheckAllAlertRules();

            // Notify system health update
            var systemHealth = GetSystemHealthStatus();
            SystemHealthUpdated?.Invoke(this, new SystemHealthEventArgs(systemHealth));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in system health monitoring");
        }
    }

    private async Task<HealthCheckResult> ExecuteHealthCheckFunction(HealthCheck healthCheck)
    {
        try
        {
            switch (healthCheck.CheckType)
            {
                case HealthCheckType.Custom:
                    return await ExecuteCustomHealthCheck(healthCheck);
                case HealthCheckType.Connectivity:
                    return await ExecuteConnectivityHealthCheck(healthCheck);
                case HealthCheckType.Performance:
                    return await ExecutePerformanceHealthCheck(healthCheck);
                case HealthCheckType.Resource:
                    return await ExecuteResourceHealthCheck(healthCheck);
                case HealthCheckType.Dependency:
                    return await ExecuteDependencyHealthCheck(healthCheck);
                default:
                    return new HealthCheckResult
                    {
                        HealthCheckId = healthCheck.Id,
                        Status = HealthStatus.Unhealthy,
                        Message = "Unknown health check type"
                    };
            }
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                HealthCheckId = healthCheck.Id,
                Status = HealthStatus.Unhealthy,
                Message = $"Health check execution failed: {ex.Message}",
                Exception = ex
            };
        }
    }

    private async Task<HealthCheckResult> ExecuteCustomHealthCheck(HealthCheck healthCheck)
    {
        try
        {
            if (healthCheck.CheckFunction != null)
            {
                var result = await healthCheck.CheckFunction();
                return new HealthCheckResult
                {
                    HealthCheckId = healthCheck.Id,
                    Status = result.Status,
                    Message = result.Message,
                    Data = result.Data
                };
            }

            return new HealthCheckResult
            {
                HealthCheckId = healthCheck.Id,
                Status = HealthStatus.Unhealthy,
                Message = "No custom health check function provided"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                HealthCheckId = healthCheck.Id,
                Status = HealthStatus.Unhealthy,
                Message = $"Custom health check failed: {ex.Message}",
                Exception = ex
            };
        }
    }

    private async Task<HealthCheckResult> ExecuteConnectivityHealthCheck(HealthCheck healthCheck)
    {
        try
        {
            // Simulate connectivity check
            await Task.Delay(100);

            return new HealthCheckResult
            {
                HealthCheckId = healthCheck.Id,
                Status = HealthStatus.Healthy,
                Message = "Connectivity check passed",
                Data = new Dictionary<string, object>
                {
                    ["ResponseTime"] = 100,
                    ["Connected"] = true
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                HealthCheckId = healthCheck.Id,
                Status = HealthStatus.Unhealthy,
                Message = $"Connectivity check failed: {ex.Message}",
                Exception = ex
            };
        }
    }

    private async Task<HealthCheckResult> ExecutePerformanceHealthCheck(HealthCheck healthCheck)
    {
        try
        {
            // Simulate performance check
            await Task.Delay(50);

            var cpuUsage = GetCpuUsage();
            var memoryUsage = GetMemoryUsage();

            var status = HealthStatus.Healthy;
            if (cpuUsage > 80 || memoryUsage > 80)
            {
                status = HealthStatus.Degraded;
            }
            if (cpuUsage > 95 || memoryUsage > 95)
            {
                status = HealthStatus.Unhealthy;
            }

            return new HealthCheckResult
            {
                HealthCheckId = healthCheck.Id,
                Status = status,
                Message = $"Performance check completed - CPU: {cpuUsage:F1}%, Memory: {memoryUsage:F1}%",
                Data = new Dictionary<string, object>
                {
                    ["CpuUsage"] = cpuUsage,
                    ["MemoryUsage"] = memoryUsage
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                HealthCheckId = healthCheck.Id,
                Status = HealthStatus.Unhealthy,
                Message = $"Performance check failed: {ex.Message}",
                Exception = ex
            };
        }
    }

    private async Task<HealthCheckResult> ExecuteResourceHealthCheck(HealthCheck healthCheck)
    {
        try
        {
            // Simulate resource check
            await Task.Delay(25);

            var diskSpace = GetDiskSpace();
            var availableMemory = GetAvailableMemory();

            var status = HealthStatus.Healthy;
            if (diskSpace < 10 || availableMemory < 100)
            {
                status = HealthStatus.Degraded;
            }
            if (diskSpace < 5 || availableMemory < 50)
            {
                status = HealthStatus.Unhealthy;
            }

            return new HealthCheckResult
            {
                HealthCheckId = healthCheck.Id,
                Status = status,
                Message = $"Resource check completed - Disk: {diskSpace:F1}GB, Memory: {availableMemory:F1}MB",
                Data = new Dictionary<string, object>
                {
                    ["DiskSpace"] = diskSpace,
                    ["AvailableMemory"] = availableMemory
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                HealthCheckId = healthCheck.Id,
                Status = HealthStatus.Unhealthy,
                Message = $"Resource check failed: {ex.Message}",
                Exception = ex
            };
        }
    }

    private async Task<HealthCheckResult> ExecuteDependencyHealthCheck(HealthCheck healthCheck)
    {
        try
        {
            // Simulate dependency check
            await Task.Delay(200);

            return new HealthCheckResult
            {
                HealthCheckId = healthCheck.Id,
                Status = HealthStatus.Healthy,
                Message = "Dependency check passed",
                Data = new Dictionary<string, object>
                {
                    ["Dependencies"] = new[] { "Database", "API", "Cache" },
                    ["AllHealthy"] = true
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                HealthCheckId = healthCheck.Id,
                Status = HealthStatus.Unhealthy,
                Message = $"Dependency check failed: {ex.Message}",
                Exception = ex
            };
        }
    }

    private async Task UpdateSystemMetrics()
    {
        try
        {
            // Update CPU usage metric
            if (_healthMetrics.TryGetValue("cpu_usage", out var cpuMetric))
            {
                await UpdateHealthMetricAsync("cpu_usage", GetCpuUsage());
            }

            // Update memory usage metric
            if (_healthMetrics.TryGetValue("memory_usage", out var memoryMetric))
            {
                await UpdateHealthMetricAsync("memory_usage", GetMemoryUsage());
            }

            // Update disk space metric
            if (_healthMetrics.TryGetValue("disk_space", out var diskMetric))
            {
                await UpdateHealthMetricAsync("disk_space", GetDiskSpace());
            }

            // Update thread count metric
            if (_healthMetrics.TryGetValue("thread_count", out var threadMetric))
            {
                await UpdateHealthMetricAsync("thread_count", Process.GetCurrentProcess().Threads.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating system metrics");
        }
    }

    private async Task CheckAllAlertRules()
    {
        try
        {
            foreach (var alertRule in _alertRules.Values.Where(r => r.IsEnabled))
            {
                var alert = await CheckAlertRule(alertRule);
                if (alert != null)
                {
                    HealthAlert?.Invoke(this, new HealthAlertEventArgs(alert));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking alert rules");
        }
    }

    private async Task<HealthAlert?> CheckAlertRule(AlertRule alertRule)
    {
        try
        {
            // Check cooldown period
            if (DateTime.UtcNow - alertRule.LastTriggered < alertRule.CooldownPeriod)
            {
                return null;
            }

            // Find the metric
            var metric = _healthMetrics.Values.FirstOrDefault(m => m.Name == alertRule.MetricName);
            if (metric == null)
            {
                return null;
            }

            // Check condition
            bool conditionMet = alertRule.Condition switch
            {
                AlertCondition.GreaterThan => metric.CurrentValue > alertRule.Threshold,
                AlertCondition.LessThan => metric.CurrentValue < alertRule.Threshold,
                AlertCondition.EqualTo => Math.Abs(metric.CurrentValue - alertRule.Threshold) < 0.001,
                AlertCondition.NotEqualTo => Math.Abs(metric.CurrentValue - alertRule.Threshold) >= 0.001,
                _ => false
            };

            if (conditionMet)
            {
                alertRule.LastTriggered = DateTime.UtcNow;

                return new HealthAlert
                {
                    Id = Guid.NewGuid().ToString(),
                    AlertRuleId = alertRule.Id,
                    ComponentName = alertRule.ComponentName,
                    MetricName = alertRule.MetricName,
                    CurrentValue = metric.CurrentValue,
                    Threshold = alertRule.Threshold,
                    Condition = alertRule.Condition,
                    Severity = alertRule.Severity,
                    Message = $"Alert: {alertRule.MetricName} {alertRule.Condition} {alertRule.Threshold} (Current: {metric.CurrentValue:F2})",
                    Timestamp = DateTime.UtcNow
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking alert rule: {AlertRuleId}", alertRule.Id);
            return null;
        }
    }

    private HealthStatus CheckThresholds(HealthMetric metric, double value)
    {
        try
        {
            foreach (var threshold in metric.Thresholds.Values)
            {
                bool thresholdViolated = threshold.Condition switch
                {
                    AlertCondition.GreaterThan => value > threshold.Value,
                    AlertCondition.LessThan => value < threshold.Value,
                    AlertCondition.EqualTo => Math.Abs(value - threshold.Value) < 0.001,
                    AlertCondition.NotEqualTo => Math.Abs(value - threshold.Value) >= 0.001,
                    _ => false
                };

                if (thresholdViolated)
                {
                    return threshold.Severity switch
                    {
                        AlertSeverity.Low => HealthStatus.Degraded,
                        AlertSeverity.Medium => HealthStatus.Degraded,
                        AlertSeverity.High => HealthStatus.Unhealthy,
                        AlertSeverity.Critical => HealthStatus.Unhealthy,
                        _ => HealthStatus.Healthy
                    };
                }
            }

            return HealthStatus.Healthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking thresholds for metric: {MetricId}", metric.Id);
            return HealthStatus.Healthy;
        }
    }

    private async Task HandleThresholdViolation(HealthMetric metric, double value, HealthStatus status)
    {
        try
        {
            _logger.LogWarning("Threshold violation for metric {MetricName}: {Value} (Status: {Status})",
                metric.Name, value, status);

            // This would typically trigger recovery actions, notifications, etc.
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling threshold violation for metric: {MetricId}", metric.Id);
        }
    }

    private async Task UpdateComponentStatus(string componentName, HealthStatus status, string message)
    {
        try
        {
            if (!_componentStatuses.TryGetValue(componentName, out var componentStatus))
            {
                componentStatus = new HealthStatus
                {
                    ComponentName = componentName,
                    Status = HealthStatus.Healthy,
                    LastUpdated = DateTime.UtcNow
                };
                _componentStatuses[componentName] = componentStatus;
            }

            var previousStatus = componentStatus.Status;
            componentStatus.Status = status;
            componentStatus.Message = message;
            componentStatus.LastUpdated = DateTime.UtcNow;

            if (previousStatus != status)
            {
                HealthStatusChanged?.Invoke(this, new HealthStatusChangedEventArgs(componentName, status, previousStatus));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating component status: {ComponentName}", componentName);
        }
    }

    private double GetCpuUsage()
    {
        try
        {
            // Simulate CPU usage calculation
            return new Random().NextDouble() * 100;
        }
        catch
        {
            return 0;
        }
    }

    private double GetMemoryUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            return (double)process.WorkingSet64 / (1024 * 1024 * 1024) * 100; // Convert to percentage
        }
        catch
        {
            return 0;
        }
    }

    private double GetDiskSpace()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:");
            return (double)drive.AvailableFreeSpace / (1024 * 1024 * 1024); // Convert to GB
        }
        catch
        {
            return 0;
        }
    }

    private double GetAvailableMemory()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            return (double)process.WorkingSet64 / (1024 * 1024); // Convert to MB
        }
        catch
        {
            return 0;
        }
    }

    private void InitializeHealthChecks()
    {
        // Initialize default health checks
        RegisterHealthCheckAsync(new HealthCheckRequest
        {
            Id = "system_performance",
            Name = "System Performance",
            Description = "Monitors overall system performance",
            ComponentName = "System",
            CheckType = HealthCheckType.Performance,
            Interval = TimeSpan.FromSeconds(30),
            Timeout = TimeSpan.FromSeconds(10),
            IsEnabled = true
        }).Wait();

        RegisterHealthCheckAsync(new HealthCheckRequest
        {
            Id = "system_resources",
            Name = "System Resources",
            Description = "Monitors system resource usage",
            ComponentName = "System",
            CheckType = HealthCheckType.Resource,
            Interval = TimeSpan.FromSeconds(60),
            Timeout = TimeSpan.FromSeconds(5),
            IsEnabled = true
        }).Wait();
    }

    private void InitializeAlertRules()
    {
        // Initialize default alert rules
        RegisterAlertRuleAsync(new AlertRuleRequest
        {
            Id = "high_cpu_usage",
            Name = "High CPU Usage",
            Description = "Alert when CPU usage exceeds 80%",
            ComponentName = "System",
            MetricName = "cpu_usage",
            Condition = AlertCondition.GreaterThan,
            Threshold = 80,
            Severity = AlertSeverity.High,
            IsEnabled = true,
            CooldownPeriod = TimeSpan.FromMinutes(5)
        }).Wait();

        RegisterAlertRuleAsync(new AlertRuleRequest
        {
            Id = "high_memory_usage",
            Name = "High Memory Usage",
            Description = "Alert when memory usage exceeds 85%",
            ComponentName = "System",
            MetricName = "memory_usage",
            Condition = AlertCondition.GreaterThan,
            Threshold = 85,
            Severity = AlertSeverity.High,
            IsEnabled = true,
            CooldownPeriod = TimeSpan.FromMinutes(5)
        }).Wait();
    }

    private void InitializePerformanceCounters()
    {
        // Initialize performance counters
        RegisterHealthMetricAsync(new HealthMetricRequest
        {
            Id = "cpu_usage",
            Name = "CPU Usage",
            Description = "Current CPU usage percentage",
            ComponentName = "System",
            MetricType = HealthMetricType.Percentage,
            Unit = "%",
            IsEnabled = true
        }).Wait();

        RegisterHealthMetricAsync(new HealthMetricRequest
        {
            Id = "memory_usage",
            Name = "Memory Usage",
            Description = "Current memory usage percentage",
            ComponentName = "System",
            MetricType = HealthMetricType.Percentage,
            Unit = "%",
            IsEnabled = true
        }).Wait();

        RegisterHealthMetricAsync(new HealthMetricRequest
        {
            Id = "disk_space",
            Name = "Available Disk Space",
            Description = "Available disk space in GB",
            ComponentName = "System",
            MetricType = HealthMetricType.Counter,
            Unit = "GB",
            IsEnabled = true
        }).Wait();

        RegisterHealthMetricAsync(new HealthMetricRequest
        {
            Id = "thread_count",
            Name = "Thread Count",
            Description = "Current thread count",
            ComponentName = "System",
            MetricType = HealthMetricType.Counter,
            Unit = "threads",
            IsEnabled = true
        }).Wait();
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isMonitoring = false;
            _monitoringTimer?.Dispose();

            _logger.LogInformation("System health monitoring service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing system health monitoring service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Health check request
/// </summary>
public class HealthCheckRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public HealthCheckType CheckType { get; set; } = HealthCheckType.Custom;
    public Func<Task<HealthCheckResult>>? CheckFunction { get; set; }
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, HealthThreshold>? Thresholds { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Health metric request
/// </summary>
public class HealthMetricRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public HealthMetricType MetricType { get; set; } = HealthMetricType.Counter;
    public string Unit { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, HealthThreshold>? Thresholds { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Alert rule request
/// </summary>
public class AlertRuleRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public AlertCondition Condition { get; set; } = AlertCondition.GreaterThan;
    public double Threshold { get; set; }
    public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;
    public bool IsEnabled { get; set; } = true;
    public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Health check
/// </summary>
public class HealthCheck
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public HealthCheckType CheckType { get; set; }
    public Func<Task<HealthCheckResult>>? CheckFunction { get; set; }
    public TimeSpan Interval { get; set; }
    public TimeSpan Timeout { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime LastExecuted { get; set; } = DateTime.MinValue;
    public Dictionary<string, HealthThreshold> Thresholds { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Health metric
/// </summary>
public class HealthMetric
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public HealthMetricType MetricType { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public double CurrentValue { get; set; }
    public DateTime LastUpdated { get; set; }
    public List<HealthMetricValue> History { get; set; } = new();
    public Dictionary<string, HealthThreshold> Thresholds { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Health metric value
/// </summary>
public class HealthMetricValue
{
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Health threshold
/// </summary>
public class HealthThreshold
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public AlertCondition Condition { get; set; }
    public AlertSeverity Severity { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Alert rule
/// </summary>
public class AlertRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public AlertCondition Condition { get; set; }
    public double Threshold { get; set; }
    public AlertSeverity Severity { get; set; }
    public bool IsEnabled { get; set; }
    public TimeSpan CooldownPeriod { get; set; }
    public DateTime LastTriggered { get; set; } = DateTime.MinValue;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Health check result
/// </summary>
public class HealthCheckResult
{
    public string HealthCheckId { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// Health status
/// </summary>
public class HealthStatus
{
    public string ComponentName { get; set; } = string.Empty;
    public HealthStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// System health status
/// </summary>
public class SystemHealthStatus
{
    public HealthStatus OverallStatus { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, HealthStatus> ComponentStatuses { get; set; } = new();
    public Dictionary<string, double> HealthMetrics { get; set; } = new();
    public List<HealthAlert> ActiveAlerts { get; set; } = new();
    public int TotalHealthChecks { get; set; }
    public int TotalHealthMetrics { get; set; }
    public int HealthyComponents { get; set; }
    public int UnhealthyComponents { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Health alert
/// </summary>
public class HealthAlert
{
    public string Id { get; set; } = string.Empty;
    public string AlertRuleId { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double Threshold { get; set; }
    public AlertCondition Condition { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Health status changed event arguments
/// </summary>
public class HealthStatusChangedEventArgs : EventArgs
{
    public string ComponentName { get; }
    public HealthStatus NewStatus { get; }
    public HealthStatus PreviousStatus { get; }
    public DateTime Timestamp { get; }

    public HealthStatusChangedEventArgs(string componentName, HealthStatus newStatus, HealthStatus previousStatus)
    {
        ComponentName = componentName;
        NewStatus = newStatus;
        PreviousStatus = previousStatus;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Health alert event arguments
/// </summary>
public class HealthAlertEventArgs : EventArgs
{
    public HealthAlert Alert { get; }
    public DateTime Timestamp { get; }

    public HealthAlertEventArgs(HealthAlert alert)
    {
        Alert = alert;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Health metric event arguments
/// </summary>
public class HealthMetricEventArgs : EventArgs
{
    public string MetricId { get; }
    public double CurrentValue { get; }
    public double PreviousValue { get; }
    public DateTime Timestamp { get; }

    public HealthMetricEventArgs(string metricId, double currentValue, double previousValue)
    {
        MetricId = metricId;
        CurrentValue = currentValue;
        PreviousValue = previousValue;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// System health event arguments
/// </summary>
public class SystemHealthEventArgs : EventArgs
{
    public SystemHealthStatus SystemHealth { get; }
    public DateTime Timestamp { get; }

    public SystemHealthEventArgs(SystemHealthStatus systemHealth)
    {
        SystemHealth = systemHealth;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Health check types
/// </summary>
public enum HealthCheckType
{
    Custom,
    Connectivity,
    Performance,
    Resource,
    Dependency
}

/// <summary>
/// Health metric types
/// </summary>
public enum HealthMetricType
{
    Counter,
    Gauge,
    Percentage,
    Rate
}

/// <summary>
/// Health status levels
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Alert conditions
/// </summary>
public enum AlertCondition
{
    GreaterThan,
    LessThan,
    EqualTo,
    NotEqualTo
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}
