using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;

namespace PartyLights.Services;

/// <summary>
/// Service for configuration management
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
    }

    public async Task<AppConfiguration> LoadConfigurationAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogInformation("Configuration file not found, creating default configuration");
                var defaultConfig = new AppConfiguration();
                await SaveConfigurationAsync(defaultConfig);
                return defaultConfig;
            }

            var json = await File.ReadAllTextAsync(_configPath);
            var config = System.Text.Json.JsonSerializer.Deserialize<AppConfiguration>(json);

            _logger.LogInformation("Configuration loaded successfully");
            return config ?? new AppConfiguration();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration");
            return new AppConfiguration();
        }
    }

    public async Task SaveConfigurationAsync(AppConfiguration configuration)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(configuration, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_configPath, json);
            _logger.LogInformation("Configuration saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration");
            throw;
        }
    }

    public async Task<bool> ExportConfigurationAsync(string filePath)
    {
        try
        {
            var config = await LoadConfigurationAsync();
            var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("Configuration exported to: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export configuration to: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<AppConfiguration> ImportConfigurationAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var config = System.Text.Json.JsonSerializer.Deserialize<AppConfiguration>(json);

            if (config != null)
            {
                await SaveConfigurationAsync(config);
                _logger.LogInformation("Configuration imported from: {FilePath}", filePath);
            }

            return config ?? new AppConfiguration();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import configuration from: {FilePath}", filePath);
            return new AppConfiguration();
        }
    }
}
