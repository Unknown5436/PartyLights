using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;

namespace PartyLights.Services;

/// <summary>
/// Enhanced Spotify service that integrates with comprehensive Web API services
/// </summary>
public class SpotifyService : ISpotifyService
{
    private readonly ILogger<SpotifyService> _logger;
    private readonly ISpotifyWebApiService _webApiService;
    private readonly ISpotifyWebSocketService _webSocketService;
    private readonly ISpotifyAudioAnalysisService _audioAnalysisService;
    private bool _isListening;

    public bool IsListening => _isListening;

    public event EventHandler<SpotifyTrackEventArgs>? TrackChanged;

    public SpotifyService(
        ILogger<SpotifyService> logger,
        ISpotifyWebApiService webApiService,
        ISpotifyWebSocketService webSocketService,
        ISpotifyAudioAnalysisService audioAnalysisService)
    {
        _logger = logger;
        _webApiService = webApiService;
        _webSocketService = webSocketService;
        _audioAnalysisService = audioAnalysisService;

        // Subscribe to track change events
        _webApiService.TrackChanged += OnTrackChanged;
        _webSocketService.TrackChanged += OnTrackChanged;
    }

    /// <summary>
    /// Authenticates with Spotify
    /// </summary>
    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            _logger.LogInformation("Starting Spotify authentication");
            var success = await _webApiService.AuthenticateAsync();

            if (success)
            {
                _logger.LogInformation("Spotify authentication successful");
            }
            else
            {
                _logger.LogWarning("Spotify authentication failed or requires manual intervention");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Spotify authentication");
            return false;
        }
    }

    /// <summary>
    /// Checks if Spotify is authenticated
    /// </summary>
    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            return await _webApiService.IsAuthenticatedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Spotify authentication status");
            return false;
        }
    }

    /// <summary>
    /// Gets the current playing track
    /// </summary>
    public async Task<SpotifyTrack?> GetCurrentTrackAsync()
    {
        try
        {
            var playbackState = await _webApiService.GetPlaybackStateAsync();
            return playbackState?.CurrentTrack;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current track");
            return null;
        }
    }

    /// <summary>
    /// Starts listening for Spotify updates
    /// </summary>
    public async Task<bool> StartListeningAsync()
    {
        try
        {
            if (_isListening)
            {
                _logger.LogWarning("Already listening to Spotify updates");
                return true;
            }

            _logger.LogInformation("Starting Spotify listening");

            // Connect to WebSocket service for real-time updates
            var connected = await _webSocketService.ConnectAsync();
            if (!connected)
            {
                _logger.LogWarning("Failed to connect to Spotify WebSocket service");
                return false;
            }

            // Subscribe to playback updates
            var subscribed = await _webSocketService.SubscribeToPlaybackUpdatesAsync();
            if (!subscribed)
            {
                _logger.LogWarning("Failed to subscribe to Spotify playback updates");
                await _webSocketService.DisconnectAsync();
                return false;
            }

            _isListening = true;
            _logger.LogInformation("Successfully started listening to Spotify updates");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Spotify listening");
            return false;
        }
    }

    /// <summary>
    /// Stops listening for Spotify updates
    /// </summary>
    public async Task StopListeningAsync()
    {
        try
        {
            if (!_isListening)
            {
                _logger.LogWarning("Not currently listening to Spotify updates");
                return;
            }

            _logger.LogInformation("Stopping Spotify listening");

            // Unsubscribe from updates
            await _webSocketService.UnsubscribeFromPlaybackUpdatesAsync();

            // Disconnect from WebSocket service
            await _webSocketService.DisconnectAsync();

            _isListening = false;
            _logger.LogInformation("Successfully stopped listening to Spotify updates");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Spotify listening");
        }
    }

    #region Additional Methods for Enhanced Functionality

    /// <summary>
    /// Gets comprehensive track information including audio features
    /// </summary>
    public async Task<SpotifyTrack?> GetTrackWithAnalysisAsync(string trackId)
    {
        try
        {
            var track = await _webApiService.GetTrackAsync(trackId);
            if (track == null)
            {
                return null;
            }

            // Get audio features
            var audioFeatures = await _audioAnalysisService.AnalyzeTrackAsync(trackId);
            if (audioFeatures != null)
            {
                track.AudioFeatures = new Dictionary<string, object>
                {
                    ["Danceability"] = audioFeatures.Danceability,
                    ["Energy"] = audioFeatures.Energy,
                    ["Valence"] = audioFeatures.Valence,
                    ["Tempo"] = audioFeatures.Tempo,
                    ["Loudness"] = audioFeatures.Loudness,
                    ["Speechiness"] = audioFeatures.Speechiness,
                    ["Acousticness"] = audioFeatures.Acousticness,
                    ["Instrumentalness"] = audioFeatures.Instrumentalness,
                    ["Liveness"] = audioFeatures.Liveness
                };
            }

            return track;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting track with analysis: {TrackId}", trackId);
            return null;
        }
    }

    /// <summary>
    /// Gets track mood analysis
    /// </summary>
    public async Task<Dictionary<string, float>> GetTrackMoodAsync(string trackId)
    {
        try
        {
            return await _audioAnalysisService.GetTrackMoodAsync(trackId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting track mood: {TrackId}", trackId);
            return new Dictionary<string, float>();
        }
    }

    /// <summary>
    /// Gets recommended presets for a track
    /// </summary>
    public async Task<IEnumerable<string>> GetRecommendedPresetsAsync(string trackId)
    {
        try
        {
            return await _audioAnalysisService.GetRecommendedPresetsAsync(trackId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommended presets: {TrackId}", trackId);
            return new[] { "VolumeReactive", "Static" };
        }
    }

    /// <summary>
    /// Checks if a track is suitable for lighting effects
    /// </summary>
    public async Task<bool> IsTrackSuitableForLightingAsync(string trackId)
    {
        try
        {
            return await _audioAnalysisService.IsTrackSuitableForLightingAsync(trackId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking track suitability: {TrackId}", trackId);
            return true; // Default to suitable
        }
    }

    /// <summary>
    /// Gets user's playlists
    /// </summary>
    public async Task<IEnumerable<SpotifyPlaylist>> GetUserPlaylistsAsync()
    {
        try
        {
            return await _webApiService.GetUserPlaylistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user playlists");
            return Enumerable.Empty<SpotifyPlaylist>();
        }
    }

    /// <summary>
    /// Searches for tracks
    /// </summary>
    public async Task<IEnumerable<SpotifyTrack>> SearchTracksAsync(string query)
    {
        try
        {
            return await _webApiService.SearchTracksAsync(query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching tracks: {Query}", query);
            return Enumerable.Empty<SpotifyTrack>();
        }
    }

    /// <summary>
    /// Controls playback
    /// </summary>
    public async Task<bool> PlayTrackAsync(string trackId)
    {
        try
        {
            return await _webApiService.PlayTrackAsync(trackId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing track: {TrackId}", trackId);
            return false;
        }
    }

    /// <summary>
    /// Pauses playback
    /// </summary>
    public async Task<bool> PausePlaybackAsync()
    {
        try
        {
            return await _webApiService.PausePlaybackAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing playback");
            return false;
        }
    }

    /// <summary>
    /// Resumes playback
    /// </summary>
    public async Task<bool> ResumePlaybackAsync()
    {
        try
        {
            return await _webApiService.ResumePlaybackAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming playback");
            return false;
        }
    }

    #endregion

    #region Event Handlers

    private void OnTrackChanged(object? sender, SpotifyTrackChangedEventArgs e)
    {
        TrackChanged?.Invoke(this, new SpotifyTrackEventArgs(e.CurrentTrack));
    }

    #endregion
}