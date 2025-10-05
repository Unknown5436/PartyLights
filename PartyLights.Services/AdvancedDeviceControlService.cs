using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;

namespace PartyLights.Services;

/// <summary>
/// Advanced device control service for managing multiple devices and groups
/// </summary>
public class AdvancedDeviceControlService : IAdvancedDeviceControlService
{
    private readonly ILogger<AdvancedDeviceControlService> _logger;
    private readonly IDeviceManagerService _deviceManagerService;
    private readonly ConcurrentDictionary<string, DeviceGroup> _deviceGroups = new();
    private readonly ConcurrentDictionary<string, DeviceConfiguration> _savedConfigurations = new();
    private readonly ConcurrentDictionary<string, DevicePerformanceMetrics> _performanceMetrics = new();

    public AdvancedDeviceControlService(
        ILogger<AdvancedDeviceControlService> logger,
        IDeviceManagerService deviceManagerService)
    {
        _logger = logger;
        _deviceManagerService = deviceManagerService;

        // Initialize default groups
        InitializeDefaultGroups();
    }

    #region All Devices Control

    public async Task<bool> SetColorToAllDevicesAsync(int r, int g, int b)
    {
        try
        {
            _logger.LogInformation("Setting color to all devices: RGB({R}, {G}, {B})", r, g, b);

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var tasks = devices.Select(device => SetDeviceColorAsync(device, r, g, b));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Color set to {SuccessCount}/{TotalCount} devices", successCount, devices.Count());
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting color to all devices");
            return false;
        }
    }

    public async Task<bool> SetBrightnessToAllDevicesAsync(int brightness)
    {
        try
        {
            _logger.LogInformation("Setting brightness to all devices: {Brightness}", brightness);

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var tasks = devices.Select(device => SetDeviceBrightnessAsync(device, brightness));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Brightness set to {SuccessCount}/{TotalCount} devices", successCount, devices.Count());
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting brightness to all devices");
            return false;
        }
    }

    public async Task<bool> SetEffectToAllDevicesAsync(string effectName)
    {
        try
        {
            _logger.LogInformation("Setting effect to all devices: {EffectName}", effectName);

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var tasks = devices.Select(device => SetDeviceEffectAsync(device, effectName));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Effect set to {SuccessCount}/{TotalCount} devices", successCount, devices.Count());
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting effect to all devices");
            return false;
        }
    }

    public async Task<bool> TurnOnAllDevicesAsync()
    {
        try
        {
            _logger.LogInformation("Turning on all devices");

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var tasks = devices.Select(device => SetDevicePowerAsync(device, true));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Turned on {SuccessCount}/{TotalCount} devices", successCount, devices.Count());
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning on all devices");
            return false;
        }
    }

    public async Task<bool> TurnOffAllDevicesAsync()
    {
        try
        {
            _logger.LogInformation("Turning off all devices");

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var tasks = devices.Select(device => SetDevicePowerAsync(device, false));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Turned off {SuccessCount}/{TotalCount} devices", successCount, devices.Count());
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning off all devices");
            return false;
        }
    }

    #endregion

    #region Device Group Control

    public async Task<bool> SetColorToDeviceGroupAsync(string groupName, int r, int g, int b)
    {
        try
        {
            _logger.LogInformation("Setting color to device group '{GroupName}': RGB({R}, {G}, {B})", groupName, r, g, b);

            var group = GetDeviceGroup(groupName);
            if (group == null)
            {
                _logger.LogWarning("Device group '{GroupName}' not found", groupName);
                return false;
            }

            var devices = await GetDevicesInGroupAsync(group);
            var tasks = devices.Select(device => SetDeviceColorAsync(device, r, g, b));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Color set to {SuccessCount}/{TotalCount} devices in group '{GroupName}'", successCount, devices.Count(), groupName);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting color to device group '{GroupName}'", groupName);
            return false;
        }
    }

    public async Task<bool> SetBrightnessToDeviceGroupAsync(string groupName, int brightness)
    {
        try
        {
            _logger.LogInformation("Setting brightness to device group '{GroupName}': {Brightness}", groupName, brightness);

            var group = GetDeviceGroup(groupName);
            if (group == null)
            {
                _logger.LogWarning("Device group '{GroupName}' not found", groupName);
                return false;
            }

            var devices = await GetDevicesInGroupAsync(group);
            var tasks = devices.Select(device => SetDeviceBrightnessAsync(device, brightness));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Brightness set to {SuccessCount}/{TotalCount} devices in group '{GroupName}'", successCount, devices.Count(), groupName);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting brightness to device group '{GroupName}'", groupName);
            return false;
        }
    }

    public async Task<bool> SetEffectToDeviceGroupAsync(string groupName, string effectName)
    {
        try
        {
            _logger.LogInformation("Setting effect to device group '{GroupName}': {EffectName}", groupName, effectName);

            var group = GetDeviceGroup(groupName);
            if (group == null)
            {
                _logger.LogWarning("Device group '{GroupName}' not found", groupName);
                return false;
            }

            var devices = await GetDevicesInGroupAsync(group);
            var tasks = devices.Select(device => SetDeviceEffectAsync(device, effectName));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Effect set to {SuccessCount}/{TotalCount} devices in group '{GroupName}'", successCount, devices.Count(), groupName);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting effect to device group '{GroupName}'", groupName);
            return false;
        }
    }

    public async Task<bool> TurnOnDeviceGroupAsync(string groupName)
    {
        try
        {
            _logger.LogInformation("Turning on device group '{GroupName}'", groupName);

            var group = GetDeviceGroup(groupName);
            if (group == null)
            {
                _logger.LogWarning("Device group '{GroupName}' not found", groupName);
                return false;
            }

            var devices = await GetDevicesInGroupAsync(group);
            var tasks = devices.Select(device => SetDevicePowerAsync(device, true));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Turned on {SuccessCount}/{TotalCount} devices in group '{GroupName}'", successCount, devices.Count(), groupName);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning on device group '{GroupName}'", groupName);
            return false;
        }
    }

    public async Task<bool> TurnOffDeviceGroupAsync(string groupName)
    {
        try
        {
            _logger.LogInformation("Turning off device group '{GroupName}'", groupName);

            var group = GetDeviceGroup(groupName);
            if (group == null)
            {
                _logger.LogWarning("Device group '{GroupName}' not found", groupName);
                return false;
            }

            var devices = await GetDevicesInGroupAsync(group);
            var tasks = devices.Select(device => SetDevicePowerAsync(device, false));

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Turned off {SuccessCount}/{TotalCount} devices in group '{GroupName}'", successCount, devices.Count(), groupName);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning off device group '{GroupName}'", groupName);
            return false;
        }
    }

    #endregion

    #region Device Type Control

    public async Task<bool> SetColorToDeviceTypeAsync(DeviceType deviceType, int r, int g, int b)
    {
        try
        {
            _logger.LogInformation("Setting color to {DeviceType} devices: RGB({R}, {G}, {B})", deviceType, r, g, b);

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var filteredDevices = devices.Where(d => d.Type == deviceType);

            var tasks = filteredDevices.Select(device => SetDeviceColorAsync(device, r, g, b));
            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Color set to {SuccessCount}/{TotalCount} {DeviceType} devices", successCount, filteredDevices.Count(), deviceType);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting color to {DeviceType} devices", deviceType);
            return false;
        }
    }

    public async Task<bool> SetBrightnessToDeviceTypeAsync(DeviceType deviceType, int brightness)
    {
        try
        {
            _logger.LogInformation("Setting brightness to {DeviceType} devices: {Brightness}", deviceType, brightness);

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var filteredDevices = devices.Where(d => d.Type == deviceType);

            var tasks = filteredDevices.Select(device => SetDeviceBrightnessAsync(device, brightness));
            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Brightness set to {SuccessCount}/{TotalCount} {DeviceType} devices", successCount, filteredDevices.Count(), deviceType);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting brightness to {DeviceType} devices", deviceType);
            return false;
        }
    }

    public async Task<bool> SetEffectToDeviceTypeAsync(DeviceType deviceType, string effectName)
    {
        try
        {
            _logger.LogInformation("Setting effect to {DeviceType} devices: {EffectName}", deviceType, effectName);

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var filteredDevices = devices.Where(d => d.Type == deviceType);

            var tasks = filteredDevices.Select(device => SetDeviceEffectAsync(device, effectName));
            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Effect set to {SuccessCount}/{TotalCount} {DeviceType} devices", successCount, filteredDevices.Count(), deviceType);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting effect to {DeviceType} devices", deviceType);
            return false;
        }
    }

    public async Task<bool> TurnOnDeviceTypeAsync(DeviceType deviceType)
    {
        try
        {
            _logger.LogInformation("Turning on {DeviceType} devices", deviceType);

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var filteredDevices = devices.Where(d => d.Type == deviceType);

            var tasks = filteredDevices.Select(device => SetDevicePowerAsync(device, true));
            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Turned on {SuccessCount}/{TotalCount} {DeviceType} devices", successCount, filteredDevices.Count(), deviceType);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning on {DeviceType} devices", deviceType);
            return false;
        }
    }

    public async Task<bool> TurnOffDeviceTypeAsync(DeviceType deviceType)
    {
        try
        {
            _logger.LogInformation("Turning off {DeviceType} devices", deviceType);

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var filteredDevices = devices.Where(d => d.Type == deviceType);

            var tasks = filteredDevices.Select(device => SetDevicePowerAsync(device, false));
            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Turned off {SuccessCount}/{TotalCount} {DeviceType} devices", successCount, filteredDevices.Count(), deviceType);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning off {DeviceType} devices", deviceType);
            return false;
        }
    }

    #endregion

    #region Device Group Management

    public async Task<bool> CreateDeviceGroupAsync(string groupName, IEnumerable<SmartDevice> devices)
    {
        try
        {
            _logger.LogInformation("Creating device group '{GroupName}' with {DeviceCount} devices", groupName, devices.Count());

            var group = new DeviceGroup
            {
                Id = Guid.NewGuid().ToString(),
                Name = groupName,
                Description = $"Device group created on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
                DeviceIds = devices.Select(d => d.Id).ToList(),
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            _deviceGroups.TryAdd(groupName, group);

            _logger.LogInformation("Device group '{GroupName}' created successfully", groupName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating device group '{GroupName}'", groupName);
            return false;
        }
    }

    public async Task<bool> DeleteDeviceGroupAsync(string groupName)
    {
        try
        {
            _logger.LogInformation("Deleting device group '{GroupName}'", groupName);

            var removed = _deviceGroups.TryRemove(groupName, out _);

            if (removed)
            {
                _logger.LogInformation("Device group '{GroupName}' deleted successfully", groupName);
            }
            else
            {
                _logger.LogWarning("Device group '{GroupName}' not found", groupName);
            }

            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting device group '{GroupName}'", groupName);
            return false;
        }
    }

    public async Task<IEnumerable<DeviceGroup>> GetDeviceGroupsAsync()
    {
        await Task.CompletedTask;
        return _deviceGroups.Values;
    }

    public async Task<bool> AddDeviceToGroupAsync(string groupName, SmartDevice device)
    {
        try
        {
            _logger.LogInformation("Adding device '{DeviceName}' to group '{GroupName}'", device.Name, groupName);

            var group = GetDeviceGroup(groupName);
            if (group == null)
            {
                _logger.LogWarning("Device group '{GroupName}' not found", groupName);
                return false;
            }

            if (!group.DeviceIds.Contains(device.Id))
            {
                group.DeviceIds.Add(device.Id);
                group.LastModified = DateTime.UtcNow;

                _logger.LogInformation("Device '{DeviceName}' added to group '{GroupName}'", device.Name, groupName);
                return true;
            }
            else
            {
                _logger.LogWarning("Device '{DeviceName}' already in group '{GroupName}'", device.Name, groupName);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding device to group '{GroupName}'", groupName);
            return false;
        }
    }

    public async Task<bool> RemoveDeviceFromGroupAsync(string groupName, SmartDevice device)
    {
        try
        {
            _logger.LogInformation("Removing device '{DeviceName}' from group '{GroupName}'", device.Name, groupName);

            var group = GetDeviceGroup(groupName);
            if (group == null)
            {
                _logger.LogWarning("Device group '{GroupName}' not found", groupName);
                return false;
            }

            var removed = group.DeviceIds.Remove(device.Id);
            if (removed)
            {
                group.LastModified = DateTime.UtcNow;
                _logger.LogInformation("Device '{DeviceName}' removed from group '{GroupName}'", device.Name, groupName);
            }
            else
            {
                _logger.LogWarning("Device '{DeviceName}' not found in group '{GroupName}'", device.Name, groupName);
            }

            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing device from group '{GroupName}'", groupName);
            return false;
        }
    }

    #endregion

    #region Device Configuration Management

    public async Task<bool> SaveDeviceConfigurationAsync(SmartDevice device)
    {
        try
        {
            _logger.LogInformation("Saving configuration for device '{DeviceName}'", device.Name);

            var config = new DeviceConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{device.Name} Configuration",
                Description = $"Configuration saved on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
                DeviceType = device.Type,
                Settings = new Dictionary<string, object>
                {
                    ["IpAddress"] = device.IpAddress,
                    ["Capabilities"] = device.Capabilities,
                    ["IsEnabled"] = device.IsEnabled
                },
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            _savedConfigurations.TryAdd(config.Id, config);

            _logger.LogInformation("Configuration saved for device '{DeviceName}'", device.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration for device '{DeviceName}'", device.Name);
            return false;
        }
    }

    public async Task<bool> LoadDeviceConfigurationAsync(SmartDevice device)
    {
        try
        {
            _logger.LogInformation("Loading configuration for device '{DeviceName}'", device.Name);

            var config = _savedConfigurations.Values.FirstOrDefault(c => c.DeviceType == device.Type);
            if (config == null)
            {
                _logger.LogWarning("No saved configuration found for device '{DeviceName}'", device.Name);
                return false;
            }

            // Apply configuration settings
            if (config.Settings.TryGetValue("IpAddress", out var ipAddress))
            {
                device.IpAddress = ipAddress.ToString() ?? device.IpAddress;
            }

            if (config.Settings.TryGetValue("IsEnabled", out var isEnabled))
            {
                device.IsEnabled = Convert.ToBoolean(isEnabled);
            }

            _logger.LogInformation("Configuration loaded for device '{DeviceName}'", device.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration for device '{DeviceName}'", device.Name);
            return false;
        }
    }

    public async Task<IEnumerable<DeviceConfiguration>> GetSavedConfigurationsAsync()
    {
        await Task.CompletedTask;
        return _savedConfigurations.Values;
    }

    #endregion

    #region Private Helper Methods

    private void InitializeDefaultGroups()
    {
        var defaultGroups = new[]
        {
            new DeviceGroup
            {
                Id = "all-devices",
                Name = "All Devices",
                Description = "All connected devices",
                DeviceIds = new List<string>(),
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            },
            new DeviceGroup
            {
                Id = "living-room",
                Name = "Living Room",
                Description = "Living room devices",
                DeviceIds = new List<string>(),
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            },
            new DeviceGroup
            {
                Id = "bedroom",
                Name = "Bedroom",
                Description = "Bedroom devices",
                DeviceIds = new List<string>(),
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            }
        };

        foreach (var group in defaultGroups)
        {
            _deviceGroups.TryAdd(group.Name, group);
        }
    }

    private DeviceGroup? GetDeviceGroup(string groupName)
    {
        _deviceGroups.TryGetValue(groupName, out var group);
        return group;
    }

    private async Task<IEnumerable<SmartDevice>> GetDevicesInGroupAsync(DeviceGroup group)
    {
        var allDevices = await _deviceManagerService.GetConnectedDevicesAsync();
        return allDevices.Where(d => group.DeviceIds.Contains(d.Id));
    }

    private async Task<bool> SetDeviceColorAsync(SmartDevice device, int r, int g, int b)
    {
        try
        {
            var controller = _deviceManagerService.GetController(device.Type);
            if (controller == null)
            {
                _logger.LogWarning("No controller found for device type {DeviceType}", device.Type);
                return false;
            }

            var success = await controller.SetColorAsync(r, g, b);
            UpdatePerformanceMetrics(device.Id, success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting color for device '{DeviceName}'", device.Name);
            UpdatePerformanceMetrics(device.Id, false);
            return false;
        }
    }

    private async Task<bool> SetDeviceBrightnessAsync(SmartDevice device, int brightness)
    {
        try
        {
            var controller = _deviceManagerService.GetController(device.Type);
            if (controller == null)
            {
                _logger.LogWarning("No controller found for device type {DeviceType}", device.Type);
                return false;
            }

            var success = await controller.SetBrightnessAsync(brightness);
            UpdatePerformanceMetrics(device.Id, success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting brightness for device '{DeviceName}'", device.Name);
            UpdatePerformanceMetrics(device.Id, false);
            return false;
        }
    }

    private async Task<bool> SetDeviceEffectAsync(SmartDevice device, string effectName)
    {
        try
        {
            var controller = _deviceManagerService.GetController(device.Type);
            if (controller == null)
            {
                _logger.LogWarning("No controller found for device type {DeviceType}", device.Type);
                return false;
            }

            var success = await controller.SetEffectAsync(effectName);
            UpdatePerformanceMetrics(device.Id, success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting effect for device '{DeviceName}'", device.Name);
            UpdatePerformanceMetrics(device.Id, false);
            return false;
        }
    }

    private async Task<bool> SetDevicePowerAsync(SmartDevice device, bool isOn)
    {
        try
        {
            var controller = _deviceManagerService.GetController(device.Type);
            if (controller == null)
            {
                _logger.LogWarning("No controller found for device type {DeviceType}", device.Type);
                return false;
            }

            var success = isOn ? await controller.TurnOnAsync() : await controller.TurnOffAsync();
            UpdatePerformanceMetrics(device.Id, success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting power for device '{DeviceName}'", device.Name);
            UpdatePerformanceMetrics(device.Id, false);
            return false;
        }
    }

    private void UpdatePerformanceMetrics(string deviceId, bool success)
    {
        var metrics = _performanceMetrics.GetOrAdd(deviceId, _ => new DevicePerformanceMetrics
        {
            DeviceId = deviceId,
            LastCommandTime = DateTime.UtcNow
        });

        if (success)
        {
            metrics.SuccessfulCommands++;
        }
        else
        {
            metrics.FailedCommands++;
        }

        metrics.LastCommandTime = DateTime.UtcNow;
    }

    #endregion
}
