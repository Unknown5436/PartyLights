using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using Q42.HueApi;
using Q42.HueApi.Models.Bridge;
using System.Net.NetworkInformation;

namespace PartyLights.Devices;

/// <summary>
/// Controller for Philips Hue devices
/// </summary>
public class HueDeviceController : IDeviceController
{
    private readonly ILogger<HueDeviceController> _logger;
    private readonly HttpClient _httpClient;
    private bool _isConnected;
    private string? _bridgeIpAddress;
    private string? _username;
    private LocalHueClient? _hueClient;

    public DeviceType DeviceType => DeviceType.PhilipsHue;
    public bool IsConnected => _isConnected;

    public HueDeviceController(ILogger<HueDeviceController> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<bool> ConnectAsync(string ipAddress)
    {
        try
        {
            _logger.LogInformation("Connecting to Hue Bridge at {IpAddress}", ipAddress);

            _bridgeIpAddress = ipAddress;
            _hueClient = new LocalHueClient(ipAddress);

            // Test connection
            var bridgeInfo = await _hueClient.GetBridgeAsync();
            if (bridgeInfo != null)
            {
                _logger.LogInformation("Successfully connected to Hue Bridge: {Name}", bridgeInfo.Name);
                _isConnected = true;
                return true;
            }

            _logger.LogWarning("Failed to connect to Hue Bridge at {IpAddress}", ipAddress);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to Hue Bridge at {IpAddress}", ipAddress);
            return false;
        }
    }

    public async Task<bool> DisconnectAsync()
    {
        try
        {
            _logger.LogInformation("Disconnecting from Hue Bridge");

            _hueClient = null;
            _bridgeIpAddress = null;
            _username = null;
            _isConnected = false;

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from Hue Bridge");
            return false;
        }
    }

    public async Task<bool> SetColorAsync(int r, int g, int b)
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Hue color to RGB({R}, {G}, {B})", r, g, b);

            var command = new LightCommand();
            command.SetColor(r, g, b);

            await _hueClient.SendCommandAsync(command);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hue color");
            return false;
        }
    }

    public async Task<bool> SetBrightnessAsync(int brightness)
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Hue brightness to {Brightness}", brightness);

            var command = new LightCommand();
            command.Brightness = (byte)Math.Clamp(brightness, 0, 255);

            await _hueClient.SendCommandAsync(command);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hue brightness");
            return false;
        }
    }

    public async Task<bool> SetEffectAsync(string effectName)
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Hue effect to {EffectName}", effectName);

            var command = new LightCommand();

            switch (effectName.ToLower())
            {
                case "colorloop":
                    command.Effect = Effect.ColorLoop;
                    break;
                case "none":
                default:
                    command.Effect = Effect.None;
                    break;
            }

            await _hueClient.SendCommandAsync(command);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hue effect");
            return false;
        }
    }

    public async Task<bool> TurnOnAsync()
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Turning Hue device on");

            var command = new LightCommand();
            command.On = true;

            await _hueClient.SendCommandAsync(command);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning Hue device on");
            return false;
        }
    }

    public async Task<bool> TurnOffAsync()
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Turning Hue device off");

            var command = new LightCommand();
            command.On = false;

            await _hueClient.SendCommandAsync(command);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning Hue device off");
            return false;
        }
    }

    /// <summary>
    /// Discovers Hue bridges on the local network
    /// </summary>
    public static async Task<List<Bridge>> DiscoverBridgesAsync()
    {
        try
        {
            var locator = new HttpBridgeLocator();
            var bridges = await locator.LocateBridgesAsync(TimeSpan.FromSeconds(5));
            return bridges.ToList();
        }
        catch (Exception)
        {
            return new List<Bridge>();
        }
    }

    /// <summary>
    /// Registers a new user with the Hue bridge
    /// </summary>
    public async Task<string?> RegisterUserAsync(string appName = "PartyLights", string deviceName = "PartyLights")
    {
        if (_hueClient == null)
        {
            _logger.LogWarning("Hue client not initialized");
            return null;
        }

        try
        {
            _logger.LogInformation("Registering new user with Hue Bridge");

            var result = await _hueClient.RegisterAsync(appName, deviceName);
            if (!string.IsNullOrEmpty(result))
            {
                _username = result;
                _logger.LogInformation("Successfully registered user: {Username}", result);
                return result;
            }

            _logger.LogWarning("Failed to register user with Hue Bridge");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user with Hue Bridge");
            return null;
        }
    }

    /// <summary>
    /// Gets all lights from the connected bridge
    /// </summary>
    public async Task<List<Light>> GetLightsAsync()
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return new List<Light>();
        }

        try
        {
            var lights = await _hueClient.GetLightsAsync();
            return lights.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lights from Hue Bridge");
            return new List<Light>();
        }
    }

    /// <summary>
    /// Tests network connectivity to a Hue bridge
    /// </summary>
    public static async Task<bool> TestConnectivityAsync(string ipAddress)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, 3000);
            return reply.Status == IPStatus.Success;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
