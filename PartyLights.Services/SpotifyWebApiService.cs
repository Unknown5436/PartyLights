using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;

namespace PartyLights.Services;

/// <summary>
/// Comprehensive Spotify Web API service implementation
/// </summary>
public class SpotifyWebApiService : ISpotifyWebApiService
{
    private readonly ILogger<SpotifyWebApiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ISpotifyAuthenticationService _authService;
    private readonly SpotifyApiConfig _config;
    private SpotifyToken? _currentToken;
    private SpotifyPlaybackState? _currentPlaybackState;
    private SpotifyTrack? _currentTrack;

    public event EventHandler<SpotifyPlaybackStateChangedEventArgs>? PlaybackStateChanged;
    public event EventHandler<SpotifyTrackChangedEventArgs>? TrackChanged;
    public event EventHandler<SpotifyAuthenticationEventArgs>? AuthenticationChanged;
    public event EventHandler<SpotifyErrorEventArgs>? ErrorOccurred;

    public SpotifyWebApiService(
        ILogger<SpotifyWebApiService> logger,
        HttpClient httpClient,
        ISpotifyAuthenticationService authService,
        SpotifyApiConfig config)
    {
        _logger = logger;
        _httpClient = httpClient;
        _authService = authService;
        _config = config;

        // Subscribe to authentication events
        _authService.AuthenticationChanged += OnAuthenticationChanged;
    }

    #region Authentication

    /// <summary>
    /// Authenticates with Spotify using OAuth 2.0 PKCE flow
    /// </summary>
    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            _logger.LogInformation("Starting Spotify authentication");

            // Try to load existing token first
            var existingToken = await _authService.LoadStoredTokenAsync();
            if (existingToken != null && existingToken.IsValid)
            {
                _currentToken = existingToken;
                SetAuthorizationHeader();
                AuthenticationChanged?.Invoke(this, new SpotifyAuthenticationEventArgs(true));
                _logger.LogInformation("Using existing valid token");
                return true;
            }

            // Need to authenticate
            var authUrl = await _authService.GetAuthorizationUrlAsync();
            _logger.LogInformation("Please visit the following URL to authenticate: {AuthUrl}", authUrl);

            // In a real implementation, this would open a browser or show a web view
            // For now, we'll return false to indicate manual authentication is needed
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Spotify authentication");
            ErrorOccurred?.Invoke(this, new SpotifyErrorEventArgs("Authentication failed", null, ex));
            return false;
        }
    }

    /// <summary>
    /// Refreshes the access token
    /// </summary>
    public async Task<bool> RefreshTokenAsync()
    {
        try
        {
            if (_currentToken?.RefreshToken == null)
            {
                _logger.LogWarning("No refresh token available");
                return false;
            }

            var refreshedToken = await _authService.RefreshAccessTokenAsync(_currentToken.RefreshToken);
            if (refreshedToken != null)
            {
                _currentToken = refreshedToken;
                SetAuthorizationHeader();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return false;
        }
    }

    /// <summary>
    /// Checks if the service is authenticated
    /// </summary>
    public async Task<bool> IsAuthenticatedAsync()
    {
        if (_currentToken == null)
        {
            return false;
        }

        if (!_currentToken.IsValid)
        {
            return await RefreshTokenAsync();
        }

        return true;
    }

    /// <summary>
    /// Gets the current token
    /// </summary>
    public async Task<SpotifyToken?> GetTokenAsync()
    {
        await Task.CompletedTask;
        return _currentToken;
    }

    /// <summary>
    /// Revokes the current token
    /// </summary>
    public async Task<bool> RevokeTokenAsync()
    {
        try
        {
            if (_currentToken == null)
            {
                return true;
            }

            var success = await _authService.RevokeTokenAsync(_currentToken);
            if (success)
            {
                _currentToken = null;
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking token");
            return false;
        }
    }

    #endregion

    #region User Profile

    /// <summary>
    /// Gets the current user's profile
    /// </summary>
    public async Task<SpotifyUserProfile?> GetUserProfileAsync()
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return null;
            }

            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/me");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var profileData = JsonSerializer.Deserialize<SpotifyUserProfileResponse>(json);

                if (profileData != null)
                {
                    return new SpotifyUserProfile
                    {
                        Id = profileData.Id,
                        DisplayName = profileData.DisplayName,
                        Email = profileData.Email,
                        ImageUrl = profileData.Images?.FirstOrDefault()?.Url ?? string.Empty,
                        Followers = profileData.Followers?.Total ?? 0,
                        Country = profileData.Country,
                        Product = profileData.Product,
                        Images = profileData.Images?.Select(i => i.Url).ToList() ?? new List<string>()
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile");
            return null;
        }
    }

    /// <summary>
    /// Gets the user's available devices
    /// </summary>
    public async Task<IEnumerable<SpotifyDevice>> GetUserDevicesAsync()
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return Enumerable.Empty<SpotifyDevice>();
            }

            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/me/player/devices");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var devicesData = JsonSerializer.Deserialize<SpotifyDevicesResponse>(json);

                if (devicesData?.Devices != null)
                {
                    return devicesData.Devices.Select(d => new SpotifyDevice
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Type = d.Type,
                        VolumePercent = d.VolumePercent,
                        IsActive = d.IsActive,
                        IsPrivateSession = d.IsPrivateSession,
                        IsRestricted = d.IsRestricted
                    });
                }
            }

            return Enumerable.Empty<SpotifyDevice>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user devices");
            return Enumerable.Empty<SpotifyDevice>();
        }
    }

    #endregion

    #region Playback Control

    /// <summary>
    /// Gets the current playback state
    /// </summary>
    public async Task<SpotifyPlaybackState?> GetPlaybackStateAsync()
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return null;
            }

            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/me/player");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var playbackData = JsonSerializer.Deserialize<SpotifyPlaybackStateResponse>(json);

                if (playbackData != null)
                {
                    var playbackState = new SpotifyPlaybackState
                    {
                        IsPlaying = playbackData.IsPlaying,
                        ProgressMs = playbackData.ProgressMs,
                        VolumePercent = playbackData.Device?.VolumePercent ?? 0,
                        ShuffleState = playbackData.ShuffleState,
                        RepeatState = playbackData.RepeatState,
                        Device = playbackData.Device != null ? new SpotifyDevice
                        {
                            Id = playbackData.Device.Id,
                            Name = playbackData.Device.Name,
                            Type = playbackData.Device.Type,
                            VolumePercent = playbackData.Device.VolumePercent,
                            IsActive = playbackData.Device.IsActive,
                            IsPrivateSession = playbackData.Device.IsPrivateSession,
                            IsRestricted = playbackData.Device.IsRestricted
                        } : null,
                        CurrentTrack = playbackData.Item != null ? ConvertToSpotifyTrack(playbackData.Item) : null
                    };

                    // Check if track changed
                    if (_currentTrack?.Id != playbackState.CurrentTrack?.Id)
                    {
                        var previousTrack = _currentTrack;
                        _currentTrack = playbackState.CurrentTrack;
                        TrackChanged?.Invoke(this, new SpotifyTrackChangedEventArgs(previousTrack, _currentTrack));
                    }

                    // Check if playback state changed
                    if (_currentPlaybackState?.IsPlaying != playbackState.IsPlaying ||
                        _currentPlaybackState?.ProgressMs != playbackState.ProgressMs)
                    {
                        var previousState = _currentPlaybackState;
                        _currentPlaybackState = playbackState;
                        PlaybackStateChanged?.Invoke(this, new SpotifyPlaybackStateChangedEventArgs(previousState, playbackState));
                    }

                    return playbackState;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting playback state");
            return null;
        }
    }

    /// <summary>
    /// Plays a specific track
    /// </summary>
    public async Task<bool> PlayTrackAsync(string trackId)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return false;
            }

            var requestBody = new { uris = new[] { $"spotify:track:{trackId}" } };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"{_config.BaseUrl}/me/player/play", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing track: {TrackId}", trackId);
            return false;
        }
    }

    /// <summary>
    /// Plays a playlist
    /// </summary>
    public async Task<bool> PlayPlaylistAsync(string playlistId)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return false;
            }

            var requestBody = new { context_uri = $"spotify:playlist:{playlistId}" };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"{_config.BaseUrl}/me/player/play", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing playlist: {PlaylistId}", playlistId);
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
            if (!await IsAuthenticatedAsync())
            {
                return false;
            }

            var response = await _httpClient.PutAsync($"{_config.BaseUrl}/me/player/pause", null);
            return response.IsSuccessStatusCode;
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
            if (!await IsAuthenticatedAsync())
            {
                return false;
            }

            var response = await _httpClient.PutAsync($"{_config.BaseUrl}/me/player/play", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming playback");
            return false;
        }
    }

    /// <summary>
    /// Skips to next track
    /// </summary>
    public async Task<bool> SkipToNextAsync()
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return false;
            }

            var response = await _httpClient.PostAsync($"{_config.BaseUrl}/me/player/next", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error skipping to next track");
            return false;
        }
    }

    /// <summary>
    /// Skips to previous track
    /// </summary>
    public async Task<bool> SkipToPreviousAsync()
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return false;
            }

            var response = await _httpClient.PostAsync($"{_config.BaseUrl}/me/player/previous", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error skipping to previous track");
            return false;
        }
    }

    /// <summary>
    /// Seeks to a specific position
    /// </summary>
    public async Task<bool> SeekToPositionAsync(int positionMs)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return false;
            }

            var response = await _httpClient.PutAsync($"{_config.BaseUrl}/me/player/seek?position_ms={positionMs}", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeking to position: {PositionMs}", positionMs);
            return false;
        }
    }

    /// <summary>
    /// Sets the volume
    /// </summary>
    public async Task<bool> SetVolumeAsync(int volumePercent)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return false;
            }

            var response = await _httpClient.PutAsync($"{_config.BaseUrl}/me/player/volume?volume_percent={volumePercent}", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting volume: {VolumePercent}", volumePercent);
            return false;
        }
    }

    /// <summary>
    /// Sets the repeat mode
    /// </summary>
    public async Task<bool> SetRepeatModeAsync(string state)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return false;
            }

            var response = await _httpClient.PutAsync($"{_config.BaseUrl}/me/player/repeat?state={state}", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting repeat mode: {State}", state);
            return false;
        }
    }

    /// <summary>
    /// Sets the shuffle mode
    /// </summary>
    public async Task<bool> SetShuffleModeAsync(bool enabled)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return false;
            }

            var response = await _httpClient.PutAsync($"{_config.BaseUrl}/me/player/shuffle?state={enabled.ToString().ToLower()}", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting shuffle mode: {Enabled}", enabled);
            return false;
        }
    }

    #endregion

    #region Track Information

    /// <summary>
    /// Gets track information by ID
    /// </summary>
    public async Task<SpotifyTrack?> GetTrackAsync(string trackId)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return null;
            }

            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/tracks/{trackId}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var trackData = JsonSerializer.Deserialize<SpotifyTrackResponse>(json);

                if (trackData != null)
                {
                    return ConvertToSpotifyTrack(trackData);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting track: {TrackId}", trackId);
            return null;
        }
    }

    /// <summary>
    /// Gets audio features for a track
    /// </summary>
    public async Task<SpotifyAudioFeatures?> GetTrackAudioFeaturesAsync(string trackId)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return null;
            }

            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/audio-features/{trackId}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var featuresData = JsonSerializer.Deserialize<SpotifyAudioFeaturesResponse>(json);

                if (featuresData != null)
                {
                    return new SpotifyAudioFeatures
                    {
                        TrackId = featuresData.Id,
                        Danceability = featuresData.Danceability,
                        Energy = featuresData.Energy,
                        Key = featuresData.Key,
                        Loudness = featuresData.Loudness,
                        Mode = featuresData.Mode,
                        Speechiness = featuresData.Speechiness,
                        Acousticness = featuresData.Acousticness,
                        Instrumentalness = featuresData.Instrumentalness,
                        Liveness = featuresData.Liveness,
                        Valence = featuresData.Valence,
                        Tempo = featuresData.Tempo,
                        DurationMs = featuresData.DurationMs,
                        TimeSignature = featuresData.TimeSignature
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting track audio features: {TrackId}", trackId);
            return null;
        }
    }

    /// <summary>
    /// Gets recently played tracks
    /// </summary>
    public async Task<IEnumerable<SpotifyTrack>> GetRecentlyPlayedTracksAsync(int limit = 20)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return Enumerable.Empty<SpotifyTrack>();
            }

            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/me/player/recently-played?limit={limit}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var tracksData = JsonSerializer.Deserialize<SpotifyRecentlyPlayedResponse>(json);

                if (tracksData?.Items != null)
                {
                    return tracksData.Items.Select(item => ConvertToSpotifyTrack(item.Track));
                }
            }

            return Enumerable.Empty<SpotifyTrack>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recently played tracks");
            return Enumerable.Empty<SpotifyTrack>();
        }
    }

    /// <summary>
    /// Gets top tracks
    /// </summary>
    public async Task<IEnumerable<SpotifyTrack>> GetTopTracksAsync(string timeRange = "medium_term", int limit = 20)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return Enumerable.Empty<SpotifyTrack>();
            }

            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/me/top/tracks?time_range={timeRange}&limit={limit}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var tracksData = JsonSerializer.Deserialize<SpotifyTopTracksResponse>(json);

                if (tracksData?.Items != null)
                {
                    return tracksData.Items.Select(ConvertToSpotifyTrack);
                }
            }

            return Enumerable.Empty<SpotifyTrack>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top tracks");
            return Enumerable.Empty<SpotifyTrack>();
        }
    }

    #endregion

    #region Search

    /// <summary>
    /// Searches for tracks, artists, albums, and playlists
    /// </summary>
    public async Task<SpotifySearchResults?> SearchAsync(string query, string types = "track,artist,album,playlist", int limit = 20)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return null;
            }

            var encodedQuery = HttpUtility.UrlEncode(query);
            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/search?q={encodedQuery}&type={types}&limit={limit}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var searchData = JsonSerializer.Deserialize<SpotifySearchResponse>(json);

                if (searchData != null)
                {
                    return new SpotifySearchResults
                    {
                        Tracks = searchData.Tracks?.Items?.Select(ConvertToSpotifyTrack).ToList() ?? new List<SpotifyTrack>(),
                        Artists = searchData.Artists?.Items?.Select(ConvertToSpotifyArtist).ToList() ?? new List<SpotifyArtist>(),
                        Albums = searchData.Albums?.Items?.Select(ConvertToSpotifyAlbum).ToList() ?? new List<SpotifyAlbum>(),
                        Playlists = searchData.Playlists?.Items?.Select(ConvertToSpotifyPlaylist).ToList() ?? new List<SpotifyPlaylist>()
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching: {Query}", query);
            return null;
        }
    }

    /// <summary>
    /// Searches for tracks
    /// </summary>
    public async Task<IEnumerable<SpotifyTrack>> SearchTracksAsync(string query, int limit = 20)
    {
        var results = await SearchAsync(query, "track", limit);
        return results?.Tracks ?? Enumerable.Empty<SpotifyTrack>();
    }

    /// <summary>
    /// Searches for artists
    /// </summary>
    public async Task<IEnumerable<SpotifyArtist>> SearchArtistsAsync(string query, int limit = 20)
    {
        var results = await SearchAsync(query, "artist", limit);
        return results?.Artists ?? Enumerable.Empty<SpotifyArtist>();
    }

    /// <summary>
    /// Searches for albums
    /// </summary>
    public async Task<IEnumerable<SpotifyAlbum>> SearchAlbumsAsync(string query, int limit = 20)
    {
        var results = await SearchAsync(query, "album", limit);
        return results?.Albums ?? Enumerable.Empty<SpotifyAlbum>();
    }

    /// <summary>
    /// Searches for playlists
    /// </summary>
    public async Task<IEnumerable<SpotifyPlaylist>> SearchPlaylistsAsync(string query, int limit = 20)
    {
        var results = await SearchAsync(query, "playlist", limit);
        return results?.Playlists ?? Enumerable.Empty<SpotifyPlaylist>();
    }

    #endregion

    #region Playlists

    /// <summary>
    /// Gets user's playlists
    /// </summary>
    public async Task<IEnumerable<SpotifyPlaylist>> GetUserPlaylistsAsync(int limit = 50)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return Enumerable.Empty<SpotifyPlaylist>();
            }

            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/me/playlists?limit={limit}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var playlistsData = JsonSerializer.Deserialize<SpotifyPlaylistsResponse>(json);

                if (playlistsData?.Items != null)
                {
                    return playlistsData.Items.Select(ConvertToSpotifyPlaylist);
                }
            }

            return Enumerable.Empty<SpotifyPlaylist>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user playlists");
            return Enumerable.Empty<SpotifyPlaylist>();
        }
    }

    /// <summary>
    /// Gets a specific playlist
    /// </summary>
    public async Task<SpotifyPlaylist?> GetPlaylistAsync(string playlistId)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return null;
            }

            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/playlists/{playlistId}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var playlistData = JsonSerializer.Deserialize<SpotifyPlaylistResponse>(json);

                if (playlistData != null)
                {
                    return ConvertToSpotifyPlaylist(playlistData);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting playlist: {PlaylistId}", playlistId);
            return null;
        }
    }

    /// <summary>
    /// Gets tracks in a playlist
    /// </summary>
    public async Task<IEnumerable<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId, int limit = 100)
    {
        try
        {
            if (!await IsAuthenticatedAsync())
            {
                return Enumerable.Empty<SpotifyTrack>();
            }

            var response = await _httpClient.GetAsync($"{_config.BaseUrl}/playlists/{playlistId}/tracks?limit={limit}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var tracksData = JsonSerializer.Deserialize<SpotifyPlaylistTracksResponse>(json);

                if (tracksData?.Items != null)
                {
                    return tracksData.Items.Where(item => item.Track != null).Select(item => ConvertToSpotifyTrack(item.Track!));
                }
            }

            return Enumerable.Empty<SpotifyTrack>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting playlist tracks: {PlaylistId}", playlistId);
            return Enumerable.Empty<SpotifyTrack>();
        }
    }

    #endregion

    #region Private Methods

    private void SetAuthorizationHeader()
    {
        if (_currentToken != null)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _currentToken.AccessToken);
        }
    }

    private void OnAuthenticationChanged(object? sender, SpotifyAuthenticationEventArgs e)
    {
        AuthenticationChanged?.Invoke(this, e);
    }

    private SpotifyTrack ConvertToSpotifyTrack(SpotifyTrackResponse trackData)
    {
        return new SpotifyTrack
        {
            Id = trackData.Id,
            Name = trackData.Name,
            Artist = trackData.Artists?.FirstOrDefault()?.Name ?? string.Empty,
            Album = trackData.Album?.Name ?? string.Empty,
            ImageUrl = trackData.Album?.Images?.FirstOrDefault()?.Url ?? string.Empty,
            DurationMs = trackData.DurationMs,
            PreviewUrl = trackData.PreviewUrl ?? string.Empty,
            Popularity = trackData.Popularity,
            Artists = trackData.Artists?.Select(a => a.Name).ToList() ?? new List<string>(),
            AlbumId = trackData.Album?.Id ?? string.Empty,
            AlbumImageUrl = trackData.Album?.Images?.FirstOrDefault()?.Url ?? string.Empty,
            ExternalUrl = trackData.ExternalUrls?.Spotify ?? string.Empty
        };
    }

    private SpotifyArtist ConvertToSpotifyArtist(SpotifyArtistResponse artistData)
    {
        return new SpotifyArtist
        {
            Id = artistData.Id,
            Name = artistData.Name,
            ImageUrl = artistData.Images?.FirstOrDefault()?.Url ?? string.Empty,
            Popularity = artistData.Popularity,
            Genres = artistData.Genres ?? new List<string>(),
            Followers = artistData.Followers?.Total ?? 0,
            ExternalUrl = artistData.ExternalUrls?.Spotify ?? string.Empty
        };
    }

    private SpotifyAlbum ConvertToSpotifyAlbum(SpotifyAlbumResponse albumData)
    {
        return new SpotifyAlbum
        {
            Id = albumData.Id,
            Name = albumData.Name,
            Artist = albumData.Artists?.FirstOrDefault()?.Name ?? string.Empty,
            ImageUrl = albumData.Images?.FirstOrDefault()?.Url ?? string.Empty,
            ReleaseDate = DateTime.TryParse(albumData.ReleaseDate, out var releaseDate) ? releaseDate : DateTime.MinValue,
            TotalTracks = albumData.TotalTracks,
            AlbumType = albumData.AlbumType,
            Genres = albumData.Genres ?? new List<string>(),
            ExternalUrl = albumData.ExternalUrls?.Spotify ?? string.Empty
        };
    }

    private SpotifyPlaylist ConvertToSpotifyPlaylist(SpotifyPlaylistResponse playlistData)
    {
        return new SpotifyPlaylist
        {
            Id = playlistData.Id,
            Name = playlistData.Name,
            Description = playlistData.Description ?? string.Empty,
            ImageUrl = playlistData.Images?.FirstOrDefault()?.Url ?? string.Empty,
            Owner = playlistData.Owner?.DisplayName ?? string.Empty,
            TotalTracks = playlistData.Tracks?.Total ?? 0,
            IsPublic = playlistData.Public,
            IsCollaborative = playlistData.Collaborative,
            ExternalUrl = playlistData.ExternalUrls?.Spotify ?? string.Empty
        };
    }

    #endregion
}

// Response models for JSON deserialization
internal class SpotifyUserProfileResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("followers")]
    public SpotifyFollowersResponse? Followers { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("product")]
    public string Product { get; set; } = string.Empty;

    [JsonPropertyName("images")]
    public List<SpotifyImageResponse>? Images { get; set; }
}

internal class SpotifyFollowersResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

internal class SpotifyImageResponse
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

internal class SpotifyDevicesResponse
{
    [JsonPropertyName("devices")]
    public List<SpotifyDeviceResponse>? Devices { get; set; }
}

internal class SpotifyDeviceResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("volume_percent")]
    public int VolumePercent { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_private_session")]
    public bool IsPrivateSession { get; set; }

    [JsonPropertyName("is_restricted")]
    public bool IsRestricted { get; set; }
}

internal class SpotifyPlaybackStateResponse
{
    [JsonPropertyName("is_playing")]
    public bool IsPlaying { get; set; }

    [JsonPropertyName("progress_ms")]
    public int ProgressMs { get; set; }

    [JsonPropertyName("shuffle_state")]
    public bool ShuffleState { get; set; }

    [JsonPropertyName("repeat_state")]
    public string RepeatState { get; set; } = string.Empty;

    [JsonPropertyName("device")]
    public SpotifyDeviceResponse? Device { get; set; }

    [JsonPropertyName("item")]
    public SpotifyTrackResponse? Item { get; set; }
}

internal class SpotifyTrackResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("artists")]
    public List<SpotifyArtistResponse>? Artists { get; set; }

    [JsonPropertyName("album")]
    public SpotifyAlbumResponse? Album { get; set; }

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; set; }

    [JsonPropertyName("preview_url")]
    public string? PreviewUrl { get; set; }

    [JsonPropertyName("popularity")]
    public int Popularity { get; set; }

    [JsonPropertyName("external_urls")]
    public SpotifyExternalUrlsResponse? ExternalUrls { get; set; }
}

internal class SpotifyArtistResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("images")]
    public List<SpotifyImageResponse>? Images { get; set; }

    [JsonPropertyName("popularity")]
    public int Popularity { get; set; }

    [JsonPropertyName("genres")]
    public List<string>? Genres { get; set; }

    [JsonPropertyName("followers")]
    public SpotifyFollowersResponse? Followers { get; set; }

    [JsonPropertyName("external_urls")]
    public SpotifyExternalUrlsResponse? ExternalUrls { get; set; }
}

internal class SpotifyAlbumResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("artists")]
    public List<SpotifyArtistResponse>? Artists { get; set; }

    [JsonPropertyName("images")]
    public List<SpotifyImageResponse>? Images { get; set; }

    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; set; } = string.Empty;

    [JsonPropertyName("total_tracks")]
    public int TotalTracks { get; set; }

    [JsonPropertyName("album_type")]
    public string AlbumType { get; set; } = string.Empty;

    [JsonPropertyName("genres")]
    public List<string>? Genres { get; set; }

    [JsonPropertyName("external_urls")]
    public SpotifyExternalUrlsResponse? ExternalUrls { get; set; }
}

internal class SpotifyPlaylistResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("images")]
    public List<SpotifyImageResponse>? Images { get; set; }

    [JsonPropertyName("owner")]
    public SpotifyOwnerResponse? Owner { get; set; }

    [JsonPropertyName("tracks")]
    public SpotifyPlaylistTracksInfoResponse? Tracks { get; set; }

    [JsonPropertyName("public")]
    public bool Public { get; set; }

    [JsonPropertyName("collaborative")]
    public bool Collaborative { get; set; }

    [JsonPropertyName("external_urls")]
    public SpotifyExternalUrlsResponse? ExternalUrls { get; set; }
}

internal class SpotifyOwnerResponse
{
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;
}

internal class SpotifyPlaylistTracksInfoResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

internal class SpotifyPlaylistTracksResponse
{
    [JsonPropertyName("items")]
    public List<SpotifyPlaylistTrackItemResponse>? Items { get; set; }
}

internal class SpotifyPlaylistTrackItemResponse
{
    [JsonPropertyName("track")]
    public SpotifyTrackResponse? Track { get; set; }
}

internal class SpotifyAudioFeaturesResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("danceability")]
    public float Danceability { get; set; }

    [JsonPropertyName("energy")]
    public float Energy { get; set; }

    [JsonPropertyName("key")]
    public int Key { get; set; }

    [JsonPropertyName("loudness")]
    public float Loudness { get; set; }

    [JsonPropertyName("mode")]
    public int Mode { get; set; }

    [JsonPropertyName("speechiness")]
    public float Speechiness { get; set; }

    [JsonPropertyName("acousticness")]
    public float Acousticness { get; set; }

    [JsonPropertyName("instrumentalness")]
    public float Instrumentalness { get; set; }

    [JsonPropertyName("liveness")]
    public float Liveness { get; set; }

    [JsonPropertyName("valence")]
    public float Valence { get; set; }

    [JsonPropertyName("tempo")]
    public float Tempo { get; set; }

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; set; }

    [JsonPropertyName("time_signature")]
    public int TimeSignature { get; set; }
}

internal class SpotifyRecentlyPlayedResponse
{
    [JsonPropertyName("items")]
    public List<SpotifyRecentlyPlayedItemResponse>? Items { get; set; }
}

internal class SpotifyRecentlyPlayedItemResponse
{
    [JsonPropertyName("track")]
    public SpotifyTrackResponse Track { get; set; } = new();
}

internal class SpotifyTopTracksResponse
{
    [JsonPropertyName("items")]
    public List<SpotifyTrackResponse>? Items { get; set; }
}

internal class SpotifySearchResponse
{
    [JsonPropertyName("tracks")]
    public SpotifySearchTracksResponse? Tracks { get; set; }

    [JsonPropertyName("artists")]
    public SpotifySearchArtistsResponse? Artists { get; set; }

    [JsonPropertyName("albums")]
    public SpotifySearchAlbumsResponse? Albums { get; set; }

    [JsonPropertyName("playlists")]
    public SpotifySearchPlaylistsResponse? Playlists { get; set; }
}

internal class SpotifySearchTracksResponse
{
    [JsonPropertyName("items")]
    public List<SpotifyTrackResponse>? Items { get; set; }
}

internal class SpotifySearchArtistsResponse
{
    [JsonPropertyName("items")]
    public List<SpotifyArtistResponse>? Items { get; set; }
}

internal class SpotifySearchAlbumsResponse
{
    [JsonPropertyName("items")]
    public List<SpotifyAlbumResponse>? Items { get; set; }
}

internal class SpotifySearchPlaylistsResponse
{
    [JsonPropertyName("items")]
    public List<SpotifyPlaylistResponse>? Items { get; set; }
}

internal class SpotifyPlaylistsResponse
{
    [JsonPropertyName("items")]
    public List<SpotifyPlaylistResponse>? Items { get; set; }
}

internal class SpotifyExternalUrlsResponse
{
    [JsonPropertyName("spotify")]
    public string Spotify { get; set; } = string.Empty;
}
