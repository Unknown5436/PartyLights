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
