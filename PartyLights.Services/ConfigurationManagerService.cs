using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;

namespace PartyLights.Services;

/// <summary>
/// High-level configuration management service
/// </summary>
public class ConfigurationManagerService
{
    private readonly ILogger<ConfigurationManagerService> _logger;
    private readonly IConfigurationService _configurationService;
    private AppConfiguration? _currentConfiguration;
    private readonly SemaphoreSlim _configSemaphore = new(1, 1);

    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
    public event EventHandler<ConfigurationValidationEventArgs>? ConfigurationValidated;

    public ConfigurationManagerService(
        ILogger<ConfigurationManagerService> logger,
        IConfigurationService configurationService)
    {
        _logger = logger;
        _configurationService = configurationService;
    }

    #region Configuration Access

    /// <summary>
    /// Gets the current configuration
    /// </summary>
    public async Task<AppConfiguration> GetCurrentConfigurationAsync()
    {
        await _configSemaphore.WaitAsync();
        try
        {
            if (_currentConfiguration == null)
            {
                _currentConfiguration = await _configurationService.LoadConfigurationAsync();
            }
            return _currentConfiguration;
        }
        finally
        {
            _configSemaphore.Release();
        }
    }

    /// <summary>
    /// Updates the current configuration
    /// </summary>
    public async Task<bool> UpdateConfigurationAsync(AppConfiguration configuration)
    {
        await _configSemaphore.WaitAsync();
        try
        {
            // Validate configuration
            var validationResult = ValidateConfiguration(configuration);
            ConfigurationValidated?.Invoke(this, new ConfigurationValidationEventArgs(validationResult));

            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Configuration validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return false;
            }

            // Save configuration
            await _configurationService.SaveConfigurationAsync(configuration);
            _currentConfiguration = configuration;

            // Notify configuration changed
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(configuration));

            _logger.LogInformation("Configuration updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating configuration");
            return false;
        }
        finally
        {
            _configSemaphore.Release();
        }
    }

    #endregion

    #region Specific Settings Management

    /// <summary>
    /// Updates audio settings
    /// </summary>
    public async Task<bool> UpdateAudioSettingsAsync(AudioSettings audioSettings)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            config.Audio = audioSettings;
            return await UpdateConfigurationAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating audio settings");
            return false;
        }
    }

    /// <summary>
    /// Updates device settings
    /// </summary>
    public async Task<bool> UpdateDeviceSettingsAsync(DeviceSettings deviceSettings)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            config.Devices = deviceSettings;
            return await UpdateConfigurationAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device settings");
            return false;
        }
    }

    /// <summary>
    /// Updates effect settings
    /// </summary>
    public async Task<bool> UpdateEffectSettingsAsync(EffectSettings effectSettings)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            config.Effects = effectSettings;
            return await UpdateConfigurationAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating effect settings");
            return false;
        }
    }

    /// <summary>
    /// Updates UI settings
    /// </summary>
    public async Task<bool> UpdateUiSettingsAsync(UiSettings uiSettings)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            config.UI = uiSettings;
            return await UpdateConfigurationAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating UI settings");
            return false;
        }
    }

    /// <summary>
    /// Updates synchronization settings
    /// </summary>
    public async Task<bool> UpdateSyncSettingsAsync(SyncSettings syncSettings)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            config.Synchronization = syncSettings;
            return await UpdateConfigurationAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sync settings");
            return false;
        }
    }

    /// <summary>
    /// Updates performance settings
    /// </summary>
    public async Task<bool> UpdatePerformanceSettingsAsync(PerformanceSettings performanceSettings)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            config.Performance = performanceSettings;
            return await UpdateConfigurationAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance settings");
            return false;
        }
    }

    /// <summary>
    /// Updates security settings
    /// </summary>
    public async Task<bool> UpdateSecuritySettingsAsync(SecuritySettings securitySettings)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            config.Security = securitySettings;
            return await UpdateConfigurationAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating security settings");
            return false;
        }
    }

    /// <summary>
    /// Updates logging settings
    /// </summary>
    public async Task<bool> UpdateLoggingSettingsAsync(LoggingSettings loggingSettings)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            config.Logging = loggingSettings;
            return await UpdateConfigurationAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating logging settings");
            return false;
        }
    }

    #endregion

    #region Device Configuration Management

    /// <summary>
    /// Adds a device to the configuration
    /// </summary>
    public async Task<bool> AddDeviceAsync(SmartDevice device)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();

            // Remove existing device with same ID
            config.Devices.Devices.RemoveAll(d => d.Id == device.Id);

            // Add new device
            config.Devices.Devices.Add(device);

            return await UpdateConfigurationAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding device to configuration");
            return false;
        }
    }

    /// <summary>
    /// Removes a device from the configuration
    /// </summary>
    public async Task<bool> RemoveDeviceAsync(string deviceId)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();

            var removed = config.Devices.Devices.RemoveAll(d => d.Id == deviceId) > 0;

            if (removed)
            {
                return await UpdateConfigurationAsync(config);
            }

            return true; // Device wasn't found, consider it successful
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing device from configuration");
            return false;
        }
    }

    /// <summary>
    /// Updates a device in the configuration
    /// </summary>
    public async Task<bool> UpdateDeviceAsync(SmartDevice device)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();

            var existingDevice = config.Devices.Devices.FirstOrDefault(d => d.Id == device.Id);
            if (existingDevice != null)
            {
                var index = config.Devices.Devices.IndexOf(existingDevice);
                config.Devices.Devices[index] = device;

                return await UpdateConfigurationAsync(config);
            }

            return false; // Device not found
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device in configuration");
            return false;
        }
    }

    /// <summary>
    /// Gets a device from the configuration
    /// </summary>
    public async Task<SmartDevice?> GetDeviceAsync(string deviceId)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            return config.Devices.Devices.FirstOrDefault(d => d.Id == deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device from configuration");
            return null;
        }
    }

    /// <summary>
    /// Gets all devices from the configuration
    /// </summary>
    public async Task<IEnumerable<SmartDevice>> GetAllDevicesAsync()
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            return config.Devices.Devices.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all devices from configuration");
            return Enumerable.Empty<SmartDevice>();
        }
    }

    #endregion

    #region Device Group Management

    /// <summary>
    /// Adds a device group to the configuration
    /// </summary>
    public async Task<bool> AddDeviceGroupAsync(DeviceGroup deviceGroup)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            config.Devices.DeviceGroups[deviceGroup.Name] = deviceGroup;

            return await UpdateConfigurationAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding device group to configuration");
            return false;
        }
    }

    /// <summary>
    /// Removes a device group from the configuration
    /// </summary>
    public async Task<bool> RemoveDeviceGroupAsync(string groupName)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();

            var removed = config.Devices.DeviceGroups.Remove(groupName);

            if (removed)
            {
                return await UpdateConfigurationAsync(config);
            }

            return true; // Group wasn't found, consider it successful
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing device group from configuration");
            return false;
        }
    }

    /// <summary>
    /// Gets a device group from the configuration
    /// </summary>
    public async Task<DeviceGroup?> GetDeviceGroupAsync(string groupName)
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            config.Devices.DeviceGroups.TryGetValue(groupName, out var group);
            return group;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device group from configuration");
            return null;
        }
    }

    /// <summary>
    /// Gets all device groups from the configuration
    /// </summary>
    public async Task<IEnumerable<DeviceGroup>> GetAllDeviceGroupsAsync()
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            return config.Devices.DeviceGroups.Values.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all device groups from configuration");
            return Enumerable.Empty<DeviceGroup>();
        }
    }

    #endregion

    #region Configuration Operations

    /// <summary>
    /// Resets configuration to defaults
    /// </summary>
    public async Task<AppConfiguration> ResetToDefaultsAsync()
    {
        try
        {
            _logger.LogInformation("Resetting configuration to defaults");

            var defaultConfig = await _configurationService.ResetToDefaultsAsync();
            _currentConfiguration = defaultConfig;

            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(defaultConfig));

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
            return await _configurationService.ExportConfigurationAsync(filePath);
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
            var importedConfig = await _configurationService.ImportConfigurationAsync(filePath);
            _currentConfiguration = importedConfig;

            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(importedConfig));

            return importedConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing configuration");
            throw;
        }
    }

    /// <summary>
    /// Validates current configuration
    /// </summary>
    public async Task<ConfigurationValidationResult> ValidateCurrentConfigurationAsync()
    {
        try
        {
            var config = await GetCurrentConfigurationAsync();
            return ValidateConfiguration(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating current configuration");
            return new ConfigurationValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Validation error: {ex.Message}" }
            };
        }
    }

    #endregion

    #region Private Methods

    private ConfigurationValidationResult ValidateConfiguration(AppConfiguration configuration)
    {
        var result = new ConfigurationValidationResult();

        try
        {
            // Basic validation
            if (configuration.Audio == null)
            {
                result.Errors.Add("Audio settings are required");
            }

            if (configuration.Devices == null)
            {
                result.Errors.Add("Device settings are required");
            }

            if (configuration.Effects == null)
            {
                result.Errors.Add("Effect settings are required");
            }

            if (configuration.UI == null)
            {
                result.Errors.Add("UI settings are required");
            }

            // Validate specific settings
            if (configuration.Audio != null)
            {
                ValidateAudioSettings(configuration.Audio, result);
            }

            if (configuration.Devices != null)
            {
                ValidateDeviceSettings(configuration.Devices, result);
            }

            if (configuration.Effects != null)
            {
                ValidateEffectSettings(configuration.Effects, result);
            }

            if (configuration.UI != null)
            {
                ValidateUiSettings(configuration.UI, result);
            }

            result.IsValid = !result.Errors.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration");
            result.Errors.Add($"Validation error: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    private void ValidateAudioSettings(AudioSettings settings, ConfigurationValidationResult result)
    {
        if (settings.Sensitivity < 0.1f || settings.Sensitivity > 2.0f)
        {
            result.Errors.Add("Audio sensitivity must be between 0.1 and 2.0");
        }

        if (settings.LatencyMs < 10 || settings.LatencyMs > 1000)
        {
            result.Errors.Add("Audio latency must be between 10 and 1000 milliseconds");
        }
    }

    private void ValidateDeviceSettings(DeviceSettings settings, ConfigurationValidationResult result)
    {
        if (settings.DiscoveryTimeoutSeconds < 5 || settings.DiscoveryTimeoutSeconds > 60)
        {
            result.Errors.Add("Discovery timeout must be between 5 and 60 seconds");
        }
    }

    private void ValidateEffectSettings(EffectSettings settings, ConfigurationValidationResult result)
    {
        if (settings.EffectIntensity < 0.1f || settings.EffectIntensity > 1.0f)
        {
            result.Errors.Add("Effect intensity must be between 0.1 and 1.0");
        }
    }

    private void ValidateUiSettings(UiSettings settings, ConfigurationValidationResult result)
    {
        if (settings.WindowSize.Width < 800 || settings.WindowSize.Height < 600)
        {
            result.Errors.Add("Window size must be at least 800x600 pixels");
        }
    }

    #endregion
}

/// <summary>
/// Event arguments for configuration changed events
/// </summary>
public class ConfigurationChangedEventArgs : EventArgs
{
    public AppConfiguration Configuration { get; }
    public DateTime ChangedAt { get; }

    public ConfigurationChangedEventArgs(AppConfiguration configuration)
    {
        Configuration = configuration;
        ChangedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for configuration validation events
/// </summary>
public class ConfigurationValidationEventArgs : EventArgs
{
    public ConfigurationValidationResult ValidationResult { get; }

    public ConfigurationValidationEventArgs(ConfigurationValidationResult validationResult)
    {
        ValidationResult = validationResult;
    }
}
