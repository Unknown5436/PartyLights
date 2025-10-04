using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;

namespace PartyLights.Services;

/// <summary>
/// Background service for audio processing
/// </summary>
public class AudioProcessingService : BackgroundService
{
    private readonly ILogger<AudioProcessingService> _logger;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ILightingEffectService _lightingEffectService;

    public AudioProcessingService(
        ILogger<AudioProcessingService> logger,
        IAudioCaptureService audioCaptureService,
        ILightingEffectService lightingEffectService)
    {
        _logger = logger;
        _audioCaptureService = audioCaptureService;
        _lightingEffectService = lightingEffectService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audio processing service started");

        // Subscribe to audio analysis events
        _audioCaptureService.AnalysisUpdated += OnAudioAnalysisUpdated;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // TODO: Implement continuous audio processing logic
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Audio processing service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in audio processing service");
        }
    }

    private async void OnAudioAnalysisUpdated(object? sender, Core.Models.AudioAnalysisEventArgs e)
    {
        try
        {
            // TODO: Process audio analysis and apply lighting effects
            _logger.LogDebug("Processing audio analysis: Volume={Volume}, Beat={Beat}",
                e.Analysis.Volume, e.Analysis.BeatIntensity);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio analysis");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping audio processing service");

        // Unsubscribe from events
        _audioCaptureService.AnalysisUpdated -= OnAudioAnalysisUpdated;

        await base.StopAsync(cancellationToken);
    }
}
