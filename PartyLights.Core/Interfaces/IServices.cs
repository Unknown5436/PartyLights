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
