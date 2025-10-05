using PartyLights.Core.Models;

namespace PartyLights.Core.Interfaces;

/// <summary>
/// Service for managing smart devices
/// </summary>
public interface IDeviceManagerService
{
    Task<IEnumerable<SmartDevice>> DiscoverDevicesAsync();
    Task<bool> ConnectToDeviceAsync(SmartDevice device);
    Task<bool> DisconnectFromDeviceAsync(SmartDevice device);
    Task<bool> UpdateDeviceStateAsync(SmartDevice device);
    Task<IEnumerable<SmartDevice>> GetConnectedDevicesAsync();
    event EventHandler<DeviceEventArgs>? DeviceConnected;
    event EventHandler<DeviceEventArgs>? DeviceDisconnected;
    event EventHandler<DeviceEventArgs>? DeviceStateChanged;
}

/// <summary>
/// Service for audio capture and analysis
/// </summary>
public interface IAudioCaptureService
{
    Task<bool> StartCaptureAsync();
    Task StopCaptureAsync();
    bool IsCapturing { get; }
    AudioAnalysis? CurrentAnalysis { get; }
    event EventHandler<AudioAnalysisEventArgs>? AnalysisUpdated;
}

/// <summary>
/// Service for Spotify integration
/// </summary>
public interface ISpotifyService
{
    Task<bool> AuthenticateAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<SpotifyTrack?> GetCurrentTrackAsync();
    Task<bool> StartListeningAsync();
    Task StopListeningAsync();
    bool IsListening { get; }
    event EventHandler<SpotifyTrackEventArgs>? TrackChanged;
}

/// <summary>
/// Interface for comprehensive Spotify Web API service
/// </summary>
public interface ISpotifyWebApiService
{
    // Authentication
    Task<bool> AuthenticateAsync();
    Task<bool> RefreshTokenAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<SpotifyToken?> GetTokenAsync();
    Task<bool> RevokeTokenAsync();

    // User Profile
    Task<SpotifyUserProfile?> GetUserProfileAsync();
    Task<IEnumerable<SpotifyDevice>> GetUserDevicesAsync();

    // Playback Control
    Task<SpotifyPlaybackState?> GetPlaybackStateAsync();
    Task<bool> PlayTrackAsync(string trackId);
    Task<bool> PlayPlaylistAsync(string playlistId);
    Task<bool> PausePlaybackAsync();
    Task<bool> ResumePlaybackAsync();
    Task<bool> SkipToNextAsync();
    Task<bool> SkipToPreviousAsync();
    Task<bool> SeekToPositionAsync(int positionMs);
    Task<bool> SetVolumeAsync(int volumePercent);
    Task<bool> SetRepeatModeAsync(string state);
    Task<bool> SetShuffleModeAsync(bool enabled);

    // Track Information
    Task<SpotifyTrack?> GetTrackAsync(string trackId);
    Task<SpotifyAudioFeatures?> GetTrackAudioFeaturesAsync(string trackId);
    Task<IEnumerable<SpotifyTrack>> GetRecentlyPlayedTracksAsync(int limit = 20);
    Task<IEnumerable<SpotifyTrack>> GetTopTracksAsync(string timeRange = "medium_term", int limit = 20);

    // Search
    Task<SpotifySearchResults?> SearchAsync(string query, string types = "track,artist,album,playlist", int limit = 20);
    Task<IEnumerable<SpotifyTrack>> SearchTracksAsync(string query, int limit = 20);
    Task<IEnumerable<SpotifyArtist>> SearchArtistsAsync(string query, int limit = 20);
    Task<IEnumerable<SpotifyAlbum>> SearchAlbumsAsync(string query, int limit = 20);
    Task<IEnumerable<SpotifyPlaylist>> SearchPlaylistsAsync(string query, int limit = 20);

    // Playlists
    Task<IEnumerable<SpotifyPlaylist>> GetUserPlaylistsAsync(int limit = 50);
    Task<SpotifyPlaylist?> GetPlaylistAsync(string playlistId);
    Task<IEnumerable<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId, int limit = 100);

    // Events
    event EventHandler<SpotifyPlaybackStateChangedEventArgs>? PlaybackStateChanged;
    event EventHandler<SpotifyTrackChangedEventArgs>? TrackChanged;
    event EventHandler<SpotifyAuthenticationEventArgs>? AuthenticationChanged;
    event EventHandler<SpotifyErrorEventArgs>? ErrorOccurred;
}

/// <summary>
/// Interface for Spotify authentication service
/// </summary>
public interface ISpotifyAuthenticationService
{
    Task<string> GetAuthorizationUrlAsync();
    Task<SpotifyToken?> ExchangeCodeForTokenAsync(string code);
    Task<SpotifyToken?> RefreshAccessTokenAsync(string refreshToken);
    Task<bool> ValidateTokenAsync(SpotifyToken token);
    Task<bool> RevokeTokenAsync(SpotifyToken token);
    Task<SpotifyToken?> LoadStoredTokenAsync();
    Task<bool> StoreTokenAsync(SpotifyToken token);
    Task<bool> ClearStoredTokenAsync();
}

/// <summary>
/// Interface for Spotify WebSocket service for real-time updates
/// </summary>
public interface ISpotifyWebSocketService
{
    Task<bool> ConnectAsync();
    Task<bool> DisconnectAsync();
    bool IsConnected { get; }
    Task<bool> SubscribeToPlaybackUpdatesAsync();
    Task<bool> UnsubscribeFromPlaybackUpdatesAsync();

    event EventHandler<SpotifyPlaybackStateChangedEventArgs>? PlaybackStateChanged;
    event EventHandler<SpotifyTrackChangedEventArgs>? TrackChanged;
    event EventHandler<SpotifyDeviceChangedEventArgs>? DeviceChanged;
    event EventHandler<SpotifyConnectionEventArgs>? ConnectionChanged;
}

/// <summary>
/// Interface for Spotify audio analysis service
/// </summary>
public interface ISpotifyAudioAnalysisService
{
    Task<SpotifyAudioFeatures?> AnalyzeTrackAsync(string trackId);
    Task<Dictionary<string, float>> GetTrackMoodAsync(string trackId);
    Task<string> GetTrackGenreAsync(string trackId);
    Task<float> GetTrackEnergyLevelAsync(string trackId);
    Task<float> GetTrackDanceabilityAsync(string trackId);
    Task<bool> IsTrackSuitableForLightingAsync(string trackId);
    Task<IEnumerable<string>> GetRecommendedPresetsAsync(string trackId);
}

/// <summary>
/// Service for managing lighting effects
/// </summary>
public interface ILightingEffectService
{
    Task<bool> ApplyPresetAsync(LightingPreset preset);
    Task<bool> StopEffectsAsync();
    Task<IEnumerable<LightingPreset>> GetAvailablePresetsAsync();
    Task<bool> CreatePresetAsync(LightingPreset preset);
    Task<bool> UpdatePresetAsync(LightingPreset preset);
    Task<bool> DeletePresetAsync(string presetId);
    LightingPreset? ActivePreset { get; }
    bool IsEffectActive { get; }
}

/// <summary>
/// Service for device-specific control
/// </summary>
public interface IDeviceController
{
    DeviceType DeviceType { get; }
    Task<bool> ConnectAsync(string ipAddress);
    Task<bool> DisconnectAsync();
    Task<bool> SetColorAsync(int r, int g, int b);
    Task<bool> SetBrightnessAsync(int brightness);
    Task<bool> SetEffectAsync(string effectName);
    Task<bool> TurnOnAsync();
    Task<bool> TurnOffAsync();
    bool IsConnected { get; }
}

/// <summary>
/// Service for configuration management
/// </summary>
public interface IConfigurationService
{
    Task<AppConfiguration> LoadConfigurationAsync();
    Task SaveConfigurationAsync(AppConfiguration configuration);
    Task<bool> ExportConfigurationAsync(string filePath);
    Task<AppConfiguration> ImportConfigurationAsync(string filePath);
}

/// <summary>
/// Interface for advanced device control service
/// </summary>
public interface IAdvancedDeviceControlService
{
    Task<bool> SetColorToAllDevicesAsync(int r, int g, int b);
    Task<bool> SetBrightnessToAllDevicesAsync(int brightness);
    Task<bool> SetEffectToAllDevicesAsync(string effectName);
    Task<bool> TurnOnAllDevicesAsync();
    Task<bool> TurnOffAllDevicesAsync();

    Task<bool> SetColorToDeviceGroupAsync(string groupName, int r, int g, int b);
    Task<bool> SetBrightnessToDeviceGroupAsync(string groupName, int brightness);
    Task<bool> SetEffectToDeviceGroupAsync(string groupName, string effectName);
    Task<bool> TurnOnDeviceGroupAsync(string groupName);
    Task<bool> TurnOffDeviceGroupAsync(string groupName);

    Task<bool> SetColorToDeviceTypeAsync(DeviceType deviceType, int r, int g, int b);
    Task<bool> SetBrightnessToDeviceTypeAsync(DeviceType deviceType, int brightness);
    Task<bool> SetEffectToDeviceTypeAsync(DeviceType deviceType, string effectName);
    Task<bool> TurnOnDeviceTypeAsync(DeviceType deviceType);
    Task<bool> TurnOffDeviceTypeAsync(DeviceType deviceType);

    Task<bool> CreateDeviceGroupAsync(string groupName, IEnumerable<SmartDevice> devices);
    Task<bool> DeleteDeviceGroupAsync(string groupName);
    Task<IEnumerable<DeviceGroup>> GetDeviceGroupsAsync();
    Task<bool> AddDeviceToGroupAsync(string groupName, SmartDevice device);
    Task<bool> RemoveDeviceFromGroupAsync(string groupName, SmartDevice device);

    Task<bool> SaveDeviceConfigurationAsync(SmartDevice device);
    Task<bool> LoadDeviceConfigurationAsync(SmartDevice device);
    Task<IEnumerable<DeviceConfiguration>> GetSavedConfigurationsAsync();
}

/// <summary>
/// Interface for device synchronization service
/// </summary>
public interface IDeviceSynchronizationService
{
    Task<bool> SynchronizeAllDevicesAsync();
    Task<bool> SynchronizeDeviceGroupAsync(string groupName);
    Task<bool> SynchronizeDeviceTypeAsync(DeviceType deviceType);
    Task<bool> StartRealTimeSyncAsync();
    Task<bool> StopRealTimeSyncAsync();
    bool IsRealTimeSyncActive { get; }

    event EventHandler<SynchronizationEventArgs>? SynchronizationStarted;
    event EventHandler<SynchronizationEventArgs>? SynchronizationStopped;
    event EventHandler<SynchronizationEventArgs>? SynchronizationError;
}

/// <summary>
/// Enhanced device controller interface with advanced features
/// </summary>
public interface IAdvancedDeviceController : IDeviceController
{
    Task<bool> SetColorTemperatureAsync(int temperature);
    Task<bool> SetSaturationAsync(int saturation);
    Task<bool> SetHueAsync(int hue);
    Task<bool> SetTransitionTimeAsync(int milliseconds);
    Task<bool> SetPowerStateAsync(bool isOn);
    Task<DeviceState?> GetDeviceStateAsync();
    Task<bool> SetSceneAsync(string sceneName);
    Task<IEnumerable<string>> GetAvailableScenesAsync();
    Task<bool> SetGroupAsync(string groupName);
    Task<IEnumerable<string>> GetAvailableGroupsAsync();
    Task<bool> SetBrightnessWithTransitionAsync(int brightness, int transitionTime);
    Task<bool> SetColorWithTransitionAsync(int r, int g, int b, int transitionTime);
    Task<bool> SetEffectWithTransitionAsync(string effectName, int transitionTime);
}

/// <summary>
/// Event arguments for device events
/// </summary>
public class DeviceEventArgs : EventArgs
{
    public SmartDevice Device { get; }
    public DeviceEventArgs(SmartDevice device) => Device = device;
}

/// <summary>
/// Event arguments for audio analysis events
/// </summary>
public class AudioAnalysisEventArgs : EventArgs
{
    public AudioAnalysis Analysis { get; }
    public AudioAnalysisEventArgs(AudioAnalysis analysis) => Analysis = analysis;
}

/// <summary>
/// Event arguments for Spotify track events
/// </summary>
public class SpotifyTrackEventArgs : EventArgs
{
    public SpotifyTrack? Track { get; }
    public SpotifyTrackEventArgs(SpotifyTrack? track) => Track = track;
}

/// <summary>
/// Event arguments for synchronization events
/// </summary>
public class SynchronizationEventArgs : EventArgs
{
    public string? GroupName { get; }
    public DeviceType? DeviceType { get; }
    public string? ErrorMessage { get; }
    public DateTime Timestamp { get; }

    public SynchronizationEventArgs(string? groupName = null, DeviceType? deviceType = null, string? errorMessage = null)
    {
        GroupName = groupName;
        DeviceType = deviceType;
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for Spotify playback state changes
/// </summary>
public class SpotifyPlaybackStateChangedEventArgs : EventArgs
{
    public SpotifyPlaybackState? PreviousState { get; }
    public SpotifyPlaybackState? CurrentState { get; }
    public DateTime Timestamp { get; }

    public SpotifyPlaybackStateChangedEventArgs(SpotifyPlaybackState? previousState, SpotifyPlaybackState? currentState)
    {
        PreviousState = previousState;
        CurrentState = currentState;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for Spotify track changes
/// </summary>
public class SpotifyTrackChangedEventArgs : EventArgs
{
    public SpotifyTrack? PreviousTrack { get; }
    public SpotifyTrack? CurrentTrack { get; }
    public DateTime Timestamp { get; }

    public SpotifyTrackChangedEventArgs(SpotifyTrack? previousTrack, SpotifyTrack? currentTrack)
    {
        PreviousTrack = previousTrack;
        CurrentTrack = currentTrack;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for Spotify authentication events
/// </summary>
public class SpotifyAuthenticationEventArgs : EventArgs
{
    public bool IsAuthenticated { get; }
    public SpotifyUserProfile? UserProfile { get; }
    public string? ErrorMessage { get; }
    public DateTime Timestamp { get; }

    public SpotifyAuthenticationEventArgs(bool isAuthenticated, SpotifyUserProfile? userProfile = null, string? errorMessage = null)
    {
        IsAuthenticated = isAuthenticated;
        UserProfile = userProfile;
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for Spotify device changes
/// </summary>
public class SpotifyDeviceChangedEventArgs : EventArgs
{
    public SpotifyDevice? PreviousDevice { get; }
    public SpotifyDevice? CurrentDevice { get; }
    public DateTime Timestamp { get; }

    public SpotifyDeviceChangedEventArgs(SpotifyDevice? previousDevice, SpotifyDevice? currentDevice)
    {
        PreviousDevice = previousDevice;
        CurrentDevice = currentDevice;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for Spotify connection events
/// </summary>
public class SpotifyConnectionEventArgs : EventArgs
{
    public bool IsConnected { get; }
    public string? ErrorMessage { get; }
    public DateTime Timestamp { get; }

    public SpotifyConnectionEventArgs(bool isConnected, string? errorMessage = null)
    {
        IsConnected = isConnected;
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for Spotify error events
/// </summary>
public class SpotifyErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; }
    public string? ErrorCode { get; }
    public Exception? Exception { get; }
    public DateTime Timestamp { get; }

    public SpotifyErrorEventArgs(string errorMessage, string? errorCode = null, Exception? exception = null)
    {
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
        Exception = exception;
        Timestamp = DateTime.UtcNow;
    }
}
