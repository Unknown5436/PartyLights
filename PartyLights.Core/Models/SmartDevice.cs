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
/// Audio analysis data
/// </summary>
public class AudioAnalysis
{
    public float Volume { get; set; }
    public float[] FrequencyBands { get; set; } = Array.Empty<float>();
    public float BeatIntensity { get; set; }
    public float Tempo { get; set; }
    public float SpectralCentroid { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
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
    public float Danceability { get; set; }
    public float Energy { get; set; }
    public float Valence { get; set; }
    public float Tempo { get; set; }
    public bool IsPlaying { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan Progress { get; set; }
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
