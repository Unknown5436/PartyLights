using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PartyLights.Devices;

/// <summary>
/// Controller for TP-Link Kasa devices
/// </summary>
public class TpLinkDeviceController : IDeviceController
{
    private readonly ILogger<TpLinkDeviceController> _logger;
    private bool _isConnected;
    private string? _deviceIpAddress;
    private int _devicePort = 9999;
    private string? _deviceId;
    private string? _deviceToken;

    public DeviceType DeviceType => DeviceType.TpLink;
    public bool IsConnected => _isConnected;

    public TpLinkDeviceController(ILogger<TpLinkDeviceController> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(string ipAddress)
    {
        try
        {
            _logger.LogInformation("Connecting to TP-Link device at {IpAddress}", ipAddress);

            _deviceIpAddress = ipAddress;

            // Test connectivity first
            if (!await TestConnectivityAsync(ipAddress))
            {
                _logger.LogWarning("Cannot reach TP-Link device at {IpAddress}", ipAddress);
                return false;
            }

            // Get device info
            var deviceInfo = await GetDeviceInfoAsync(ipAddress);
            if (deviceInfo != null)
            {
                _deviceId = deviceInfo.GetValueOrDefault("deviceId")?.ToString();
                _deviceToken = deviceInfo.GetValueOrDefault("token")?.ToString();

                _logger.LogInformation("Successfully connected to TP-Link device: {Alias}",
                    deviceInfo.GetValueOrDefault("alias"));
                _isConnected = true;
                return true;
            }

            _logger.LogWarning("Failed to get device info from TP-Link device at {IpAddress}", ipAddress);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to TP-Link device at {IpAddress}", ipAddress);
            return false;
        }
    }

    public async Task<bool> DisconnectAsync()
    {
        try
        {
            _logger.LogInformation("Disconnecting from TP-Link device");

            _deviceIpAddress = null;
            _deviceId = null;
            _deviceToken = null;
            _isConnected = false;

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from TP-Link device");
            return false;
        }
    }

    public async Task<bool> SetColorAsync(int r, int g, int b)
    {
        if (!_isConnected || string.IsNullOrEmpty(_deviceIpAddress))
        {
            _logger.LogWarning("TP-Link device not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting TP-Link color to RGB({R}, {G}, {B})", r, g, b);

            var command = new
            {
                method = "set_device_info",
                params = new
                       {
                           hue = (int)(r * 360.0 / 255.0),
                           saturation = (int)(g * 100.0 / 255.0),
                           brightness = (int)(b * 100.0 / 255.0),
                           color_temp = 0
                       }
            };

            var response = await SendCommandAsync(command);
            return response != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting TP-Link color");
            return false;
        }
    }

    public async Task<bool> SetBrightnessAsync(int brightness)
    {
        if (!_isConnected || string.IsNullOrEmpty(_deviceIpAddress))
        {
            _logger.LogWarning("TP-Link device not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting TP-Link brightness to {Brightness}", brightness);

            var command = new
            {
                method = "set_device_info",
                params = new
                       {
                           brightness = Math.Clamp(brightness, 1, 100)
                       }
            };

            var response = await SendCommandAsync(command);
            return response != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting TP-Link brightness");
            return false;
        }
    }

    public async Task<bool> SetEffectAsync(string effectName)
    {
        if (!_isConnected || string.IsNullOrEmpty(_deviceIpAddress))
        {
            _logger.LogWarning("TP-Link device not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting TP-Link effect to {EffectName}", effectName);

            // TP-Link devices have limited effect support
            // This is a placeholder for future implementation
            await Task.Delay(100);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting TP-Link effect");
            return false;
        }
    }

    public async Task<bool> TurnOnAsync()
    {
        if (!_isConnected || string.IsNullOrEmpty(_deviceIpAddress))
        {
            _logger.LogWarning("TP-Link device not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Turning TP-Link device on");

            var command = new
            {
                method = "set_device_info",
                params = new
                       {
                           on_off = 1
                       }
            };

            var response = await SendCommandAsync(command);
            return response != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning TP-Link device on");
            return false;
        }
    }

    public async Task<bool> TurnOffAsync()
    {
        if (!_isConnected || string.IsNullOrEmpty(_deviceIpAddress))
        {
            _logger.LogWarning("TP-Link device not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Turning TP-Link device off");

            var command = new
            {
                method = "set_device_info",
                params = new
                       {
                           on_off = 0
                       }
            };

            var response = await SendCommandAsync(command);
            return response != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning TP-Link device off");
            return false;
        }
    }

    /// <summary>
    /// Discovers TP-Link devices on the local network using UDP broadcast
    /// </summary>
    public static async Task<List<TpLinkDeviceInfo>> DiscoverDevicesAsync()
    {
        var devices = new List<TpLinkDeviceInfo>();

        try
        {
            // TP-Link discovery message
            var discoveryMessage = "{\"system\":{\"get_sysinfo\":null}}";
            var encryptedMessage = EncryptTpLinkMessage(discoveryMessage);

            using var udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = 5000;

            // Send broadcast
            var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, 9999);
            await udpClient.SendAsync(encryptedMessage, encryptedMessage.Length, broadcastEndpoint);

            // Listen for responses
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < TimeSpan.FromSeconds(3))
            {
                try
                {
                    var result = await udpClient.ReceiveAsync();
                    var response = DecryptTpLinkMessage(result.Buffer);

                    if (!string.IsNullOrEmpty(response))
                    {
                        var deviceInfo = JsonSerializer.Deserialize<JsonElement>(response);
                        if (deviceInfo.TryGetProperty("system", out var system) &&
                            system.TryGetProperty("get_sysinfo", out var sysInfo))
                        {
                            var device = new TpLinkDeviceInfo
                            {
                                IpAddress = result.RemoteEndPoint.Address.ToString(),
                                Alias = sysInfo.GetProperty("alias").GetString() ?? "Unknown",
                                Model = sysInfo.GetProperty("model").GetString() ?? "Unknown",
                                MacAddress = sysInfo.GetProperty("mac").GetString() ?? "Unknown",
                                DeviceId = sysInfo.GetProperty("deviceId").GetString() ?? "Unknown"
                            };

                            devices.Add(device);
                        }
                    }
                }
                catch (SocketException)
                {
                    // Timeout or no response, continue
                    break;
                }
            }
        }
        catch (Exception)
        {
            // Discovery failed
        }

        return devices;
    }

    /// <summary>
    /// Gets device information from a TP-Link device
    /// </summary>
    private async Task<Dictionary<string, object>?> GetDeviceInfoAsync(string ipAddress)
    {
        try
        {
            var command = new
            {
                system = new
                {
                    get_sysinfo = (object?)null
                }
            };

            var response = await SendCommandAsync(command, ipAddress);
            if (response != null && response.TryGetProperty("system", out var system) &&
                system.TryGetProperty("get_sysinfo", out var sysInfo))
            {
                var result = new Dictionary<string, object>();
                foreach (var property in sysInfo.EnumerateObject())
                {
                    result[property.Name] = property.Value.GetString() ?? property.Value.ToString();
                }
                return result;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device info from {IpAddress}", ipAddress);
            return null;
        }
    }

    /// <summary>
    /// Sends a command to a TP-Link device
    /// </summary>
    private async Task<JsonElement?> SendCommandAsync(object command, string? ipAddress = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(command);
            var encryptedMessage = EncryptTpLinkMessage(json);

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(ipAddress ?? _deviceIpAddress!, _devicePort);

            using var stream = tcpClient.GetStream();
            await stream.WriteAsync(encryptedMessage, 0, encryptedMessage.Length);

            var responseBuffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

            if (bytesRead > 0)
            {
                var response = DecryptTpLinkMessage(responseBuffer.Take(bytesRead).ToArray());
                if (!string.IsNullOrEmpty(response))
                {
                    return JsonSerializer.Deserialize<JsonElement>(response);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command to TP-Link device");
            return null;
        }
    }

    /// <summary>
    /// Tests network connectivity to a TP-Link device
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

    /// <summary>
    /// Encrypts a message for TP-Link protocol
    /// </summary>
    private static byte[] EncryptTpLinkMessage(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var key = 171;
        var result = new byte[bytes.Length + 4];

        // Add header
        result[0] = 0x02;
        result[1] = 0x00;
        result[2] = (byte)(bytes.Length >> 8);
        result[3] = (byte)(bytes.Length & 0xFF);

        // Encrypt payload
        for (int i = 0; i < bytes.Length; i++)
        {
            var encrypted = bytes[i] ^ key;
            result[i + 4] = (byte)encrypted;
            key = encrypted;
        }

        return result;
    }

    /// <summary>
    /// Decrypts a message from TP-Link protocol
    /// </summary>
    private static string DecryptTpLinkMessage(byte[] encryptedMessage)
    {
        if (encryptedMessage.Length < 4)
            return string.Empty;

        var key = 171;
        var decryptedBytes = new byte[encryptedMessage.Length - 4];

        for (int i = 4; i < encryptedMessage.Length; i++)
        {
            var decrypted = encryptedMessage[i] ^ key;
            decryptedBytes[i - 4] = (byte)decrypted;
            key = encryptedMessage[i];
        }

        return Encoding.UTF8.GetString(decryptedBytes);
    }
}

/// <summary>
/// Information about a discovered TP-Link device
/// </summary>
public class TpLinkDeviceInfo
{
    public string IpAddress { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}
