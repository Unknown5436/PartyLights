using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;

namespace PartyLights.UI;

/// <summary>
/// Main window view model
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly IDeviceManagerService _deviceManagerService;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ILightingEffectService _lightingEffectService;
    private readonly ISpotifyService _spotifyService;

    private bool _isConnected;
    private bool _isSyncActive;
    private AudioSource _selectedAudioSource = AudioSource.System;
    private LightingPreset? _selectedPreset;
    private string _statusMessage = "Ready";
    private string _connectionStatus = "No devices connected";
    private float _sensitivity = 0.5f;
    private int _latency = 50;
    private float _colorIntensity = 0.8f;
    private int _minBrightness = 10;
    private int _maxBrightness = 255;

    public MainViewModel(
        IDeviceManagerService deviceManagerService,
        IAudioCaptureService audioCaptureService,
        ILightingEffectService lightingEffectService,
        ISpotifyService spotifyService)
    {
        _deviceManagerService = deviceManagerService;
        _audioCaptureService = audioCaptureService;
        _lightingEffectService = lightingEffectService;
        _spotifyService = spotifyService;

        // Initialize commands
        StartSyncCommand = new RelayCommand(async () => await StartSyncAsync(), () => !IsSyncActive);
        StopSyncCommand = new RelayCommand(async () => await StopSyncAsync(), () => IsSyncActive);
        DiscoverDevicesCommand = new RelayCommand(async () => await DiscoverDevicesAsync());

        // Initialize collections
        AvailablePresets = new ObservableCollection<LightingPreset>();
        ConnectedDevices = new ObservableCollection<SmartDevice>();
        DeviceGroups = new ObservableCollection<string> { "All Devices", "Living Room", "Bedroom" };

        // Subscribe to events
        _deviceManagerService.DeviceConnected += OnDeviceConnected;
        _deviceManagerService.DeviceDisconnected += OnDeviceDisconnected;
        _audioCaptureService.AnalysisUpdated += OnAudioAnalysisUpdated;

        // Load initial data
        LoadPresets();
    }

    // Properties
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public bool IsSyncActive
    {
        get => _isSyncActive;
        set
        {
            SetProperty(ref _isSyncActive, value);
            ((RelayCommand)StartSyncCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopSyncCommand).RaiseCanExecuteChanged();
        }
    }

    public AudioSource SelectedAudioSource
    {
        get => _selectedAudioSource;
        set => SetProperty(ref _selectedAudioSource, value);
    }

    public LightingPreset? SelectedPreset
    {
        get => _selectedPreset;
        set => SetProperty(ref _selectedPreset, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public float Sensitivity
    {
        get => _sensitivity;
        set => SetProperty(ref _sensitivity, value);
    }

    public int Latency
    {
        get => _latency;
        set => SetProperty(ref _latency, value);
    }

    public float ColorIntensity
    {
        get => _colorIntensity;
        set => SetProperty(ref _colorIntensity, value);
    }

    public int MinBrightness
    {
        get => _minBrightness;
        set => SetProperty(ref _minBrightness, value);
    }

    public int MaxBrightness
    {
        get => _maxBrightness;
        set => SetProperty(ref _maxBrightness, value);
    }

    // Collections
    public ObservableCollection<LightingPreset> AvailablePresets { get; }
    public ObservableCollection<SmartDevice> ConnectedDevices { get; }
    public ObservableCollection<string> DeviceGroups { get; }

    // Commands
    public ICommand StartSyncCommand { get; }
    public ICommand StopSyncCommand { get; }
    public ICommand DiscoverDevicesCommand { get; }

    // Methods
    public async Task StartSyncAsync()
    {
        try
        {
            StatusMessage = "Starting sync...";

            if (SelectedAudioSource == AudioSource.System)
            {
                await _audioCaptureService.StartCaptureAsync();
            }
            else if (SelectedAudioSource == AudioSource.Spotify)
            {
                await _spotifyService.StartListeningAsync();
            }

            if (SelectedPreset != null)
            {
                await _lightingEffectService.ApplyPresetAsync(SelectedPreset);
            }

            IsSyncActive = true;
            StatusMessage = "Sync active";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public async Task StopSyncAsync()
    {
        try
        {
            StatusMessage = "Stopping sync...";

            await _audioCaptureService.StopCaptureAsync();
            await _spotifyService.StopListeningAsync();
            await _lightingEffectService.StopEffectsAsync();

            IsSyncActive = false;
            StatusMessage = "Sync stopped";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public async Task DiscoverDevicesAsync()
    {
        try
        {
            StatusMessage = "Discovering devices...";

            var devices = await _deviceManagerService.DiscoverDevicesAsync();

            ConnectedDevices.Clear();
            foreach (var device in devices)
            {
                ConnectedDevices.Add(device);
            }

            ConnectionStatus = $"{ConnectedDevices.Count} devices connected";
            IsConnected = ConnectedDevices.Count > 0;
            StatusMessage = "Device discovery complete";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Discovery error: {ex.Message}";
        }
    }

    public void SelectPreset(LightingPreset preset)
    {
        SelectedPreset = preset;
    }

    private async void LoadPresets()
    {
        try
        {
            var presets = await _lightingEffectService.GetAvailablePresetsAsync();

            AvailablePresets.Clear();
            foreach (var preset in presets)
            {
                AvailablePresets.Add(preset);
            }

            if (AvailablePresets.Count > 0)
            {
                SelectedPreset = AvailablePresets.First();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading presets: {ex.Message}";
        }
    }

    private void OnDeviceConnected(object? sender, DeviceEventArgs e)
    {
        ConnectedDevices.Add(e.Device);
        ConnectionStatus = $"{ConnectedDevices.Count} devices connected";
        IsConnected = true;
    }

    private void OnDeviceDisconnected(object? sender, DeviceEventArgs e)
    {
        var device = ConnectedDevices.FirstOrDefault(d => d.Id == e.Device.Id);
        if (device != null)
        {
            ConnectedDevices.Remove(device);
        }

        ConnectionStatus = $"{ConnectedDevices.Count} devices connected";
        IsConnected = ConnectedDevices.Count > 0;
    }

    private void OnAudioAnalysisUpdated(object? sender, AudioAnalysisEventArgs e)
    {
        // Update UI with audio analysis data
        // This will be enhanced in later phases
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Simple relay command implementation
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? (() => true);
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute();

    public async void Execute(object? parameter)
    {
        await _execute();
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}