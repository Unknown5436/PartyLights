using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using Q42.HueApi;
using Q42.HueApi.Models.Bridge;
using System.Net.NetworkInformation;

namespace PartyLights.Devices;

/// <summary>
/// Advanced controller for Philips Hue devices
/// </summary>
public class HueDeviceController : IAdvancedDeviceController
{
    private readonly ILogger<HueDeviceController> _logger;
    private readonly HttpClient _httpClient;
    private bool _isConnected;
    private string? _bridgeIpAddress;
    private string? _username;
    private LocalHueClient? _hueClient;
    private DeviceState? _currentState;

    public DeviceType DeviceType => DeviceType.PhilipsHue;
    public bool IsConnected => _isConnected;

    public HueDeviceController(ILogger<HueDeviceController> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    #region Basic Control Methods

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

                // Load current state
                await LoadDeviceStateAsync();

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
            _currentState = null;

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

            // Update state
            if (_currentState != null)
            {
                _currentState.Red = r;
                _currentState.Green = g;
                _currentState.Blue = b;
                _currentState.LastUpdated = DateTime.UtcNow;
            }

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

            // Update state
            if (_currentState != null)
            {
                _currentState.Brightness = brightness;
                _currentState.LastUpdated = DateTime.UtcNow;
            }

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

            // Update state
            if (_currentState != null)
            {
                _currentState.CurrentEffect = effectName;
                _currentState.LastUpdated = DateTime.UtcNow;
            }

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

            // Update state
            if (_currentState != null)
            {
                _currentState.IsOn = true;
                _currentState.LastUpdated = DateTime.UtcNow;
            }

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

            // Update state
            if (_currentState != null)
            {
                _currentState.IsOn = false;
                _currentState.LastUpdated = DateTime.UtcNow;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning Hue device off");
            return false;
        }
    }

    #endregion

    #region Advanced Control Methods

    public async Task<bool> SetColorTemperatureAsync(int temperature)
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Hue color temperature to {Temperature}", temperature);

            var command = new LightCommand();
            command.ColorTemperature = temperature;

            await _hueClient.SendCommandAsync(command);

            // Update state
            if (_currentState != null)
            {
                _currentState.ColorTemperature = temperature;
                _currentState.LastUpdated = DateTime.UtcNow;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hue color temperature");
            return false;
        }
    }

    public async Task<bool> SetSaturationAsync(int saturation)
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Hue saturation to {Saturation}", saturation);

            var command = new LightCommand();
            command.Saturation = (byte)Math.Clamp(saturation, 0, 254);

            await _hueClient.SendCommandAsync(command);

            // Update state
            if (_currentState != null)
            {
                _currentState.Saturation = saturation;
                _currentState.LastUpdated = DateTime.UtcNow;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hue saturation");
            return false;
        }
    }

    public async Task<bool> SetHueAsync(int hue)
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Hue hue to {Hue}", hue);

            var command = new LightCommand();
            command.Hue = hue;

            await _hueClient.SendCommandAsync(command);

            // Update state
            if (_currentState != null)
            {
                _currentState.Hue = hue;
                _currentState.LastUpdated = DateTime.UtcNow;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hue hue");
            return false;
        }
    }

    public async Task<bool> SetTransitionTimeAsync(int milliseconds)
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Hue transition time to {Milliseconds}ms", milliseconds);

            var command = new LightCommand();
            command.TransitionTime = TimeSpan.FromMilliseconds(milliseconds);

            await _hueClient.SendCommandAsync(command);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hue transition time");
            return false;
        }
    }

    public async Task<bool> SetPowerStateAsync(bool isOn)
    {
        return isOn ? await TurnOnAsync() : await TurnOffAsync();
    }

    public async Task<DeviceState?> GetDeviceStateAsync()
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return null;
        }

        try
        {
            await LoadDeviceStateAsync();
            return _currentState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Hue device state");
            return null;
        }
    }

    public async Task<bool> SetSceneAsync(string sceneName)
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Hue scene to {SceneName}", sceneName);

            // Hue scenes are managed through groups
            var groups = await _hueClient.GetGroupsAsync();
            var group = groups.FirstOrDefault(g => g.Name.Equals(sceneName, StringComparison.OrdinalIgnoreCase));

            if (group != null)
            {
                var command = new GroupCommand();
                command.Scene = sceneName;

                await _hueClient.SendGroupCommandAsync(command, group.Id);

                // Update state
                if (_currentState != null)
                {
                    _currentState.CurrentScene = sceneName;
                    _currentState.LastUpdated = DateTime.UtcNow;
                }

                return true;
            }

            _logger.LogWarning("Scene '{SceneName}' not found", sceneName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hue scene");
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetAvailableScenesAsync()
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return Enumerable.Empty<string>();
        }

        try
        {
            var scenes = await _hueClient.GetScenesAsync();
            return scenes.Select(s => s.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Hue scenes");
            return Enumerable.Empty<string>();
        }
    }

    public async Task<bool> SetGroupAsync(string groupName)
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Hue group to {GroupName}", groupName);

            var groups = await _hueClient.GetGroupsAsync();
            var group = groups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));

            if (group != null)
            {
                // Update state
                if (_currentState != null)
                {
                    _currentState.CurrentGroup = groupName;
                    _currentState.LastUpdated = DateTime.UtcNow;
                }

                return true;
            }

            _logger.LogWarning("Group '{GroupName}' not found", groupName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hue group");
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetAvailableGroupsAsync()
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return Enumerable.Empty<string>();
        }

        try
        {
            var groups = await _hueClient.GetGroupsAsync();
            return groups.Select(g => g.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Hue groups");
            return Enumerable.Empty<string>();
        }
    }

    public async Task<bool> SetBrightnessWithTransitionAsync(int brightness, int transitionTime)
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Hue brightness to {Brightness} with {TransitionTime}ms transition", brightness, transitionTime);

            var command = new LightCommand();
            command.Brightness = (byte)Math.Clamp(brightness, 0, 255);
            command.TransitionTime = TimeSpan.FromMilliseconds(transitionTime);

            await _hueClient.SendCommandAsync(command);

            // Update state
            if (_currentState != null)
            {
                _currentState.Brightness = brightness;
                _currentState.LastUpdated = DateTime.UtcNow;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hue brightness with transition");
            return false;
        }
    }

    public async Task<bool> SetColorWithTransitionAsync(int r, int g, int b, int transitionTime)
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Hue color to RGB({R}, {G}, {B}) with {TransitionTime}ms transition", r, g, b, transitionTime);

            var command = new LightCommand();
            command.SetColor(r, g, b);
            command.TransitionTime = TimeSpan.FromMilliseconds(transitionTime);

            await _hueClient.SendCommandAsync(command);

            // Update state
            if (_currentState != null)
            {
                _currentState.Red = r;
                _currentState.Green = g;
                _currentState.Blue = b;
                _currentState.LastUpdated = DateTime.UtcNow;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hue color with transition");
            return false;
        }
    }

    public async Task<bool> SetEffectWithTransitionAsync(string effectName, int transitionTime)
    {
        if (!_isConnected || _hueClient == null)
        {
            _logger.LogWarning("Hue client not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Hue effect to {EffectName} with {TransitionTime}ms transition", effectName, transitionTime);

            var command = new LightCommand();
            command.TransitionTime = TimeSpan.FromMilliseconds(transitionTime);

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

            // Update state
            if (_currentState != null)
            {
                _currentState.CurrentEffect = effectName;
                _currentState.LastUpdated = DateTime.UtcNow;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Hue effect with transition");
            return false;
        }
    }

    #endregion

    #region Private Methods

    private async Task LoadDeviceStateAsync()
    {
        if (!_isConnected || _hueClient == null)
            return;

        try
        {
            var lights = await _hueClient.GetLightsAsync();
            var firstLight = lights.FirstOrDefault();

            if (firstLight != null)
            {
                _currentState = new DeviceState
                {
                    IsOn = firstLight.State.On,
                    Brightness = firstLight.State.Brightness,
                    Red = firstLight.State.ColorCoordinates?.X ?? 0,
                    Green = firstLight.State.ColorCoordinates?.Y ?? 0,
                    Blue = 0, // Hue doesn't store RGB directly
                    ColorTemperature = firstLight.State.ColorTemperature ?? 0,
                    Saturation = firstLight.State.Saturation ?? 0,
                    Hue = firstLight.State.Hue ?? 0,
                    CurrentEffect = firstLight.State.Effect?.ToString(),
                    LastUpdated = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Hue device state");
        }
    }

    #endregion

    #region Static Methods

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

    #endregion
}
