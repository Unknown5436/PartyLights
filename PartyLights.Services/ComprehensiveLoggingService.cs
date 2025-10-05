using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace PartyLights.Services;

/// <summary>
/// Comprehensive logging service for detailed error tracking and analysis
/// </summary>
public class ComprehensiveLoggingService : IDisposable
{
    private readonly ILogger<ComprehensiveLoggingService> _logger;
    private readonly ConcurrentDictionary<string, LogEntry> _logEntries = new();
    private readonly ConcurrentDictionary<string, LogSession> _logSessions = new();
    private readonly Timer _logProcessingTimer;
    private readonly object _lockObject = new();

    private const int LogProcessingIntervalMs = 10000; // 10 seconds
    private bool _isProcessing;

    // Log management
    private readonly Dictionary<string, LogConfiguration> _logConfigurations = new();
    private readonly Dictionary<string, LogFilter> _logFilters = new();
    private readonly Dictionary<string, LogAnalyzer> _logAnalyzers = new();
    private readonly string _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PartyLights", "Logs");

    public event EventHandler<LogEntryEventArgs>? LogEntryCreated;
    public event EventHandler<LogSessionEventArgs>? LogSessionStarted;
    public event EventHandler<LogSessionEventArgs>? LogSessionEnded;
    public event EventHandler<LogAnalysisEventArgs>? LogAnalysisCompleted;

    public ComprehensiveLoggingService(ILogger<ComprehensiveLoggingService> logger)
    {
        _logger = logger;

        _logProcessingTimer = new Timer(ProcessLogs, null, LogProcessingIntervalMs, LogProcessingIntervalMs);
        _isProcessing = true;

        InitializeLogDirectory();
        InitializeDefaultConfigurations();

        _logger.LogInformation("Comprehensive logging service initialized");
    }

    /// <summary>
    /// Creates a new log session
    /// </summary>
    public async Task<string> StartLogSessionAsync(LogSessionRequest request)
    {
        try
        {
            var sessionId = Guid.NewGuid().ToString();

            var session = new LogSession
            {
                SessionId = sessionId,
                Name = request.Name,
                Description = request.Description,
                StartTime = DateTime.UtcNow,
                IsActive = true,
                Configuration = request.Configuration ?? GetDefaultLogConfiguration(),
                Filters = request.Filters ?? new List<LogFilter>(),
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _logSessions[sessionId] = session;

            LogSessionStarted?.Invoke(this, new LogSessionEventArgs(sessionId, LogSessionAction.Started));
            _logger.LogInformation("Started log session: {SessionName} ({SessionId})", request.Name, sessionId);

            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting log session: {SessionName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Ends a log session
    /// </summary>
    public async Task<bool> EndLogSessionAsync(string sessionId)
    {
        try
        {
            if (!_logSessions.TryGetValue(sessionId, out var session))
            {
                _logger.LogWarning("Log session not found: {SessionId}", sessionId);
                return false;
            }

            session.EndTime = DateTime.UtcNow;
            session.IsActive = false;
            session.Duration = session.EndTime - session.StartTime;

            // Process session logs
            await ProcessSessionLogs(session);

            LogSessionEnded?.Invoke(this, new LogSessionEventArgs(sessionId, LogSessionAction.Ended));
            _logger.LogInformation("Ended log session: {SessionName} ({SessionId})", session.Name, sessionId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending log session: {SessionId}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Logs an entry with comprehensive context
    /// </summary>
    public async Task<string> LogEntryAsync(LogEntryRequest request)
    {
        try
        {
            var entryId = Guid.NewGuid().ToString();

            var entry = new LogEntry
            {
                EntryId = entryId,
                SessionId = request.SessionId,
                Level = request.Level,
                Category = request.Category,
                Message = request.Message,
                Exception = request.Exception,
                Timestamp = DateTime.UtcNow,
                ThreadId = Environment.CurrentManagedThreadId,
                ProcessId = Environment.ProcessId,
                UserId = request.UserId,
                RequestId = request.RequestId,
                CorrelationId = request.CorrelationId,
                Properties = request.Properties ?? new Dictionary<string, object>(),
                Tags = request.Tags ?? new List<string>(),
                Metrics = request.Metrics ?? new Dictionary<string, double>(),
                Context = request.Context ?? new Dictionary<string, object>()
            };

            // Apply filters
            if (ShouldLogEntry(entry, request.SessionId))
            {
                _logEntries[entryId] = entry;

                // Write to file if configured
                if (request.WriteToFile)
                {
                    await WriteLogEntryToFile(entry);
                }

                LogEntryCreated?.Invoke(this, new LogEntryEventArgs(entryId, entry));
            }

            return entryId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging entry: {Message}", request.Message);
            return string.Empty;
        }
    }

    /// <summary>
    /// Logs performance metrics
    /// </summary>
    public async Task<string> LogPerformanceMetricsAsync(PerformanceMetricsRequest request)
    {
        try
        {
            var entryId = Guid.NewGuid().ToString();

            var entry = new LogEntry
            {
                EntryId = entryId,
                SessionId = request.SessionId,
                Level = LogLevel.Information,
                Category = "Performance",
                Message = $"Performance metrics for {request.OperationName}",
                Timestamp = DateTime.UtcNow,
                ThreadId = Environment.CurrentManagedThreadId,
                ProcessId = Environment.ProcessId,
                Properties = new Dictionary<string, object>
                {
                    ["OperationName"] = request.OperationName,
                    ["Duration"] = request.Duration,
                    ["MemoryUsage"] = request.MemoryUsage,
                    ["CpuUsage"] = request.CpuUsage,
                    ["ThreadCount"] = request.ThreadCount,
                    ["GcCollections"] = request.GcCollections
                },
                Metrics = new Dictionary<string, double>
                {
                    ["Duration"] = request.Duration.TotalMilliseconds,
                    ["MemoryUsage"] = request.MemoryUsage,
                    ["CpuUsage"] = request.CpuUsage,
                    ["ThreadCount"] = request.ThreadCount,
                    ["GcCollections"] = request.GcCollections
                },
                Tags = new List<string> { "Performance", "Metrics" }
            };

            _logEntries[entryId] = entry;

            if (request.WriteToFile)
            {
                await WriteLogEntryToFile(entry);
            }

            LogEntryCreated?.Invoke(this, new LogEntryEventArgs(entryId, entry));

            return entryId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging performance metrics: {OperationName}", request.OperationName);
            return string.Empty;
        }
    }

    /// <summary>
    /// Logs user activity
    /// </summary>
    public async Task<string> LogUserActivityAsync(UserActivityRequest request)
    {
        try
        {
            var entryId = Guid.NewGuid().ToString();

            var entry = new LogEntry
            {
                EntryId = entryId,
                SessionId = request.SessionId,
                Level = LogLevel.Information,
                Category = "UserActivity",
                Message = $"User activity: {request.ActivityType}",
                Timestamp = DateTime.UtcNow,
                ThreadId = Environment.CurrentManagedThreadId,
                ProcessId = Environment.ProcessId,
                UserId = request.UserId,
                Properties = new Dictionary<string, object>
                {
                    ["ActivityType"] = request.ActivityType,
                    ["ActivityDescription"] = request.ActivityDescription,
                    ["TargetObject"] = request.TargetObject ?? string.Empty,
                    ["TargetObjectType"] = request.TargetObjectType ?? string.Empty,
                    ["Success"] = request.Success,
                    ["Duration"] = request.Duration?.TotalMilliseconds ?? 0
                },
                Tags = new List<string> { "UserActivity", request.ActivityType }
            };

            _logEntries[entryId] = entry;

            if (request.WriteToFile)
            {
                await WriteLogEntryToFile(entry);
            }

            LogEntryCreated?.Invoke(this, new LogEntryEventArgs(entryId, entry));

            return entryId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging user activity: {ActivityType}", request.ActivityType);
            return string.Empty;
        }
    }

    /// <summary>
    /// Analyzes logs for patterns and insights
    /// </summary>
    public async Task<LogAnalysisResult> AnalyzeLogsAsync(LogAnalysisRequest request)
    {
        try
        {
            var analyzer = new LogAnalyzer
            {
                Name = request.AnalyzerName,
                AnalysisType = request.AnalysisType,
                Parameters = request.Parameters ?? new Dictionary<string, object>(),
                StartTime = DateTime.UtcNow
            };

            var result = new LogAnalysisResult
            {
                AnalyzerName = request.AnalyzerName,
                AnalysisType = request.AnalysisType,
                StartTime = analyzer.StartTime,
                EndTime = DateTime.UtcNow,
                Success = false,
                Insights = new List<string>(),
                Patterns = new List<LogPattern>(),
                Metrics = new Dictionary<string, double>(),
                Recommendations = new List<string>()
            };

            // Filter logs based on request criteria
            var filteredLogs = FilterLogs(request.FilterCriteria);

            // Perform analysis based on type
            switch (request.AnalysisType)
            {
                case LogAnalysisType.ErrorPatterns:
                    result = await AnalyzeErrorPatterns(filteredLogs, result);
                    break;
                case LogAnalysisType.PerformanceTrends:
                    result = await AnalyzePerformanceTrends(filteredLogs, result);
                    break;
                case LogAnalysisType.UserBehavior:
                    result = await AnalyzeUserBehavior(filteredLogs, result);
                    break;
                case LogAnalysisType.SystemHealth:
                    result = await AnalyzeSystemHealth(filteredLogs, result);
                    break;
                case LogAnalysisType.Custom:
                    result = await PerformCustomAnalysis(filteredLogs, result, request.Parameters);
                    break;
            }

            result.EndTime = DateTime.UtcNow;
            result.Success = true;

            LogAnalysisCompleted?.Invoke(this, new LogAnalysisEventArgs(result.AnalyzerName, result));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing logs: {AnalyzerName}", request.AnalyzerName);
            return new LogAnalysisResult
            {
                AnalyzerName = request.AnalyzerName,
                AnalysisType = request.AnalysisType,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Exports logs to various formats
    /// </summary>
    public async Task<bool> ExportLogsAsync(LogExportRequest request)
    {
        try
        {
            var filteredLogs = FilterLogs(request.FilterCriteria);

            switch (request.ExportFormat)
            {
                case LogExportFormat.Json:
                    return await ExportLogsToJson(filteredLogs, request.FilePath);
                case LogExportFormat.Csv:
                    return await ExportLogsToCsv(filteredLogs, request.FilePath);
                case LogExportFormat.Xml:
                    return await ExportLogsToXml(filteredLogs, request.FilePath);
                case LogExportFormat.Text:
                    return await ExportLogsToText(filteredLogs, request.FilePath);
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting logs to: {FilePath}", request.FilePath);
            return false;
        }
    }

    /// <summary>
    /// Gets log entries with filtering
    /// </summary>
    public IEnumerable<LogEntry> GetLogEntries(LogFilterCriteria? criteria = null)
    {
        var entries = _logEntries.Values.AsEnumerable();

        if (criteria != null)
        {
            if (criteria.SessionId != null)
                entries = entries.Where(e => e.SessionId == criteria.SessionId);

            if (criteria.Level != null)
                entries = entries.Where(e => e.Level == criteria.Level);

            if (criteria.Category != null)
                entries = entries.Where(e => e.Category == criteria.Category);

            if (criteria.StartTime.HasValue)
                entries = entries.Where(e => e.Timestamp >= criteria.StartTime.Value);

            if (criteria.EndTime.HasValue)
                entries = entries.Where(e => e.Timestamp <= criteria.EndTime.Value);

            if (criteria.Tags != null && criteria.Tags.Any())
                entries = entries.Where(e => criteria.Tags.Any(tag => e.Tags.Contains(tag)));

            if (!string.IsNullOrEmpty(criteria.SearchTerm))
                entries = entries.Where(e => e.Message.Contains(criteria.SearchTerm, StringComparison.OrdinalIgnoreCase));
        }

        return entries.OrderBy(e => e.Timestamp);
    }

    /// <summary>
    /// Gets log statistics
    /// </summary>
    public LogStatistics GetLogStatistics(LogFilterCriteria? criteria = null)
    {
        var entries = GetLogEntries(criteria);

        return new LogStatistics
        {
            TotalEntries = entries.Count(),
            ErrorEntries = entries.Count(e => e.Level >= LogLevel.Error),
            WarningEntries = entries.Count(e => e.Level == LogLevel.Warning),
            InformationEntries = entries.Count(e => e.Level == LogLevel.Information),
            DebugEntries = entries.Count(e => e.Level == LogLevel.Debug),
            TraceEntries = entries.Count(e => e.Level == LogLevel.Trace),
            FirstEntryTime = entries.Any() ? entries.Min(e => e.Timestamp) : DateTime.MinValue,
            LastEntryTime = entries.Any() ? entries.Max(e => e.Timestamp) : DateTime.MinValue,
            CategoryCounts = entries.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Count()),
            LevelCounts = entries.GroupBy(e => e.Level).ToDictionary(g => g.Key.ToString(), g => g.Count())
        };
    }

    #region Private Methods

    private async void ProcessLogs(object? state)
    {
        if (!_isProcessing)
        {
            return;
        }

        try
        {
            // Process log entries for cleanup, analysis, etc.
            var currentTime = DateTime.UtcNow;
            var oldEntries = _logEntries.Values
                .Where(e => e.Timestamp < currentTime.AddDays(-7)) // Keep logs for 7 days
                .ToList();

            foreach (var entry in oldEntries)
            {
                _logEntries.TryRemove(entry.EntryId, out _);
            }

            // Process active sessions
            foreach (var session in _logSessions.Values.Where(s => s.IsActive))
            {
                await ProcessActiveSession(session);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing logs");
        }
    }

    private async Task ProcessSessionLogs(LogSession session)
    {
        try
        {
            var sessionLogs = _logEntries.Values.Where(e => e.SessionId == session.SessionId).ToList();

            // Generate session summary
            var summary = new LogSessionSummary
            {
                SessionId = session.SessionId,
                TotalEntries = sessionLogs.Count,
                ErrorCount = sessionLogs.Count(e => e.Level >= LogLevel.Error),
                WarningCount = sessionLogs.Count(e => e.Level == LogLevel.Warning),
                Duration = session.Duration,
                StartTime = session.StartTime,
                EndTime = session.EndTime
            };

            session.Summary = summary;

            // Write session summary to file
            var summaryPath = Path.Combine(_logDirectory, $"session_{session.SessionId}_summary.json");
            var summaryJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(summaryPath, summaryJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing session logs: {SessionId}", session.SessionId);
        }
    }

    private async Task ProcessActiveSession(LogSession session)
    {
        try
        {
            // Process active session for real-time analysis
            var recentLogs = _logEntries.Values
                .Where(e => e.SessionId == session.SessionId && e.Timestamp > DateTime.UtcNow.AddMinutes(-5))
                .ToList();

            // Check for error patterns
            var errorCount = recentLogs.Count(e => e.Level >= LogLevel.Error);
            if (errorCount > 10) // Threshold for error spike
            {
                _logger.LogWarning("High error rate detected in session: {SessionId} - {ErrorCount} errors in last 5 minutes",
                    session.SessionId, errorCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing active session: {SessionId}", session.SessionId);
        }
    }

    private bool ShouldLogEntry(LogEntry entry, string? sessionId)
    {
        try
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return true; // Log all entries if no session specified
            }

            if (_logSessions.TryGetValue(sessionId, out var session))
            {
                // Apply session filters
                foreach (var filter in session.Filters)
                {
                    if (!MatchesFilter(entry, filter))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking log entry filter");
            return true; // Default to logging if filter check fails
        }
    }

    private bool MatchesFilter(LogEntry entry, LogFilter filter)
    {
        try
        {
            switch (filter.Type)
            {
                case LogFilterType.Level:
                    return entry.Level >= filter.MinLevel;
                case LogFilterType.Category:
                    return filter.Categories.Contains(entry.Category);
                case LogFilterType.Tag:
                    return filter.Tags.Any(tag => entry.Tags.Contains(tag));
                case LogFilterType.TimeRange:
                    return entry.Timestamp >= filter.StartTime && entry.Timestamp <= filter.EndTime;
                case LogFilterType.Message:
                    return entry.Message.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase);
                default:
                    return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error matching log filter");
            return true; // Default to including entry if filter match fails
        }
    }

    private async Task WriteLogEntryToFile(LogEntry entry)
    {
        try
        {
            var logFile = Path.Combine(_logDirectory, $"log_{DateTime.UtcNow:yyyyMMdd}.json");
            var logLine = JsonSerializer.Serialize(entry) + Environment.NewLine;
            await File.AppendAllTextAsync(logFile, logLine);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing log entry to file");
        }
    }

    private IEnumerable<LogEntry> FilterLogs(LogFilterCriteria? criteria)
    {
        return GetLogEntries(criteria);
    }

    private async Task<LogAnalysisResult> AnalyzeErrorPatterns(IEnumerable<LogEntry> logs, LogAnalysisResult result)
    {
        try
        {
            var errorLogs = logs.Where(l => l.Level >= LogLevel.Error).ToList();

            // Analyze error frequency
            var errorFrequency = errorLogs.GroupBy(l => l.Message)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count());

            result.Metrics["ErrorFrequency"] = errorFrequency.Values.Sum();
            result.Metrics["UniqueErrors"] = errorFrequency.Count;
            result.Metrics["MostCommonError"] = errorFrequency.Values.FirstOrDefault();

            // Generate insights
            if (errorFrequency.Any())
            {
                var mostCommonError = errorFrequency.First();
                result.Insights.Add($"Most common error: '{mostCommonError.Key}' occurred {mostCommonError.Value} times");
            }

            // Generate patterns
            foreach (var error in errorFrequency.Take(5))
            {
                result.Patterns.Add(new LogPattern
                {
                    PatternType = "ErrorFrequency",
                    Description = $"Error '{error.Key}' occurs frequently",
                    Frequency = error.Value,
                    Severity = LogLevel.Error
                });
            }

            // Generate recommendations
            if (errorFrequency.Values.Sum() > 100)
            {
                result.Recommendations.Add("High error rate detected - investigate root causes");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing error patterns");
            return result;
        }
    }

    private async Task<LogAnalysisResult> AnalyzePerformanceTrends(IEnumerable<LogEntry> logs, LogAnalysisResult result)
    {
        try
        {
            var performanceLogs = logs.Where(l => l.Category == "Performance").ToList();

            if (performanceLogs.Any())
            {
                var durations = performanceLogs
                    .Where(l => l.Metrics.ContainsKey("Duration"))
                    .Select(l => l.Metrics["Duration"])
                    .ToList();

                if (durations.Any())
                {
                    result.Metrics["AverageDuration"] = durations.Average();
                    result.Metrics["MaxDuration"] = durations.Max();
                    result.Metrics["MinDuration"] = durations.Min();
                    result.Metrics["DurationVariance"] = durations.Variance();

                    result.Insights.Add($"Average operation duration: {durations.Average():F2}ms");
                    result.Insights.Add($"Maximum operation duration: {durations.Max():F2}ms");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing performance trends");
            return result;
        }
    }

    private async Task<LogAnalysisResult> AnalyzeUserBehavior(IEnumerable<LogEntry> logs, LogAnalysisResult result)
    {
        try
        {
            var userActivityLogs = logs.Where(l => l.Category == "UserActivity").ToList();

            if (userActivityLogs.Any())
            {
                var activityTypes = userActivityLogs.GroupBy(l => l.Properties.GetValueOrDefault("ActivityType", "Unknown"))
                    .ToDictionary(g => g.Key.ToString(), g => g.Count());

                result.Metrics["TotalActivities"] = userActivityLogs.Count;
                result.Metrics["UniqueActivityTypes"] = activityTypes.Count;
                result.Metrics["MostCommonActivity"] = activityTypes.Values.Max();

                result.Insights.Add($"Total user activities: {userActivityLogs.Count}");
                result.Insights.Add($"Most common activity: {activityTypes.OrderByDescending(kvp => kvp.Value).First().Key}");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing user behavior");
            return result;
        }
    }

    private async Task<LogAnalysisResult> AnalyzeSystemHealth(IEnumerable<LogEntry> logs, LogAnalysisResult result)
    {
        try
        {
            var errorCount = logs.Count(l => l.Level >= LogLevel.Error);
            var warningCount = logs.Count(l => l.Level == LogLevel.Warning);
            var totalLogs = logs.Count();

            result.Metrics["ErrorRate"] = totalLogs > 0 ? (double)errorCount / totalLogs : 0;
            result.Metrics["WarningRate"] = totalLogs > 0 ? (double)warningCount / totalLogs : 0;
            result.Metrics["SystemHealthScore"] = Math.Max(0, 100 - (result.Metrics["ErrorRate"] * 100) - (result.Metrics["WarningRate"] * 50));

            if (result.Metrics["SystemHealthScore"] < 70)
            {
                result.Recommendations.Add("System health is below optimal - investigate errors and warnings");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing system health");
            return result;
        }
    }

    private async Task<LogAnalysisResult> PerformCustomAnalysis(IEnumerable<LogEntry> logs, LogAnalysisResult result, Dictionary<string, object>? parameters)
    {
        try
        {
            // Custom analysis logic based on parameters
            result.Insights.Add("Custom analysis completed");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing custom analysis");
            return result;
        }
    }

    private async Task<bool> ExportLogsToJson(IEnumerable<LogEntry> logs, string filePath)
    {
        try
        {
            var logData = new
            {
                ExportTime = DateTime.UtcNow,
                TotalEntries = logs.Count(),
                Logs = logs.ToList()
            };

            var json = JsonSerializer.Serialize(logData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting logs to JSON: {FilePath}", filePath);
            return false;
        }
    }

    private async Task<bool> ExportLogsToCsv(IEnumerable<LogEntry> logs, string filePath)
    {
        try
        {
            var csvLines = new List<string>
            {
                "Timestamp,Level,Category,Message,ThreadId,ProcessId,UserId,RequestId,CorrelationId"
            };

            foreach (var log in logs)
            {
                csvLines.Add($"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\",\"{log.Level}\",\"{log.Category}\",\"{log.Message}\",{log.ThreadId},{log.ProcessId},\"{log.UserId}\",\"{log.RequestId}\",\"{log.CorrelationId}\"");
            }

            await File.WriteAllLinesAsync(filePath, csvLines);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting logs to CSV: {FilePath}", filePath);
            return false;
        }
    }

    private async Task<bool> ExportLogsToXml(IEnumerable<LogEntry> logs, string filePath)
    {
        try
        {
            var xml = new System.Xml.XmlDocument();
            var root = xml.CreateElement("Logs");
            xml.AppendChild(root);

            foreach (var log in logs)
            {
                var logElement = xml.CreateElement("LogEntry");
                logElement.SetAttribute("Timestamp", log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                logElement.SetAttribute("Level", log.Level.ToString());
                logElement.SetAttribute("Category", log.Category);
                logElement.SetAttribute("Message", log.Message);
                logElement.SetAttribute("ThreadId", log.ThreadId.ToString());
                logElement.SetAttribute("ProcessId", log.ProcessId.ToString());
                root.AppendChild(logElement);
            }

            xml.Save(filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting logs to XML: {FilePath}", filePath);
            return false;
        }
    }

    private async Task<bool> ExportLogsToText(IEnumerable<LogEntry> logs, string filePath)
    {
        try
        {
            var textLines = new List<string>();

            foreach (var log in logs)
            {
                textLines.Add($"[{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{log.Level}] [{log.Category}] {log.Message}");
            }

            await File.WriteAllLinesAsync(filePath, textLines);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting logs to text: {FilePath}", filePath);
            return false;
        }
    }

    private LogConfiguration GetDefaultLogConfiguration()
    {
        return new LogConfiguration
        {
            Name = "Default",
            MinLevel = LogLevel.Information,
            WriteToFile = true,
            WriteToConsole = true,
            MaxFileSize = 10 * 1024 * 1024, // 10MB
            MaxFiles = 5,
            RetentionDays = 7
        };
    }

    private void InitializeLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating log directory: {LogDirectory}", _logDirectory);
        }
    }

    private void InitializeDefaultConfigurations()
    {
        // Initialize default log configurations
        _logConfigurations["Default"] = GetDefaultLogConfiguration();
        _logConfigurations["Debug"] = new LogConfiguration
        {
            Name = "Debug",
            MinLevel = LogLevel.Debug,
            WriteToFile = true,
            WriteToConsole = false,
            MaxFileSize = 5 * 1024 * 1024, // 5MB
            MaxFiles = 3,
            RetentionDays = 3
        };
        _logConfigurations["Production"] = new LogConfiguration
        {
            Name = "Production",
            MinLevel = LogLevel.Warning,
            WriteToFile = true,
            WriteToConsole = false,
            MaxFileSize = 50 * 1024 * 1024, // 50MB
            MaxFiles = 10,
            RetentionDays = 30
        };
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isProcessing = false;
            _logProcessingTimer?.Dispose();

            _logger.LogInformation("Comprehensive logging service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing comprehensive logging service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Log session request
/// </summary>
public class LogSessionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LogConfiguration? Configuration { get; set; }
    public List<LogFilter>? Filters { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Log entry request
/// </summary>
public class LogEntryRequest
{
    public string? SessionId { get; set; }
    public LogLevel Level { get; set; } = LogLevel.Information;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public string? UserId { get; set; }
    public string? RequestId { get; set; }
    public string? CorrelationId { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
    public List<string>? Tags { get; set; }
    public Dictionary<string, double>? Metrics { get; set; }
    public Dictionary<string, object>? Context { get; set; }
    public bool WriteToFile { get; set; } = true;
}

/// <summary>
/// Performance metrics request
/// </summary>
public class PerformanceMetricsRequest
{
    public string? SessionId { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public long MemoryUsage { get; set; }
    public double CpuUsage { get; set; }
    public int ThreadCount { get; set; }
    public int GcCollections { get; set; }
    public bool WriteToFile { get; set; } = true;
}

/// <summary>
/// User activity request
/// </summary>
public class UserActivityRequest
{
    public string? SessionId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string ActivityDescription { get; set; } = string.Empty;
    public string? TargetObject { get; set; }
    public string? TargetObjectType { get; set; }
    public bool Success { get; set; } = true;
    public TimeSpan? Duration { get; set; }
    public string? UserId { get; set; }
    public bool WriteToFile { get; set; } = true;
}

/// <summary>
/// Log analysis request
/// </summary>
public class LogAnalysisRequest
{
    public string AnalyzerName { get; set; } = string.Empty;
    public LogAnalysisType AnalysisType { get; set; } = LogAnalysisType.ErrorPatterns;
    public LogFilterCriteria? FilterCriteria { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Log export request
/// </summary>
public class LogExportRequest
{
    public string FilePath { get; set; } = string.Empty;
    public LogExportFormat ExportFormat { get; set; } = LogExportFormat.Json;
    public LogFilterCriteria? FilterCriteria { get; set; }
}

/// <summary>
/// Log entry
/// </summary>
public class LogEntry
{
    public string EntryId { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; }
    public int ThreadId { get; set; }
    public int ProcessId { get; set; }
    public string? UserId { get; set; }
    public string? RequestId { get; set; }
    public string? CorrelationId { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, double> Metrics { get; set; } = new();
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Log session
/// </summary>
public class LogSession
{
    public string SessionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsActive { get; set; }
    public LogConfiguration Configuration { get; set; } = new();
    public List<LogFilter> Filters { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public LogSessionSummary? Summary { get; set; }
}

/// <summary>
/// Log configuration
/// </summary>
public class LogConfiguration
{
    public string Name { get; set; } = string.Empty;
    public LogLevel MinLevel { get; set; } = LogLevel.Information;
    public bool WriteToFile { get; set; } = true;
    public bool WriteToConsole { get; set; } = true;
    public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB
    public int MaxFiles { get; set; } = 5;
    public int RetentionDays { get; set; } = 7;
}

/// <summary>
/// Log filter
/// </summary>
public class LogFilter
{
    public string Name { get; set; } = string.Empty;
    public LogFilterType Type { get; set; }
    public LogLevel MinLevel { get; set; } = LogLevel.Trace;
    public List<string> Categories { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public DateTime StartTime { get; set; } = DateTime.MinValue;
    public DateTime EndTime { get; set; } = DateTime.MaxValue;
    public string SearchTerm { get; set; } = string.Empty;
}

/// <summary>
/// Log filter criteria
/// </summary>
public class LogFilterCriteria
{
    public string? SessionId { get; set; }
    public LogLevel? Level { get; set; }
    public string? Category { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<string>? Tags { get; set; }
    public string? SearchTerm { get; set; }
}

/// <summary>
/// Log analyzer
/// </summary>
public class LogAnalyzer
{
    public string Name { get; set; } = string.Empty;
    public LogAnalysisType AnalysisType { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime StartTime { get; set; }
}

/// <summary>
/// Log analysis result
/// </summary>
public class LogAnalysisResult
{
    public string AnalyzerName { get; set; } = string.Empty;
    public LogAnalysisType AnalysisType { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Insights { get; set; } = new();
    public List<LogPattern> Patterns { get; set; } = new();
    public Dictionary<string, double> Metrics { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Log pattern
/// </summary>
public class LogPattern
{
    public string PatternType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Frequency { get; set; }
    public LogLevel Severity { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// Log session summary
/// </summary>
public class LogSessionSummary
{
    public string SessionId { get; set; } = string.Empty;
    public int TotalEntries { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

/// <summary>
/// Log statistics
/// </summary>
public class LogStatistics
{
    public int TotalEntries { get; set; }
    public int ErrorEntries { get; set; }
    public int WarningEntries { get; set; }
    public int InformationEntries { get; set; }
    public int DebugEntries { get; set; }
    public int TraceEntries { get; set; }
    public DateTime FirstEntryTime { get; set; }
    public DateTime LastEntryTime { get; set; }
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
    public Dictionary<string, int> LevelCounts { get; set; } = new();
}

/// <summary>
/// Log entry event arguments
/// </summary>
public class LogEntryEventArgs : EventArgs
{
    public string EntryId { get; }
    public LogEntry Entry { get; }
    public DateTime Timestamp { get; }

    public LogEntryEventArgs(string entryId, LogEntry entry)
    {
        EntryId = entryId;
        Entry = entry;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Log session event arguments
/// </summary>
public class LogSessionEventArgs : EventArgs
{
    public string SessionId { get; }
    public LogSessionAction Action { get; }
    public DateTime Timestamp { get; }

    public LogSessionEventArgs(string sessionId, LogSessionAction action)
    {
        SessionId = sessionId;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Log analysis event arguments
/// </summary>
public class LogAnalysisEventArgs : EventArgs
{
    public string AnalyzerName { get; }
    public LogAnalysisResult Result { get; }
    public DateTime Timestamp { get; }

    public LogAnalysisEventArgs(string analyzerName, LogAnalysisResult result)
    {
        AnalyzerName = analyzerName;
        Result = result;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Log analysis types
/// </summary>
public enum LogAnalysisType
{
    ErrorPatterns,
    PerformanceTrends,
    UserBehavior,
    SystemHealth,
    Custom
}

/// <summary>
/// Log export formats
/// </summary>
public enum LogExportFormat
{
    Json,
    Csv,
    Xml,
    Text
}

/// <summary>
/// Log filter types
/// </summary>
public enum LogFilterType
{
    Level,
    Category,
    Tag,
    TimeRange,
    Message
}

/// <summary>
/// Log session actions
/// </summary>
public enum LogSessionAction
{
    Started,
    Ended
}

/// <summary>
/// Extension methods
/// </summary>
public static class Extensions
{
    public static double Variance(this IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0) return 0;

        var mean = list.Average();
        var variance = list.Select(x => Math.Pow(x - mean, 2)).Average();
        return variance;
    }
}
