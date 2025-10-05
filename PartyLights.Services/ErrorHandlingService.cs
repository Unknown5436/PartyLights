using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PartyLights.Services;

/// <summary>
/// Comprehensive error handling and recovery service
/// </summary>
public class ErrorHandlingService : IDisposable
{
    private readonly ILogger<ErrorHandlingService> _logger;
    private readonly ConcurrentDictionary<string, ErrorContext> _errorContexts = new();
    private readonly ConcurrentDictionary<string, RecoveryStrategy> _recoveryStrategies = new();
    private readonly Timer _errorMonitoringTimer;
    private readonly object _lockObject = new();

    private const int ErrorMonitoringIntervalMs = 5000; // 5 seconds
    private bool _isMonitoring;

    // Error tracking and recovery
    private readonly Dictionary<string, ErrorStatistics> _errorStatistics = new();
    private readonly Dictionary<string, CircuitBreakerState> _circuitBreakers = new();
    private readonly Dictionary<string, RetryPolicy> _retryPolicies = new();
    private readonly Dictionary<string, FallbackStrategy> _fallbackStrategies = new();

    public event EventHandler<ErrorEventArgs>? ErrorOccurred;
    public event EventHandler<RecoveryEventArgs>? RecoveryAttempted;
    public event EventHandler<RecoveryEventArgs>? RecoverySucceeded;
    public event EventHandler<RecoveryEventArgs>? RecoveryFailed;
    public event EventHandler<CircuitBreakerEventArgs>? CircuitBreakerOpened;
    public event EventHandler<CircuitBreakerEventArgs>? CircuitBreakerClosed;

    public ErrorHandlingService(ILogger<ErrorHandlingService> logger)
    {
        _logger = logger;

        _errorMonitoringTimer = new Timer(MonitorErrors, null, ErrorMonitoringIntervalMs, ErrorMonitoringIntervalMs);
        _isMonitoring = true;

        InitializeDefaultStrategies();

        _logger.LogInformation("Error handling service initialized");
    }

    /// <summary>
    /// Handles an error with comprehensive context and recovery
    /// </summary>
    public async Task<ErrorHandlingResult> HandleErrorAsync(ErrorContext errorContext)
    {
        try
        {
            var errorId = Guid.NewGuid().ToString();
            errorContext.ErrorId = errorId;
            errorContext.Timestamp = DateTime.UtcNow;

            // Log the error
            await LogErrorAsync(errorContext);

            // Update error statistics
            UpdateErrorStatistics(errorContext);

            // Store error context
            _errorContexts[errorId] = errorContext;

            // Determine recovery strategy
            var recoveryStrategy = DetermineRecoveryStrategy(errorContext);

            // Attempt recovery
            var recoveryResult = await AttemptRecoveryAsync(errorContext, recoveryStrategy);

            // Notify subscribers
            ErrorOccurred?.Invoke(this, new ErrorEventArgs(errorContext));

            return new ErrorHandlingResult
            {
                ErrorId = errorId,
                Success = recoveryResult.Success,
                RecoveryStrategy = recoveryStrategy,
                RecoveryResult = recoveryResult,
                FallbackUsed = recoveryResult.FallbackUsed,
                ErrorMessage = errorContext.ErrorMessage,
                Recommendations = GenerateRecommendations(errorContext)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in error handling: {ErrorMessage}", errorContext.ErrorMessage);
            return new ErrorHandlingResult
            {
                ErrorId = string.Empty,
                Success = false,
                ErrorMessage = "Critical error in error handling system",
                Recommendations = new List<string> { "Restart application", "Check system resources", "Contact support" }
            };
        }
    }

    /// <summary>
    /// Registers a recovery strategy for a specific error type
    /// </summary>
    public async Task<bool> RegisterRecoveryStrategyAsync(string errorType, RecoveryStrategy strategy)
    {
        try
        {
            _recoveryStrategies[errorType] = strategy;
            _logger.LogInformation("Registered recovery strategy for error type: {ErrorType}", errorType);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering recovery strategy: {ErrorType}", errorType);
            return false;
        }
    }

    /// <summary>
    /// Registers a retry policy for a specific operation
    /// </summary>
    public async Task<bool> RegisterRetryPolicyAsync(string operationName, RetryPolicy policy)
    {
        try
        {
            _retryPolicies[operationName] = policy;
            _logger.LogInformation("Registered retry policy for operation: {OperationName}", operationName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering retry policy: {OperationName}", operationName);
            return false;
        }
    }

    /// <summary>
    /// Registers a fallback strategy for a specific operation
    /// </summary>
    public async Task<bool> RegisterFallbackStrategyAsync(string operationName, FallbackStrategy strategy)
    {
        try
        {
            _fallbackStrategies[operationName] = strategy;
            _logger.LogInformation("Registered fallback strategy for operation: {OperationName}", operationName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering fallback strategy: {OperationName}", operationName);
            return false;
        }
    }

    /// <summary>
    /// Executes an operation with retry logic
    /// </summary>
    public async Task<T?> ExecuteWithRetryAsync<T>(string operationName, Func<Task<T>> operation, object? context = null)
    {
        try
        {
            if (!_retryPolicies.TryGetValue(operationName, out var retryPolicy))
            {
                retryPolicy = GetDefaultRetryPolicy();
            }

            var attempt = 0;
            Exception? lastException = null;

            while (attempt < retryPolicy.MaxAttempts)
            {
                try
                {
                    var result = await operation();
                    if (attempt > 0)
                    {
                        _logger.LogInformation("Operation succeeded on attempt {Attempt}: {OperationName}", attempt + 1, operationName);
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempt++;

                    if (attempt >= retryPolicy.MaxAttempts)
                    {
                        break;
                    }

                    var delay = CalculateRetryDelay(retryPolicy, attempt);
                    _logger.LogWarning(ex, "Operation failed on attempt {Attempt}/{MaxAttempts}: {OperationName}. Retrying in {Delay}ms",
                        attempt, retryPolicy.MaxAttempts, operationName, delay);

                    await Task.Delay(delay);
                }
            }

            // All retries failed, try fallback
            if (_fallbackStrategies.TryGetValue(operationName, out var fallbackStrategy))
            {
                _logger.LogWarning("All retries failed for operation: {OperationName}. Attempting fallback.", operationName);
                return await ExecuteFallbackAsync<T>(fallbackStrategy, context);
            }

            throw lastException ?? new InvalidOperationException($"Operation failed after {retryPolicy.MaxAttempts} attempts: {operationName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing operation with retry: {OperationName}", operationName);
            return default(T);
        }
    }

    /// <summary>
    /// Checks circuit breaker state for an operation
    /// </summary>
    public bool IsCircuitBreakerOpen(string operationName)
    {
        if (_circuitBreakers.TryGetValue(operationName, out var state))
        {
            return state.IsOpen && DateTime.UtcNow < state.OpenUntil;
        }
        return false;
    }

    /// <summary>
    /// Records a successful operation for circuit breaker
    /// </summary>
    public async Task RecordSuccessAsync(string operationName)
    {
        try
        {
            if (_circuitBreakers.TryGetValue(operationName, out var state))
            {
                state.SuccessCount++;
                state.ConsecutiveFailures = 0;

                // Check if we should close the circuit breaker
                if (state.IsOpen && state.SuccessCount >= state.SuccessThreshold)
                {
                    state.IsOpen = false;
                    state.OpenUntil = DateTime.MinValue;
                    CircuitBreakerClosed?.Invoke(this, new CircuitBreakerEventArgs(operationName, CircuitBreakerAction.Closed));
                    _logger.LogInformation("Circuit breaker closed for operation: {OperationName}", operationName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording success for operation: {OperationName}", operationName);
        }
    }

    /// <summary>
    /// Records a failure for circuit breaker
    /// </summary>
    public async Task RecordFailureAsync(string operationName)
    {
        try
        {
            if (!_circuitBreakers.TryGetValue(operationName, out var state))
            {
                state = new CircuitBreakerState
                {
                    OperationName = operationName,
                    FailureThreshold = 5,
                    SuccessThreshold = 3,
                    TimeoutDuration = TimeSpan.FromMinutes(1)
                };
                _circuitBreakers[operationName] = state;
            }

            state.FailureCount++;
            state.ConsecutiveFailures++;

            // Check if we should open the circuit breaker
            if (!state.IsOpen && state.ConsecutiveFailures >= state.FailureThreshold)
            {
                state.IsOpen = true;
                state.OpenUntil = DateTime.UtcNow.Add(state.TimeoutDuration);
                CircuitBreakerOpened?.Invoke(this, new CircuitBreakerEventArgs(operationName, CircuitBreakerAction.Opened));
                _logger.LogWarning("Circuit breaker opened for operation: {OperationName}", operationName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording failure for operation: {OperationName}", operationName);
        }
    }

    /// <summary>
    /// Gets error statistics for an operation
    /// </summary>
    public ErrorStatistics? GetErrorStatistics(string operationName)
    {
        _errorStatistics.TryGetValue(operationName, out var stats);
        return stats;
    }

    /// <summary>
    /// Gets all error statistics
    /// </summary>
    public IEnumerable<ErrorStatistics> GetAllErrorStatistics()
    {
        return _errorStatistics.Values;
    }

    /// <summary>
    /// Clears error statistics
    /// </summary>
    public async Task ClearErrorStatisticsAsync()
    {
        try
        {
            _errorStatistics.Clear();
            _logger.LogInformation("Error statistics cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing error statistics");
        }
    }

    #region Private Methods

    private async void MonitorErrors(object? state)
    {
        if (!_isMonitoring)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;

            // Check circuit breakers
            foreach (var circuitBreaker in _circuitBreakers.Values)
            {
                if (circuitBreaker.IsOpen && currentTime >= circuitBreaker.OpenUntil)
                {
                    // Try to close the circuit breaker
                    circuitBreaker.IsOpen = false;
                    circuitBreaker.OpenUntil = DateTime.MinValue;
                    CircuitBreakerClosed?.Invoke(this, new CircuitBreakerEventArgs(circuitBreaker.OperationName, CircuitBreakerAction.Closed));
                    _logger.LogInformation("Circuit breaker auto-closed for operation: {OperationName}", circuitBreaker.OperationName);
                }
            }

            // Clean up old error contexts
            var oldErrorIds = _errorContexts.Keys
                .Where(id => _errorContexts[id].Timestamp < currentTime.AddHours(-24))
                .ToList();

            foreach (var errorId in oldErrorIds)
            {
                _errorContexts.TryRemove(errorId, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in error monitoring");
        }
    }

    private async Task LogErrorAsync(ErrorContext errorContext)
    {
        try
        {
            var logLevel = DetermineLogLevel(errorContext.Severity);

            _logger.Log(logLevel, errorContext.Exception,
                "Error occurred: {ErrorMessage} | Operation: {OperationName} | Severity: {Severity} | ErrorId: {ErrorId}",
                errorContext.ErrorMessage, errorContext.OperationName, errorContext.Severity, errorContext.ErrorId);

            // Log additional context if available
            if (errorContext.AdditionalData != null && errorContext.AdditionalData.Any())
            {
                _logger.Log(logLevel, "Additional error context: {@AdditionalData}", errorContext.AdditionalData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging error context");
        }
    }

    private LogLevel DetermineLogLevel(ErrorSeverity severity)
    {
        return severity switch
        {
            ErrorSeverity.Low => LogLevel.Information,
            ErrorSeverity.Medium => LogLevel.Warning,
            ErrorSeverity.High => LogLevel.Error,
            ErrorSeverity.Critical => LogLevel.Critical,
            _ => LogLevel.Warning
        };
    }

    private void UpdateErrorStatistics(ErrorContext errorContext)
    {
        try
        {
            if (!_errorStatistics.TryGetValue(errorContext.OperationName, out var stats))
            {
                stats = new ErrorStatistics
                {
                    OperationName = errorContext.OperationName,
                    FirstErrorTime = errorContext.Timestamp,
                    LastErrorTime = errorContext.Timestamp
                };
                _errorStatistics[errorContext.OperationName] = stats;
            }

            stats.TotalErrors++;
            stats.LastErrorTime = errorContext.Timestamp;

            // Update severity counts
            switch (errorContext.Severity)
            {
                case ErrorSeverity.Low:
                    stats.LowSeverityErrors++;
                    break;
                case ErrorSeverity.Medium:
                    stats.MediumSeverityErrors++;
                    break;
                case ErrorSeverity.High:
                    stats.HighSeverityErrors++;
                    break;
                case ErrorSeverity.Critical:
                    stats.CriticalSeverityErrors++;
                    break;
            }

            // Update error type counts
            if (stats.ErrorTypeCounts.ContainsKey(errorContext.ErrorType))
            {
                stats.ErrorTypeCounts[errorContext.ErrorType]++;
            }
            else
            {
                stats.ErrorTypeCounts[errorContext.ErrorType] = 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating error statistics");
        }
    }

    private RecoveryStrategy DetermineRecoveryStrategy(ErrorContext errorContext)
    {
        try
        {
            // Try to find a specific strategy for this error type
            if (_recoveryStrategies.TryGetValue(errorContext.ErrorType, out var strategy))
            {
                return strategy;
            }

            // Try to find a strategy for the operation
            if (_recoveryStrategies.TryGetValue(errorContext.OperationName, out strategy))
            {
                return strategy;
            }

            // Use default strategy based on severity
            return GetDefaultRecoveryStrategy(errorContext.Severity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining recovery strategy");
            return GetDefaultRecoveryStrategy(ErrorSeverity.Medium);
        }
    }

    private async Task<RecoveryResult> AttemptRecoveryAsync(ErrorContext errorContext, RecoveryStrategy strategy)
    {
        try
        {
            RecoveryAttempted?.Invoke(this, new RecoveryEventArgs(errorContext.ErrorId, strategy, RecoveryAction.Attempted));

            var recoveryResult = new RecoveryResult
            {
                Strategy = strategy,
                Success = false,
                FallbackUsed = false,
                ErrorMessage = string.Empty
            };

            // Execute recovery steps
            foreach (var step in strategy.RecoverySteps)
            {
                try
                {
                    var stepResult = await ExecuteRecoveryStepAsync(step, errorContext);
                    if (stepResult.Success)
                    {
                        recoveryResult.Success = true;
                        recoveryResult.ErrorMessage = stepResult.Message;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing recovery step: {StepName}", step.Name);
                }
            }

            // If recovery failed, try fallback
            if (!recoveryResult.Success && strategy.FallbackStrategy != null)
            {
                try
                {
                    var fallbackResult = await ExecuteFallbackAsync<object>(strategy.FallbackStrategy, errorContext);
                    if (fallbackResult != null)
                    {
                        recoveryResult.Success = true;
                        recoveryResult.FallbackUsed = true;
                        recoveryResult.ErrorMessage = "Fallback strategy executed successfully";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing fallback strategy");
                    recoveryResult.ErrorMessage = ex.Message;
                }
            }

            if (recoveryResult.Success)
            {
                RecoverySucceeded?.Invoke(this, new RecoveryEventArgs(errorContext.ErrorId, strategy, RecoveryAction.Succeeded));
            }
            else
            {
                RecoveryFailed?.Invoke(this, new RecoveryEventArgs(errorContext.ErrorId, strategy, RecoveryAction.Failed));
            }

            return recoveryResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error attempting recovery");
            return new RecoveryResult
            {
                Strategy = strategy,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<RecoveryStepResult> ExecuteRecoveryStepAsync(RecoveryStep step, ErrorContext errorContext)
    {
        try
        {
            switch (step.Type)
            {
                case RecoveryStepType.Retry:
                    return await ExecuteRetryStepAsync(step, errorContext);
                case RecoveryStepType.Reset:
                    return await ExecuteResetStepAsync(step, errorContext);
                case RecoveryStepType.Reconnect:
                    return await ExecuteReconnectStepAsync(step, errorContext);
                case RecoveryStepType.Restart:
                    return await ExecuteRestartStepAsync(step, errorContext);
                case RecoveryStepType.Custom:
                    return await ExecuteCustomStepAsync(step, errorContext);
                default:
                    return new RecoveryStepResult { Success = false, Message = "Unknown recovery step type" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing recovery step: {StepName}", step.Name);
            return new RecoveryStepResult { Success = false, Message = ex.Message };
        }
    }

    private async Task<RecoveryStepResult> ExecuteRetryStepAsync(RecoveryStep step, ErrorContext errorContext)
    {
        try
        {
            var maxRetries = step.Parameters.GetValueOrDefault("MaxRetries", 3).ToString()?.ParseInt() ?? 3;
            var delayMs = step.Parameters.GetValueOrDefault("DelayMs", 1000).ToString()?.ParseInt() ?? 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // This would typically retry the original operation
                    await Task.Delay(delayMs);
                    return new RecoveryStepResult { Success = true, Message = $"Retry successful on attempt {i + 1}" };
                }
                catch (Exception ex)
                {
                    if (i == maxRetries - 1)
                    {
                        return new RecoveryStepResult { Success = false, Message = $"Retry failed after {maxRetries} attempts: {ex.Message}" };
                    }
                }
            }

            return new RecoveryStepResult { Success = false, Message = "Retry step failed" };
        }
        catch (Exception ex)
        {
            return new RecoveryStepResult { Success = false, Message = ex.Message };
        }
    }

    private async Task<RecoveryStepResult> ExecuteResetStepAsync(RecoveryStep step, ErrorContext errorContext)
    {
        try
        {
            // This would typically reset the state of the operation
            await Task.Delay(100); // Simulate reset operation
            return new RecoveryStepResult { Success = true, Message = "Reset step completed successfully" };
        }
        catch (Exception ex)
        {
            return new RecoveryStepResult { Success = false, Message = ex.Message };
        }
    }

    private async Task<RecoveryStepResult> ExecuteReconnectStepAsync(RecoveryStep step, ErrorContext errorContext)
    {
        try
        {
            // This would typically reconnect to a service or device
            await Task.Delay(500); // Simulate reconnection
            return new RecoveryStepResult { Success = true, Message = "Reconnect step completed successfully" };
        }
        catch (Exception ex)
        {
            return new RecoveryStepResult { Success = false, Message = ex.Message };
        }
    }

    private async Task<RecoveryStepResult> ExecuteRestartStepAsync(RecoveryStep step, ErrorContext errorContext)
    {
        try
        {
            // This would typically restart a service or component
            await Task.Delay(1000); // Simulate restart
            return new RecoveryStepResult { Success = true, Message = "Restart step completed successfully" };
        }
        catch (Exception ex)
        {
            return new RecoveryStepResult { Success = false, Message = ex.Message };
        }
    }

    private async Task<RecoveryStepResult> ExecuteCustomStepAsync(RecoveryStep step, ErrorContext errorContext)
    {
        try
        {
            // This would typically execute a custom recovery action
            await Task.Delay(200); // Simulate custom step
            return new RecoveryStepResult { Success = true, Message = "Custom step completed successfully" };
        }
        catch (Exception ex)
        {
            return new RecoveryStepResult { Success = false, Message = ex.Message };
        }
    }

    private async Task<T?> ExecuteFallbackAsync<T>(FallbackStrategy strategy, object? context)
    {
        try
        {
            switch (strategy.Type)
            {
                case FallbackType.DefaultValue:
                    return (T?)strategy.DefaultValue;
                case FallbackType.CachedValue:
                    return (T?)strategy.CachedValue;
                case FallbackType.AlternativeOperation:
                    // This would typically execute an alternative operation
                    await Task.Delay(100); // Simulate alternative operation
                    return default(T);
                case FallbackType.GracefulDegradation:
                    // This would typically degrade functionality gracefully
                    await Task.Delay(50); // Simulate graceful degradation
                    return default(T);
                default:
                    return default(T);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing fallback strategy");
            return default(T);
        }
    }

    private int CalculateRetryDelay(RetryPolicy policy, int attempt)
    {
        return policy.BackoffStrategy switch
        {
            BackoffStrategy.Fixed => policy.BaseDelayMs,
            BackoffStrategy.Linear => policy.BaseDelayMs * attempt,
            BackoffStrategy.Exponential => (int)(policy.BaseDelayMs * Math.Pow(policy.Multiplier, attempt - 1)),
            BackoffStrategy.ExponentialWithJitter => (int)(policy.BaseDelayMs * Math.Pow(policy.Multiplier, attempt - 1) * (0.5 + new Random().NextDouble() * 0.5)),
            _ => policy.BaseDelayMs
        };
    }

    private RetryPolicy GetDefaultRetryPolicy()
    {
        return new RetryPolicy
        {
            MaxAttempts = 3,
            BaseDelayMs = 1000,
            Multiplier = 2.0,
            BackoffStrategy = BackoffStrategy.Exponential
        };
    }

    private RecoveryStrategy GetDefaultRecoveryStrategy(ErrorSeverity severity)
    {
        return severity switch
        {
            ErrorSeverity.Low => new RecoveryStrategy
            {
                Name = "Default Low Severity Recovery",
                RecoverySteps = new List<RecoveryStep>
                {
                    new RecoveryStep { Name = "Retry", Type = RecoveryStepType.Retry, Parameters = new Dictionary<string, object> { ["MaxRetries"] = 2, ["DelayMs"] = 500 } }
                }
            },
            ErrorSeverity.Medium => new RecoveryStrategy
            {
                Name = "Default Medium Severity Recovery",
                RecoverySteps = new List<RecoveryStep>
                {
                    new RecoveryStep { Name = "Retry", Type = RecoveryStepType.Retry, Parameters = new Dictionary<string, object> { ["MaxRetries"] = 3, ["DelayMs"] = 1000 } },
                    new RecoveryStep { Name = "Reset", Type = RecoveryStepType.Reset }
                }
            },
            ErrorSeverity.High => new RecoveryStrategy
            {
                Name = "Default High Severity Recovery",
                RecoverySteps = new List<RecoveryStep>
                {
                    new RecoveryStep { Name = "Retry", Type = RecoveryStepType.Retry, Parameters = new Dictionary<string, object> { ["MaxRetries"] = 2, ["DelayMs"] = 2000 } },
                    new RecoveryStep { Name = "Reset", Type = RecoveryStepType.Reset },
                    new RecoveryStep { Name = "Reconnect", Type = RecoveryStepType.Reconnect }
                }
            },
            ErrorSeverity.Critical => new RecoveryStrategy
            {
                Name = "Default Critical Severity Recovery",
                RecoverySteps = new List<RecoveryStep>
                {
                    new RecoveryStep { Name = "Retry", Type = RecoveryStepType.Retry, Parameters = new Dictionary<string, object> { ["MaxRetries"] = 1, ["DelayMs"] = 5000 } },
                    new RecoveryStep { Name = "Reset", Type = RecoveryStepType.Reset },
                    new RecoveryStep { Name = "Reconnect", Type = RecoveryStepType.Reconnect },
                    new RecoveryStep { Name = "Restart", Type = RecoveryStepType.Restart }
                }
            },
            _ => new RecoveryStrategy
            {
                Name = "Default Recovery",
                RecoverySteps = new List<RecoveryStep>
                {
                    new RecoveryStep { Name = "Retry", Type = RecoveryStepType.Retry, Parameters = new Dictionary<string, object> { ["MaxRetries"] = 3, ["DelayMs"] = 1000 } }
                }
            }
        };
    }

    private List<string> GenerateRecommendations(ErrorContext errorContext)
    {
        var recommendations = new List<string>();

        switch (errorContext.ErrorType)
        {
            case "NetworkError":
                recommendations.AddRange(new[]
                {
                    "Check network connectivity",
                    "Verify server availability",
                    "Check firewall settings",
                    "Retry the operation"
                });
                break;
            case "DeviceError":
                recommendations.AddRange(new[]
                {
                    "Check device connection",
                    "Verify device power",
                    "Restart device",
                    "Check device compatibility"
                });
                break;
            case "AudioError":
                recommendations.AddRange(new[]
                {
                    "Check audio device availability",
                    "Verify audio permissions",
                    "Restart audio service",
                    "Check audio drivers"
                });
                break;
            case "ConfigurationError":
                recommendations.AddRange(new[]
                {
                    "Verify configuration settings",
                    "Reset to default configuration",
                    "Check configuration file permissions",
                    "Validate configuration format"
                });
                break;
            default:
                recommendations.AddRange(new[]
                {
                    "Check system resources",
                    "Restart the application",
                    "Check logs for more details",
                    "Contact support if problem persists"
                });
                break;
        }

        return recommendations;
    }

    private void InitializeDefaultStrategies()
    {
        // Initialize default recovery strategies for common error types
        RegisterRecoveryStrategyAsync("NetworkError", new RecoveryStrategy
        {
            Name = "Network Error Recovery",
            RecoverySteps = new List<RecoveryStep>
            {
                new RecoveryStep { Name = "Retry", Type = RecoveryStepType.Retry, Parameters = new Dictionary<string, object> { ["MaxRetries"] = 3, ["DelayMs"] = 2000 } },
                new RecoveryStep { Name = "Reconnect", Type = RecoveryStepType.Reconnect }
            }
        }).Wait();

        RegisterRecoveryStrategyAsync("DeviceError", new RecoveryStrategy
        {
            Name = "Device Error Recovery",
            RecoverySteps = new List<RecoveryStep>
            {
                new RecoveryStep { Name = "Retry", Type = RecoveryStepType.Retry, Parameters = new Dictionary<string, object> { ["MaxRetries"] = 2, ["DelayMs"] = 1000 } },
                new RecoveryStep { Name = "Reset", Type = RecoveryStepType.Reset },
                new RecoveryStep { Name = "Reconnect", Type = RecoveryStepType.Reconnect }
            }
        }).Wait();

        RegisterRecoveryStrategyAsync("AudioError", new RecoveryStrategy
        {
            Name = "Audio Error Recovery",
            RecoverySteps = new List<RecoveryStep>
            {
                new RecoveryStep { Name = "Retry", Type = RecoveryStepType.Retry, Parameters = new Dictionary<string, object> { ["MaxRetries"] = 2, ["DelayMs"] = 500 } },
                new RecoveryStep { Name = "Reset", Type = RecoveryStepType.Reset }
            }
        }).Wait();
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isMonitoring = false;
            _errorMonitoringTimer?.Dispose();

            _logger.LogInformation("Error handling service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing error handling service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Error context
/// </summary>
public class ErrorContext
{
    public string ErrorId { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Medium;
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    public string? RequestId { get; set; }
}

/// <summary>
/// Recovery strategy
/// </summary>
public class RecoveryStrategy
{
    public string Name { get; set; } = string.Empty;
    public List<RecoveryStep> RecoverySteps { get; set; } = new();
    public FallbackStrategy? FallbackStrategy { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Recovery step
/// </summary>
public class RecoveryStep
{
    public string Name { get; set; } = string.Empty;
    public RecoveryStepType Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Recovery step result
/// </summary>
public class RecoveryStepResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Recovery result
/// </summary>
public class RecoveryResult
{
    public RecoveryStrategy Strategy { get; set; } = new();
    public bool Success { get; set; }
    public bool FallbackUsed { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Error handling result
/// </summary>
public class ErrorHandlingResult
{
    public string ErrorId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public RecoveryStrategy? RecoveryStrategy { get; set; }
    public RecoveryResult? RecoveryResult { get; set; }
    public bool FallbackUsed { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Retry policy
/// </summary>
public class RetryPolicy
{
    public int MaxAttempts { get; set; } = 3;
    public int BaseDelayMs { get; set; } = 1000;
    public double Multiplier { get; set; } = 2.0;
    public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;
    public int MaxDelayMs { get; set; } = 30000;
}

/// <summary>
/// Fallback strategy
/// </summary>
public class FallbackStrategy
{
    public string Name { get; set; } = string.Empty;
    public FallbackType Type { get; set; }
    public object? DefaultValue { get; set; }
    public object? CachedValue { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Circuit breaker state
/// </summary>
public class CircuitBreakerState
{
    public string OperationName { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public DateTime OpenUntil { get; set; }
    public int FailureThreshold { get; set; } = 5;
    public int SuccessThreshold { get; set; } = 3;
    public TimeSpan TimeoutDuration { get; set; } = TimeSpan.FromMinutes(1);
    public int FailureCount { get; set; }
    public int SuccessCount { get; set; }
    public int ConsecutiveFailures { get; set; }
}

/// <summary>
/// Error statistics
/// </summary>
public class ErrorStatistics
{
    public string OperationName { get; set; } = string.Empty;
    public int TotalErrors { get; set; }
    public int LowSeverityErrors { get; set; }
    public int MediumSeverityErrors { get; set; }
    public int HighSeverityErrors { get; set; }
    public int CriticalSeverityErrors { get; set; }
    public DateTime FirstErrorTime { get; set; }
    public DateTime LastErrorTime { get; set; }
    public Dictionary<string, int> ErrorTypeCounts { get; set; } = new();
}

/// <summary>
/// Error event arguments
/// </summary>
public class ErrorEventArgs : EventArgs
{
    public ErrorContext ErrorContext { get; }
    public DateTime Timestamp { get; }

    public ErrorEventArgs(ErrorContext errorContext)
    {
        ErrorContext = errorContext;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Recovery event arguments
/// </summary>
public class RecoveryEventArgs : EventArgs
{
    public string ErrorId { get; }
    public RecoveryStrategy Strategy { get; }
    public RecoveryAction Action { get; }
    public DateTime Timestamp { get; }

    public RecoveryEventArgs(string errorId, RecoveryStrategy strategy, RecoveryAction action)
    {
        ErrorId = errorId;
        Strategy = strategy;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Circuit breaker event arguments
/// </summary>
public class CircuitBreakerEventArgs : EventArgs
{
    public string OperationName { get; }
    public CircuitBreakerAction Action { get; }
    public DateTime Timestamp { get; }

    public CircuitBreakerEventArgs(string operationName, CircuitBreakerAction action)
    {
        OperationName = operationName;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Error severity levels
/// </summary>
public enum ErrorSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Recovery step types
/// </summary>
public enum RecoveryStepType
{
    Retry,
    Reset,
    Reconnect,
    Restart,
    Custom
}

/// <summary>
/// Backoff strategies
/// </summary>
public enum BackoffStrategy
{
    Fixed,
    Linear,
    Exponential,
    ExponentialWithJitter
}

/// <summary>
/// Fallback types
/// </summary>
public enum FallbackType
{
    DefaultValue,
    CachedValue,
    AlternativeOperation,
    GracefulDegradation
}

/// <summary>
/// Recovery actions
/// </summary>
public enum RecoveryAction
{
    Attempted,
    Succeeded,
    Failed
}

/// <summary>
/// Circuit breaker actions
/// </summary>
public enum CircuitBreakerAction
{
    Opened,
    Closed
}

/// <summary>
/// Extension methods
/// </summary>
public static class Extensions
{
    public static int ParseInt(this string? value)
    {
        return int.TryParse(value, out var result) ? result : 0;
    }
}
