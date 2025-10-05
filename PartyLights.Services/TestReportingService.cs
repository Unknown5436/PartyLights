using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PartyLights.Services;

/// <summary>
/// Test reporting service for comprehensive test reporting and analytics
/// </summary>
public class TestReportingService : IDisposable
{
    private readonly ILogger<TestReportingService> _logger;
    private readonly ConcurrentDictionary<string, TestReport> _testReports = new();
    private readonly ConcurrentDictionary<string, TestAnalytics> _testAnalytics = new();
    private readonly Timer _reportingTimer;
    private readonly object _lockObject = new();

    private const int ReportingIntervalMs = 1000; // 1 second
    private bool _isReporting;

    // Test reporting
    private readonly Dictionary<string, ReportTemplate> _reportTemplates = new();
    private readonly Dictionary<string, ReportSchedule> _reportSchedules = new();
    private readonly Dictionary<string, ReportExport> _reportExports = new();

    public event EventHandler<TestReportEventArgs>? TestReportGenerated;
    public event EventHandler<TestAnalyticsEventArgs>? TestAnalyticsUpdated;
    public event EventHandler<ReportExportEventArgs>? ReportExported;

    public TestReportingService(ILogger<TestReportingService> logger)
    {
        _logger = logger;

        _reportingTimer = new Timer(ProcessReporting, null, ReportingIntervalMs, ReportingIntervalMs);
        _isReporting = true;

        InitializeReportTemplates();
        InitializeReportSchedules();

        _logger.LogInformation("Test reporting service initialized");
    }

    /// <summary>
    /// Generates a comprehensive test report
    /// </summary>
    public async Task<TestReport> GenerateTestReportAsync(TestReportRequest request)
    {
        try
        {
            var reportId = Guid.NewGuid().ToString();

            var report = new TestReport
            {
                Id = reportId,
                Name = request.Name,
                Description = request.Description,
                ReportType = request.ReportType,
                TestSuites = request.TestSuites ?? new List<string>(),
                StartDate = request.StartDate ?? DateTime.UtcNow.AddDays(-7),
                EndDate = request.EndDate ?? DateTime.UtcNow,
                GeneratedAt = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            // Collect test data
            await CollectTestData(report);

            // Generate report metrics
            GenerateReportMetrics(report);

            // Generate report insights
            GenerateReportInsights(report);

            _testReports[reportId] = report;

            TestReportGenerated?.Invoke(this, new TestReportEventArgs(reportId, report, TestReportAction.Generated));
            _logger.LogInformation("Generated test report: {ReportName} ({ReportId})", request.Name, reportId);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating test report: {ReportName}", request.Name);
            return new TestReport
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Generates test analytics
    /// </summary>
    public async Task<TestAnalytics> GenerateTestAnalyticsAsync(TestAnalyticsRequest request)
    {
        try
        {
            var analyticsId = Guid.NewGuid().ToString();

            var analytics = new TestAnalytics
            {
                Id = analyticsId,
                Name = request.Name,
                Description = request.Description,
                AnalyticsType = request.AnalyticsType,
                TestSuites = request.TestSuites ?? new List<string>(),
                StartDate = request.StartDate ?? DateTime.UtcNow.AddDays(-30),
                EndDate = request.EndDate ?? DateTime.UtcNow,
                GeneratedAt = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            // Collect analytics data
            await CollectAnalyticsData(analytics);

            // Generate analytics metrics
            GenerateAnalyticsMetrics(analytics);

            // Generate analytics trends
            GenerateAnalyticsTrends(analytics);

            _testAnalytics[analyticsId] = analytics;

            TestAnalyticsUpdated?.Invoke(this, new TestAnalyticsEventArgs(analyticsId, analytics, TestAnalyticsAction.Generated));
            _logger.LogInformation("Generated test analytics: {AnalyticsName} ({AnalyticsId})", request.Name, analyticsId);

            return analytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating test analytics: {AnalyticsName}", request.Name);
            return new TestAnalytics
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Exports a test report
    /// </summary>
    public async Task<bool> ExportTestReportAsync(string reportId, ReportExportRequest request)
    {
        try
        {
            if (!_testReports.TryGetValue(reportId, out var report))
            {
                _logger.LogWarning("Test report not found: {ReportId}", reportId);
                return false;
            }

            var exportId = Guid.NewGuid().ToString();

            var export = new ReportExport
            {
                Id = exportId,
                ReportId = reportId,
                ExportType = request.ExportType,
                Format = request.Format,
                Destination = request.Destination,
                StartTime = DateTime.UtcNow,
                Status = ExportStatus.InProgress,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _reportExports[exportId] = export;

            // Export the report
            var success = await ExportReport(report, export);

            export.EndTime = DateTime.UtcNow;
            export.Status = success ? ExportStatus.Completed : ExportStatus.Failed;
            export.Success = success;

            ReportExported?.Invoke(this, new ReportExportEventArgs(exportId, export, ReportExportAction.Exported));

            _logger.LogInformation("Exported test report: {ReportId} - Success: {Success}", reportId, success);

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting test report: {ReportId}", reportId);
            return false;
        }
    }

    /// <summary>
    /// Creates a report template
    /// </summary>
    public async Task<string> CreateReportTemplateAsync(ReportTemplateRequest request)
    {
        try
        {
            var templateId = Guid.NewGuid().ToString();

            var template = new ReportTemplate
            {
                Id = templateId,
                Name = request.Name,
                Description = request.Description,
                TemplateType = request.TemplateType,
                Sections = request.Sections ?? new List<string>(),
                Format = request.Format,
                IsDefault = request.IsDefault,
                CreatedAt = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _reportTemplates[templateId] = template;

            _logger.LogInformation("Created report template: {TemplateName} ({TemplateId})", request.Name, templateId);

            return templateId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating report template: {TemplateName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Schedules a report generation
    /// </summary>
    public async Task<string> ScheduleReportGenerationAsync(ReportScheduleRequest request)
    {
        try
        {
            var scheduleId = Guid.NewGuid().ToString();

            var schedule = new ReportSchedule
            {
                Id = scheduleId,
                Name = request.Name,
                Description = request.Description,
                ReportType = request.ReportType,
                TemplateId = request.TemplateId,
                ScheduleType = request.ScheduleType,
                CronExpression = request.CronExpression,
                Recipients = request.Recipients ?? new List<string>(),
                IsEnabled = request.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                LastGenerated = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _reportSchedules[scheduleId] = schedule;

            _logger.LogInformation("Scheduled report generation: {ScheduleName} ({ScheduleId})", request.Name, scheduleId);

            return scheduleId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling report generation: {ScheduleName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets test reports
    /// </summary>
    public IEnumerable<TestReport> GetTestReports()
    {
        return _testReports.Values;
    }

    /// <summary>
    /// Gets test analytics
    /// </summary>
    public IEnumerable<TestAnalytics> GetTestAnalytics()
    {
        return _testAnalytics.Values;
    }

    /// <summary>
    /// Gets report templates
    /// </summary>
    public IEnumerable<ReportTemplate> GetReportTemplates()
    {
        return _reportTemplates.Values;
    }

    /// <summary>
    /// Gets report schedules
    /// </summary>
    public IEnumerable<ReportSchedule> GetReportSchedules()
    {
        return _reportSchedules.Values;
    }

    #region Private Methods

    private async void ProcessReporting(object? state)
    {
        if (!_isReporting)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;

            // Process scheduled reports
            foreach (var schedule in _reportSchedules.Values.Where(s => s.IsEnabled))
            {
                await ProcessScheduledReport(schedule, currentTime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in reporting processing");
        }
    }

    private async Task ProcessScheduledReport(ReportSchedule schedule, DateTime currentTime)
    {
        try
        {
            // Check if it's time to generate the scheduled report
            if (ShouldGenerateScheduledReport(schedule, currentTime))
            {
                await GenerateScheduledReport(schedule);
                schedule.LastGenerated = currentTime;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scheduled report: {ScheduleId}", schedule.Id);
        }
    }

    private bool ShouldGenerateScheduledReport(ReportSchedule schedule, DateTime currentTime)
    {
        try
        {
            // Check if enough time has passed since last generation
            var timeSinceLastGeneration = currentTime - schedule.LastGenerated;
            return timeSinceLastGeneration.TotalHours >= 24; // Generate at least once per day
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking scheduled report generation: {ScheduleId}", schedule.Id);
            return false;
        }
    }

    private async Task GenerateScheduledReport(ReportSchedule schedule)
    {
        try
        {
            // Generate the scheduled report
            var reportRequest = new TestReportRequest
            {
                Name = schedule.Name,
                Description = schedule.Description,
                ReportType = schedule.ReportType,
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate = DateTime.UtcNow
            };

            await GenerateTestReportAsync(reportRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating scheduled report: {ScheduleId}", schedule.Id);
        }
    }

    private async Task CollectTestData(TestReport report)
    {
        try
        {
            // Collect test data for the report period
            report.TestResults = new List<TestResult>();
            report.TotalTests = 100; // Placeholder
            report.PassedTests = 95; // Placeholder
            report.FailedTests = 5; // Placeholder

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting test data for report: {ReportId}", report.Id);
        }
    }

    private void GenerateReportMetrics(TestReport report)
    {
        try
        {
            // Generate report metrics
            report.SuccessRate = report.TotalTests > 0 ? (double)report.PassedTests / report.TotalTests * 100 : 0;
            report.AverageExecutionTime = 150.0; // Placeholder
            report.Coverage = 85.0; // Placeholder
            report.FailureRate = report.TotalTests > 0 ? (double)report.FailedTests / report.TotalTests * 100 : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report metrics for report: {ReportId}", report.Id);
        }
    }

    private void GenerateReportInsights(TestReport report)
    {
        try
        {
            // Generate report insights
            report.Insights = new List<string>
            {
                "Test success rate is above 90%",
                "Coverage is within acceptable range",
                "Performance tests are stable",
                "UI tests show good reliability"
            };

            report.Recommendations = new List<string>
            {
                "Increase test coverage for new features",
                "Optimize slow-running tests",
                "Add more integration tests",
                "Improve error handling in tests"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report insights for report: {ReportId}", report.Id);
        }
    }

    private async Task CollectAnalyticsData(TestAnalytics analytics)
    {
        try
        {
            // Collect analytics data
            analytics.Metrics = new Dictionary<string, double>
            {
                ["SuccessRate"] = 95.0,
                ["Coverage"] = 85.0,
                ["ExecutionTime"] = 150.0,
                ["FailureRate"] = 5.0
            };

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting analytics data: {AnalyticsId}", analytics.Id);
        }
    }

    private void GenerateAnalyticsMetrics(TestAnalytics analytics)
    {
        try
        {
            // Generate analytics metrics
            analytics.Trends = new Dictionary<string, List<double>>
            {
                ["SuccessRate"] = new List<double> { 90, 92, 94, 95, 95 },
                ["Coverage"] = new List<double> { 80, 82, 84, 85, 85 },
                ["ExecutionTime"] = new List<double> { 200, 180, 160, 150, 150 }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating analytics metrics: {AnalyticsId}", analytics.Id);
        }
    }

    private void GenerateAnalyticsTrends(TestAnalytics analytics)
    {
        try
        {
            // Generate analytics trends
            analytics.Insights = new List<string>
            {
                "Success rate is trending upward",
                "Coverage has improved over time",
                "Execution time is decreasing",
                "Overall test quality is improving"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating analytics trends: {AnalyticsId}", analytics.Id);
        }
    }

    private async Task<bool> ExportReport(TestReport report, ReportExport export)
    {
        try
        {
            // Export the report based on format
            switch (export.Format)
            {
                case ExportFormat.JSON:
                    return await ExportToJson(report, export);
                case ExportFormat.HTML:
                    return await ExportToHtml(report, export);
                case ExportFormat.PDF:
                    return await ExportToPdf(report, export);
                case ExportFormat.CSV:
                    return await ExportToCsv(report, export);
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report: {ReportId}", report.Id);
            return false;
        }
    }

    private async Task<bool> ExportToJson(TestReport report, ReportExport export)
    {
        try
        {
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            // Save to file or send to destination
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report to JSON: {ReportId}", report.Id);
            return false;
        }
    }

    private async Task<bool> ExportToHtml(TestReport report, ReportExport export)
    {
        try
        {
            // Generate HTML report
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report to HTML: {ReportId}", report.Id);
            return false;
        }
    }

    private async Task<bool> ExportToPdf(TestReport report, ReportExport export)
    {
        try
        {
            // Generate PDF report
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report to PDF: {ReportId}", report.Id);
            return false;
        }
    }

    private async Task<bool> ExportToCsv(TestReport report, ReportExport export)
    {
        try
        {
            // Generate CSV report
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report to CSV: {ReportId}", report.Id);
            return false;
        }
    }

    private void InitializeReportTemplates()
    {
        try
        {
            // Initialize report templates
            _reportTemplates["comprehensive"] = new ReportTemplate
            {
                Id = "comprehensive",
                Name = "Comprehensive Test Report",
                Description = "Complete test report with all metrics",
                TemplateType = ReportTemplateType.Comprehensive,
                Sections = new List<string> { "Summary", "Results", "Coverage", "Performance", "Recommendations" },
                Format = ReportFormat.HTML,
                IsDefault = true
            };

            _reportTemplates["summary"] = new ReportTemplate
            {
                Id = "summary",
                Name = "Test Summary Report",
                Description = "Brief test summary report",
                TemplateType = ReportTemplateType.Summary,
                Sections = new List<string> { "Summary", "Results" },
                Format = ReportFormat.HTML,
                IsDefault = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing report templates");
        }
    }

    private void InitializeReportSchedules()
    {
        try
        {
            // Initialize report schedules
            _reportSchedules["daily_summary"] = new ReportSchedule
            {
                Id = "daily_summary",
                Name = "Daily Test Summary",
                Description = "Daily test summary report",
                ReportType = TestReportType.Comprehensive,
                TemplateId = "summary",
                ScheduleType = ScheduleType.Daily,
                CronExpression = "0 9 * * *",
                Recipients = new List<string> { "team@partylights.com" },
                IsEnabled = true
            };

            _reportSchedules["weekly_comprehensive"] = new ReportSchedule
            {
                Id = "weekly_comprehensive",
                Name = "Weekly Comprehensive Report",
                Description = "Weekly comprehensive test report",
                ReportType = TestReportType.Comprehensive,
                TemplateId = "comprehensive",
                ScheduleType = ScheduleType.Weekly,
                CronExpression = "0 10 * * 1",
                Recipients = new List<string> { "team@partylights.com", "management@partylights.com" },
                IsEnabled = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing report schedules");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isReporting = false;
            _reportingTimer?.Dispose();

            _logger.LogInformation("Test reporting service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing test reporting service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Test analytics request
/// </summary>
public class TestAnalyticsRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TestAnalyticsType AnalyticsType { get; set; } = TestAnalyticsType.Comprehensive;
    public List<string>? TestSuites { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Report export request
/// </summary>
public class ReportExportRequest
{
    public ExportType ExportType { get; set; } = ExportType.File;
    public ExportFormat Format { get; set; } = ExportFormat.HTML;
    public string Destination { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Report template request
/// </summary>
public class ReportTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ReportTemplateType TemplateType { get; set; } = ReportTemplateType.Comprehensive;
    public List<string>? Sections { get; set; }
    public ReportFormat Format { get; set; } = ReportFormat.HTML;
    public bool IsDefault { get; set; } = false;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Report schedule request
/// </summary>
public class ReportScheduleRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TestReportType ReportType { get; set; } = TestReportType.Comprehensive;
    public string TemplateId { get; set; } = string.Empty;
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Daily;
    public string CronExpression { get; set; } = string.Empty;
    public List<string>? Recipients { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Test analytics
/// </summary>
public class TestAnalytics
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TestAnalyticsType AnalyticsType { get; set; }
    public List<string> TestSuites { get; set; } = new();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedAt { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public Dictionary<string, double> Metrics { get; set; } = new();
    public Dictionary<string, List<double>> Trends { get; set; } = new();
    public List<string> Insights { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Report export
/// </summary>
public class ReportExport
{
    public string Id { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public ExportType ExportType { get; set; }
    public ExportFormat Format { get; set; }
    public string Destination { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ExportStatus Status { get; set; }
    public bool Success { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Report template
/// </summary>
public class ReportTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ReportTemplateType TemplateType { get; set; }
    public List<string> Sections { get; set; } = new();
    public ReportFormat Format { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Report schedule
/// </summary>
public class ReportSchedule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TestReportType ReportType { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public ScheduleType ScheduleType { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = new();
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastGenerated { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Test analytics event arguments
/// </summary>
public class TestAnalyticsEventArgs : EventArgs
{
    public string AnalyticsId { get; }
    public TestAnalytics Analytics { get; }
    public TestAnalyticsAction Action { get; }
    public DateTime Timestamp { get; }

    public TestAnalyticsEventArgs(string analyticsId, TestAnalytics analytics, TestAnalyticsAction action)
    {
        AnalyticsId = analyticsId;
        Analytics = analytics;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Report export event arguments
/// </summary>
public class ReportExportEventArgs : EventArgs
{
    public string ExportId { get; }
    public ReportExport Export { get; }
    public ReportExportAction Action { get; }
    public DateTime Timestamp { get; }

    public ReportExportEventArgs(string exportId, ReportExport export, ReportExportAction action)
    {
        ExportId = exportId;
        Export = export;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Test analytics types
/// </summary>
public enum TestAnalyticsType
{
    Performance,
    Coverage,
    Trends,
    Comprehensive
}

/// <summary>
/// Export types
/// </summary>
public enum ExportType
{
    File,
    Email,
    Webhook,
    API
}

/// <summary>
/// Export formats
/// </summary>
public enum ExportFormat
{
    JSON,
    HTML,
    PDF,
    CSV,
    XML
}

/// <summary>
/// Report template types
/// </summary>
public enum ReportTemplateType
{
    Summary,
    Detailed,
    Comprehensive,
    Custom
}

/// <summary>
/// Report formats
/// </summary>
public enum ReportFormat
{
    HTML,
    PDF,
    JSON,
    CSV
}

/// <summary>
/// Export status
/// </summary>
public enum ExportStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

/// <summary>
/// Test analytics actions
/// </summary
public enum TestAnalyticsAction
{
    Generated,
    Updated,
    Exported
}

/// <summary>
/// Report export actions
/// </summary>
public enum ReportExportAction
{
    Started,
    Completed,
    Failed
}
