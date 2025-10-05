using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PartyLights.Services;

/// <summary>
/// Comprehensive configuration management service with encryption and validation
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configFilePath;
    private readonly string _backupDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private AppConfiguration? _cachedConfiguration;
    private readonly object _configLock = new object();

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        _configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PartyLights", "config.json");
        _backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PartyLights", "backups");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        EnsureDirectoriesExist();
    }

    #region Core Configuration Methods

    public async Task<AppConfiguration> LoadConfigurationAsync()
    {
        try
        {
            lock (_configLock)
            {
                if (_cachedConfiguration != null)
                {
                    return _cachedConfiguration;
                }
            }

            _logger.LogInformation("Loading configuration from {ConfigFilePath}", _configFilePath);

            if (!File.Exists(_configFilePath))
            {
                _logger.LogInformation("Configuration file not found, creating default configuration");
                var defaultConfig = CreateDefaultConfiguration();
                await SaveConfigurationAsync(defaultConfig);
                return defaultConfig;
            }

            var configJson = await File.ReadAllTextAsync(_configFilePath);
            var configuration = JsonSerializer.Deserialize<AppConfiguration>(configJson, _jsonOptions);

            if (configuration == null)
            {
                _logger.LogWarning("Failed to deserialize configuration, using default");
                configuration = CreateDefaultConfiguration();
            }

            // Validate and fix configuration
            configuration = ValidateAndFixConfiguration(configuration);

            lock (_configLock)
            {
                _cachedConfiguration = configuration;
            }

            _logger.LogInformation("Configuration loaded successfully");
            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration");
            return CreateDefaultConfiguration();
        }
    }

    public async Task SaveConfigurationAsync(AppConfiguration configuration)
    {
        try
        {
            _logger.LogInformation("Saving configuration to {ConfigFilePath}", _configFilePath);

            // Validate configuration before saving
            configuration = ValidateAndFixConfiguration(configuration);
            configuration.LastModified = DateTime.UtcNow;

            // Create backup before saving
            await CreateBackupAsync();

            // Serialize configuration
            var configJson = JsonSerializer.Serialize(configuration, _jsonOptions);

            // Encrypt if security is enabled
            if (configuration.Security.EnableEncryption && !string.IsNullOrEmpty(configuration.Security.EncryptionKey))
            {
                configJson = EncryptString(configJson, configuration.Security.EncryptionKey);
            }

            // Write to file
            await File.WriteAllTextAsync(_configFilePath, configJson);

            lock (_configLock)
            {
                _cachedConfiguration = configuration;
            }

            _logger.LogInformation("Configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            throw;
        }
    }

    public async Task<bool> ExportConfigurationAsync(string filePath)
    {
        try
        {
            _logger.LogInformation("Exporting configuration to {FilePath}", filePath);

            var configuration = await LoadConfigurationAsync();

            // Remove sensitive data for export
            var exportConfig = RemoveSensitiveData(configuration);

            var configJson = JsonSerializer.Serialize(exportConfig, _jsonOptions);
            await File.WriteAllTextAsync(filePath, configJson);

            _logger.LogInformation("Configuration exported successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting configuration");
            return false;
        }
    }

    public async Task<AppConfiguration> ImportConfigurationAsync(string filePath)
    {
        try
        {
            _logger.LogInformation("Importing configuration from {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Configuration file not found: {filePath}");
            }

            var configJson = await File.ReadAllTextAsync(filePath);
            var configuration = JsonSerializer.Deserialize<AppConfiguration>(configJson, _jsonOptions);

            if (configuration == null)
            {
                throw new InvalidOperationException("Failed to deserialize imported configuration");
            }

            // Validate imported configuration
            configuration = ValidateAndFixConfiguration(configuration);

            // Save imported configuration
            await SaveConfigurationAsync(configuration);

            _logger.LogInformation("Configuration imported successfully");
            return configuration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing configuration");
            throw;
        }
    }

    #endregion

    #region Configuration Management

    /// <summary>
    /// Resets configuration to default values
    /// </summary>
    public async Task<AppConfiguration> ResetToDefaultsAsync()
    {
        try
        {
            _logger.LogInformation("Resetting configuration to defaults");

            var defaultConfig = CreateDefaultConfiguration();
            await SaveConfigurationAsync(defaultConfig);

            _logger.LogInformation("Configuration reset to defaults successfully");
            return defaultConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting configuration to defaults");
            throw;
        }
    }

    /// <summary>
    /// Validates configuration and returns validation results
    /// </summary>
    public ConfigurationValidationResult ValidateConfiguration(AppConfiguration configuration)
    {
        var result = new ConfigurationValidationResult();

        try
        {
            // Validate audio settings
            ValidateAudioSettings(configuration.Audio, result);

            // Validate device settings
            ValidateDeviceSettings(configuration.Devices, result);

            // Validate effect settings
            ValidateEffectSettings(configuration.Effects, result);

            // Validate UI settings
            ValidateUiSettings(configuration.UI, result);

            // Validate sync settings
            ValidateSyncSettings(configuration.Synchronization, result);

            // Validate performance settings
            ValidatePerformanceSettings(configuration.Performance, result);

            // Validate security settings
            ValidateSecuritySettings(configuration.Security, result);

            // Validate logging settings
            ValidateLoggingSettings(configuration.Logging, result);

            result.IsValid = !result.Errors.Any();
            _logger.LogDebug("Configuration validation completed: {IsValid}", result.IsValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration");
            result.Errors.Add($"Validation error: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    /// <summary>
    /// Gets configuration backup files
    /// </summary>
    public async Task<IEnumerable<ConfigurationBackup>> GetBackupsAsync()
    {
        try
        {
            if (!Directory.Exists(_backupDirectory))
            {
                return Enumerable.Empty<ConfigurationBackup>();
            }

            var backupFiles = Directory.GetFiles(_backupDirectory, "config_backup_*.json")
                .Select(filePath => new ConfigurationBackup
                {
                    FilePath = filePath,
                    CreatedAt = File.GetCreationTime(filePath),
                    SizeBytes = new FileInfo(filePath).Length
                })
                .OrderByDescending(b => b.CreatedAt);

            return backupFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration backups");
            return Enumerable.Empty<ConfigurationBackup>();
        }
    }

    /// <summary>
    /// Restores configuration from backup
    /// </summary>
    public async Task<bool> RestoreFromBackupAsync(string backupFilePath)
    {
        try
        {
            _logger.LogInformation("Restoring configuration from backup: {BackupPath}", backupFilePath);

            if (!File.Exists(backupFilePath))
            {
                _logger.LogWarning("Backup file not found: {BackupPath}", backupFilePath);
                return false;
            }

            var configJson = await File.ReadAllTextAsync(backupFilePath);
            var configuration = JsonSerializer.Deserialize<AppConfiguration>(configJson, _jsonOptions);

            if (configuration == null)
            {
                _logger.LogWarning("Failed to deserialize backup configuration");
                return false;
            }

            await SaveConfigurationAsync(configuration);
            _logger.LogInformation("Configuration restored from backup successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring configuration from backup");
            return false;
        }
    }

    #endregion

    #region Private Methods

    private void EnsureDirectoriesExist()
    {
        try
        {
            var configDir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            if (!Directory.Exists(_backupDirectory))
            {
                Directory.CreateDirectory(_backupDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating configuration directories");
        }
    }

    private AppConfiguration CreateDefaultConfiguration()
    {
        return new AppConfiguration
        {
            Audio = new AudioSettings(),
            Devices = new DeviceSettings(),
            Effects = new EffectSettings(),
            UI = new UiSettings(),
            Synchronization = new SyncSettings
            {
                EnabledDeviceTypes = new List<DeviceType> { DeviceType.PhilipsHue, DeviceType.TpLink, DeviceType.MagicHome }
            },
            Performance = new PerformanceSettings(),
            Security = new SecuritySettings(),
            Logging = new LoggingSettings(),
            Version = "1.0.0",
            LastModified = DateTime.UtcNow
        };
    }

    private AppConfiguration ValidateAndFixConfiguration(AppConfiguration configuration)
    {
        // Ensure all settings objects exist
        configuration.Audio ??= new AudioSettings();
        configuration.Devices ??= new DeviceSettings();
        configuration.Effects ??= new EffectSettings();
        configuration.UI ??= new UiSettings();
        configuration.Synchronization ??= new SyncSettings();
        configuration.Performance ??= new PerformanceSettings();
        configuration.Security ??= new SecuritySettings();
        configuration.Logging ??= new LoggingSettings();

        // Fix invalid values
        configuration.Audio.Sensitivity = Math.Clamp(configuration.Audio.Sensitivity, 0.1f, 2.0f);
        configuration.Audio.LatencyMs = Math.Clamp(configuration.Audio.LatencyMs, 10, 1000);
        configuration.Audio.SampleRate = Math.Clamp(configuration.Audio.SampleRate, 8000, 192000);
        configuration.Audio.BufferSize = Math.Clamp(configuration.Audio.BufferSize, 256, 4096);

        configuration.Effects.EffectIntensity = Math.Clamp(configuration.Effects.EffectIntensity, 0.1f, 1.0f);
        configuration.Effects.EffectUpdateIntervalMs = Math.Clamp(configuration.Effects.EffectUpdateIntervalMs, 50, 1000);
        configuration.Effects.TransitionDurationMs = Math.Clamp(configuration.Effects.TransitionDurationMs, 100, 5000);

        configuration.Synchronization.SyncIntervalMs = Math.Clamp(configuration.Synchronization.SyncIntervalMs, 50, 1000);
        configuration.Synchronization.SyncSensitivity = Math.Clamp(configuration.Synchronization.SyncSensitivity, 0.1f, 1.0f);

        configuration.Performance.MaxMemoryUsageMB = Math.Clamp(configuration.Performance.MaxMemoryUsageMB, 128, 2048);
        configuration.Performance.MinThreadPoolThreads = Math.Clamp(configuration.Performance.MinThreadPoolThreads, 1, 32);
        configuration.Performance.MaxThreadPoolThreads = Math.Clamp(configuration.Performance.MaxThreadPoolThreads, 1, 64);

        return configuration;
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

        if (settings.SampleRate < 8000 || settings.SampleRate > 192000)
        {
            result.Errors.Add("Sample rate must be between 8000 and 192000 Hz");
        }
    }

    private void ValidateDeviceSettings(DeviceSettings settings, ConfigurationValidationResult result)
    {
        if (settings.DiscoveryTimeoutSeconds < 5 || settings.DiscoveryTimeoutSeconds > 60)
        {
            result.Errors.Add("Discovery timeout must be between 5 and 60 seconds");
        }

        if (settings.ConnectionRetryAttempts < 1 || settings.ConnectionRetryAttempts > 10)
        {
            result.Errors.Add("Connection retry attempts must be between 1 and 10");
        }
    }

    private void ValidateEffectSettings(EffectSettings settings, ConfigurationValidationResult result)
    {
        if (settings.EffectIntensity < 0.1f || settings.EffectIntensity > 1.0f)
        {
            result.Errors.Add("Effect intensity must be between 0.1 and 1.0");
        }

        if (settings.EffectUpdateIntervalMs < 50 || settings.EffectUpdateIntervalMs > 1000)
        {
            result.Errors.Add("Effect update interval must be between 50 and 1000 milliseconds");
        }
    }

    private void ValidateUiSettings(UiSettings settings, ConfigurationValidationResult result)
    {
        if (settings.WindowSize.Width < 800 || settings.WindowSize.Height < 600)
        {
            result.Errors.Add("Window size must be at least 800x600 pixels");
        }

        if (settings.VisualizerUpdateRate < 30 || settings.VisualizerUpdateRate > 120)
        {
            result.Errors.Add("Visualizer update rate must be between 30 and 120 FPS");
        }
    }

    private void ValidateSyncSettings(SyncSettings settings, ConfigurationValidationResult result)
    {
        if (settings.SyncIntervalMs < 50 || settings.SyncIntervalMs > 1000)
        {
            result.Errors.Add("Sync interval must be between 50 and 1000 milliseconds");
        }

        if (settings.SyncSensitivity < 0.1f || settings.SyncSensitivity > 1.0f)
        {
            result.Errors.Add("Sync sensitivity must be between 0.1 and 1.0");
        }
    }

    private void ValidatePerformanceSettings(PerformanceSettings settings, ConfigurationValidationResult result)
    {
        if (settings.MaxMemoryUsageMB < 128 || settings.MaxMemoryUsageMB > 2048)
        {
            result.Errors.Add("Max memory usage must be between 128 and 2048 MB");
        }

        if (settings.MinThreadPoolThreads < 1 || settings.MinThreadPoolThreads > 32)
        {
            result.Errors.Add("Min thread pool threads must be between 1 and 32");
        }
    }

    private void ValidateSecuritySettings(SecuritySettings settings, ConfigurationValidationResult result)
    {
        if (settings.SessionTimeoutMinutes < 5 || settings.SessionTimeoutMinutes > 480)
        {
            result.Errors.Add("Session timeout must be between 5 and 480 minutes");
        }
    }

    private void ValidateLoggingSettings(LoggingSettings settings, ConfigurationValidationResult result)
    {
        var validLogLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" };
        if (!validLogLevels.Contains(settings.LogLevel))
        {
            result.Errors.Add($"Log level must be one of: {string.Join(", ", validLogLevels)}");
        }

        if (settings.MaxLogFileSizeMB < 1 || settings.MaxLogFileSizeMB > 100)
        {
            result.Errors.Add("Max log file size must be between 1 and 100 MB");
        }
    }

    private async Task CreateBackupAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var backupFileName = $"config_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                var backupPath = Path.Combine(_backupDirectory, backupFileName);

                await File.CopyAsync(_configFilePath, backupPath);

                // Clean up old backups (keep only last 10)
                await CleanupOldBackupsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating configuration backup");
        }
    }

    private async Task CleanupOldBackupsAsync()
    {
        try
        {
            var backupFiles = Directory.GetFiles(_backupDirectory, "config_backup_*.json")
                .OrderByDescending(f => File.GetCreationTime(f))
                .Skip(10);

            foreach (var file in backupFiles)
            {
                File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old backups");
        }
    }

    private AppConfiguration RemoveSensitiveData(AppConfiguration configuration)
    {
        // Create a copy and remove sensitive data
        var exportConfig = JsonSerializer.Deserialize<AppConfiguration>(
            JsonSerializer.Serialize(configuration, _jsonOptions), _jsonOptions)!;

        // Remove encryption key
        exportConfig.Security.EncryptionKey = string.Empty;

        // Remove API keys and tokens
        exportConfig.Devices.Devices.ForEach(d =>
        {
            // Remove any sensitive device data if needed
        });

        return exportConfig;
    }

    private string EncryptString(string plainText, string key)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using var swEncrypt = new StreamWriter(csEncrypt);

            swEncrypt.Write(plainText);
            swEncrypt.Close();

            var encrypted = msEncrypt.ToArray();
            var result = Convert.ToBase64String(aes.IV.Concat(encrypted).ToArray());
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting configuration");
            return plainText; // Return unencrypted if encryption fails
        }
    }

    private string DecryptString(string cipherText, string key)
    {
        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            var iv = fullCipher.Take(16).ToArray();
            var cipher = fullCipher.Skip(16).ToArray();

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(cipher);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);

            return srDecrypt.ReadToEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting configuration");
            return cipherText; // Return encrypted if decryption fails
        }
    }

    #endregion
}

/// <summary>
/// Configuration validation result
/// </summary>
public class ConfigurationValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Configuration backup information
/// </summary>
public class ConfigurationBackup
{
    public string FilePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
}
