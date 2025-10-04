using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PartyLights.Devices;

/// <summary>
/// Controller for Magic Home LED devices
/// </summary>
public class MagicHomeDeviceController : IDeviceController
{
    private readonly ILogger<MagicHomeDeviceController> _logger;
    private bool _isConnected;
    private string? _deviceIpAddress;
    private int _devicePort = 5577;
    private UdpClient? _udpClient;
    private TcpClient? _tcpClient;

    public DeviceType DeviceType => DeviceType.MagicHome;
    public bool IsConnected => _isConnected;

    public MagicHomeDeviceController(ILogger<MagicHomeDeviceController> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(string ipAddress)
    {
        try
        {
            _logger.LogInformation("Connecting to Magic Home device at {IpAddress}", ipAddress);

            _deviceIpAddress = ipAddress;

            // Test connectivity first
            if (!await TestConnectivityAsync(ipAddress))
            {
                _logger.LogWarning("Cannot reach Magic Home device at {IpAddress}", ipAddress);
                return false;
            }

            // Test device response
            var deviceInfo = await GetDeviceInfoAsync(ipAddress);
            if (deviceInfo != null)
            {
                _logger.LogInformation("Successfully connected to Magic Home device: {Model}", deviceInfo.Model);
                _isConnected = true;
                return true;
            }

            _logger.LogWarning("Failed to get device info from Magic Home device at {IpAddress}", ipAddress);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to Magic Home device at {IpAddress}", ipAddress);
            return false;
        }
    }

    public async Task<bool> DisconnectAsync()
    {
        try
        {
            _logger.LogInformation("Disconnecting from Magic Home device");

            _udpClient?.Close();
            _tcpClient?.Close();
            _udpClient = null;
            _tcpClient = null;
            _deviceIpAddress = null;
            _isConnected = false;

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from Magic Home device");
            return false;
        }
    }

    public async Task<bool> SetColorAsync(int r, int g, int b)
    {
        if (!_isConnected || string.IsNullOrEmpty(_deviceIpAddress))
        {
            _logger.LogWarning("Magic Home device not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Magic Home color to RGB({R}, {G}, {B})", r, g, b);

            var command = new byte[] { 0x31, (byte)r, (byte)g, (byte)b, 0x00, 0x0f };
            var response = await SendTcpCommandAsync(command);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Magic Home color");
            return false;
        }
    }

    public async Task<bool> SetBrightnessAsync(int brightness)
    {
        if (!_isConnected || string.IsNullOrEmpty(_deviceIpAddress))
        {
            _logger.LogWarning("Magic Home device not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Magic Home brightness to {Brightness}", brightness);

            var command = new byte[] { 0x31, 0x00, 0x00, 0x00, (byte)brightness, 0x0f };
            var response = await SendTcpCommandAsync(command);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Magic Home brightness");
            return false;
        }
    }

    public async Task<bool> SetEffectAsync(string effectName)
    {
        if (!_isConnected || string.IsNullOrEmpty(_deviceIpAddress))
        {
            _logger.LogWarning("Magic Home device not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Setting Magic Home effect to {EffectName}", effectName);

            byte effectCode = effectName.ToLower() switch
            {
                "seven_color_cross_fade" => 0x25,
                "red_gradual_change" => 0x26,
                "green_gradual_change" => 0x27,
                "blue_gradual_change" => 0x28,
                "yellow_gradual_change" => 0x29,
                "cyan_gradual_change" => 0x2a,
                "purple_gradual_change" => 0x2b,
                "white_gradual_change" => 0x2c,
                "red_green_cross_fade" => 0x2d,
                "red_blue_cross_fade" => 0x2e,
                "green_blue_cross_fade" => 0x2f,
                "seven_color_strobe_flash" => 0x30,
                "red_strobe_flash" => 0x31,
                "green_strobe_flash" => 0x32,
                "blue_strobe_flash" => 0x33,
                "yellow_strobe_flash" => 0x34,
                "cyan_strobe_flash" => 0x35,
                "purple_strobe_flash" => 0x36,
                "white_strobe_flash" => 0x37,
                "seven_color_jumping_change" => 0x38,
                _ => 0x25 // Default to seven color cross fade
            };

            var command = new byte[] { effectCode, 0x00, 0x00, 0x00, 0x00, 0x0f };
            var response = await SendTcpCommandAsync(command);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Magic Home effect");
            return false;
        }
    }

    public async Task<bool> TurnOnAsync()
    {
        if (!_isConnected || string.IsNullOrEmpty(_deviceIpAddress))
        {
            _logger.LogWarning("Magic Home device not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Turning Magic Home device on");

            var command = new byte[] { 0x71, 0x23, 0x0f };
            var response = await SendTcpCommandAsync(command);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning Magic Home device on");
            return false;
        }
    }

    public async Task<bool> TurnOffAsync()
    {
        if (!_isConnected || string.IsNullOrEmpty(_deviceIpAddress))
        {
            _logger.LogWarning("Magic Home device not connected");
            return false;
        }

        try
        {
            _logger.LogDebug("Turning Magic Home device off");

            var command = new byte[] { 0x71, 0x24, 0x0f };
            var response = await SendTcpCommandAsync(command);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error turning Magic Home device off");
            return false;
        }
    }

    /// <summary>
    /// Discovers Magic Home devices on the local network using UDP broadcast
    /// </summary>
    public static async Task<List<MagicHomeDeviceInfo>> DiscoverDevicesAsync()
    {
        var devices = new List<MagicHomeDeviceInfo>();

        try
        {
            using var udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = 5000;

            // Magic Home discovery message
            var discoveryMessage = new byte[] { 0x20, 0x00, 0x00, 0x00, 0x16, 0x02, 0x62, 0x3A, 0xD5, 0xED, 0xA3, 0x01, 0xAE, 0x08, 0x2D, 0x46, 0x61, 0x41, 0xA7, 0xF6, 0xDC, 0xAF, 0xD3, 0xE6, 0x00, 0x00, 0x1E };

            // Send broadcast
            var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, 48899);
            await udpClient.SendAsync(discoveryMessage, discoveryMessage.Length, broadcastEndpoint);

            // Listen for responses
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < TimeSpan.FromSeconds(3))
            {
                try
                {
                    var result = await udpClient.ReceiveAsync();
                    var response = result.Buffer;

                    if (response.Length >= 20)
                    {
                        var device = new MagicHomeDeviceInfo
                        {
                            IpAddress = result.RemoteEndPoint.Address.ToString(),
                            MacAddress = BitConverter.ToString(response, 0, 6).Replace("-", ":"),
                            Model = "Magic Home Controller",
                            FirmwareVersion = $"{response[6]}.{response[7]}"
                        };

                        devices.Add(device);
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
    /// Gets device information from a Magic Home device
    /// </summary>
    private async Task<MagicHomeDeviceInfo?> GetDeviceInfoAsync(string ipAddress)
    {
        try
        {
            // Try to get device info via TCP
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(ipAddress, _devicePort);

            using var stream = tcpClient.GetStream();

            // Send status request
            var statusCommand = new byte[] { 0x81, 0x8A, 0x8B };
            await stream.WriteAsync(statusCommand, 0, statusCommand.Length);

            // Read response
            var responseBuffer = new byte[14];
            var bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

            if (bytesRead >= 14)
            {
                return new MagicHomeDeviceInfo
                {
                    IpAddress = ipAddress,
                    Model = "Magic Home Controller",
                    MacAddress = "Unknown",
                    FirmwareVersion = "Unknown",
                    IsOn = responseBuffer[2] == 0x23,
                    Mode = responseBuffer[3],
                    Red = responseBuffer[6],
                    Green = responseBuffer[7],
                    Blue = responseBuffer[8],
                    Brightness = responseBuffer[9]
                };
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
    /// Sends a TCP command to the Magic Home device
    /// </summary>
    private async Task<bool> SendTcpCommandAsync(byte[] command)
    {
        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(_deviceIpAddress!, _devicePort);

            using var stream = tcpClient.GetStream();
            await stream.WriteAsync(command, 0, command.Length);

            // Read response
            var responseBuffer = new byte[1024];
            var bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

            return bytesRead > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending TCP command to Magic Home device");
            return false;
        }
    }

    /// <summary>
    /// Tests network connectivity to a Magic Home device
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

/// <summary>
/// Information about a discovered Magic Home device
/// </summary>
public class MagicHomeDeviceInfo
{
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public bool IsOn { get; set; }
    public byte Mode { get; set; }
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
    public byte Brightness { get; set; }
}
