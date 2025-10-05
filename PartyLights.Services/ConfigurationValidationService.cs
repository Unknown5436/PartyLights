using Microsoft.Extensions.Logging;
using PartyLights.Core.Models;

namespace PartyLights.Services;

/// <summary>
/// Service for runtime configuration validation and monitoring
/// </summary>
public class ConfigurationValidationService
{
    private readonly ILogger<ConfigurationValidationService> _logger;
    private readonly ConfigurationManagerService _configurationManager;
    private readonly Timer _validationTimer;
    private ConfigurationValidationResult? _lastValidationResult;
    private DateTime _lastValidationTime;

    public event EventHandler<ConfigurationValidationEventArgs>? ValidationCompleted;
    public event EventHandler<ConfigurationErrorEventArgs>? ConfigurationError;

    public ConfigurationValidationService(
        ILogger<ConfigurationValidationService> logger,
        ConfigurationManagerService configurationManager)
    {
        _logger = logger;
        _configurationManager = configurationManager;

        // Set up periodic validation (every 5 minutes)
        _validationTimer = new Timer(PerformPeriodicValidation, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    #region Validation Methods

    /// <summary>
    /// Validates the current configuration
    /// </summary>
    public async Task<ConfigurationValidationResult> ValidateCurrentConfigurationAsync()
    {
        try
        {
            _logger.LogDebug("Validating current configuration");

            var config = await _configurationManager.GetCurrentConfigurationAsync();
            var result = await ValidateConfigurationAsync(config);

            _lastValidationResult = result;
            _lastValidationTime = DateTime.UtcNow;

            ValidationCompleted?.Invoke(this, new ConfigurationValidationEventArgs(result));

            if (!result.IsValid)
            {
                ConfigurationError?.Invoke(this, new ConfigurationErrorEventArgs(result.Errors));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating current configuration");
            var errorResult = new ConfigurationValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Validation error: {ex.Message}" }
            };

            ConfigurationError?.Invoke(this, new ConfigurationErrorEventArgs(errorResult.Errors));
            return errorResult;
        }
    }

    /// <summary>
    /// Validates a specific configuration
    /// </summary>
    public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(AppConfiguration configuration)
    {
        try
        {
            var result = new ConfigurationValidationResult();

            // Validate each section
            ValidateAudioConfiguration(configuration.Audio, result);
            ValidateDeviceConfiguration(configuration.Devices, result);
            ValidateEffectConfiguration(configuration.Effects, result);
            ValidateUiConfiguration(configuration.UI, result);
            ValidateSyncConfiguration(configuration.Synchronization, result);
            ValidatePerformanceConfiguration(configuration.Performance, result);
            ValidateSecurityConfiguration(configuration.Security, result);
            ValidateLoggingConfiguration(configuration.Logging, result);

            // Cross-section validation
            ValidateCrossSectionConfiguration(configuration, result);

            result.IsValid = !result.Errors.Any();

            _logger.LogDebug("Configuration validation completed: {IsValid}, {ErrorCount} errors, {WarningCount} warnings",
                result.IsValid, result.Errors.Count, result.Warnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration");
            return new ConfigurationValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Validation error: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Gets the last validation result
    /// </summary>
    public ConfigurationValidationResult? GetLastValidationResult()
    {
        return _lastValidationResult;
    }

    /// <summary>
    /// Gets the last validation time
    /// </summary>
    public DateTime GetLastValidationTime()
    {
        return _lastValidationTime;
    }

    #endregion

    #region Specific Validation Methods

    private void ValidateAudioConfiguration(AudioSettings settings, ConfigurationValidationResult result)
    {
        if (settings == null)
        {
            result.Errors.Add("Audio settings are required");
            return;
        }

        // Validate sensitivity
        if (settings.Sensitivity < 0.1f || settings.Sensitivity > 2.0f)
        {
            result.Errors.Add("Audio sensitivity must be between 0.1 and 2.0");
        }

        // Validate latency
        if (settings.LatencyMs < 10 || settings.LatencyMs > 1000)
        {
            result.Errors.Add("Audio latency must be between 10 and 1000 milliseconds");
        }

        // Validate sample rate
        if (settings.SampleRate < 8000 || settings.SampleRate > 192000)
        {
            result.Errors.Add("Sample rate must be between 8000 and 192000 Hz");
        }

        // Validate buffer size
        if (settings.BufferSize < 256 || settings.BufferSize > 4096)
        {
            result.Errors.Add("Buffer size must be between 256 and 4096 samples");
        }

        // Validate volume threshold
        if (settings.VolumeThreshold < 0.0f || settings.VolumeThreshold > 1.0f)
        {
            result.Errors.Add("Volume threshold must be between 0.0 and 1.0");
        }

        // Validate frequency bands
        if (settings.FrequencyBands < 4 || settings.FrequencyBands > 32)
        {
            result.Warnings.Add("Frequency bands should be between 4 and 32 for optimal performance");
        }
    }

    private void ValidateDeviceConfiguration(DeviceSettings settings, ConfigurationValidationResult result)
    {
        if (settings == null)
        {
            result.Errors.Add("Device settings are required");
            return;
        }

        // Validate discovery timeout
        if (settings.DiscoveryTimeoutSeconds < 5 || settings.DiscoveryTimeoutSeconds > 60)
        {
            result.Errors.Add("Discovery timeout must be between 5 and 60 seconds");
        }

        // Validate connection retry attempts
        if (settings.ConnectionRetryAttempts < 1 || settings.ConnectionRetryAttempts > 10)
        {
            result.Errors.Add("Connection retry attempts must be between 1 and 10");
        }

        // Validate connection timeout
        if (settings.ConnectionTimeoutSeconds < 1 || settings.ConnectionTimeoutSeconds > 30)
        {
            result.Errors.Add("Connection timeout must be between 1 and 30 seconds");
        }

        // Validate state cache timeout
        if (settings.StateCacheTimeoutSeconds < 10 || settings.StateCacheTimeoutSeconds > 300)
        {
            result.Warnings.Add("State cache timeout should be between 10 and 300 seconds");
        }

        // Validate device configurations
        foreach (var device in settings.Devices)
        {
            ValidateDevice(device, result);
        }

        // Validate device groups
        foreach (var group in settings.DeviceGroups.Values)
        {
            ValidateDeviceGroup(group, result);
        }
    }

    private void ValidateEffectConfiguration(EffectSettings settings, ConfigurationValidationResult result)
    {
        if (settings == null)
        {
            result.Errors.Add("Effect settings are required");
            return;
        }

        // Validate effect intensity
        if (settings.EffectIntensity < 0.1f || settings.EffectIntensity > 1.0f)
        {
            result.Errors.Add("Effect intensity must be between 0.1 and 1.0");
        }

        // Validate effect update interval
        if (settings.EffectUpdateIntervalMs < 50 || settings.EffectUpdateIntervalMs > 1000)
        {
            result.Errors.Add("Effect update interval must be between 50 and 1000 milliseconds");
        }

        // Validate transition duration
        if (settings.TransitionDurationMs < 100 || settings.TransitionDurationMs > 5000)
        {
            result.Errors.Add("Transition duration must be between 100 and 5000 milliseconds");
        }

        // Check for conflicting effects
        var enabledEffects = new List<string>();
        if (settings.EnableBeatSync) enabledEffects.Add("Beat Sync");
        if (settings.EnableFrequencyVisualization) enabledEffects.Add("Frequency Visualization");
        if (settings.EnableMoodLighting) enabledEffects.Add("Mood Lighting");
        if (settings.EnableSpectrumAnalyzer) enabledEffects.Add("Spectrum Analyzer");
        if (settings.EnablePartyMode) enabledEffects.Add("Party Mode");

        if (enabledEffects.Count > 3)
        {
            result.Warnings.Add($"Multiple effects enabled ({enabledEffects.Count}): {string.Join(", ", enabledEffects)}. This may impact performance.");
        }
    }

    private void ValidateUiConfiguration(UiSettings settings, ConfigurationValidationResult result)
    {
        if (settings == null)
        {
            result.Errors.Add("UI settings are required");
            return;
        }

        // Validate window size
        if (settings.WindowSize.Width < 800 || settings.WindowSize.Height < 600)
        {
            result.Errors.Add("Window size must be at least 800x600 pixels");
        }

        if (settings.WindowSize.Width > 3840 || settings.WindowSize.Height > 2160)
        {
            result.Warnings.Add("Window size is very large, may impact performance on lower-end systems");
        }

        // Validate visualizer update rate
        if (settings.VisualizerUpdateRate < 30 || settings.VisualizerUpdateRate > 120)
        {
            result.Errors.Add("Visualizer update rate must be between 30 and 120 FPS");
        }

        // Validate theme
        var validThemes = new[] { "Dark", "Light", "Auto" };
        if (!validThemes.Contains(settings.Theme))
        {
            result.Errors.Add($"Theme must be one of: {string.Join(", ", validThemes)}");
        }

        // Validate window position
        var validPositions = new[] { "Center", "TopLeft", "TopRight", "BottomLeft", "BottomRight", "TopCenter", "BottomCenter", "LeftCenter", "RightCenter" };
        if (!validPositions.Contains(settings.WindowPosition))
        {
            result.Warnings.Add($"Window position '{settings.WindowPosition}' is not recognized, using 'Center'");
        }
    }

    private void ValidateSyncConfiguration(SyncSettings settings, ConfigurationValidationResult result)
    {
        if (settings == null)
        {
            result.Errors.Add("Sync settings are required");
            return;
        }

        // Validate sync interval
        if (settings.SyncIntervalMs < 50 || settings.SyncIntervalMs > 1000)
        {
            result.Errors.Add("Sync interval must be between 50 and 1000 milliseconds");
        }

        // Validate sync sensitivity
        if (settings.SyncSensitivity < 0.1f || settings.SyncSensitivity > 1.0f)
        {
            result.Errors.Add("Sync sensitivity must be between 0.1 and 1.0");
        }

        // Validate command timeout
        if (settings.CommandTimeoutMs < 1000 || settings.CommandTimeoutMs > 30000)
        {
            result.Errors.Add("Command timeout must be between 1000 and 30000 milliseconds");
        }

        // Validate batch size
        if (settings.BatchSize < 1 || settings.BatchSize > 100)
        {
            result.Errors.Add("Batch size must be between 1 and 100");
        }

        // Validate max queue size
        if (settings.MaxQueueSize < 100 || settings.MaxQueueSize > 10000)
        {
            result.Warnings.Add("Max queue size should be between 100 and 10000 for optimal performance");
        }
    }

    private void ValidatePerformanceConfiguration(PerformanceSettings settings, ConfigurationValidationResult result)
    {
        if (settings == null)
        {
            result.Errors.Add("Performance settings are required");
            return;
        }

        // Validate memory usage
        if (settings.MaxMemoryUsageMB < 128 || settings.MaxMemoryUsageMB > 2048)
        {
            result.Errors.Add("Max memory usage must be between 128 and 2048 MB");
        }

        // Validate thread pool settings
        if (settings.MinThreadPoolThreads < 1 || settings.MinThreadPoolThreads > 32)
        {
            result.Errors.Add("Min thread pool threads must be between 1 and 32");
        }

        if (settings.MaxThreadPoolThreads < 1 || settings.MaxThreadPoolThreads > 64)
        {
            result.Errors.Add("Max thread pool threads must be between 1 and 64");
        }

        if (settings.MinThreadPoolThreads > settings.MaxThreadPoolThreads)
        {
            result.Errors.Add("Min thread pool threads cannot be greater than max thread pool threads");
        }

        // Validate async concurrency level
        if (settings.AsyncConcurrencyLevel < 1 || settings.AsyncConcurrencyLevel > 16)
        {
            result.Errors.Add("Async concurrency level must be between 1 and 16");
        }

        // Validate metrics collection interval
        if (settings.MetricsCollectionIntervalMs < 500 || settings.MetricsCollectionIntervalMs > 10000)
        {
            result.Warnings.Add("Metrics collection interval should be between 500 and 10000 milliseconds");
        }
    }

    private void ValidateSecurityConfiguration(SecuritySettings settings, ConfigurationValidationResult result)
    {
        if (settings == null)
        {
            result.Errors.Add("Security settings are required");
            return;
        }

        // Validate session timeout
        if (settings.SessionTimeoutMinutes < 5 || settings.SessionTimeoutMinutes > 480)
        {
            result.Errors.Add("Session timeout must be between 5 and 480 minutes");
        }

        // Validate encryption key if encryption is enabled
        if (settings.EnableEncryption && string.IsNullOrEmpty(settings.EncryptionKey))
        {
            result.Warnings.Add("Encryption is enabled but no encryption key is set");
        }

        // Validate allowed IP addresses
        foreach (var ip in settings.AllowedIpAddresses)
        {
            if (!System.Net.IPAddress.TryParse(ip, out _))
            {
                result.Errors.Add($"Invalid IP address format: {ip}");
            }
        }
    }

    private void ValidateLoggingConfiguration(LoggingSettings settings, ConfigurationValidationResult result)
    {
        if (settings == null)
        {
            result.Errors.Add("Logging settings are required");
            return;
        }

        // Validate log level
        var validLogLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
        if (!validLogLevels.Contains(settings.LogLevel))
        {
            result.Errors.Add($"Log level must be one of: {string.Join(", ", validLogLevels)}");
        }

        // Validate log file size
        if (settings.MaxLogFileSizeMB < 1 || settings.MaxLogFileSizeMB > 100)
        {
            result.Errors.Add("Max log file size must be between 1 and 100 MB");
        }

        // Validate max log files
        if (settings.MaxLogFiles < 1 || settings.MaxLogFiles > 50)
        {
            result.Warnings.Add("Max log files should be between 1 and 50");
        }

        // Validate log file path
        if (settings.EnableFileLogging && string.IsNullOrEmpty(settings.LogFilePath))
        {
            result.Errors.Add("Log file path is required when file logging is enabled");
        }
    }

    private void ValidateDevice(SmartDevice device, ConfigurationValidationResult result)
    {
        if (string.IsNullOrEmpty(device.Id))
        {
            result.Errors.Add($"Device ID is required for device: {device.Name}");
        }

        if (string.IsNullOrEmpty(device.Name))
        {
            result.Errors.Add($"Device name is required for device ID: {device.Id}");
        }

        if (string.IsNullOrEmpty(device.IpAddress))
        {
            result.Errors.Add($"IP address is required for device: {device.Name}");
        }
        else if (!System.Net.IPAddress.TryParse(device.IpAddress, out _))
        {
            result.Errors.Add($"Invalid IP address format for device {device.Name}: {device.IpAddress}");
        }

        if (device.Capabilities == null)
        {
            result.Warnings.Add($"Device capabilities not set for device: {device.Name}");
        }
    }

    private void ValidateDeviceGroup(DeviceGroup group, ConfigurationValidationResult result)
    {
        if (string.IsNullOrEmpty(group.Id))
        {
            result.Errors.Add($"Group ID is required for group: {group.Name}");
        }

        if (string.IsNullOrEmpty(group.Name))
        {
            result.Errors.Add($"Group name is required for group ID: {group.Id}");
        }

        if (group.DeviceIds == null || !group.DeviceIds.Any())
        {
            result.Warnings.Add($"Device group '{group.Name}' has no devices assigned");
        }
    }

    private void ValidateCrossSectionConfiguration(AppConfiguration configuration, ConfigurationValidationResult result)
    {
        // Validate that enabled device types in sync settings match available devices
        if (configuration.Synchronization.EnabledDeviceTypes.Any())
        {
            var availableDeviceTypes = configuration.Devices.Devices.Select(d => d.Type).Distinct();
            var enabledTypesNotAvailable = configuration.Synchronization.EnabledDeviceTypes
                .Except(availableDeviceTypes).ToList();

            if (enabledTypesNotAvailable.Any())
            {
                result.Warnings.Add($"Sync enabled for device types that are not available: {string.Join(", ", enabledTypesNotAvailable)}");
            }
        }

        // Validate that enabled device groups exist
        if (configuration.Synchronization.EnabledDeviceGroups.Any())
        {
            var availableGroups = configuration.Devices.DeviceGroups.Keys;
            var enabledGroupsNotAvailable = configuration.Synchronization.EnabledDeviceGroups
                .Except(availableGroups).ToList();

            if (enabledGroupsNotAvailable.Any())
            {
                result.Warnings.Add($"Sync enabled for device groups that do not exist: {string.Join(", ", enabledGroupsNotAvailable)}");
            }
        }

        // Validate performance settings against system capabilities
        var totalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
        if (configuration.Performance.MaxMemoryUsageMB < totalMemoryMB * 0.5)
        {
            result.Warnings.Add($"Max memory usage ({configuration.Performance.MaxMemoryUsageMB} MB) may be too low for current system usage ({totalMemoryMB} MB)");
        }
    }

    #endregion

    #region Private Methods

    private async void PerformPeriodicValidation(object? state)
    {
        try
        {
            await ValidateCurrentConfigurationAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic configuration validation");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _validationTimer?.Dispose();
    }

    #endregion
}

/// <summary>
/// Event arguments for configuration error events
/// </summary>
public class ConfigurationErrorEventArgs : EventArgs
{
    public List<string> Errors { get; }
    public DateTime ErrorTime { get; }

    public ConfigurationErrorEventArgs(List<string> errors)
    {
        Errors = errors;
        ErrorTime = DateTime.UtcNow;
    }
}
