using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;

namespace PartyLights.Audio;

/// <summary>
/// Advanced lighting effect processor using audio analysis pipeline
/// </summary>
public class AdvancedLightingEffectProcessor
{
    private readonly ILogger<AdvancedLightingEffectProcessor> _logger;
    private readonly IDeviceManagerService _deviceManagerService;
    private readonly ConcurrentQueue<AudioAnalysisResult> _analysisQueue;
    private readonly LightingEffectConfig _config;

    private bool _isProcessing;
    private Task? _processingTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private AudioAnalysisResult? _lastAnalysis;

    public AdvancedLightingEffectProcessor(
        ILogger<AdvancedLightingEffectProcessor> logger,
        IDeviceManagerService deviceManagerService,
        LightingEffectConfig config)
    {
        _logger = logger;
        _deviceManagerService = deviceManagerService;
        _config = config;
        _analysisQueue = new ConcurrentQueue<AudioAnalysisResult>();
    }

    public event EventHandler<LightingEffectEventArgs>? EffectApplied;

    /// <summary>
    /// Starts the lighting effect processor
    /// </summary>
    public void Start()
    {
        if (_isProcessing)
        {
            _logger.LogWarning("Lighting effect processor is already running");
            return;
        }

        _logger.LogInformation("Starting advanced lighting effect processor");

        _cancellationTokenSource = new CancellationTokenSource();
        _isProcessing = true;

        _processingTask = Task.Run(ProcessLightingEffects, _cancellationTokenSource.Token);

        _logger.LogInformation("Advanced lighting effect processor started");
    }

    /// <summary>
    /// Stops the lighting effect processor
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isProcessing)
        {
            _logger.LogWarning("Lighting effect processor is not running");
            return;
        }

        _logger.LogInformation("Stopping advanced lighting effect processor");

        _isProcessing = false;
        _cancellationTokenSource?.Cancel();

        if (_processingTask != null)
        {
            await _processingTask;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _logger.LogInformation("Advanced lighting effect processor stopped");
    }

    /// <summary>
    /// Adds an audio analysis result for processing
    /// </summary>
    public void AddAnalysisResult(AudioAnalysisResult result)
    {
        if (_isProcessing)
        {
            _analysisQueue.Enqueue(result);
        }
    }

    private async Task ProcessLightingEffects()
    {
        _logger.LogInformation("Lighting effect processing started");

        while (_isProcessing && !_cancellationTokenSource!.Token.IsCancellationRequested)
        {
            try
            {
                if (_analysisQueue.TryDequeue(out var analysis))
                {
                    await ProcessAnalysisAsync(analysis);
                    _lastAnalysis = analysis;
                }
                else
                {
                    await Task.Delay(50, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing lighting effects");
                await Task.Delay(100);
            }
        }

        _logger.LogInformation("Lighting effect processing stopped");
    }

    private async Task ProcessAnalysisAsync(AudioAnalysisResult analysis)
    {
        try
        {
            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            if (!devices.Any())
            {
                return;
            }

            // Process different lighting effects based on configuration
            if (_config.EnableBeatSync)
            {
                await ProcessBeatSyncEffect(analysis, devices);
            }

            if (_config.EnableFrequencyVisualization)
            {
                await ProcessFrequencyVisualization(analysis, devices);
            }

            if (_config.EnableMoodLighting)
            {
                await ProcessMoodLighting(analysis, devices);
            }

            if (_config.EnableSpectrumAnalyzer)
            {
                await ProcessSpectrumAnalyzer(analysis, devices);
            }

            if (_config.EnablePartyMode)
            {
                await ProcessPartyMode(analysis, devices);
            }

            // Fire effect applied event
            EffectApplied?.Invoke(this, new LightingEffectEventArgs(analysis));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing analysis for lighting effects");
        }
    }

    private async Task ProcessBeatSyncEffect(AudioAnalysisResult analysis, IEnumerable<SmartDevice> devices)
    {
        try
        {
            var features = AudioFeatureExtractor.ExtractBeatSyncFeatures(analysis);

            if (features.BeatDetected)
            {
                // Strong beat - flash effect
                foreach (var device in devices)
                {
                    var controller = _deviceManagerService.GetController(device.Type);
                    if (controller != null)
                    {
                        await controller.TurnOnAsync();
                        await controller.SetBrightnessAsync(255);

                        // Quick flash
                        await Task.Delay(100);
                        await controller.SetBrightnessAsync((int)(128 * features.BeatIntensity));
                    }
                }
            }
            else
            {
                // Volume-based brightness
                var brightness = Math.Clamp((int)(features.Volume * 255), 10, 255);

                foreach (var device in devices)
                {
                    var controller = _deviceManagerService.GetController(device.Type);
                    if (controller != null)
                    {
                        await controller.SetBrightnessAsync(brightness);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing beat sync effect");
        }
    }

    private async Task ProcessFrequencyVisualization(AudioAnalysisResult analysis, IEnumerable<SmartDevice> devices)
    {
        try
        {
            var features = AudioFeatureExtractor.ExtractFrequencyFeatures(analysis);

            if (features.FrequencyBands.Length >= 3)
            {
                // Map frequency bands to RGB
                var red = (int)(features.FrequencyBands[0] * 255);   // Low frequencies
                var green = (int)(features.FrequencyBands[1] * 255); // Mid frequencies
                var blue = (int)(features.FrequencyBands[2] * 255);   // High frequencies

                // Clamp values
                red = Math.Clamp(red, 0, 255);
                green = Math.Clamp(green, 0, 255);
                blue = Math.Clamp(blue, 0, 255);

                foreach (var device in devices)
                {
                    var controller = _deviceManagerService.GetController(device.Type);
                    if (controller != null)
                    {
                        await controller.SetColorAsync(red, green, blue);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing frequency visualization");
        }
    }

    private async Task ProcessMoodLighting(AudioAnalysisResult analysis, IEnumerable<SmartDevice> devices)
    {
        try
        {
            var features = AudioFeatureExtractor.ExtractMoodFeatures(analysis);

            // Map mood to colors
            var (red, green, blue) = GetMoodColor(features.Mood, features.Intensity);

            foreach (var device in devices)
            {
                var controller = _deviceManagerService.GetController(device.Type);
                if (controller != null)
                {
                    await controller.SetColorAsync(red, green, blue);

                    // Adjust brightness based on arousal
                    var brightness = (int)(128 + features.Arousal * 127);
                    await controller.SetBrightnessAsync(Math.Clamp(brightness, 10, 255));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing mood lighting");
        }
    }

    private async Task ProcessSpectrumAnalyzer(AudioAnalysisResult analysis, IEnumerable<SmartDevice> devices)
    {
        try
        {
            var features = AudioFeatureExtractor.ExtractSpectrumFeatures(analysis);

            // Create a spectrum visualization effect
            if (features.FrequencyBands.Length > 0)
            {
                // Find the dominant frequency band
                var maxBandIndex = 0;
                var maxValue = features.FrequencyBands[0];

                for (int i = 1; i < features.FrequencyBands.Length; i++)
                {
                    if (features.FrequencyBands[i] > maxValue)
                    {
                        maxValue = features.FrequencyBands[i];
                        maxBandIndex = i;
                    }
                }

                // Map band index to hue
                var hue = (maxBandIndex * 360.0f) / features.FrequencyBands.Length;
                var (red, green, blue) = HsvToRgb(hue, 1.0f, 1.0f);

                foreach (var device in devices)
                {
                    var controller = _deviceManagerService.GetController(device.Type);
                    if (controller != null)
                    {
                        await controller.SetColorAsync(red, green, blue);

                        // Brightness based on spectral flux
                        var brightness = (int)(features.SpectralFlux * 255);
                        await controller.SetBrightnessAsync(Math.Clamp(brightness, 10, 255));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing spectrum analyzer");
        }
    }

    private async Task ProcessPartyMode(AudioAnalysisResult analysis, IEnumerable<SmartDevice> devices)
    {
        try
        {
            var features = AudioFeatureExtractor.ExtractPartyFeatures(analysis);

            // Dynamic party mode with multiple effects
            if (features.BeatDetected)
            {
                // Beat-driven color changes
                var hue = (DateTime.UtcNow.Millisecond / 1000.0f) * 360.0f;
                var (red, green, blue) = HsvToRgb(hue, 1.0f, 1.0f);

                foreach (var device in devices)
                {
                    var controller = _deviceManagerService.GetController(device.Type);
                    if (controller != null)
                    {
                        await controller.SetColorAsync(red, green, blue);
                        await controller.SetBrightnessAsync(255);
                    }
                }
            }
            else
            {
                // Energy-based effects
                var intensity = features.Energy;
                var brightness = (int)(intensity * 255);

                foreach (var device in devices)
                {
                    var controller = _deviceManagerService.GetController(device.Type);
                    if (controller != null)
                    {
                        await controller.SetBrightnessAsync(Math.Clamp(brightness, 10, 255));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing party mode");
        }
    }

    private (int red, int green, int blue) GetMoodColor(AudioMood mood, float intensity)
    {
        return mood switch
        {
            AudioMood.Happy => HsvToRgb(60, 1.0f, intensity),   // Yellow
            AudioMood.Sad => HsvToRgb(240, 1.0f, intensity),    // Blue
            AudioMood.Excited => HsvToRgb(0, 1.0f, intensity),  // Red
            AudioMood.Calm => HsvToRgb(180, 1.0f, intensity),  // Cyan
            AudioMood.Angry => HsvToRgb(0, 1.0f, intensity),   // Red
            _ => HsvToRgb(0, 0, intensity)                     // White
        };
    }

    private (int red, int green, int blue) HsvToRgb(float h, float s, float v)
    {
        h = h % 360;
        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        var m = v - c;

        float r, g, b;
        if (h < 60)
        {
            r = c; g = x; b = 0;
        }
        else if (h < 120)
        {
            r = x; g = c; b = 0;
        }
        else if (h < 180)
        {
            r = 0; g = c; b = x;
        }
        else if (h < 240)
        {
            r = 0; g = x; b = c;
        }
        else if (h < 300)
        {
            r = x; g = 0; b = c;
        }
        else
        {
            r = c; g = 0; b = x;
        }

        return (
            (int)((r + m) * 255),
            (int)((g + m) * 255),
            (int)((b + m) * 255)
        );
    }

    /// <summary>
    /// Updates the lighting effect configuration
    /// </summary>
    public void UpdateConfig(LightingEffectConfig config)
    {
        _config.EnableBeatSync = config.EnableBeatSync;
        _config.EnableFrequencyVisualization = config.EnableFrequencyVisualization;
        _config.EnableMoodLighting = config.EnableMoodLighting;
        _config.EnableSpectrumAnalyzer = config.EnableSpectrumAnalyzer;
        _config.EnablePartyMode = config.EnablePartyMode;

        _logger.LogInformation("Lighting effect configuration updated");
    }

    /// <summary>
    /// Gets the last analysis result
    /// </summary>
    public AudioAnalysisResult? GetLastAnalysis()
    {
        return _lastAnalysis;
    }
}

/// <summary>
/// Configuration for lighting effects
/// </summary>
public class LightingEffectConfig
{
    public bool EnableBeatSync { get; set; } = true;
    public bool EnableFrequencyVisualization { get; set; } = true;
    public bool EnableMoodLighting { get; set; } = true;
    public bool EnableSpectrumAnalyzer { get; set; } = true;
    public bool EnablePartyMode { get; set; } = false;
    public float EffectIntensity { get; set; } = 1.0f;
    public int EffectSpeed { get; set; } = 1;
}

/// <summary>
/// Event args for lighting effect application
/// </summary>
public class LightingEffectEventArgs : EventArgs
{
    public AudioAnalysisResult Analysis { get; }

    public LightingEffectEventArgs(AudioAnalysisResult analysis)
    {
        Analysis = analysis;
    }
}
