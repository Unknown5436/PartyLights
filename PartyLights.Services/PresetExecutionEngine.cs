using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;

namespace PartyLights.Services;

/// <summary>
/// Engine for executing lighting presets in real-time
/// </summary>
public class PresetExecutionEngine
{
    private readonly ILogger<PresetExecutionEngine> _logger;
    private readonly IAdvancedDeviceControlService _deviceControlService;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ConcurrentDictionary<string, PresetExecutionContext> _activeExecutions = new();
    private readonly Timer _executionTimer;
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);

    public event EventHandler<PresetExecutionEventArgs>? PresetExecutionStarted;
    public event EventHandler<PresetExecutionEventArgs>? PresetExecutionStopped;
    public event EventHandler<PresetExecutionErrorEventArgs>? PresetExecutionError;

    public PresetExecutionEngine(
        ILogger<PresetExecutionEngine> logger,
        IAdvancedDeviceControlService deviceControlService,
        IAudioCaptureService audioCaptureService)
    {
        _logger = logger;
        _deviceControlService = deviceControlService;
        _audioCaptureService = audioCaptureService;

        // Set up execution timer (runs every 50ms for smooth effects)
        _executionTimer = new Timer(ExecutePresetEffects, null, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));

        // Subscribe to audio analysis updates
        _audioCaptureService.AnalysisUpdated += OnAudioAnalysisUpdated;
    }

    #region Preset Execution Management

    /// <summary>
    /// Starts executing a preset
    /// </summary>
    public async Task<bool> StartPresetExecutionAsync(PresetExecutionContext context)
    {
        try
        {
            await _executionSemaphore.WaitAsync();

            _logger.LogInformation("Starting preset execution: {PresetId}", context.PresetId);

            // Stop any existing execution of the same preset
            await StopPresetExecutionAsync(context.PresetId);

            context.IsActive = true;
            context.StartedAt = DateTime.UtcNow;
            context.ExecutionId = Guid.NewGuid().ToString();

            _activeExecutions.TryAdd(context.ExecutionId, context);

            // Initialize preset-specific execution state
            await InitializePresetExecutionAsync(context);

            PresetExecutionStarted?.Invoke(this, new PresetExecutionEventArgs(context));
            _logger.LogInformation("Preset execution started successfully: {ExecutionId}", context.ExecutionId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting preset execution: {PresetId}", context.PresetId);
            PresetExecutionError?.Invoke(this, new PresetExecutionErrorEventArgs(context.PresetId, ex.Message));
            return false;
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    /// <summary>
    /// Stops executing a preset
    /// </summary>
    public async Task<bool> StopPresetExecutionAsync(string presetId)
    {
        try
        {
            await _executionSemaphore.WaitAsync();

            var executions = _activeExecutions.Values.Where(e => e.PresetId == presetId).ToList();
            var success = true;

            foreach (var execution in executions)
            {
                if (await StopExecutionAsync(execution))
                {
                    PresetExecutionStopped?.Invoke(this, new PresetExecutionEventArgs(execution));
                }
                else
                {
                    success = false;
                }
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping preset execution: {PresetId}", presetId);
            return false;
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    /// <summary>
    /// Stops all preset executions
    /// </summary>
    public async Task<bool> StopAllPresetExecutionsAsync()
    {
        try
        {
            await _executionSemaphore.WaitAsync();

            var executions = _activeExecutions.Values.ToList();
            var tasks = executions.Select(e => StopExecutionAsync(e));
            var results = await Task.WhenAll(tasks);

            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping all preset executions");
            return false;
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets active preset executions
    /// </summary>
    public async Task<IEnumerable<PresetExecutionContext>> GetActiveExecutionsAsync()
    {
        await Task.CompletedTask;
        return _activeExecutions.Values.Where(e => e.IsActive).ToList();
    }

    #endregion

    #region Preset Effect Execution

    private async void ExecutePresetEffects(object? state)
    {
        try
        {
            if (_activeExecutions.IsEmpty)
                return;

            var executions = _activeExecutions.Values.Where(e => e.IsActive).ToList();
            var tasks = executions.Select(ExecutePresetEffectAsync);
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing preset effects");
        }
    }

    private async Task ExecutePresetEffectAsync(PresetExecutionContext context)
    {
        try
        {
            // Get current audio analysis
            var audioAnalysis = _audioCaptureService.GetCurrentAnalysis();
            if (audioAnalysis == null)
                return;

            // Execute preset-specific effects
            switch (context.PresetId)
            {
                case "builtin_beatsync":
                    await ExecuteBeatSyncEffectAsync(context, audioAnalysis);
                    break;
                case "builtin_frequencyviz":
                    await ExecuteFrequencyVisualizationEffectAsync(context, audioAnalysis);
                    break;
                case "builtin_volumereactive":
                    await ExecuteVolumeReactiveEffectAsync(context, audioAnalysis);
                    break;
                case "builtin_moodlighting":
                    await ExecuteMoodLightingEffectAsync(context, audioAnalysis);
                    break;
                case "builtin_partymode":
                    await ExecutePartyModeEffectAsync(context, audioAnalysis);
                    break;
                default:
                    await ExecuteCustomPresetEffectAsync(context, audioAnalysis);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing preset effect: {PresetId}", context.PresetId);
        }
    }

    private async Task ExecuteBeatSyncEffectAsync(PresetExecutionContext context, AudioAnalysis audioAnalysis)
    {
        try
        {
            if (audioAnalysis.BeatIntensity > 0.5f) // Beat detected
            {
                var beatSensitivity = GetParameterValue(context.RuntimeParameters, "beatSensitivity", 0.7f);
                var flashDuration = GetParameterValue(context.RuntimeParameters, "flashDuration", 200);
                var brightnessMultiplier = GetParameterValue(context.RuntimeParameters, "brightnessMultiplier", 1.0f);

                if (audioAnalysis.BeatIntensity >= beatSensitivity)
                {
                    // Flash effect on beat
                    var brightness = (int)(255 * brightnessMultiplier * audioAnalysis.BeatIntensity);

                    // Apply to all target devices
                    foreach (var deviceId in context.TargetDeviceIds)
                    {
                        await _deviceControlService.SetBrightnessToAllDevicesAsync(brightness);
                    }

                    // Apply to device groups
                    foreach (var groupId in context.TargetDeviceGroupIds)
                    {
                        var groups = await _deviceControlService.GetDeviceGroupsAsync();
                        var group = groups.FirstOrDefault(g => g.Id == groupId);
                        if (group != null)
                        {
                            await _deviceControlService.SetBrightnessToDeviceGroupAsync(group.Name, brightness);
                        }
                    }

                    // Brief delay for flash effect
                    await Task.Delay(flashDuration);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing beat sync effect");
        }
    }

    private async Task ExecuteFrequencyVisualizationEffectAsync(PresetExecutionContext context, AudioAnalysis audioAnalysis)
    {
        try
        {
            var sensitivity = GetParameterValue(context.RuntimeParameters, "sensitivity", 0.8f);
            var smoothing = GetParameterValue(context.RuntimeParameters, "smoothing", 0.3f);

            // Map frequency bands to colors
            var lowFreqColor = GetParameterValue(context.RuntimeParameters, "lowFreqColor", "Red");
            var midFreqColor = GetParameterValue(context.RuntimeParameters, "midFreqColor", "Green");
            var highFreqColor = GetParameterValue(context.RuntimeParameters, "highFreqColor", "Blue");

            // Calculate dominant frequency band
            var lowFreq = audioAnalysis.FrequencyBands.Take(4).Average();
            var midFreq = audioAnalysis.FrequencyBands.Skip(4).Take(4).Average();
            var highFreq = audioAnalysis.FrequencyBands.Skip(8).Average();

            var maxFreq = Math.Max(Math.Max(lowFreq, midFreq), highFreq);
            var brightness = (int)(255 * sensitivity * (maxFreq / 100.0f));

            if (brightness > 10) // Only apply if there's significant audio
            {
                int r = 0, g = 0, b = 0;

                if (maxFreq == lowFreq)
                {
                    // Low frequency dominant - red
                    r = brightness;
                }
                else if (maxFreq == midFreq)
                {
                    // Mid frequency dominant - green
                    g = brightness;
                }
                else
                {
                    // High frequency dominant - blue
                    b = brightness;
                }

                // Apply color to devices
                foreach (var deviceId in context.TargetDeviceIds)
                {
                    await _deviceControlService.SetColorToAllDevicesAsync(r, g, b);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing frequency visualization effect");
        }
    }

    private async Task ExecuteVolumeReactiveEffectAsync(PresetExecutionContext context, AudioAnalysis audioAnalysis)
    {
        try
        {
            var volumeSensitivity = GetParameterValue(context.RuntimeParameters, "volumeSensitivity", 0.6f);
            var minBrightness = GetParameterValue(context.RuntimeParameters, "minBrightness", 10);
            var maxBrightness = GetParameterValue(context.RuntimeParameters, "maxBrightness", 255);
            var smoothing = GetParameterValue(context.RuntimeParameters, "smoothing", 0.5f);

            // Calculate brightness based on volume
            var volume = audioAnalysis.Volume * volumeSensitivity;
            var brightness = (int)Math.Clamp(
                minBrightness + (maxBrightness - minBrightness) * volume,
                minBrightness,
                maxBrightness
            );

            // Apply brightness to devices
            foreach (var deviceId in context.TargetDeviceIds)
            {
                await _deviceControlService.SetBrightnessToAllDevicesAsync(brightness);
            }

            // Apply to device groups
            foreach (var groupId in context.TargetDeviceGroupIds)
            {
                var groups = await _deviceControlService.GetDeviceGroupsAsync();
                var group = groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                {
                    await _deviceControlService.SetBrightnessToDeviceGroupAsync(group.Name, brightness);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing volume reactive effect");
        }
    }

    private async Task ExecuteMoodLightingEffectAsync(PresetExecutionContext context, AudioAnalysis audioAnalysis)
    {
        try
        {
            var moodSensitivity = GetParameterValue(context.RuntimeParameters, "moodSensitivity", 0.8f);
            var transitionSpeed = GetParameterValue(context.RuntimeParameters, "transitionSpeed", 2.0f);
            var colorIntensity = GetParameterValue(context.RuntimeParameters, "colorIntensity", 0.9f);

            // Determine mood-based colors
            int r = 0, g = 0, b = 0;

            if (audioAnalysis.MoodInfo != null)
            {
                var energy = audioAnalysis.MoodInfo.Energy;
                var valence = audioAnalysis.MoodInfo.Valence;

                if (energy > 0.7f && valence > 0.5f)
                {
                    // High energy, positive - bright warm colors
                    r = (int)(255 * colorIntensity);
                    g = (int)(200 * colorIntensity);
                    b = (int)(100 * colorIntensity);
                }
                else if (energy > 0.7f && valence < 0.3f)
                {
                    // High energy, negative - bright cool colors
                    r = (int)(100 * colorIntensity);
                    g = (int)(150 * colorIntensity);
                    b = (int)(255 * colorIntensity);
                }
                else if (energy < 0.3f && valence > 0.5f)
                {
                    // Low energy, positive - soft warm colors
                    r = (int)(180 * colorIntensity);
                    g = (int)(220 * colorIntensity);
                    b = (int)(150 * colorIntensity);
                }
                else if (energy < 0.3f && valence < 0.3f)
                {
                    // Low energy, negative - soft cool colors
                    r = (int)(120 * colorIntensity);
                    g = (int)(150 * colorIntensity);
                    b = (int)(200 * colorIntensity);
                }
                else
                {
                    // Neutral - balanced colors
                    r = (int)(200 * colorIntensity);
                    g = (int)(200 * colorIntensity);
                    b = (int)(200 * colorIntensity);
                }
            }

            // Apply mood-based colors
            foreach (var deviceId in context.TargetDeviceIds)
            {
                await _deviceControlService.SetColorToAllDevicesAsync(r, g, b);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing mood lighting effect");
        }
    }

    private async Task ExecutePartyModeEffectAsync(PresetExecutionContext context, AudioAnalysis audioAnalysis)
    {
        try
        {
            var effectSpeed = GetParameterValue(context.RuntimeParameters, "effectSpeed", 3.0f);
            var colorCycleSpeed = GetParameterValue(context.RuntimeParameters, "colorCycleSpeed", 2.0f);
            var strobeEnabled = GetParameterValue(context.RuntimeParameters, "strobeEnabled", true);
            var strobeFrequency = GetParameterValue(context.RuntimeParameters, "strobeFrequency", 0.5f);
            var rainbowEnabled = GetParameterValue(context.RuntimeParameters, "rainbowEnabled", true);
            var intensity = GetParameterValue(context.RuntimeParameters, "intensity", 1.0f);

            // Calculate time-based effects
            var time = DateTime.UtcNow.Millisecond / 1000.0f;
            var cyclePosition = (time * colorCycleSpeed) % 1.0f;

            if (rainbowEnabled)
            {
                // Rainbow color cycling
                var hue = (int)(cyclePosition * 360);
                var (r, g, b) = HsvToRgb(hue, 1.0f, intensity);

                foreach (var deviceId in context.TargetDeviceIds)
                {
                    await _deviceControlService.SetColorToAllDevicesAsync(r, g, b);
                }
            }

            if (strobeEnabled && audioAnalysis.BeatIntensity > strobeFrequency)
            {
                // Strobe effect on beat
                var strobeBrightness = (int)(255 * intensity);
                await _deviceControlService.SetBrightnessToAllDevicesAsync(strobeBrightness);
                await Task.Delay(50); // Brief strobe
                await _deviceControlService.SetBrightnessToAllDevicesAsync(0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing party mode effect");
        }
    }

    private async Task ExecuteCustomPresetEffectAsync(PresetExecutionContext context, AudioAnalysis audioAnalysis)
    {
        try
        {
            // Execute custom preset logic based on parameters
            _logger.LogDebug("Executing custom preset effect: {PresetId}", context.PresetId);

            // This would be extended to support custom preset types
            // For now, we'll apply a basic volume-reactive effect
            var volume = audioAnalysis.Volume;
            var brightness = (int)(255 * volume);

            foreach (var deviceId in context.TargetDeviceIds)
            {
                await _deviceControlService.SetBrightnessToAllDevicesAsync(brightness);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing custom preset effect");
        }
    }

    #endregion

    #region Private Methods

    private async Task<bool> StopExecutionAsync(PresetExecutionContext execution)
    {
        try
        {
            execution.IsActive = false;
            _activeExecutions.TryRemove(execution.ExecutionId, out _);

            // Clean up preset-specific state
            await CleanupPresetExecutionAsync(execution);

            _logger.LogInformation("Preset execution stopped: {ExecutionId}", execution.ExecutionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping execution: {ExecutionId}", execution.ExecutionId);
            return false;
        }
    }

    private async Task InitializePresetExecutionAsync(PresetExecutionContext context)
    {
        try
        {
            // Initialize any preset-specific state
            _logger.LogDebug("Initializing preset execution: {PresetId}", context.PresetId);

            // This could include setting up effect-specific variables,
            // initializing color palettes, etc.

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing preset execution");
        }
    }

    private async Task CleanupPresetExecutionAsync(PresetExecutionContext context)
    {
        try
        {
            // Clean up preset-specific state
            _logger.LogDebug("Cleaning up preset execution: {PresetId}", context.PresetId);

            // This could include resetting device states,
            // clearing effect variables, etc.

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up preset execution");
        }
    }

    private async void OnAudioAnalysisUpdated(object? sender, AudioAnalysisEventArgs e)
    {
        try
        {
            // Audio analysis updated - presets will use this in their execution
            // The actual execution happens in the timer callback
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling audio analysis update");
        }
    }

    private T GetParameterValue<T>(Dictionary<string, object> parameters, string key, T defaultValue)
    {
        try
        {
            if (parameters.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private (int r, int g, int b) HsvToRgb(int h, float s, float v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60.0f) % 2 - 1));
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

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _executionTimer?.Dispose();
        _executionSemaphore?.Dispose();
    }

    #endregion
}

/// <summary>
/// Event arguments for preset execution error events
/// </summary>
public class PresetExecutionErrorEventArgs : EventArgs
{
    public string PresetId { get; }
    public string ErrorMessage { get; }
    public DateTime ErrorTime { get; }

    public PresetExecutionErrorEventArgs(string presetId, string errorMessage)
    {
        PresetId = presetId;
        ErrorMessage = errorMessage;
        ErrorTime = DateTime.UtcNow;
    }
}
