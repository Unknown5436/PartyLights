using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using PartyLights.Devices;
using Q42.HueApi.Models.Bridge;

namespace PartyLights.Services;

/// <summary>
/// Service for managing smart devices
/// </summary>
public class DeviceManagerService : IDeviceManagerService
{
    private readonly ILogger<DeviceManagerService> _logger;
    private readonly List<SmartDevice> _devices = new();
    private readonly Dictionary<DeviceType, IDeviceController> _controllers = new();

    public DeviceManagerService(ILogger<DeviceManagerService> logger)
    {
        _logger = logger;
    }

    public event EventHandler<DeviceEventArgs>? DeviceConnected;
    public event EventHandler<DeviceEventArgs>? DeviceDisconnected;
    public event EventHandler<DeviceEventArgs>? DeviceStateChanged;

    public async Task<IEnumerable<SmartDevice>> DiscoverDevicesAsync()
    {
        _logger.LogInformation("Starting comprehensive device discovery");
        var discoveredDevices = new List<SmartDevice>();

        try
        {
            // Discover Philips Hue devices
            _logger.LogInformation("Discovering Philips Hue devices...");
            var hueBridges = await HueDeviceController.DiscoverBridgesAsync();
            foreach (var bridge in hueBridges)
            {
                var device = new SmartDevice
                {
                    Id = $"hue_bridge_{bridge.IpAddress}",
                    Name = $"Hue Bridge ({bridge.IpAddress})",
                    Type = DeviceType.PhilipsHue,
                    IpAddress = bridge.IpAddress,
                    IsConnected = false,
                    Capabilities = new DeviceCapabilities
                    {
                        SupportsColor = true,
                        SupportsBrightness = true,
                        SupportsTemperature = true,
                        SupportsEffects = true,
                        MaxBrightness = 255,
                        MinBrightness = 0
                    }
                };
                discoveredDevices.Add(device);
                _logger.LogInformation("Discovered Hue Bridge: {IpAddress}", bridge.IpAddress);
            }

            // Discover TP-Link devices
            _logger.LogInformation("Discovering TP-Link devices...");
            var tpLinkDevices = await TpLinkDeviceController.DiscoverDevicesAsync();
            foreach (var tpDevice in tpLinkDevices)
            {
                var device = new SmartDevice
                {
                    Id = $"tplink_{tpDevice.DeviceId}",
                    Name = tpDevice.Alias,
                    Type = DeviceType.TpLink,
                    IpAddress = tpDevice.IpAddress,
                    IsConnected = false,
                    Capabilities = new DeviceCapabilities
                    {
                        SupportsColor = true,
                        SupportsBrightness = true,
                        SupportsTemperature = true,
                        SupportsEffects = false,
                        MaxBrightness = 100,
                        MinBrightness = 1
                    }
                };
                discoveredDevices.Add(device);
                _logger.LogInformation("Discovered TP-Link device: {Alias} ({IpAddress})", tpDevice.Alias, tpDevice.IpAddress);
            }

            // Discover Magic Home devices
            _logger.LogInformation("Discovering Magic Home devices...");
            var magicHomeDevices = await MagicHomeDeviceController.DiscoverDevicesAsync();
            foreach (var mhDevice in magicHomeDevices)
            {
                var device = new SmartDevice
                {
                    Id = $"magichome_{mhDevice.MacAddress.Replace(":", "")}",
                    Name = $"Magic Home Controller ({mhDevice.IpAddress})",
                    Type = DeviceType.MagicHome,
                    IpAddress = mhDevice.IpAddress,
                    IsConnected = false,
                    Capabilities = new DeviceCapabilities
                    {
                        SupportsColor = true,
                        SupportsBrightness = true,
                        SupportsTemperature = false,
                        SupportsEffects = true,
                        MaxBrightness = 255,
                        MinBrightness = 0
                    }
                };
                discoveredDevices.Add(device);
                _logger.LogInformation("Discovered Magic Home device: {IpAddress}", mhDevice.IpAddress);
            }

            _logger.LogInformation("Device discovery completed. Found {Count} devices", discoveredDevices.Count);
            return discoveredDevices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device discovery");
            return discoveredDevices;
        }
    }

    public async Task<bool> ConnectToDeviceAsync(SmartDevice device)
    {
        try
        {
            _logger.LogInformation("Connecting to device: {DeviceName} ({DeviceType})", device.Name, device.Type);

            IDeviceController controller = device.Type switch
            {
                DeviceType.PhilipsHue => new HueDeviceController(_logger.CreateLogger<HueDeviceController>(), new HttpClient()),
                DeviceType.TpLink => new TpLinkDeviceController(_logger.CreateLogger<TpLinkDeviceController>()),
                DeviceType.MagicHome => new MagicHomeDeviceController(_logger.CreateLogger<MagicHomeDeviceController>()),
                _ => throw new NotSupportedException($"Device type {device.Type} is not supported")
            };

            var success = await controller.ConnectAsync(device.IpAddress);
            if (success)
            {
                _controllers[device.Type] = controller;
                device.IsConnected = true;
                device.LastSeen = DateTime.UtcNow;

                if (!_devices.Any(d => d.Id == device.Id))
                {
                    _devices.Add(device);
                }

                DeviceConnected?.Invoke(this, new DeviceEventArgs(device));
                _logger.LogInformation("Successfully connected to device: {DeviceName}", device.Name);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to connect to device: {DeviceName}", device.Name);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to device: {DeviceName}", device.Name);
            return false;
        }
    }

    public async Task<bool> DisconnectFromDeviceAsync(SmartDevice device)
    {
        try
        {
            _logger.LogInformation("Disconnecting from device: {DeviceName}", device.Name);

            if (_controllers.TryGetValue(device.Type, out var controller))
            {
                await controller.DisconnectAsync();
                _controllers.Remove(device.Type);
            }

            device.IsConnected = false;
            DeviceDisconnected?.Invoke(this, new DeviceEventArgs(device));

            _logger.LogInformation("Successfully disconnected from device: {DeviceName}", device.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from device: {DeviceName}", device.Name);
            return false;
        }
    }

    public async Task<bool> UpdateDeviceStateAsync(SmartDevice device)
    {
        try
        {
            _logger.LogDebug("Updating device state: {DeviceName}", device.Name);

            // Test connectivity
            var isReachable = device.Type switch
            {
                DeviceType.PhilipsHue => await HueDeviceController.TestConnectivityAsync(device.IpAddress),
                DeviceType.TpLink => await TpLinkDeviceController.TestConnectivityAsync(device.IpAddress),
                DeviceType.MagicHome => await MagicHomeDeviceController.TestConnectivityAsync(device.IpAddress),
                _ => false
            };

            var wasConnected = device.IsConnected;
            device.IsConnected = isReachable;
            device.LastSeen = DateTime.UtcNow;

            if (wasConnected != isReachable)
            {
                if (isReachable)
                {
                    DeviceConnected?.Invoke(this, new DeviceEventArgs(device));
                }
                else
                {
                    DeviceDisconnected?.Invoke(this, new DeviceEventArgs(device));
                }
            }
            else
            {
                DeviceStateChanged?.Invoke(this, new DeviceEventArgs(device));
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device state: {DeviceName}", device.Name);
            return false;
        }
    }

    public async Task<IEnumerable<SmartDevice>> GetConnectedDevicesAsync()
    {
        await Task.CompletedTask;
        return _devices.Where(d => d.IsConnected);
    }

    /// <summary>
    /// Tests connectivity to all devices
    /// </summary>
    public async Task TestAllDevicesConnectivityAsync()
    {
        _logger.LogInformation("Testing connectivity to all devices");

        var tasks = _devices.Select(device => UpdateDeviceStateAsync(device));
        await Task.WhenAll(tasks);

        var connectedCount = _devices.Count(d => d.IsConnected);
        _logger.LogInformation("Connectivity test completed. {ConnectedCount}/{TotalCount} devices connected",
            connectedCount, _devices.Count);
    }

    /// <summary>
    /// Gets a controller for a specific device type
    /// </summary>
    public IDeviceController? GetController(DeviceType deviceType)
    {
        _controllers.TryGetValue(deviceType, out var controller);
        return controller;
    }

    /// <summary>
    /// Sends a command to all connected devices of a specific type
    /// </summary>
    public async Task<bool> SendCommandToDeviceTypeAsync(DeviceType deviceType, Func<IDeviceController, Task<bool>> command)
    {
        try
        {
            if (_controllers.TryGetValue(deviceType, out var controller))
            {
                return await command(controller);
            }

            _logger.LogWarning("No controller found for device type: {DeviceType}", deviceType);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command to device type: {DeviceType}", deviceType);
            return false;
        }
    }
}
