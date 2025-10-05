using Microsoft.Extensions.Logging;
using PartyLights.Core.Models;

namespace PartyLights.Services;

/// <summary>
/// Service for managing configuration through the UI
/// </summary>
public class ConfigurationUiService
{
    private readonly ILogger<ConfigurationUiService> _logger;
    private readonly ConfigurationManagerService _configurationManager;
    private readonly Dictionary<string, object> _pendingChanges = new();
    private bool _hasUnsavedChanges;

    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
    public event EventHandler<bool>? UnsavedChangesChanged;

    public ConfigurationUiService(
        ILogger<ConfigurationUiService> logger,
        ConfigurationManagerService configurationManager)
    {
        _logger = logger;
        _configurationManager = configurationManager;

        // Subscribe to configuration changes
        _configurationManager.ConfigurationChanged += OnConfigurationChanged;
    }

    #region Configuration Access

    /// <summary>
    /// Gets current audio settings
    /// </summary>
    public async Task<AudioSettings> GetAudioSettingsAsync()
    {
        try
        {
            var config = await _configurationManager.GetCurrentConfigurationAsync();
            return config.Audio;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audio settings");
            return new AudioSettings();
        }
    }

    /// <summary>
    /// Gets current device settings
    /// </summary>
    public async Task<DeviceSettings> GetDeviceSettingsAsync()
    {
        try
        {
            var config = await _configurationManager.GetCurrentConfigurationAsync();
            return config.Devices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device settings");
            return new DeviceSettings();
        }
    }

    /// <summary>
    /// Gets current effect settings
    /// </summary>
    public async Task<EffectSettings> GetEffectSettingsAsync()
    {
        try
        {
            var config = await _configurationManager.GetCurrentConfigurationAsync();
            return config.Effects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting effect settings");
            return new EffectSettings();
        }
    }

    /// <summary>
    /// Gets current UI settings
    /// </summary>
    public async Task<UiSettings> GetUiSettingsAsync()
    {
        try
        {
            var config = await _configurationManager.GetCurrentConfigurationAsync();
            return config.UI;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting UI settings");
            return new UiSettings();
        }
    }

    /// <summary>
    /// Gets current sync settings
    /// </summary>
    public async Task<SyncSettings> GetSyncSettingsAsync()
    {
        try
        {
            var config = await _configurationManager.GetCurrentConfigurationAsync();
            return config.Synchronization;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync settings");
            return new SyncSettings();
        }
    }

    /// <summary>
    /// Gets current performance settings
    /// </summary>
    public async Task<PerformanceSettings> GetPerformanceSettingsAsync()
    {
        try
        {
            var config = await _configurationManager.GetCurrentConfigurationAsync();
            return config.Performance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance settings");
            return new PerformanceSettings();
        }
    }

    /// <summary>
    /// Gets current security settings
    /// </summary>
    public async Task<SecuritySettings> GetSecuritySettingsAsync()
    {
        try
        {
            var config = await _configurationManager.GetCurrentConfigurationAsync();
            return config.Security;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting security settings");
            return new SecuritySettings();
        }
    }

    /// <summary>
    /// Gets current logging settings
    /// </summary>
    public async Task<LoggingSettings> GetLoggingSettingsAsync()
    {
        try
        {
            var config = await _configurationManager.GetCurrentConfigurationAsync();
            return config.Logging;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting logging settings");
            return new LoggingSettings();
        }
    }

    #endregion

    #region Settings Updates

    /// <summary>
    /// Updates audio settings with validation
    /// </summary>
    public async Task<bool> UpdateAudioSettingsAsync(AudioSettings audioSettings)
    {
        try
        {
            // Validate settings
            var validationResult = ValidateAudioSettings(audioSettings);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Audio settings validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return false;
            }

            var success = await _configurationManager.UpdateAudioSettingsAsync(audioSettings);
            if (success)
            {
                MarkSettingChanged("Audio", audioSettings);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating audio settings");
            return false;
        }
    }

    /// <summary>
    /// Updates device settings with validation
    /// </summary>
    public async Task<bool> UpdateDeviceSettingsAsync(DeviceSettings deviceSettings)
    {
        try
        {
            // Validate settings
            var validationResult = ValidateDeviceSettings(deviceSettings);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Device settings validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return false;
            }

            var success = await _configurationManager.UpdateDeviceSettingsAsync(deviceSettings);
            if (success)
            {
                MarkSettingChanged("Devices", deviceSettings);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device settings");
            return false;
        }
    }

    /// <summary>
    /// Updates effect settings with validation
    /// </summary>
    public async Task<bool> UpdateEffectSettingsAsync(EffectSettings effectSettings)
    {
        try
        {
            // Validate settings
            var validationResult = ValidateEffectSettings(effectSettings);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Effect settings validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return false;
            }

            var success = await _configurationManager.UpdateEffectSettingsAsync(effectSettings);
            if (success)
            {
                MarkSettingChanged("Effects", effectSettings);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating effect settings");
            return false;
        }
    }

    /// <summary>
    /// Updates UI settings with validation
    /// </summary>
    public async Task<bool> UpdateUiSettingsAsync(UiSettings uiSettings)
    {
        try
        {
            // Validate settings
            var validationResult = ValidateUiSettings(uiSettings);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("UI settings validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return false;
            }

            var success = await _configurationManager.UpdateUiSettingsAsync(uiSettings);
            if (success)
            {
                MarkSettingChanged("UI", uiSettings);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating UI settings");
            return false;
        }
    }

    /// <summary>
    /// Updates sync settings with validation
    /// </summary>
    public async Task<bool> UpdateSyncSettingsAsync(SyncSettings syncSettings)
    {
        try
        {
            // Validate settings
            var validationResult = ValidateSyncSettings(syncSettings);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Sync settings validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return false;
            }

            var success = await _configurationManager.UpdateSyncSettingsAsync(syncSettings);
            if (success)
            {
                MarkSettingChanged("Synchronization", syncSettings);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sync settings");
            return false;
        }
    }

    /// <summary>
    /// Updates performance settings with validation
    /// </summary>
    public async Task<bool> UpdatePerformanceSettingsAsync(PerformanceSettings performanceSettings)
    {
        try
        {
            // Validate settings
            var validationResult = ValidatePerformanceSettings(performanceSettings);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Performance settings validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return false;
            }

            var success = await _configurationManager.UpdatePerformanceSettingsAsync(performanceSettings);
            if (success)
            {
                MarkSettingChanged("Performance", performanceSettings);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance settings");
            return false;
        }
    }

    /// <summary>
    /// Updates security settings with validation
    /// </summary>
    public async Task<bool> UpdateSecuritySettingsAsync(SecuritySettings securitySettings)
    {
        try
        {
            // Validate settings
            var validationResult = ValidateSecuritySettings(securitySettings);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Security settings validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return false;
            }

            var success = await _configurationManager.UpdateSecuritySettingsAsync(securitySettings);
            if (success)
            {
                MarkSettingChanged("Security", securitySettings);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating security settings");
            return false;
        }
    }

    /// <summary>
    /// Updates logging settings with validation
    /// </summary>
    public async Task<bool> UpdateLoggingSettingsAsync(LoggingSettings loggingSettings)
    {
        try
        {
            // Validate settings
            var validationResult = ValidateLoggingSettings(loggingSettings);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Logging settings validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return false;
            }

            var success = await _configurationManager.UpdateLoggingSettingsAsync(loggingSettings);
            if (success)
            {
                MarkSettingChanged("Logging", loggingSettings);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating logging settings");
            return false;
        }
    }

    #endregion

    #region Configuration Operations

    /// <summary>
    /// Saves all pending changes
    /// </summary>
    public async Task<bool> SaveAllChangesAsync()
    {
        try
        {
            if (!_hasUnsavedChanges)
            {
                return true; // No changes to save
            }

            _logger.LogInformation("Saving all pending configuration changes");

            var config = await _configurationManager.GetCurrentConfigurationAsync();
            var success = await _configurationManager.UpdateConfigurationAsync(config);

            if (success)
            {
                _pendingChanges.Clear();
                _hasUnsavedChanges = false;
                UnsavedChangesChanged?.Invoke(this, false);
                _logger.LogInformation("All configuration changes saved successfully");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving all configuration changes");
            return false;
        }
    }

    /// <summary>
    /// Discards all pending changes
    /// </summary>
    public void DiscardAllChanges()
    {
        try
        {
            _logger.LogInformation("Discarding all pending configuration changes");

            _pendingChanges.Clear();
            _hasUnsavedChanges = false;
            UnsavedChangesChanged?.Invoke(this, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discarding configuration changes");
        }
    }

    /// <summary>
    /// Checks if there are unsaved changes
    /// </summary>
    public bool HasUnsavedChanges()
    {
        return _hasUnsavedChanges;
    }

    /// <summary>
    /// Resets configuration to defaults
    /// </summary>
    public async Task<AppConfiguration> ResetToDefaultsAsync()
    {
        try
        {
            _logger.LogInformation("Resetting configuration to defaults");

            var defaultConfig = await _configurationManager.ResetToDefaultsAsync();

            _pendingChanges.Clear();
            _hasUnsavedChanges = false;
            UnsavedChangesChanged?.Invoke(this, false);

            return defaultConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting configuration to defaults");
            throw;
        }
    }

    /// <summary>
    /// Exports configuration to file
    /// </summary>
    public async Task<bool> ExportConfigurationAsync(string filePath)
    {
        try
        {
            return await _configurationManager.ExportConfigurationAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting configuration");
            return false;
        }
    }

    /// <summary>
    /// Imports configuration from file
    /// </summary>
    public async Task<AppConfiguration> ImportConfigurationAsync(string filePath)
    {
        try
        {
            var importedConfig = await _configurationManager.ImportConfigurationAsync(filePath);

            _pendingChanges.Clear();
            _hasUnsavedChanges = false;
            UnsavedChangesChanged?.Invoke(this, false);

            return importedConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing configuration");
            throw;
        }
    }

    #endregion

    #region Private Methods

    private void MarkSettingChanged(string settingName, object settingValue)
    {
        _pendingChanges[settingName] = settingValue;
        _hasUnsavedChanges = true;
        UnsavedChangesChanged?.Invoke(this, true);
    }

    private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
    {
        ConfigurationChanged?.Invoke(this, e);
    }

    private ConfigurationValidationResult ValidateAudioSettings(AudioSettings settings)
    {
        var result = new ConfigurationValidationResult();

        if (settings.Sensitivity < 0.1f || settings.Sensitivity > 2.0f)
        {
            result.Errors.Add("Audio sensitivity must be between 0.1 and 2.0");
        }

        if (settings.LatencyMs < 10 || settings.LatencyMs > 1000)
        {
            result.Errors.Add("Audio latency must be between 10 and 1000 milliseconds");
        }

        if (settings.SampleRate < 8000 || settings.SampleRate > 192000)
        {
            result.Errors.Add("Sample rate must be between 8000 and 192000 Hz");
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private ConfigurationValidationResult ValidateDeviceSettings(DeviceSettings settings)
    {
        var result = new ConfigurationValidationResult();

        if (settings.DiscoveryTimeoutSeconds < 5 || settings.DiscoveryTimeoutSeconds > 60)
        {
            result.Errors.Add("Discovery timeout must be between 5 and 60 seconds");
        }

        if (settings.ConnectionRetryAttempts < 1 || settings.ConnectionRetryAttempts > 10)
        {
            result.Errors.Add("Connection retry attempts must be between 1 and 10");
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private ConfigurationValidationResult ValidateEffectSettings(EffectSettings settings)
    {
        var result = new ConfigurationValidationResult();

        if (settings.EffectIntensity < 0.1f || settings.EffectIntensity > 1.0f)
        {
            result.Errors.Add("Effect intensity must be between 0.1 and 1.0");
        }

        if (settings.EffectUpdateIntervalMs < 50 || settings.EffectUpdateIntervalMs > 1000)
        {
            result.Errors.Add("Effect update interval must be between 50 and 1000 milliseconds");
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private ConfigurationValidationResult ValidateUiSettings(UiSettings settings)
    {
        var result = new ConfigurationValidationResult();

        if (settings.WindowSize.Width < 800 || settings.WindowSize.Height < 600)
        {
            result.Errors.Add("Window size must be at least 800x600 pixels");
        }

        if (settings.VisualizerUpdateRate < 30 || settings.VisualizerUpdateRate > 120)
        {
            result.Errors.Add("Visualizer update rate must be between 30 and 120 FPS");
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private ConfigurationValidationResult ValidateSyncSettings(SyncSettings settings)
    {
        var result = new ConfigurationValidationResult();

        if (settings.SyncIntervalMs < 50 || settings.SyncIntervalMs > 1000)
        {
            result.Errors.Add("Sync interval must be between 50 and 1000 milliseconds");
        }

        if (settings.SyncSensitivity < 0.1f || settings.SyncSensitivity > 1.0f)
        {
            result.Errors.Add("Sync sensitivity must be between 0.1 and 1.0");
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private ConfigurationValidationResult ValidatePerformanceSettings(PerformanceSettings settings)
    {
        var result = new ConfigurationValidationResult();

        if (settings.MaxMemoryUsageMB < 128 || settings.MaxMemoryUsageMB > 2048)
        {
            result.Errors.Add("Max memory usage must be between 128 and 2048 MB");
        }

        if (settings.MinThreadPoolThreads < 1 || settings.MinThreadPoolThreads > 32)
        {
            result.Errors.Add("Min thread pool threads must be between 1 and 32");
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private ConfigurationValidationResult ValidateSecuritySettings(SecuritySettings settings)
    {
        var result = new ConfigurationValidationResult();

        if (settings.SessionTimeoutMinutes < 5 || settings.SessionTimeoutMinutes > 480)
        {
            result.Errors.Add("Session timeout must be between 5 and 480 minutes");
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private ConfigurationValidationResult ValidateLoggingSettings(LoggingSettings settings)
    {
        var result = new ConfigurationValidationResult();

        var validLogLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
        if (!validLogLevels.Contains(settings.LogLevel))
        {
            result.Errors.Add($"Log level must be one of: {string.Join(", ", validLogLevels)}");
        }

        if (settings.MaxLogFileSizeMB < 1 || settings.MaxLogFileSizeMB > 100)
        {
            result.Errors.Add("Max log file size must be between 1 and 100 MB");
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    #endregion
}
