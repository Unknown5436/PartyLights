using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;

namespace PartyLights.Services;

/// <summary>
/// Device synchronization service for real-time device coordination
/// </summary>
public class DeviceSynchronizationService : IDeviceSynchronizationService
{
    private readonly ILogger<DeviceSynchronizationService> _logger;
    private readonly IDeviceManagerService _deviceManagerService;
    private readonly IAdvancedDeviceControlService _advancedDeviceControl;
    private readonly ConcurrentDictionary<string, SynchronizationSettings> _syncSettings = new();
    private readonly ConcurrentDictionary<string, DeviceCommand> _commandQueue = new();

    private bool _isRealTimeSyncActive;
    private Task? _syncTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private Timer? _syncTimer;

    public DeviceSynchronizationService(
        ILogger<DeviceSynchronizationService> logger,
        IDeviceManagerService deviceManagerService,
        IAdvancedDeviceControlService advancedDeviceControl)
    {
        _logger = logger;
        _deviceManagerService = deviceManagerService;
        _advancedDeviceControl = advancedDeviceControl;

        // Initialize default synchronization settings
        InitializeDefaultSettings();
    }

    public bool IsRealTimeSyncActive => _isRealTimeSyncActive;

    public event EventHandler<SynchronizationEventArgs>? SynchronizationStarted;
    public event EventHandler<SynchronizationEventArgs>? SynchronizationStopped;
    public event EventHandler<SynchronizationEventArgs>? SynchronizationError;

    #region Synchronization Control

    public async Task<bool> StartRealTimeSyncAsync()
    {
        try
        {
            if (_isRealTimeSyncActive)
            {
                _logger.LogWarning("Real-time synchronization is already active");
                return true;
            }

            _logger.LogInformation("Starting real-time device synchronization");

            _cancellationTokenSource = new CancellationTokenSource();
            _isRealTimeSyncActive = true;

            // Start synchronization task
            _syncTask = Task.Run(ProcessSynchronizationLoop, _cancellationTokenSource.Token);

            // Start timer for periodic synchronization
            var syncInterval = GetSyncInterval();
            _syncTimer = new Timer(OnSyncTimer, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(syncInterval));

            SynchronizationStarted?.Invoke(this, new SynchronizationEventArgs());

            _logger.LogInformation("Real-time device synchronization started");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting real-time synchronization");
            SynchronizationError?.Invoke(this, new SynchronizationEventArgs(errorMessage: ex.Message));
            return false;
        }
    }

    public async Task<bool> StopRealTimeSyncAsync()
    {
        try
        {
            if (!_isRealTimeSyncActive)
            {
                _logger.LogWarning("Real-time synchronization is not active");
                return true;
            }

            _logger.LogInformation("Stopping real-time device synchronization");

            _isRealTimeSyncActive = false;
            _cancellationTokenSource?.Cancel();
            _syncTimer?.Dispose();
            _syncTimer = null;

            if (_syncTask != null)
            {
                await _syncTask;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            SynchronizationStopped?.Invoke(this, new SynchronizationEventArgs());

            _logger.LogInformation("Real-time device synchronization stopped");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping real-time synchronization");
            SynchronizationError?.Invoke(this, new SynchronizationEventArgs(errorMessage: ex.Message));
            return false;
        }
    }

    #endregion

    #region Synchronization Methods

    public async Task<bool> SynchronizeAllDevicesAsync()
    {
        try
        {
            _logger.LogInformation("Synchronizing all devices");

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var syncTasks = devices.Select(device => SynchronizeDeviceAsync(device));

            var results = await Task.WhenAll(syncTasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Synchronized {SuccessCount}/{TotalCount} devices", successCount, devices.Count());
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing all devices");
            SynchronizationError?.Invoke(this, new SynchronizationEventArgs(errorMessage: ex.Message));
            return false;
        }
    }

    public async Task<bool> SynchronizeDeviceGroupAsync(string groupName)
    {
        try
        {
            _logger.LogInformation("Synchronizing device group '{GroupName}'", groupName);

            var groups = await _advancedDeviceControl.GetDeviceGroupsAsync();
            var group = groups.FirstOrDefault(g => g.Name == groupName);

            if (group == null)
            {
                _logger.LogWarning("Device group '{GroupName}' not found", groupName);
                return false;
            }

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var groupDevices = devices.Where(d => group.DeviceIds.Contains(d.Id));

            var syncTasks = groupDevices.Select(device => SynchronizeDeviceAsync(device));
            var results = await Task.WhenAll(syncTasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Synchronized {SuccessCount}/{TotalCount} devices in group '{GroupName}'", successCount, groupDevices.Count(), groupName);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing device group '{GroupName}'", groupName);
            SynchronizationError?.Invoke(this, new SynchronizationEventArgs(groupName: groupName, errorMessage: ex.Message));
            return false;
        }
    }

    public async Task<bool> SynchronizeDeviceTypeAsync(DeviceType deviceType)
    {
        try
        {
            _logger.LogInformation("Synchronizing {DeviceType} devices", deviceType);

            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var filteredDevices = devices.Where(d => d.Type == deviceType);

            var syncTasks = filteredDevices.Select(device => SynchronizeDeviceAsync(device));
            var results = await Task.WhenAll(syncTasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Synchronized {SuccessCount}/{TotalCount} {DeviceType} devices", successCount, filteredDevices.Count(), deviceType);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing {DeviceType} devices", deviceType);
            SynchronizationError?.Invoke(this, new SynchronizationEventArgs(deviceType: deviceType, errorMessage: ex.Message));
            return false;
        }
    }

    #endregion

    #region Command Processing

    /// <summary>
    /// Adds a command to the synchronization queue
    /// </summary>
    public void QueueCommand(DeviceCommand command)
    {
        try
        {
            _commandQueue.TryAdd(command.DeviceId, command);
            _logger.LogDebug("Command queued for device '{DeviceId}': {CommandType}", command.DeviceId, command.Type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing command for device '{DeviceId}'", command.DeviceId);
        }
    }

    /// <summary>
    /// Processes all queued commands
    /// </summary>
    public async Task<bool> ProcessQueuedCommandsAsync()
    {
        try
        {
            var commands = _commandQueue.Values.ToList();
            if (!commands.Any())
            {
                return true;
            }

            _logger.LogDebug("Processing {CommandCount} queued commands", commands.Count);

            var tasks = commands.Select(command => ProcessCommandAsync(command));
            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            // Clear processed commands
            foreach (var command in commands)
            {
                _commandQueue.TryRemove(command.DeviceId, out _);
            }

            _logger.LogDebug("Processed {SuccessCount}/{TotalCount} commands", successCount, commands.Count);
            return successCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing queued commands");
            return false;
        }
    }

    #endregion

    #region Settings Management

    /// <summary>
    /// Updates synchronization settings
    /// </summary>
    public void UpdateSynchronizationSettings(SynchronizationSettings settings)
    {
        try
        {
            _syncSettings.AddOrUpdate("default", settings, (key, existing) => settings);

            // Update timer interval if sync is active
            if (_isRealTimeSyncActive && _syncTimer != null)
            {
                _syncTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(settings.SyncIntervalMs));
            }

            _logger.LogInformation("Synchronization settings updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating synchronization settings");
        }
    }

    /// <summary>
    /// Gets current synchronization settings
    /// </summary>
    public SynchronizationSettings GetSynchronizationSettings()
    {
        _syncSettings.TryGetValue("default", out var settings);
        return settings ?? new SynchronizationSettings();
    }

    #endregion

    #region Private Methods

    private void InitializeDefaultSettings()
    {
        var defaultSettings = new SynchronizationSettings
        {
            EnableRealTimeSync = true,
            SyncIntervalMs = 100,
            SyncOnBeat = true,
            SyncOnVolume = true,
            SyncOnFrequency = true,
            SyncSensitivity = 0.5f,
            EnabledDeviceTypes = new List<DeviceType> { DeviceType.PhilipsHue, DeviceType.TpLink, DeviceType.MagicHome }
        };

        _syncSettings.TryAdd("default", defaultSettings);
    }

    private async Task ProcessSynchronizationLoop()
    {
        _logger.LogInformation("Device synchronization loop started");

        while (_isRealTimeSyncActive && !_cancellationTokenSource!.Token.IsCancellationRequested)
        {
            try
            {
                // Process queued commands
                await ProcessQueuedCommandsAsync();

                // Perform periodic synchronization
                await PerformPeriodicSynchronizationAsync();

                // Wait before next iteration
                await Task.Delay(50, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in synchronization loop");
                await Task.Delay(1000); // Wait longer on error
            }
        }

        _logger.LogInformation("Device synchronization loop stopped");
    }

    private async Task PerformPeriodicSynchronizationAsync()
    {
        try
        {
            var settings = GetSynchronizationSettings();

            if (settings.EnableRealTimeSync)
            {
                // Synchronize enabled device types
                foreach (var deviceType in settings.EnabledDeviceTypes)
                {
                    await SynchronizeDeviceTypeAsync(deviceType);
                }

                // Synchronize enabled device groups
                foreach (var groupName in settings.EnabledDeviceGroups)
                {
                    await SynchronizeDeviceGroupAsync(groupName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in periodic synchronization");
        }
    }

    private async Task<bool> SynchronizeDeviceAsync(SmartDevice device)
    {
        try
        {
            // Update device state
            var success = await _deviceManagerService.UpdateDeviceStateAsync(device);

            if (success)
            {
                _logger.LogDebug("Device '{DeviceName}' synchronized successfully", device.Name);
            }
            else
            {
                _logger.LogWarning("Failed to synchronize device '{DeviceName}'", device.Name);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing device '{DeviceName}'", device.Name);
            return false;
        }
    }

    private async Task<bool> ProcessCommandAsync(DeviceCommand command)
    {
        try
        {
            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            var device = devices.FirstOrDefault(d => d.Id == command.DeviceId);

            if (device == null)
            {
                _logger.LogWarning("Device '{DeviceId}' not found for command processing", command.DeviceId);
                return false;
            }

            var controller = _deviceManagerService.GetController(device.Type);
            if (controller == null)
            {
                _logger.LogWarning("No controller found for device type {DeviceType}", device.Type);
                return false;
            }

            var success = command.Type switch
            {
                CommandType.SetColor => await ProcessSetColorCommand(controller, command),
                CommandType.SetBrightness => await ProcessSetBrightnessCommand(controller, command),
                CommandType.SetEffect => await ProcessSetEffectCommand(controller, command),
                CommandType.TurnOn => await controller.TurnOnAsync(),
                CommandType.TurnOff => await controller.TurnOffAsync(),
                _ => false
            };

            _logger.LogDebug("Command {CommandType} processed for device '{DeviceId}': {Success}", command.Type, command.DeviceId, success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command for device '{DeviceId}'", command.DeviceId);
            return false;
        }
    }

    private async Task<bool> ProcessSetColorCommand(IDeviceController controller, DeviceCommand command)
    {
        if (command.Parameters.TryGetValue("r", out var r) &&
            command.Parameters.TryGetValue("g", out var g) &&
            command.Parameters.TryGetValue("b", out var b))
        {
            return await controller.SetColorAsync(Convert.ToInt32(r), Convert.ToInt32(g), Convert.ToInt32(b));
        }
        return false;
    }

    private async Task<bool> ProcessSetBrightnessCommand(IDeviceController controller, DeviceCommand command)
    {
        if (command.Parameters.TryGetValue("brightness", out var brightness))
        {
            return await controller.SetBrightnessAsync(Convert.ToInt32(brightness));
        }
        return false;
    }

    private async Task<bool> ProcessSetEffectCommand(IDeviceController controller, DeviceCommand command)
    {
        if (command.Parameters.TryGetValue("effect", out var effect))
        {
            return await controller.SetEffectAsync(effect.ToString() ?? string.Empty);
        }
        return false;
    }

    private void OnSyncTimer(object? state)
    {
        if (_isRealTimeSyncActive)
        {
            _ = Task.Run(PerformPeriodicSynchronizationAsync);
        }
    }

    private int GetSyncInterval()
    {
        var settings = GetSynchronizationSettings();
        return settings.SyncIntervalMs;
    }

    #endregion
}
