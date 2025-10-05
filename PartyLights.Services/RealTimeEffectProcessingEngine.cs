using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;

namespace PartyLights.Services;

/// <summary>
/// Real-time effect processing engine for smooth lighting transitions
/// </summary>
public class RealTimeEffectProcessingEngine : IDisposable
{
    private readonly ILogger<RealTimeEffectProcessingEngine> _logger;
    private readonly IAdvancedDeviceControlService _deviceControlService;
    private readonly IDeviceManagerService _deviceManagerService;
    private readonly ConcurrentDictionary<string, EffectProcessor> _activeProcessors = new();
    private readonly Timer _processingTimer;
    private readonly object _lockObject = new();

    private const int ProcessingIntervalMs = 16; // ~60 FPS
    private bool _isProcessing;
    private DateTime _lastProcessingTime = DateTime.MinValue;

    // Effect state management
    private readonly Dictionary<string, EffectState> _effectStates = new();
    private readonly Dictionary<string, TransitionState> _transitionStates = new();
    private readonly Dictionary<string, AnimationState> _animationStates = new();

    public event EventHandler<EffectProcessingEventArgs>? EffectStarted;
    public event EventHandler<EffectProcessingEventArgs>? EffectStopped;
    public event EventHandler<EffectProcessingEventArgs>? EffectUpdated;
    public event EventHandler<EffectErrorEventArgs>? EffectError;

    public RealTimeEffectProcessingEngine(
        ILogger<RealTimeEffectProcessingEngine> logger,
        IAdvancedDeviceControlService deviceControlService,
        IDeviceManagerService deviceManagerService)
    {
        _logger = logger;
        _deviceControlService = deviceControlService;
        _deviceManagerService = deviceManagerService;

        _processingTimer = new Timer(ProcessEffects, null, ProcessingIntervalMs, ProcessingIntervalMs);
        _isProcessing = true;

        _logger.LogInformation("Real-time effect processing engine initialized");
    }

    /// <summary>
    /// Starts processing a lighting effect in real-time
    /// </summary>
    public async Task<string> StartEffectAsync(EffectRequest request)
    {
        try
        {
            var effectId = Guid.NewGuid().ToString();
            var processor = new EffectProcessor(effectId, request, _logger);

            if (_activeProcessors.TryAdd(effectId, processor))
            {
                // Initialize effect state
                var effectState = new EffectState
                {
                    EffectId = effectId,
                    Request = request,
                    StartTime = DateTime.UtcNow,
                    IsActive = true,
                    CurrentFrame = 0
                };

                _effectStates[effectId] = effectState;

                // Initialize transition state if needed
                if (request.TransitionDurationMs > 0)
                {
                    _transitionStates[effectId] = new TransitionState
                    {
                        EffectId = effectId,
                        StartTime = DateTime.UtcNow,
                        Duration = TimeSpan.FromMilliseconds(request.TransitionDurationMs),
                        IsTransitioning = true
                    };
                }

                // Initialize animation state
                _animationStates[effectId] = new AnimationState
                {
                    EffectId = effectId,
                    StartTime = DateTime.UtcNow,
                    CurrentPhase = 0,
                    PhaseProgress = 0f
                };

                EffectStarted?.Invoke(this, new EffectProcessingEventArgs(effectId, EffectStatus.Started));
                _logger.LogInformation("Started real-time effect: {EffectId}", effectId);

                return effectId;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting real-time effect");
            EffectError?.Invoke(this, new EffectErrorEventArgs("Failed to start effect", ex));
            return string.Empty;
        }
    }

    /// <summary>
    /// Stops processing a lighting effect
    /// </summary>
    public async Task<bool> StopEffectAsync(string effectId)
    {
        try
        {
            if (_activeProcessors.TryRemove(effectId, out var processor))
            {
                // Clean up states
                _effectStates.Remove(effectId);
                _transitionStates.Remove(effectId);
                _animationStates.Remove(effectId);

                // Stop processor
                processor.Dispose();

                EffectStopped?.Invoke(this, new EffectProcessingEventArgs(effectId, EffectStatus.Stopped));
                _logger.LogInformation("Stopped real-time effect: {EffectId}", effectId);

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping real-time effect: {EffectId}", effectId);
            EffectError?.Invoke(this, new EffectErrorEventArgs($"Failed to stop effect {effectId}", ex));
            return false;
        }
    }

    /// <summary>
    /// Updates effect parameters in real-time
    /// </summary>
    public async Task<bool> UpdateEffectAsync(string effectId, EffectUpdateRequest updateRequest)
    {
        try
        {
            if (_activeProcessors.TryGetValue(effectId, out var processor))
            {
                await processor.UpdateParametersAsync(updateRequest);

                if (_effectStates.TryGetValue(effectId, out var state))
                {
                    state.LastUpdateTime = DateTime.UtcNow;
                }

                EffectUpdated?.Invoke(this, new EffectProcessingEventArgs(effectId, EffectStatus.Updated));
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating effect: {EffectId}", effectId);
            EffectError?.Invoke(this, new EffectErrorEventArgs($"Failed to update effect {effectId}", ex));
            return false;
        }
    }

    /// <summary>
    /// Gets current status of all active effects
    /// </summary>
    public IEnumerable<EffectStatusInfo> GetActiveEffects()
    {
        lock (_lockObject)
        {
            return _effectStates.Values.Select(state => new EffectStatusInfo
            {
                EffectId = state.EffectId,
                EffectType = state.Request.EffectType,
                IsActive = state.IsActive,
                StartTime = state.StartTime,
                LastUpdateTime = state.LastUpdateTime,
                CurrentFrame = state.CurrentFrame,
                Progress = CalculateEffectProgress(state)
            }).ToList();
        }
    }

    #region Private Methods

    private async void ProcessEffects(object? state)
    {
        if (!_isProcessing)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;
            var deltaTime = (float)(currentTime - _lastProcessingTime).TotalMilliseconds;
            _lastProcessingTime = currentTime;

            // Process each active effect
            foreach (var processorEntry in _activeProcessors)
            {
                var effectId = processorEntry.Key;
                var processor = processorEntry.Value;

                try
                {
                    await ProcessEffectFrame(effectId, processor, deltaTime);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing effect frame: {EffectId}", effectId);
                    await StopEffectAsync(effectId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in effect processing loop");
        }
    }

    private async Task ProcessEffectFrame(string effectId, EffectProcessor processor, float deltaTime)
    {
        // Get current states
        if (!_effectStates.TryGetValue(effectId, out var effectState))
        {
            return;
        }

        var transitionState = _transitionStates.GetValueOrDefault(effectId);
        var animationState = _animationStates.GetValueOrDefault(effectId);

        // Update effect state
        effectState.CurrentFrame++;
        effectState.LastUpdateTime = DateTime.UtcNow;

        // Calculate effect parameters for this frame
        var frameParameters = CalculateFrameParameters(effectState, transitionState, animationState, deltaTime);

        // Process the effect frame
        await processor.ProcessFrameAsync(frameParameters);

        // Check for effect completion
        if (ShouldCompleteEffect(effectState, transitionState, animationState))
        {
            await StopEffectAsync(effectId);
        }
    }

    private EffectFrameParameters CalculateFrameParameters(
        EffectState effectState,
        TransitionState? transitionState,
        AnimationState? animationState,
        float deltaTime)
    {
        var parameters = new EffectFrameParameters
        {
            EffectId = effectState.EffectId,
            FrameNumber = effectState.CurrentFrame,
            DeltaTime = deltaTime,
            ElapsedTime = (float)(DateTime.UtcNow - effectState.StartTime).TotalMilliseconds,
            AudioData = effectState.Request.AudioData
        };

        // Calculate transition progress
        if (transitionState != null && transitionState.IsTransitioning)
        {
            var transitionProgress = (float)(DateTime.UtcNow - transitionState.StartTime).TotalMilliseconds /
                                   (float)transitionState.Duration.TotalMilliseconds;
            parameters.TransitionProgress = Math.Clamp(transitionProgress, 0f, 1f);

            if (parameters.TransitionProgress >= 1f)
            {
                transitionState.IsTransitioning = false;
            }
        }
        else
        {
            parameters.TransitionProgress = 1f;
        }

        // Calculate animation progress
        if (animationState != null)
        {
            parameters.AnimationPhase = animationState.CurrentPhase;
            parameters.PhaseProgress = animationState.PhaseProgress;

            // Update animation state
            UpdateAnimationState(animationState, deltaTime);
        }

        // Calculate effect-specific parameters
        CalculateEffectSpecificParameters(parameters, effectState.Request);

        return parameters;
    }

    private void CalculateEffectSpecificParameters(EffectFrameParameters parameters, EffectRequest request)
    {
        switch (request.EffectType)
        {
            case EffectType.BeatSync:
                CalculateBeatSyncParameters(parameters, request);
                break;
            case EffectType.FrequencyVisualization:
                CalculateFrequencyVisualizationParameters(parameters, request);
                break;
            case EffectType.VolumeReactive:
                CalculateVolumeReactiveParameters(parameters, request);
                break;
            case EffectType.MoodLighting:
                CalculateMoodLightingParameters(parameters, request);
                break;
            case EffectType.SpectrumAnalyzer:
                CalculateSpectrumAnalyzerParameters(parameters, request);
                break;
            case EffectType.PartyMode:
                CalculatePartyModeParameters(parameters, request);
                break;
            case EffectType.Rainbow:
                CalculateRainbowParameters(parameters, request);
                break;
            case EffectType.Strobe:
                CalculateStrobeParameters(parameters, request);
                break;
            case EffectType.Pulse:
                CalculatePulseParameters(parameters, request);
                break;
            case EffectType.Wave:
                CalculateWaveParameters(parameters, request);
                break;
            default:
                CalculateDefaultParameters(parameters, request);
                break;
        }
    }

    private void CalculateBeatSyncParameters(EffectFrameParameters parameters, EffectRequest request)
    {
        if (parameters.AudioData?.IsBeatDetected == true)
        {
            parameters.Intensity = parameters.AudioData.BeatStrength * request.IntensityMultiplier;
            parameters.ColorHue = (parameters.ElapsedTime * request.ColorSpeed) % 360f;
            parameters.ColorSaturation = 1f;
            parameters.ColorValue = Math.Min(parameters.Intensity, 1f);
        }
        else
        {
            parameters.Intensity = parameters.AudioData?.Volume ?? 0.5f;
            parameters.ColorHue = (parameters.ElapsedTime * request.ColorSpeed * 0.1f) % 360f;
            parameters.ColorSaturation = 0.7f;
            parameters.ColorValue = 0.3f;
        }
    }

    private void CalculateFrequencyVisualizationParameters(EffectFrameParameters parameters, EffectRequest request)
    {
        if (parameters.AudioData?.FrequencyBandsDetailed != null)
        {
            var bands = parameters.AudioData.FrequencyBandsDetailed;
            var dominantBand = bands.OrderByDescending(b => b.Intensity).FirstOrDefault();

            if (dominantBand != null)
            {
                parameters.ColorHue = MapFrequencyToHue(dominantBand.FrequencyLow, dominantBand.FrequencyHigh);
                parameters.ColorSaturation = Math.Min(dominantBand.Intensity * 2f, 1f);
                parameters.ColorValue = Math.Min(dominantBand.Intensity * 1.5f, 1f);
                parameters.Intensity = dominantBand.Intensity * request.IntensityMultiplier;
            }
        }
    }

    private void CalculateVolumeReactiveParameters(EffectFrameParameters parameters, EffectRequest request)
    {
        var volume = parameters.AudioData?.Volume ?? 0.5f;
        parameters.Intensity = volume * request.IntensityMultiplier;
        parameters.ColorValue = Math.Min(volume * 2f, 1f);
        parameters.ColorSaturation = 0.8f;
        parameters.ColorHue = request.BaseHue;
    }

    private void CalculateMoodLightingParameters(EffectFrameParameters parameters, EffectRequest request)
    {
        if (parameters.AudioData != null)
        {
            // Map mood dimensions to color
            parameters.ColorHue = parameters.AudioData.Valence * 120f; // 0-120 degrees
            parameters.ColorSaturation = parameters.AudioData.Arousal;
            parameters.ColorValue = parameters.AudioData.Energy;
            parameters.Intensity = parameters.AudioData.Energy * request.IntensityMultiplier;
        }
    }

    private void CalculateSpectrumAnalyzerParameters(EffectFrameParameters parameters, EffectRequest request)
    {
        // Multi-device spectrum visualization
        parameters.DeviceSpecificParameters = new Dictionary<string, DeviceEffectParameters>();

        if (parameters.AudioData?.FrequencyBandsDetailed != null)
        {
            var bands = parameters.AudioData.FrequencyBandsDetailed;
            for (int i = 0; i < Math.Min(bands.Count, request.TargetDevices.Count); i++)
            {
                var deviceId = request.TargetDevices[i];
                var band = bands[i];

                parameters.DeviceSpecificParameters[deviceId] = new DeviceEffectParameters
                {
                    ColorHue = MapFrequencyToHue(band.FrequencyLow, band.FrequencyHigh),
                    ColorSaturation = Math.Min(band.Intensity * 2f, 1f),
                    ColorValue = Math.Min(band.Intensity * 1.5f, 1f),
                    Intensity = band.Intensity * request.IntensityMultiplier
                };
            }
        }
    }

    private void CalculatePartyModeParameters(EffectFrameParameters parameters, EffectRequest request)
    {
        // Combine multiple effects for party mode
        if (parameters.AudioData?.IsBeatDetected == true)
        {
            CalculateBeatSyncParameters(parameters, request);
        }
        else
        {
            CalculateFrequencyVisualizationParameters(parameters, request);
        }

        // Add strobe effect on high energy
        if (parameters.AudioData?.Energy > 0.8f)
        {
            parameters.StrobeEnabled = true;
            parameters.StrobeFrequency = 10f + (parameters.AudioData.Energy * 20f);
        }
    }

    private void CalculateRainbowParameters(EffectFrameParameters parameters, EffectRequest request)
    {
        parameters.ColorHue = (parameters.ElapsedTime * request.ColorSpeed) % 360f;
        parameters.ColorSaturation = 1f;
        parameters.ColorValue = request.IntensityMultiplier;
        parameters.Intensity = request.IntensityMultiplier;
    }

    private void CalculateStrobeParameters(EffectFrameParameters parameters, EffectRequest request)
    {
        var strobePhase = (parameters.ElapsedTime * request.StrobeFrequency) % 1000f;
        parameters.StrobeEnabled = strobePhase < 500f;
        parameters.Intensity = parameters.StrobeEnabled ? request.IntensityMultiplier : 0f;
        parameters.ColorHue = request.BaseHue;
        parameters.ColorSaturation = 1f;
        parameters.ColorValue = parameters.StrobeEnabled ? 1f : 0f;
    }

    private void CalculatePulseParameters(EffectFrameParameters parameters, EffectRequest request)
    {
        var pulsePhase = (parameters.ElapsedTime * request.PulseFrequency) % 2000f;
        var pulseIntensity = (float)Math.Sin(pulsePhase * Math.PI / 1000f) * 0.5f + 0.5f;

        parameters.Intensity = pulseIntensity * request.IntensityMultiplier;
        parameters.ColorHue = request.BaseHue;
        parameters.ColorSaturation = 1f;
        parameters.ColorValue = pulseIntensity;
    }

    private void CalculateWaveParameters(EffectFrameParameters parameters, EffectRequest request)
    {
        var wavePhase = (parameters.ElapsedTime * request.WaveSpeed) % 2000f;
        var waveIntensity = (float)Math.Sin(wavePhase * Math.PI / 1000f) * 0.5f + 0.5f;

        parameters.Intensity = waveIntensity * request.IntensityMultiplier;
        parameters.ColorHue = (parameters.ElapsedTime * request.ColorSpeed) % 360f;
        parameters.ColorSaturation = 1f;
        parameters.ColorValue = waveIntensity;
    }

    private void CalculateDefaultParameters(EffectFrameParameters parameters, EffectRequest request)
    {
        parameters.Intensity = request.IntensityMultiplier;
        parameters.ColorHue = request.BaseHue;
        parameters.ColorSaturation = 0.8f;
        parameters.ColorValue = 1f;
    }

    private float MapFrequencyToHue(float freqLow, float freqHigh)
    {
        var centerFreq = (freqLow + freqHigh) / 2f;
        return (centerFreq / 20000f) * 360f; // Map to 0-360 degrees
    }

    private void UpdateAnimationState(AnimationState animationState, float deltaTime)
    {
        // Update animation phase based on time
        var phaseDuration = 2000f; // 2 seconds per phase
        var totalPhaseTime = animationState.CurrentPhase * phaseDuration;
        var currentPhaseTime = (float)(DateTime.UtcNow - animationState.StartTime).TotalMilliseconds - totalPhaseTime;

        if (currentPhaseTime >= phaseDuration)
        {
            animationState.CurrentPhase++;
            animationState.PhaseProgress = 0f;
        }
        else
        {
            animationState.PhaseProgress = currentPhaseTime / phaseDuration;
        }
    }

    private bool ShouldCompleteEffect(EffectState effectState, TransitionState? transitionState, AnimationState? animationState)
    {
        // Check if effect has exceeded maximum duration
        if (effectState.Request.MaxDurationMs > 0)
        {
            var elapsed = (DateTime.UtcNow - effectState.StartTime).TotalMilliseconds;
            if (elapsed >= effectState.Request.MaxDurationMs)
            {
                return true;
            }
        }

        // Check if effect should loop
        if (!effectState.Request.LoopEnabled)
        {
            // Effect should complete after one cycle
            return effectState.CurrentFrame >= 1000; // Arbitrary frame limit
        }

        return false;
    }

    private float CalculateEffectProgress(EffectState state)
    {
        if (state.Request.MaxDurationMs > 0)
        {
            var elapsed = (DateTime.UtcNow - state.StartTime).TotalMilliseconds;
            return (float)(elapsed / state.Request.MaxDurationMs);
        }

        return 0f; // Indeterminate progress for looping effects
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isProcessing = false;
            _processingTimer?.Dispose();

            // Stop all active effects
            var effectIds = _activeProcessors.Keys.ToList();
            foreach (var effectId in effectIds)
            {
                StopEffectAsync(effectId).Wait(1000);
            }

            _logger.LogInformation("Real-time effect processing engine disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing real-time effect processing engine");
        }
    }

    #endregion
}

/// <summary>
/// Effect processor for individual effects
/// </summary>
public class EffectProcessor : IDisposable
{
    private readonly string _effectId;
    private readonly EffectRequest _request;
    private readonly ILogger _logger;
    private EffectUpdateRequest? _pendingUpdate;

    public EffectProcessor(string effectId, EffectRequest request, ILogger logger)
    {
        _effectId = effectId;
        _request = request;
        _logger = logger;
    }

    public async Task ProcessFrameAsync(EffectFrameParameters parameters)
    {
        try
        {
            // Apply pending updates
            if (_pendingUpdate != null)
            {
                ApplyUpdate(_pendingUpdate);
                _pendingUpdate = null;
            }

            // Process the effect frame
            await ProcessEffectFrame(parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing effect frame: {EffectId}", _effectId);
        }
    }

    public async Task UpdateParametersAsync(EffectUpdateRequest updateRequest)
    {
        _pendingUpdate = updateRequest;
        await Task.CompletedTask;
    }

    private void ApplyUpdate(EffectUpdateRequest updateRequest)
    {
        // Update request parameters
        if (updateRequest.IntensityMultiplier.HasValue)
        {
            _request.IntensityMultiplier = updateRequest.IntensityMultiplier.Value;
        }

        if (updateRequest.ColorSpeed.HasValue)
        {
            _request.ColorSpeed = updateRequest.ColorSpeed.Value;
        }

        if (updateRequest.BaseHue.HasValue)
        {
            _request.BaseHue = updateRequest.BaseHue.Value;
        }

        // Apply other parameter updates as needed
    }

    private async Task ProcessEffectFrame(EffectFrameParameters parameters)
    {
        // This would typically send commands to devices
        // For now, it's a placeholder implementation
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        // Cleanup resources
    }
}

#region Data Models

/// <summary>
/// Request to start a lighting effect
/// </summary>
public class EffectRequest
{
    public EffectType EffectType { get; set; }
    public List<string> TargetDevices { get; set; } = new();
    public AudioAnalysis? AudioData { get; set; }
    public float IntensityMultiplier { get; set; } = 1f;
    public float ColorSpeed { get; set; } = 1f;
    public float BaseHue { get; set; } = 0f;
    public int TransitionDurationMs { get; set; } = 500;
    public int MaxDurationMs { get; set; } = 0; // 0 = infinite
    public bool LoopEnabled { get; set; } = true;
    public float StrobeFrequency { get; set; } = 10f;
    public float PulseFrequency { get; set; } = 1f;
    public float WaveSpeed { get; set; } = 1f;
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Request to update effect parameters
/// </summary>
public class EffectUpdateRequest
{
    public float? IntensityMultiplier { get; set; }
    public float? ColorSpeed { get; set; }
    public float? BaseHue { get; set; }
    public float? StrobeFrequency { get; set; }
    public float? PulseFrequency { get; set; }
    public float? WaveSpeed { get; set; }
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Parameters for a single effect frame
/// </summary>
public class EffectFrameParameters
{
    public string EffectId { get; set; } = string.Empty;
    public int FrameNumber { get; set; }
    public float DeltaTime { get; set; }
    public float ElapsedTime { get; set; }
    public AudioAnalysis? AudioData { get; set; }
    public float TransitionProgress { get; set; } = 1f;
    public int AnimationPhase { get; set; }
    public float PhaseProgress { get; set; }

    // Color parameters
    public float ColorHue { get; set; }
    public float ColorSaturation { get; set; }
    public float ColorValue { get; set; }

    // Effect parameters
    public float Intensity { get; set; }
    public bool StrobeEnabled { get; set; }
    public float StrobeFrequency { get; set; }

    // Device-specific parameters
    public Dictionary<string, DeviceEffectParameters> DeviceSpecificParameters { get; set; } = new();
}

/// <summary>
/// Parameters for individual device effects
/// </summary>
public class DeviceEffectParameters
{
    public float ColorHue { get; set; }
    public float ColorSaturation { get; set; }
    public float ColorValue { get; set; }
    public float Intensity { get; set; }
    public bool StrobeEnabled { get; set; }
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Effect state tracking
/// </summary>
public class EffectState
{
    public string EffectId { get; set; } = string.Empty;
    public EffectRequest Request { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public bool IsActive { get; set; }
    public int CurrentFrame { get; set; }
}

/// <summary>
/// Transition state tracking
/// </summary>
public class TransitionState
{
    public string EffectId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsTransitioning { get; set; }
}

/// <summary>
/// Animation state tracking
/// </summary>
public class AnimationState
{
    public string EffectId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int CurrentPhase { get; set; }
    public float PhaseProgress { get; set; }
}

/// <summary>
/// Effect status information
/// </summary>
public class EffectStatusInfo
{
    public string EffectId { get; set; } = string.Empty;
    public EffectType EffectType { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public int CurrentFrame { get; set; }
    public float Progress { get; set; }
}

/// <summary>
/// Effect processing event arguments
/// </summary>
public class EffectProcessingEventArgs : EventArgs
{
    public string EffectId { get; }
    public EffectStatus Status { get; }
    public DateTime Timestamp { get; }

    public EffectProcessingEventArgs(string effectId, EffectStatus status)
    {
        EffectId = effectId;
        Status = status;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Effect error event arguments
/// </summary>
public class EffectErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; }
    public Exception? Exception { get; }
    public DateTime Timestamp { get; }

    public EffectErrorEventArgs(string errorMessage, Exception? exception = null)
    {
        ErrorMessage = errorMessage;
        Exception = exception;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Effect types
/// </summary>
public enum EffectType
{
    BeatSync,
    FrequencyVisualization,
    VolumeReactive,
    MoodLighting,
    SpectrumAnalyzer,
    PartyMode,
    Rainbow,
    Strobe,
    Pulse,
    Wave,
    Custom
}

/// <summary>
/// Effect status
/// </summary>
public enum EffectStatus
{
    Started,
    Stopped,
    Updated,
    Error
}
