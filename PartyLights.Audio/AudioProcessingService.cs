using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;

namespace PartyLights.Audio;

/// <summary>
/// Background service for processing audio and updating lighting effects
/// </summary>
public class AudioProcessingService : BackgroundService
{
    private readonly ILogger<AudioProcessingService> _logger;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ILightingEffectService _lightingEffectService;
    private readonly IDeviceManagerService _deviceManagerService;

    public AudioProcessingService(
        ILogger<AudioProcessingService> logger,
        IAudioCaptureService audioCaptureService,
        ILightingEffectService lightingEffectService,
        IDeviceManagerService deviceManagerService)
    {
        _logger = logger;
        _audioCaptureService = audioCaptureService;
        _lightingEffectService = lightingEffectService;
        _deviceManagerService = deviceManagerService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audio processing service started");

        // Subscribe to audio analysis updates
        _audioCaptureService.AnalysisUpdated += OnAudioAnalysisUpdated;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Process any pending audio analysis
                await ProcessAudioAnalysisAsync();

                // Wait before next iteration
                await Task.Delay(100, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Audio processing service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in audio processing service");
        }
        finally
        {
            // Unsubscribe from events
            _audioCaptureService.AnalysisUpdated -= OnAudioAnalysisUpdated;
            _logger.LogInformation("Audio processing service stopped");
        }
    }

    private void OnAudioAnalysisUpdated(object? sender, AudioAnalysisEventArgs e)
    {
        // Queue audio analysis for processing
        _ = Task.Run(() => ProcessAudioAnalysisAsync(e.Analysis));
    }

    private async Task ProcessAudioAnalysisAsync(AudioAnalysis? analysis = null)
    {
        try
        {
            if (analysis == null)
            {
                analysis = _audioCaptureService.CurrentAnalysis;
            }

            if (analysis == null)
            {
                return;
            }

            // Get connected devices
            var devices = await _deviceManagerService.GetConnectedDevicesAsync();
            if (!devices.Any())
            {
                return;
            }

            // Process different lighting effects based on audio analysis
            await ProcessVolumeBasedBrightness(analysis, devices);
            await ProcessBeatBasedEffects(analysis, devices);
            await ProcessFrequencyBasedColors(analysis, devices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio analysis");
        }
    }

    private async Task ProcessVolumeBasedBrightness(AudioAnalysis analysis, IEnumerable<SmartDevice> devices)
    {
        try
        {
            // Map volume to brightness (0-255)
            var brightness = (int)(analysis.Volume * 255);
            brightness = Math.Clamp(brightness, 10, 255); // Minimum brightness of 10

            foreach (var device in devices)
            {
                var controller = _deviceManagerService.GetController(device.Type);
                if (controller != null)
                {
                    await controller.SetBrightnessAsync(brightness);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing volume-based brightness");
        }
    }

    private async Task ProcessBeatBasedEffects(AudioAnalysis analysis, IEnumerable<SmartDevice> devices)
    {
        try
        {
            if (analysis.BeatIntensity > 0.5f) // Strong beat detected
            {
                // Flash effect on beat
                foreach (var device in devices)
                {
                    var controller = _deviceManagerService.GetController(device.Type);
                    if (controller != null)
                    {
                        // Turn on with high brightness
                        await controller.TurnOnAsync();
                        await controller.SetBrightnessAsync(255);

                        // Quick flash effect
                        await Task.Delay(100);
                        await controller.SetBrightnessAsync(128);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing beat-based effects");
        }
    }

    private async Task ProcessFrequencyBasedColors(AudioAnalysis analysis, IEnumerable<SmartDevice> devices)
    {
        try
        {
            if (analysis.FrequencyBands.Length >= 3)
            {
                // Map frequency bands to RGB values
                var red = (int)(analysis.FrequencyBands[0] * 255);   // Low frequencies
                var green = (int)(analysis.FrequencyBands[1] * 255); // Mid frequencies  
                var blue = (int)(analysis.FrequencyBands[2] * 255);   // High frequencies

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
            _logger.LogError(ex, "Error processing frequency-based colors");
        }
    }
}
