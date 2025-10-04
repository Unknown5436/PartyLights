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
    public bool IsEnabled { get; set; } = true;
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
    PartyMode
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
}

public class AudioSettings
{
    public AudioSource Source { get; set; } = AudioSource.System;
    public float Sensitivity { get; set; } = 0.5f;
    public int LatencyMs { get; set; } = 50;
    public string? AudioDeviceId { get; set; }
}

public class DeviceSettings
{
    public List<SmartDevice> Devices { get; set; } = new();
    public Dictionary<string, string> DeviceGroups { get; set; } = new();
}

public class EffectSettings
{
    public string ActivePresetId { get; set; } = string.Empty;
    public Dictionary<string, object> GlobalParameters { get; set; } = new();
}

public class UiSettings
{
    public bool DarkMode { get; set; } = true;
    public string Theme { get; set; } = "Dark";
    public bool ShowAudioVisualizer { get; set; } = true;
    public bool ShowDeviceStatus { get; set; } = true;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AudioSource
{
    System,
    Spotify
}
