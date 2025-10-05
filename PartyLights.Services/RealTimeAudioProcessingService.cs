using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;

namespace PartyLights.Services;

/// <summary>
/// Real-time audio processing service for advanced lighting control
/// </summary>
public class RealTimeAudioProcessingService : IDisposable
{
    private readonly ILogger<RealTimeAudioProcessingService> _logger;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly IAdvancedDeviceControlService _deviceControlService;
    private readonly PresetExecutionEngine _presetExecutionEngine;
    private readonly ConcurrentQueue<AudioAnalysis> _processingQueue = new();
    private readonly object _processingLock = new();

    private Timer? _processingTimer;
    private bool _isProcessing;
    private AudioAnalysis? _lastAnalysis;
    private DateTime _lastProcessingTime = DateTime.MinValue;

    // Processing parameters
    private const int ProcessingIntervalMs = 20; // 50 FPS
    private const int MaxQueueSize = 100;
    private readonly float[] _volumeHistory = new float[100];
    private int _volumeHistoryIndex = 0;
    private readonly float[] _beatHistory = new float[100];
    private int _beatHistoryIndex = 0;

    public event EventHandler<AudioAnalysisEventArgs>? ProcessingCompleted;
    public event EventHandler<RealTimeProcessingEventArgs>? ProcessingError;

    public RealTimeAudioProcessingService(
        ILogger<RealTimeAudioProcessingService> logger,
        IAudioCaptureService audioCaptureService,
        IAdvancedDeviceControlService deviceControlService,
        PresetExecutionEngine presetExecutionEngine)
    {
        _logger = logger;
        _audioCaptureService = audioCaptureService;
        _deviceControlService = deviceControlService;
        _presetExecutionEngine = presetExecutionEngine;

        // Subscribe to audio analysis updates
        _audioCaptureService.AnalysisUpdated += OnAudioAnalysisUpdated;
    }

    /// <summary>
    /// Starts real-time audio processing
    /// </summary>
    public async Task<bool> StartProcessingAsync()
    {
        try
        {
            if (_isProcessing)
            {
                _logger.LogWarning("Real-time audio processing is already running");
                return true;
            }

            _logger.LogInformation("Starting real-time audio processing");

            // Start audio capture if not already running
            if (!_audioCaptureService.IsCapturing)
            {
                var captureStarted = await _audioCaptureService.StartCaptureAsync();
                if (!captureStarted)
                {
                    _logger.LogError("Failed to start audio capture for real-time processing");
                    return false;
                }
            }

            // Start processing timer
            _processingTimer = new Timer(ProcessAudioQueue, null, 0, ProcessingIntervalMs);
            _isProcessing = true;

            _logger.LogInformation("Real-time audio processing started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting real-time audio processing");
            return false;
        }
    }

    /// <summary>
    /// Stops real-time audio processing
    /// </summary>
    public async Task StopProcessingAsync()
    {
        try
        {
            if (!_isProcessing)
            {
                _logger.LogWarning("Real-time audio processing is not running");
                return;
            }

            _logger.LogInformation("Stopping real-time audio processing");

            _isProcessing = false;
            _processingTimer?.Dispose();
            _processingTimer = null;

            // Clear processing queue
            while (_processingQueue.TryDequeue(out _)) { }

            _logger.LogInformation("Real-time audio processing stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping real-time audio processing");
        }
    }

    /// <summary>
    /// Gets current processing status
    /// </summary>
    public RealTimeProcessingStatus GetProcessingStatus()
    {
        return new RealTimeProcessingStatus
        {
            IsProcessing = _isProcessing,
            QueueSize = _processingQueue.Count,
            LastProcessingTime = _lastProcessingTime,
            AverageVolume = _volumeHistory.Average(),
            AverageBeatIntensity = _beatHistory.Average(),
            ProcessingLatencyMs = (DateTime.UtcNow - _lastProcessingTime).TotalMilliseconds
        };
    }

    #region Private Methods

    private void OnAudioAnalysisUpdated(object? sender, AudioAnalysisEventArgs e)
    {
        try
        {
            // Add to processing queue
            if (_processingQueue.Count < MaxQueueSize)
            {
                _processingQueue.Enqueue(e.Analysis);
            }
            else
            {
                // Remove oldest if queue is full
                _processingQueue.TryDequeue(out _);
                _processingQueue.Enqueue(e.Analysis);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding audio analysis to processing queue");
        }
    }

    private void ProcessAudioQueue(object? state)
    {
        try
        {
            if (!_isProcessing)
            {
                return;
            }

            // Process all available analyses
            while (_processingQueue.TryDequeue(out var analysis))
            {
                ProcessAudioAnalysis(analysis);
            }

            _lastProcessingTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in audio processing queue");
            ProcessingError?.Invoke(this, new RealTimeProcessingEventArgs(ex.Message));
        }
    }

    private void ProcessAudioAnalysis(AudioAnalysis analysis)
    {
        try
        {
            lock (_processingLock)
            {
                _lastAnalysis = analysis;

                // Update history
                UpdateProcessingHistory(analysis);

                // Perform real-time lighting control
                PerformRealTimeLightingControl(analysis);

                // Notify completion
                ProcessingCompleted?.Invoke(this, new AudioAnalysisEventArgs(analysis));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio analysis");
            ProcessingError?.Invoke(this, new RealTimeProcessingEventArgs(ex.Message));
        }
    }

    private void UpdateProcessingHistory(AudioAnalysis analysis)
    {
        // Update volume history
        _volumeHistory[_volumeHistoryIndex] = analysis.Volume;
        _volumeHistoryIndex = (_volumeHistoryIndex + 1) % _volumeHistory.Length;

        // Update beat history
        _beatHistory[_beatHistoryIndex] = analysis.BeatIntensity;
        _beatHistoryIndex = (_beatHistoryIndex + 1) % _beatHistory.Length;
    }

    private async void PerformRealTimeLightingControl(AudioAnalysis analysis)
    {
        try
        {
            // Get active preset executions
            var activeExecutions = _presetExecutionEngine.GetActiveExecutions();
            if (!activeExecutions.Any())
            {
                return; // No active presets to control
            }

            // Process each active execution
            foreach (var execution in activeExecutions)
            {
                await ProcessPresetExecution(execution, analysis);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing real-time lighting control");
        }
    }

    private async Task ProcessPresetExecution(PresetExecutionContext execution, AudioAnalysis analysis)
    {
        try
        {
            // Get preset details
            var preset = _presetExecutionEngine.GetPreset(execution.PresetId);
            if (preset == null)
            {
                return;
            }

            // Apply real-time adjustments based on audio analysis
            await ApplyRealTimeAdjustments(execution, preset, analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing preset execution: {ExecutionId}", execution.ExecutionId);
        }
    }

    private async Task ApplyRealTimeAdjustments(PresetExecutionContext execution, LightingPreset preset, AudioAnalysis analysis)
    {
        try
        {
            // Get target devices
            var targetDevices = await GetTargetDevices(execution);
            if (!targetDevices.Any())
            {
                return;
            }

            // Apply adjustments based on preset type and audio analysis
            switch (preset.Type)
            {
                case PresetType.BeatSync:
                    await ApplyBeatSyncAdjustments(targetDevices, analysis, execution);
                    break;
                case PresetType.FrequencyVisualization:
                    await ApplyFrequencyVisualizationAdjustments(targetDevices, analysis, execution);
                    break;
                case PresetType.VolumeReactive:
                    await ApplyVolumeReactiveAdjustments(targetDevices, analysis, execution);
                    break;
                case PresetType.MoodLighting:
                    await ApplyMoodLightingAdjustments(targetDevices, analysis, execution);
                    break;
                case PresetType.SpectrumAnalyzer:
                    await ApplySpectrumAnalyzerAdjustments(targetDevices, analysis, execution);
                    break;
                case PresetType.PartyMode:
                    await ApplyPartyModeAdjustments(targetDevices, analysis, execution);
                    break;
                default:
                    // Apply general adjustments
                    await ApplyGeneralAdjustments(targetDevices, analysis, execution);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying real-time adjustments");
        }
    }

    private async Task<List<SmartDevice>> GetTargetDevices(PresetExecutionContext execution)
    {
        var devices = new List<SmartDevice>();

        // This would typically get devices from the device manager service
        // For now, return empty list as this is a placeholder implementation
        return devices;
    }

    private async Task ApplyBeatSyncAdjustments(List<SmartDevice> devices, AudioAnalysis analysis, PresetExecutionContext execution)
    {
        if (!analysis.IsBeatDetected || analysis.BeatConfidence < 0.5f)
        {
            return;
        }

        // Calculate beat intensity multiplier
        float intensityMultiplier = analysis.BeatStrength * execution.Settings.IntensityMultiplier;

        // Apply beat-synchronized lighting
        foreach (var device in devices)
        {
            // Calculate color based on beat intensity
            int brightness = (int)(255 * intensityMultiplier);
            int red = (int)(analysis.Energy * 255);
            int green = (int)(analysis.Valence * 255);
            int blue = (int)((1 - analysis.Valence) * 255);

            // Apply color with beat timing
            await _deviceControlService.SetColorToDeviceGroupAsync(device.Id, red, green, blue);
        }
    }

    private async Task ApplyFrequencyVisualizationAdjustments(List<SmartDevice> devices, AudioAnalysis analysis, PresetExecutionContext execution)
    {
        // Map frequency bands to colors
        var frequencyBands = analysis.FrequencyBandsDetailed;
        if (!frequencyBands.Any())
        {
            return;
        }

        // Calculate dominant frequency band
        var dominantBand = frequencyBands.OrderByDescending(b => b.Intensity).First();

        // Map frequency to color
        var color = MapFrequencyToColor(dominantBand);

        // Apply color to devices
        foreach (var device in devices)
        {
            await _deviceControlService.SetColorToDeviceGroupAsync(device.Id, color.R, color.G, color.B);
        }
    }

    private async Task ApplyVolumeReactiveAdjustments(List<SmartDevice> devices, AudioAnalysis analysis, PresetExecutionContext execution)
    {
        // Calculate brightness based on volume
        float volumeMultiplier = analysis.Volume * execution.Settings.IntensityMultiplier;
        int brightness = (int)(255 * volumeMultiplier);

        // Apply brightness to devices
        foreach (var device in devices)
        {
            await _deviceControlService.SetBrightnessToDeviceGroupAsync(device.Id, brightness);
        }
    }

    private async Task ApplyMoodLightingAdjustments(List<SmartDevice> devices, AudioAnalysis analysis, PresetExecutionContext execution)
    {
        // Calculate mood-based color
        var moodColor = CalculateMoodColor(analysis);

        // Apply color to devices
        foreach (var device in devices)
        {
            await _deviceControlService.SetColorToDeviceGroupAsync(device.Id, moodColor.R, moodColor.G, moodColor.B);
        }
    }

    private async Task ApplySpectrumAnalyzerAdjustments(List<SmartDevice> devices, AudioAnalysis analysis, PresetExecutionContext execution)
    {
        // Apply different colors to different devices based on frequency bands
        var frequencyBands = analysis.FrequencyBandsDetailed;
        if (!frequencyBands.Any())
        {
            return;
        }

        for (int i = 0; i < Math.Min(devices.Count, frequencyBands.Count); i++)
        {
            var device = devices[i];
            var band = frequencyBands[i];
            var color = MapFrequencyToColor(band);

            await _deviceControlService.SetColorToDeviceGroupAsync(device.Id, color.R, color.G, color.B);
        }
    }

    private async Task ApplyPartyModeAdjustments(List<SmartDevice> devices, AudioAnalysis analysis, PresetExecutionContext execution)
    {
        // Combine multiple effects for party mode
        if (analysis.IsBeatDetected)
        {
            await ApplyBeatSyncAdjustments(devices, analysis, execution);
        }
        else
        {
            await ApplyFrequencyVisualizationAdjustments(devices, analysis, execution);
        }

        // Add strobe effect on high energy
        if (analysis.Energy > 0.8f)
        {
            await ApplyStrobeEffect(devices, analysis, execution);
        }
    }

    private async Task ApplyGeneralAdjustments(List<SmartDevice> devices, AudioAnalysis analysis, PresetExecutionContext execution)
    {
        // Apply general volume-reactive adjustments
        await ApplyVolumeReactiveAdjustments(devices, analysis, execution);
    }

    private async Task ApplyStrobeEffect(List<SmartDevice> devices, AudioAnalysis analysis, PresetExecutionContext execution)
    {
        // Simple strobe effect based on time and energy
        bool strobeOn = (DateTime.UtcNow.Millisecond / 100) % 2 == 0;
        int brightness = strobeOn ? 255 : 0;

        foreach (var device in devices)
        {
            await _deviceControlService.SetBrightnessToDeviceGroupAsync(device.Id, brightness);
        }
    }

    private Color MapFrequencyToColor(FrequencyBand band)
    {
        // Map frequency to HSV color space
        float hue = (band.FrequencyLow + band.FrequencyHigh) / 2 / 20000f * 360f; // Map to 0-360
        float saturation = Math.Min(band.Intensity * 2, 1f);
        float value = Math.Min(band.Intensity * 1.5f, 1f);

        return HsvToRgb(hue, saturation, value);
    }

    private Color CalculateMoodColor(AudioAnalysis analysis)
    {
        // Calculate color based on mood dimensions
        float hue = analysis.Valence * 120f; // 0-120 degrees (red to green)
        float saturation = analysis.Arousal;
        float value = analysis.Energy;

        return HsvToRgb(hue, saturation, value);
    }

    private Color HsvToRgb(float hue, float saturation, float value)
    {
        int hi = (int)(hue / 60) % 6;
        float f = hue / 60 - hi;
        float p = value * (1 - saturation);
        float q = value * (1 - f * saturation);
        float t = value * (1 - (1 - f) * saturation);

        float r, g, b;
        switch (hi)
        {
            case 0: r = value; g = t; b = p; break;
            case 1: r = q; g = value; b = p; break;
            case 2: r = p; g = value; b = t; break;
            case 3: r = p; g = q; b = value; break;
            case 4: r = t; g = p; b = value; break;
            case 5: r = value; g = p; b = q; break;
            default: r = g = b = 0; break;
        }

        return Color.FromArgb(255, (int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            StopProcessingAsync().Wait(5000);

            _processingTimer?.Dispose();

            // Unsubscribe from events
            _audioCaptureService.AnalysisUpdated -= OnAudioAnalysisUpdated;

            _logger.LogInformation("Real-time audio processing service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing real-time audio processing service");
        }
    }

    #endregion
}

/// <summary>
/// Real-time processing status information
/// </summary>
public class RealTimeProcessingStatus
{
    public bool IsProcessing { get; set; }
    public int QueueSize { get; set; }
    public DateTime LastProcessingTime { get; set; }
    public float AverageVolume { get; set; }
    public float AverageBeatIntensity { get; set; }
    public double ProcessingLatencyMs { get; set; }
}

/// <summary>
/// Event arguments for real-time processing events
/// </summary>
public class RealTimeProcessingEventArgs : EventArgs
{
    public string ErrorMessage { get; }
    public DateTime Timestamp { get; }

    public RealTimeProcessingEventArgs(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }
}
