using System.Text.Json.Serialization;

namespace PartyLights.Core.Models;

/// <summary>
/// Represents a smart lighting device
/// </summary>
public class SmartDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DeviceType Type { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DeviceCapabilities Capabilities { get; set; } = new();
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Types of supported smart devices
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeviceType
{
    PhilipsHue,
    TpLink,
    MagicHome
}

/// <summary>
/// Device capabilities for lighting control
/// </summary>
public class DeviceCapabilities
{
    public bool SupportsColor { get; set; }
    public bool SupportsBrightness { get; set; }
    public bool SupportsTemperature { get; set; }
    public bool SupportsEffects { get; set; }
    public int MaxBrightness { get; set; } = 255;
    public int MinBrightness { get; set; } = 0;
}

/// <summary>
/// Represents a lighting effect preset
/// </summary>
public class LightingPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PresetType Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public List<string> DeviceIds { get; set; } = new();
    public List<string> DeviceGroupIds { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public bool IsBuiltIn { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public string Author { get; set; } = "User";
    public string Version { get; set; } = "1.0.0";
    public List<string> Tags { get; set; } = new();
    public PresetMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Preset metadata for additional information
/// </summary>
public class PresetMetadata
{
    public string Category { get; set; } = "General";
    public int Difficulty { get; set; } = 1; // 1-5 scale
    public int EnergyLevel { get; set; } = 3; // 1-5 scale
    public List<string> CompatibleGenres { get; set; } = new();
    public List<string> CompatibleMoods { get; set; } = new();
    public int EstimatedBPM { get; set; } = 0; // 0 = any
    public string ColorScheme { get; set; } = "Dynamic";
    public bool RequiresBeatDetection { get; set; } = false;
    public bool RequiresFrequencyAnalysis { get; set; } = false;
    public bool RequiresMoodDetection { get; set; } = false;
    public string PreviewImagePath { get; set; } = string.Empty;
}

/// <summary>
/// Types of lighting effect presets
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PresetType
{
    BeatSync,
    FrequencyVisualization,
    VolumeReactive,
    MoodLighting,
    SpectrumAnalyzer,
    PartyMode,
    Custom,
    Static,
    Gradient,
    Pulse,
    Wave,
    Strobe,
    Rainbow,
    Fire,
    Water,
    Lightning,
    Disco,
    Ambient,
    Focus,
    Relax,
    Energize
}

/// <summary>
/// Preset category for organization
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PresetCategory
{
    General,
    Music,
    Gaming,
    Work,
    Relaxation,
    Party,
    Holiday,
    Custom,
    BuiltIn
}

/// <summary>
/// Preset template for creating new presets
/// </summary>
public class PresetTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PresetType Type { get; set; }
    public PresetCategory Category { get; set; }
    public Dictionary<string, object> DefaultParameters { get; set; } = new();
    public PresetMetadata Metadata { get; set; } = new();
    public bool IsBuiltIn { get; set; } = true;
}

/// <summary>
/// Preset collection for organizing multiple presets
/// </summary>
public class PresetCollection
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> PresetIds { get; set; } = new();
    public PresetCategory Category { get; set; }
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public string Author { get; set; } = "User";
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Preset execution context
/// </summary>
public class PresetExecutionContext
{
    public string PresetId { get; set; } = string.Empty;
    public List<string> TargetDeviceIds { get; set; } = new();
    public List<string> TargetDeviceGroupIds { get; set; } = new();
    public Dictionary<string, object> RuntimeParameters { get; set; } = new();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = false;
    public string ExecutionId { get; set; } = string.Empty;
    public PresetExecutionSettings Settings { get; set; } = new();
}

/// <summary>
/// Preset execution settings
/// </summary>
public class PresetExecutionSettings
{
    public bool EnableSmoothTransitions { get; set; } = true;
    public int TransitionDurationMs { get; set; } = 500;
    public bool EnableBeatSync { get; set; } = true;
    public bool EnableVolumeReactive { get; set; } = true;
    public bool EnableFrequencyVisualization { get; set; } = true;
    public float IntensityMultiplier { get; set; } = 1.0f;
    public float SpeedMultiplier { get; set; } = 1.0f;
    public bool LoopEnabled { get; set; } = true;
    public int LoopDurationMs { get; set; } = 0; // 0 = infinite
    public bool RandomizeColors { get; set; } = false;
    public bool RandomizeTiming { get; set; } = false;
}

/// <summary>
/// Advanced audio analysis data with comprehensive features
/// </summary>
public class AudioAnalysis
{
    // Basic audio features
    public float Volume { get; set; }
    public float[] FrequencyBands { get; set; } = Array.Empty<float>();
    public float BeatIntensity { get; set; }
    public float Tempo { get; set; }
    public float SpectralCentroid { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Advanced spectral features
    public float SpectralRolloff { get; set; }
    public float SpectralBandwidth { get; set; }
    public float SpectralContrast { get; set; }
    public float SpectralFlatness { get; set; }
    public float SpectralFlux { get; set; }
    public float ZeroCrossingRate { get; set; }
    public float MFCC1 { get; set; }
    public float MFCC2 { get; set; }
    public float MFCC3 { get; set; }

    // Rhythm and beat analysis
    public bool IsBeatDetected { get; set; }
    public float BeatConfidence { get; set; }
    public float BeatStrength { get; set; }
    public float RhythmRegularity { get; set; }
    public float RhythmComplexity { get; set; }
    public List<float> BeatTimes { get; set; } = new();
    public float OnsetStrength { get; set; }

    // Mood and emotion analysis
    public float Energy { get; set; }
    public float Valence { get; set; }
    public float Arousal { get; set; }
    public float Dominance { get; set; }
    public string PredictedMood { get; set; } = string.Empty;
    public float MoodConfidence { get; set; }

    // Genre and style classification
    public string PredictedGenre { get; set; } = string.Empty;
    public float GenreConfidence { get; set; }
    public Dictionary<string, float> GenreProbabilities { get; set; } = new();

    // Advanced frequency analysis
    public List<FrequencyBand> FrequencyBandsDetailed { get; set; } = new();
    public float BassIntensity { get; set; }
    public float MidIntensity { get; set; }
    public float TrebleIntensity { get; set; }
    public float SubBassIntensity { get; set; }
    public float PresenceIntensity { get; set; }

    // Dynamic range and compression
    public float DynamicRange { get; set; }
    public float CompressionRatio { get; set; }
    public float PeakLevel { get; set; }
    public float RMSLevel { get; set; }
    public float CrestFactor { get; set; }

    // Harmonic analysis
    public float Harmonicity { get; set; }
    public float Inharmonicity { get; set; }
    public float FundamentalFrequency { get; set; }
    public List<float> HarmonicFrequencies { get; set; } = new();
    public List<float> HarmonicAmplitudes { get; set; } = new();

    // Temporal features
    public float AttackTime { get; set; }
    public float DecayTime { get; set; }
    public float SustainLevel { get; set; }
    public float ReleaseTime { get; set; }
    public float TransientStrength { get; set; }

    // Quality metrics
    public float SignalToNoiseRatio { get; set; }
    public float DistortionLevel { get; set; }
    public float ClippingLevel { get; set; }
    public bool IsClipping { get; set; }
    public float AudioQuality { get; set; }
}

/// <summary>
/// Spotify track information
/// </summary>
public class SpotifyTrack
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public bool IsPlaying { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan Progress { get; set; }
    public string PreviewUrl { get; set; } = string.Empty;
    public int Popularity { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<string> Artists { get; set; } = new();
    public string AlbumId { get; set; } = string.Empty;
    public string AlbumImageUrl { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public string ExternalUrl { get; set; } = string.Empty;
    public Dictionary<string, object> AudioFeatures { get; set; } = new();
}

/// <summary>
/// Spotify artist information
/// </summary>
public class SpotifyArtist
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Popularity { get; set; }
    public List<string> Genres { get; set; } = new();
    public int Followers { get; set; }
    public string ExternalUrl { get; set; } = string.Empty;
}

/// <summary>
/// Spotify album information
/// </summary>
public class SpotifyAlbum
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public int TotalTracks { get; set; }
    public string AlbumType { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public string ExternalUrl { get; set; } = string.Empty;
}

/// <summary>
/// Spotify playlist information
/// </summary>
public class SpotifyPlaylist
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public int TotalTracks { get; set; }
    public bool IsPublic { get; set; }
    public bool IsCollaborative { get; set; }
    public string ExternalUrl { get; set; } = string.Empty;
}

/// <summary>
/// Spotify audio features
/// </summary>
public class SpotifyAudioFeatures
{
    public string TrackId { get; set; } = string.Empty;
    public float Danceability { get; set; }
    public float Energy { get; set; }
    public int Key { get; set; }
    public float Loudness { get; set; }
    public int Mode { get; set; }
    public float Speechiness { get; set; }
    public float Acousticness { get; set; }
    public float Instrumentalness { get; set; }
    public float Liveness { get; set; }
    public float Valence { get; set; }
    public float Tempo { get; set; }
    public int DurationMs { get; set; }
    public int TimeSignature { get; set; }
}

/// <summary>
/// Spotify playback state
/// </summary>
public class SpotifyPlaybackState
{
    public SpotifyTrack? CurrentTrack { get; set; }
    public bool IsPlaying { get; set; }
    public int ProgressMs { get; set; }
    public int VolumePercent { get; set; }
    public bool ShuffleState { get; set; }
    public string RepeatState { get; set; } = string.Empty;
    public SpotifyDevice? Device { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Spotify device information
/// </summary>
public class SpotifyDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int VolumePercent { get; set; }
    public bool IsActive { get; set; }
    public bool IsPrivateSession { get; set; }
    public bool IsRestricted { get; set; }
}

/// <summary>
/// Spotify user profile
/// </summary>
public class SpotifyUserProfile
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Followers { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
}

/// <summary>
/// Spotify authentication token
/// </summary>
public class SpotifyToken
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Scope { get; set; } = string.Empty;
    public bool IsValid => !string.IsNullOrEmpty(AccessToken) && DateTime.UtcNow < ExpiresAt;
}

/// <summary>
/// Spotify search results
/// </summary>
public class SpotifySearchResults
{
    public List<SpotifyTrack> Tracks { get; set; } = new();
    public List<SpotifyArtist> Artists { get; set; } = new();
    public List<SpotifyAlbum> Albums { get; set; } = new();
    public List<SpotifyPlaylist> Playlists { get; set; } = new();
}

/// <summary>
/// Spotify API configuration
/// </summary>
public class SpotifyApiConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://localhost:8888/callback";
    public List<string> Scopes { get; set; } = new()
    {
        "user-read-playback-state",
        "user-modify-playback-state",
        "user-read-currently-playing",
        "user-read-recently-played",
        "user-top-read",
        "user-read-private",
        "user-read-email",
        "playlist-read-private",
        "playlist-read-collaborative",
        "playlist-modify-public",
        "playlist-modify-private"
    };
    public string BaseUrl { get; set; } = "https://api.spotify.com/v1";
    public string AuthUrl { get; set; } = "https://accounts.spotify.com/authorize";
    public string TokenUrl { get; set; } = "https://accounts.spotify.com/api/token";
    public int RequestTimeoutMs { get; set; } = 30000;
    public int MaxRetryAttempts { get; set; } = 3;
    public bool EnableRateLimitHandling { get; set; } = true;
}

/// <summary>
/// Application configuration
/// </summary>
public class AppConfiguration
{
    public AudioSettings Audio { get; set; } = new();
    public DeviceSettings Devices { get; set; } = new();
    public EffectSettings Effects { get; set; } = new();
    public UiSettings UI { get; set; } = new();
    public SyncSettings Synchronization { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public SpotifySettings Spotify { get; set; } = new();
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public string Version { get; set; } = "1.0.0";
}

public class AudioSettings
{
    public AudioSource Source { get; set; } = AudioSource.System;
    public float Sensitivity { get; set; } = 0.5f;
    public int LatencyMs { get; set; } = 50;
    public string? AudioDeviceId { get; set; }
    public int SampleRate { get; set; } = 44100;
    public int BufferSize { get; set; } = 1024;
    public float VolumeThreshold { get; set; } = 0.1f;
    public bool EnableNoiseReduction { get; set; } = true;
    public float NoiseReductionLevel { get; set; } = 0.3f;
    public bool EnableBeatDetection { get; set; } = true;
    public float BeatSensitivity { get; set; } = 0.7f;
    public bool EnableFrequencyAnalysis { get; set; } = true;
    public int FrequencyBands { get; set; } = 12;
}

public class DeviceSettings
{
    public List<SmartDevice> Devices { get; set; } = new();
    public Dictionary<string, DeviceGroup> DeviceGroups { get; set; } = new();
    public Dictionary<string, DeviceConfiguration> SavedConfigurations { get; set; } = new();
    public bool AutoDiscoverOnStartup { get; set; } = true;
    public int DiscoveryTimeoutSeconds { get; set; } = 10;
    public bool AutoConnectOnDiscovery { get; set; } = false;
    public int ConnectionRetryAttempts { get; set; } = 3;
    public int ConnectionTimeoutSeconds { get; set; } = 5;
    public bool EnableDeviceStateCaching { get; set; } = true;
    public int StateCacheTimeoutSeconds { get; set; } = 30;
}

public class EffectSettings
{
    public string ActivePresetId { get; set; } = string.Empty;
    public Dictionary<string, object> GlobalParameters { get; set; } = new();
    public bool EnableRealTimeEffects { get; set; } = true;
    public int EffectUpdateIntervalMs { get; set; } = 100;
    public float EffectIntensity { get; set; } = 0.8f;
    public bool EnableSmoothTransitions { get; set; } = true;
    public int TransitionDurationMs { get; set; } = 500;
    public bool EnableBeatSync { get; set; } = true;
    public bool EnableFrequencyVisualization { get; set; } = true;
    public bool EnableMoodLighting { get; set; } = true;
    public bool EnableSpectrumAnalyzer { get; set; } = true;
    public bool EnablePartyMode { get; set; } = false;
}

public class UiSettings
{
    public bool DarkMode { get; set; } = true;
    public string Theme { get; set; } = "Dark";
    public bool ShowAudioVisualizer { get; set; } = true;
    public bool ShowDeviceStatus { get; set; } = true;
    public bool ShowPerformanceMetrics { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public bool EnableNotifications { get; set; } = true;
    public string WindowPosition { get; set; } = "Center";
    public Size WindowSize { get; set; } = new Size(1200, 800);
    public bool RememberWindowState { get; set; } = true;
    public int VisualizerUpdateRate { get; set; } = 60;
    public bool EnableAnimations { get; set; } = true;
}

public class SyncSettings
{
    public bool EnableRealTimeSync { get; set; } = true;
    public int SyncIntervalMs { get; set; } = 100;
    public bool SyncOnBeat { get; set; } = true;
    public bool SyncOnVolume { get; set; } = true;
    public bool SyncOnFrequency { get; set; } = true;
    public float SyncSensitivity { get; set; } = 0.5f;
    public List<string> EnabledDeviceGroups { get; set; } = new();
    public List<DeviceType> EnabledDeviceTypes { get; set; } = new();
    public bool EnableCommandQueuing { get; set; } = true;
    public int MaxQueueSize { get; set; } = 1000;
    public int CommandTimeoutMs { get; set; } = 5000;
    public bool EnableBatchProcessing { get; set; } = true;
    public int BatchSize { get; set; } = 10;
}

public class PerformanceSettings
{
    public bool EnablePerformanceMonitoring { get; set; } = true;
    public int MetricsCollectionIntervalMs { get; set; } = 1000;
    public bool EnableMemoryOptimization { get; set; } = true;
    public int MaxMemoryUsageMB { get; set; } = 512;
    public bool EnableGarbageCollectionOptimization { get; set; } = true;
    public int GCOptimizationIntervalMs { get; set; } = 30000;
    public bool EnableThreadPoolOptimization { get; set; } = true;
    public int MinThreadPoolThreads { get; set; } = 4;
    public int MaxThreadPoolThreads { get; set; } = 16;
    public bool EnableAsyncOptimization { get; set; } = true;
    public int AsyncConcurrencyLevel { get; set; } = 4;
}

public class SecuritySettings
{
    public bool EnableEncryption { get; set; } = true;
    public string EncryptionKey { get; set; } = string.Empty;
    public bool EnableSecureStorage { get; set; } = true;
    public bool EnableApiKeyProtection { get; set; } = true;
    public bool EnableNetworkSecurity { get; set; } = true;
    public bool EnableCertificateValidation { get; set; } = true;
    public int SessionTimeoutMinutes { get; set; } = 60;
    public bool EnableAuditLogging { get; set; } = true;
    public bool EnableAccessControl { get; set; } = false;
    public List<string> AllowedIpAddresses { get; set; } = new();
}

public class LoggingSettings
{
    public bool EnableLogging { get; set; } = true;
    public string LogLevel { get; set; } = "Information";
    public bool EnableFileLogging { get; set; } = true;
    public string LogFilePath { get; set; } = "logs/partylights.log";
    public int MaxLogFileSizeMB { get; set; } = 10;
    public int MaxLogFiles { get; set; } = 5;
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableEventLogging { get; set; } = false;
    public bool EnableStructuredLogging { get; set; } = true;
    public bool EnablePerformanceLogging { get; set; } = false;
    public bool EnableSecurityLogging { get; set; } = true;
}

public class SpotifySettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://localhost:8888/callback";
    public bool AutoConnect { get; set; } = false;
    public bool EnableRealTimeUpdates { get; set; } = true;
    public int UpdateIntervalMs { get; set; } = 2000;
    public bool EnableAudioAnalysis { get; set; } = true;
    public bool EnableMoodDetection { get; set; } = true;
    public bool EnablePresetRecommendations { get; set; } = true;
    public List<string> EnabledScopes { get; set; } = new()
    {
        "user-read-playback-state",
        "user-modify-playback-state",
        "user-read-currently-playing",
        "user-read-recently-played",
        "user-top-read",
        "user-read-private",
        "user-read-email",
        "playlist-read-private",
        "playlist-read-collaborative",
        "playlist-modify-public",
        "playlist-modify-private"
    };
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AudioSource
{
    System,
    Spotify
}

/// <summary>
/// Device state information
/// </summary>
public class DeviceState
{
    public bool IsOn { get; set; }
    public int Brightness { get; set; }
    public int Red { get; set; }
    public int Green { get; set; }
    public int Blue { get; set; }
    public int ColorTemperature { get; set; }
    public int Saturation { get; set; }
    public int Hue { get; set; }
    public string? CurrentEffect { get; set; }
    public string? CurrentScene { get; set; }
    public string? CurrentGroup { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Device group for managing multiple devices
/// </summary>
public class DeviceGroup
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> DeviceIds { get; set; } = new();
    public DeviceType? DeviceType { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Device configuration for saving/loading settings
/// </summary>
public class DeviceConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Device control command
/// </summary>
public class DeviceCommand
{
    public string DeviceId { get; set; } = string.Empty;
    public CommandType Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public int TransitionTime { get; set; } = 0;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Types of device commands
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommandType
{
    SetColor,
    SetBrightness,
    SetEffect,
    SetScene,
    SetGroup,
    TurnOn,
    TurnOff,
    SetColorTemperature,
    SetSaturation,
    SetHue,
    SetTransitionTime,
    GetState
}

/// <summary>
/// Device synchronization settings
/// </summary>
public class SynchronizationSettings
{
    public bool EnableRealTimeSync { get; set; } = true;
    public int SyncIntervalMs { get; set; } = 100;
    public bool SyncOnBeat { get; set; } = true;
    public bool SyncOnVolume { get; set; } = true;
    public bool SyncOnFrequency { get; set; } = true;
    public float SyncSensitivity { get; set; } = 0.5f;
    public List<string> EnabledDeviceGroups { get; set; } = new();
    public List<DeviceType> EnabledDeviceTypes { get; set; } = new();
}

/// <summary>
/// Device performance metrics
/// </summary>
public class DevicePerformanceMetrics
{
    public string DeviceId { get; set; } = string.Empty;
    public int CommandsPerSecond { get; set; }
    public double AverageResponseTime { get; set; }
    public int FailedCommands { get; set; }
    public int SuccessfulCommands { get; set; }
    public DateTime LastCommandTime { get; set; }
    public double UptimePercentage { get; set; }
}
