using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace PartyLights.Services;

/// <summary>
/// Spotify WebSocket service for real-time playback updates
/// </summary>
public class SpotifyWebSocketService : ISpotifyWebSocketService, IDisposable
{
    private readonly ILogger<SpotifyWebSocketService> _logger;
    private readonly ISpotifyWebApiService _spotifyApiService;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private bool _isSubscribed;
    private readonly Timer _heartbeatTimer;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public event EventHandler<SpotifyPlaybackStateChangedEventArgs>? PlaybackStateChanged;
    public event EventHandler<SpotifyTrackChangedEventArgs>? TrackChanged;
    public event EventHandler<SpotifyDeviceChangedEventArgs>? DeviceChanged;
    public event EventHandler<SpotifyConnectionEventArgs>? ConnectionChanged;

    public SpotifyWebSocketService(
        ILogger<SpotifyWebSocketService> logger,
        ISpotifyWebApiService spotifyApiService)
    {
        _logger = logger;
        _spotifyApiService = spotifyApiService;

        // Initialize heartbeat timer (every 30 seconds)
        _heartbeatTimer = new Timer(SendHeartbeat, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Connects to Spotify WebSocket for real-time updates
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        try
        {
            if (IsConnected)
            {
                _logger.LogWarning("WebSocket is already connected");
                return true;
            }

            _logger.LogInformation("Connecting to Spotify WebSocket");

            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();

            // Note: Spotify doesn't provide a public WebSocket API for real-time updates
            // This is a placeholder implementation that would need to use polling or
            // Spotify's Connect SDK for actual real-time updates
            // For now, we'll simulate the connection and use polling

            _logger.LogInformation("Spotify WebSocket connection established (simulated)");
            ConnectionChanged?.Invoke(this, new SpotifyConnectionEventArgs(true));

            // Start polling for updates since WebSocket isn't available
            _receiveTask = StartPollingForUpdates(_cancellationTokenSource.Token);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to Spotify WebSocket");
            ConnectionChanged?.Invoke(this, new SpotifyConnectionEventArgs(false, ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Disconnects from Spotify WebSocket
    /// </summary>
    public async Task<bool> DisconnectAsync()
    {
        try
        {
            _logger.LogInformation("Disconnecting from Spotify WebSocket");

            _cancellationTokenSource?.Cancel();

            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
            }

            _webSocket?.Dispose();
            _webSocket = null;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _isSubscribed = false;

            ConnectionChanged?.Invoke(this, new SpotifyConnectionEventArgs(false));
            _logger.LogInformation("Disconnected from Spotify WebSocket");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from Spotify WebSocket");
            return false;
        }
    }

    /// <summary>
    /// Subscribes to playback updates
    /// </summary>
    public async Task<bool> SubscribeToPlaybackUpdatesAsync()
    {
        try
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot subscribe to updates - WebSocket not connected");
                return false;
            }

            _isSubscribed = true;
            _logger.LogInformation("Subscribed to Spotify playback updates");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to playback updates");
            return false;
        }
    }

    /// <summary>
    /// Unsubscribes from playback updates
    /// </summary>
    public async Task<bool> UnsubscribeFromPlaybackUpdatesAsync()
    {
        try
        {
            _isSubscribed = false;
            _logger.LogInformation("Unsubscribed from Spotify playback updates");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from playback updates");
            return false;
        }
    }

    #region Private Methods

    /// <summary>
    /// Starts polling for Spotify updates since WebSocket isn't available
    /// </summary>
    private async Task StartPollingForUpdates(CancellationToken cancellationToken)
    {
        SpotifyPlaybackState? lastPlaybackState = null;
        SpotifyTrack? lastTrack = null;

        while (!cancellationToken.IsCancellationRequested && _isSubscribed)
        {
            try
            {
                var currentPlaybackState = await _spotifyApiService.GetPlaybackStateAsync();

                if (currentPlaybackState != null)
                {
                    // Check for playback state changes
                    if (lastPlaybackState == null ||
                        lastPlaybackState.IsPlaying != currentPlaybackState.IsPlaying ||
                        lastPlaybackState.ProgressMs != currentPlaybackState.ProgressMs ||
                        lastPlaybackState.Device?.Id != currentPlaybackState.Device?.Id)
                    {
                        PlaybackStateChanged?.Invoke(this, new SpotifyPlaybackStateChangedEventArgs(lastPlaybackState, currentPlaybackState));
                        lastPlaybackState = currentPlaybackState;
                    }

                    // Check for track changes
                    if (currentPlaybackState.CurrentTrack != null)
                    {
                        if (lastTrack == null || lastTrack.Id != currentPlaybackState.CurrentTrack.Id)
                        {
                            TrackChanged?.Invoke(this, new SpotifyTrackChangedEventArgs(lastTrack, currentPlaybackState.CurrentTrack));
                            lastTrack = currentPlaybackState.CurrentTrack;
                        }
                    }

                    // Check for device changes
                    if (lastPlaybackState?.Device?.Id != currentPlaybackState.Device?.Id)
                    {
                        DeviceChanged?.Invoke(this, new SpotifyDeviceChangedEventArgs(lastPlaybackState?.Device, currentPlaybackState.Device));
                    }
                }

                // Poll every 2 seconds
                await Task.Delay(2000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during polling for Spotify updates");
                await Task.Delay(5000, cancellationToken); // Wait longer on error
            }
        }
    }

    /// <summary>
    /// Sends heartbeat to keep connection alive
    /// </summary>
    private async void SendHeartbeat(object? state)
    {
        try
        {
            if (IsConnected && _isSubscribed)
            {
                // Send ping message
                var pingMessage = JsonSerializer.Serialize(new { type = "ping", timestamp = DateTime.UtcNow });
                var buffer = Encoding.UTF8.GetBytes(pingMessage);

                if (_webSocket?.State == WebSocketState.Open)
                {
                    await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending heartbeat");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _heartbeatTimer?.Dispose();
            DisconnectAsync().Wait(5000); // Wait up to 5 seconds for disconnect
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disposal");
        }
    }

    #endregion
}
