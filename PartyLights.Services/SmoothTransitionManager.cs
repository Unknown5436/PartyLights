using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;

namespace PartyLights.Services;

/// <summary>
/// Manages smooth transitions between lighting effects
/// </summary>
public class SmoothTransitionManager
{
    private readonly ILogger<SmoothTransitionManager> _logger;
    private readonly IAdvancedDeviceControlService _deviceControlService;
    private readonly Dictionary<string, Transition> _activeTransitions = new();
    private readonly Timer _transitionTimer;
    private readonly object _lockObject = new();

    private const int TransitionUpdateIntervalMs = 16; // ~60 FPS for smooth transitions

    public event EventHandler<TransitionEventArgs>? TransitionStarted;
    public event EventHandler<TransitionEventArgs>? TransitionCompleted;
    public event EventHandler<TransitionEventArgs>? TransitionCancelled;

    public SmoothTransitionManager(
        ILogger<SmoothTransitionManager> logger,
        IAdvancedDeviceControlService deviceControlService)
    {
        _logger = logger;
        _deviceControlService = deviceControlService;

        _transitionTimer = new Timer(ProcessTransitions, null, TransitionUpdateIntervalMs, TransitionUpdateIntervalMs);

        _logger.LogInformation("Smooth transition manager initialized");
    }

    /// <summary>
    /// Starts a smooth transition between two lighting states
    /// </summary>
    public async Task<string> StartTransitionAsync(TransitionRequest request)
    {
        try
        {
            var transitionId = Guid.NewGuid().ToString();

            var transition = new Transition
            {
                Id = transitionId,
                Request = request,
                StartTime = DateTime.UtcNow,
                IsActive = true,
                CurrentProgress = 0f
            };

            lock (_lockObject)
            {
                _activeTransitions[transitionId] = transition;
            }

            TransitionStarted?.Invoke(this, new TransitionEventArgs(transitionId, TransitionStatus.Started));
            _logger.LogInformation("Started smooth transition: {TransitionId}", transitionId);

            return transitionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting smooth transition");
            return string.Empty;
        }
    }

    /// <summary>
    /// Cancels an active transition
    /// </summary>
    public async Task<bool> CancelTransitionAsync(string transitionId)
    {
        try
        {
            lock (_lockObject)
            {
                if (_activeTransitions.TryGetValue(transitionId, out var transition))
                {
                    transition.IsActive = false;
                    _activeTransitions.Remove(transitionId);

                    TransitionCancelled?.Invoke(this, new TransitionEventArgs(transitionId, TransitionStatus.Cancelled));
                    _logger.LogInformation("Cancelled transition: {TransitionId}", transitionId);

                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling transition: {TransitionId}", transitionId);
            return false;
        }
    }

    /// <summary>
    /// Gets status of all active transitions
    /// </summary>
    public IEnumerable<TransitionStatusInfo> GetActiveTransitions()
    {
        lock (_lockObject)
        {
            return _activeTransitions.Values.Select(t => new TransitionStatusInfo
            {
                Id = t.Id,
                Type = t.Request.Type,
                Progress = t.CurrentProgress,
                StartTime = t.StartTime,
                Duration = t.Request.Duration,
                IsActive = t.IsActive
            }).ToList();
        }
    }

    #region Private Methods

    private async void ProcessTransitions(object? state)
    {
        try
        {
            var currentTime = DateTime.UtcNow;
            var transitionsToRemove = new List<string>();

            lock (_lockObject)
            {
                foreach (var transitionEntry in _activeTransitions)
                {
                    var transitionId = transitionEntry.Key;
                    var transition = transitionEntry.Value;

                    if (!transition.IsActive)
                    {
                        transitionsToRemove.Add(transitionId);
                        continue;
                    }

                    // Calculate transition progress
                    var elapsed = currentTime - transition.StartTime;
                    var progress = Math.Min((float)elapsed.TotalMilliseconds / (float)transition.Request.Duration.TotalMilliseconds, 1f);

                    transition.CurrentProgress = progress;

                    // Apply transition
                    await ApplyTransition(transition, progress);

                    // Check if transition is complete
                    if (progress >= 1f)
                    {
                        transition.IsActive = false;
                        transitionsToRemove.Add(transitionId);

                        TransitionCompleted?.Invoke(this, new TransitionEventArgs(transitionId, TransitionStatus.Completed));
                        _logger.LogInformation("Completed transition: {TransitionId}", transitionId);
                    }
                }

                // Remove completed transitions
                foreach (var transitionId in transitionsToRemove)
                {
                    _activeTransitions.Remove(transitionId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transitions");
        }
    }

    private async Task ApplyTransition(Transition transition, float progress)
    {
        try
        {
            switch (transition.Request.Type)
            {
                case TransitionType.Color:
                    await ApplyColorTransition(transition, progress);
                    break;
                case TransitionType.Brightness:
                    await ApplyBrightnessTransition(transition, progress);
                    break;
                case TransitionType.Effect:
                    await ApplyEffectTransition(transition, progress);
                    break;
                case TransitionType.Complex:
                    await ApplyComplexTransition(transition, progress);
                    break;
                default:
                    await ApplyDefaultTransition(transition, progress);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying transition: {TransitionId}", transition.Id);
        }
    }

    private async Task ApplyColorTransition(Transition transition, float progress)
    {
        var request = transition.Request;

        // Interpolate color values
        var startColor = request.StartColor;
        var endColor = request.EndColor;

        var currentColor = InterpolateColor(startColor, endColor, progress, request.EasingFunction);

        // Apply color to target devices
        foreach (var deviceId in request.TargetDevices)
        {
            await _deviceControlService.SetColorToDeviceGroupAsync(deviceId, currentColor.R, currentColor.G, currentColor.B);
        }
    }

    private async Task ApplyBrightnessTransition(Transition transition, float progress)
    {
        var request = transition.Request;

        // Interpolate brightness
        var startBrightness = request.StartBrightness;
        var endBrightness = request.EndBrightness;

        var currentBrightness = InterpolateValue(startBrightness, endBrightness, progress, request.EasingFunction);

        // Apply brightness to target devices
        foreach (var deviceId in request.TargetDevices)
        {
            await _deviceControlService.SetBrightnessToDeviceGroupAsync(deviceId, (int)currentBrightness);
        }
    }

    private async Task ApplyEffectTransition(Transition transition, float progress)
    {
        var request = transition.Request;

        // Cross-fade between effects
        var effect1Intensity = 1f - progress;
        var effect2Intensity = progress;

        // Apply weighted combination of effects
        await ApplyEffectCombination(request.TargetDevices, request.StartEffect, effect1Intensity, request.EndEffect, effect2Intensity);
    }

    private async Task ApplyComplexTransition(Transition transition, float progress)
    {
        var request = transition.Request;

        // Apply multiple simultaneous transitions
        if (request.StartColor != null && request.EndColor != null)
        {
            await ApplyColorTransition(transition, progress);
        }

        if (request.StartBrightness.HasValue && request.EndBrightness.HasValue)
        {
            await ApplyBrightnessTransition(transition, progress);
        }

        if (!string.IsNullOrEmpty(request.StartEffect) && !string.IsNullOrEmpty(request.EndEffect))
        {
            await ApplyEffectTransition(transition, progress);
        }
    }

    private async Task ApplyDefaultTransition(Transition transition, float progress)
    {
        // Default transition applies both color and brightness
        await ApplyColorTransition(transition, progress);
        await ApplyBrightnessTransition(transition, progress);
    }

    private async Task ApplyEffectCombination(List<string> targetDevices, string effect1, float intensity1, string effect2, float intensity2)
    {
        // This would typically apply a weighted combination of two effects
        // For now, it's a placeholder implementation
        await Task.CompletedTask;
    }

    private Color InterpolateColor(Color startColor, Color endColor, float progress, EasingFunction easingFunction)
    {
        var easedProgress = ApplyEasing(progress, easingFunction);

        var r = (int)(startColor.R + (endColor.R - startColor.R) * easedProgress);
        var g = (int)(startColor.G + (endColor.G - startColor.G) * easedProgress);
        var b = (int)(startColor.B + (endColor.B - startColor.B) * easedProgress);

        return Color.FromArgb(255, r, g, b);
    }

    private float InterpolateValue(float startValue, float endValue, float progress, EasingFunction easingFunction)
    {
        var easedProgress = ApplyEasing(progress, easingFunction);
        return startValue + (endValue - startValue) * easedProgress;
    }

    private float ApplyEasing(float progress, EasingFunction easingFunction)
    {
        return easingFunction switch
        {
            EasingFunction.Linear => progress,
            EasingFunction.EaseIn => progress * progress,
            EasingFunction.EaseOut => 1f - (1f - progress) * (1f - progress),
            EasingFunction.EaseInOut => progress < 0.5f ? 2f * progress * progress : 1f - 2f * (1f - progress) * (1f - progress),
            EasingFunction.EaseInCubic => progress * progress * progress,
            EasingFunction.EaseOutCubic => 1f - (1f - progress) * (1f - progress) * (1f - progress),
            EasingFunction.EaseInOutCubic => progress < 0.5f ? 4f * progress * progress * progress : 1f - 4f * (1f - progress) * (1f - progress) * (1f - progress),
            EasingFunction.EaseInSine => 1f - (float)Math.Cos(progress * Math.PI / 2),
            EasingFunction.EaseOutSine => (float)Math.Sin(progress * Math.PI / 2),
            EasingFunction.EaseInOutSine => -(float)Math.Cos(progress * Math.PI) / 2f + 0.5f,
            EasingFunction.EaseInExpo => progress == 0f ? 0f : (float)Math.Pow(2, 10 * (progress - 1)),
            EasingFunction.EaseOutExpo => progress == 1f ? 1f : 1f - (float)Math.Pow(2, -10 * progress),
            EasingFunction.EaseInOutExpo => progress == 0f ? 0f : progress == 1f ? 1f : progress < 0.5f ? (float)Math.Pow(2, 20 * progress - 10) / 2f : (2f - (float)Math.Pow(2, -20 * progress + 10)) / 2f,
            EasingFunction.EaseInBack => 2.70158f * progress * progress * progress - 1.70158f * progress * progress,
            EasingFunction.EaseOutBack => 1f + 2.70158f * (progress - 1f) * (progress - 1f) * (progress - 1f) + 1.70158f * (progress - 1f) * (progress - 1f),
            EasingFunction.EaseInOutBack => progress < 0.5f ? (2f * progress) * (2f * progress) * (3.594909f * (2f * progress) - 2.594909f) / 2f : ((2f * progress - 2f) * (2f * progress - 2f) * (3.594909f * (2f * progress - 2f) + 2.594909f) + 2f) / 2f,
            EasingFunction.EaseInElastic => progress == 0f ? 0f : progress == 1f ? 1f : -(float)Math.Pow(2, 10 * (progress - 1)) * (float)Math.Sin((progress - 1.1f) * 5 * Math.PI),
            EasingFunction.EaseOutElastic => progress == 0f ? 0f : progress == 1f ? 1f : (float)Math.Pow(2, -10 * progress) * (float)Math.Sin((progress - 0.1f) * 5 * Math.PI) + 1f,
            EasingFunction.EaseInOutElastic => progress == 0f ? 0f : progress == 1f ? 1f : progress < 0.5f ? -(float)Math.Pow(2, 20 * progress - 10) * (float)Math.Sin((20 * progress - 11.125f) * 2 * Math.PI / 4.5f) / 2f : (float)Math.Pow(2, -20 * progress + 10) * (float)Math.Sin((20 * progress - 11.125f) * 2 * Math.PI / 4.5f) / 2f + 1f,
            EasingFunction.EaseInBounce => 1f - EaseOutBounce(1f - progress),
            EasingFunction.EaseOutBounce => progress < 1f / 2.75f ? 7.5625f * progress * progress : progress < 2f / 2.75f ? 7.5625f * (progress - 1.5f / 2.75f) * (progress - 1.5f / 2.75f) + 0.75f : progress < 2.5f / 2.75f ? 7.5625f * (progress - 2.25f / 2.75f) * (progress - 2.25f / 2.75f) + 0.9375f : 7.5625f * (progress - 2.625f / 2.75f) * (progress - 2.625f / 2.75f) + 0.984375f,
            EasingFunction.EaseInOutBounce => progress < 0.5f ? (1f - EaseOutBounce(1f - 2f * progress)) / 2f : (1f + EaseOutBounce(2f * progress - 1f)) / 2f,
            _ => progress
        };
    }

    private float EaseOutBounce(float progress)
    {
        return progress < 1f / 2.75f ? 7.5625f * progress * progress : progress < 2f / 2.75f ? 7.5625f * (progress - 1.5f / 2.75f) * (progress - 1.5f / 2.75f) + 0.75f : progress < 2.5f / 2.75f ? 7.5625f * (progress - 2.25f / 2.75f) * (progress - 2.25f / 2.75f) + 0.9375f : 7.5625f * (progress - 2.625f / 2.75f) * (progress - 2.625f / 2.75f) + 0.984375f;
    }

    #endregion
}

#region Data Models

/// <summary>
/// Request for a smooth transition
/// </summary>
public class TransitionRequest
{
    public TransitionType Type { get; set; }
    public List<string> TargetDevices { get; set; } = new();
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(500);
    public EasingFunction EasingFunction { get; set; } = EasingFunction.EaseInOut;

    // Color transition
    public Color? StartColor { get; set; }
    public Color? EndColor { get; set; }

    // Brightness transition
    public float? StartBrightness { get; set; }
    public float? EndBrightness { get; set; }

    // Effect transition
    public string? StartEffect { get; set; }
    public string? EndEffect { get; set; }

    // Custom parameters
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Active transition state
/// </summary>
public class Transition
{
    public string Id { get; set; } = string.Empty;
    public TransitionRequest Request { get; set; } = new();
    public DateTime StartTime { get; set; }
    public bool IsActive { get; set; }
    public float CurrentProgress { get; set; }
}

/// <summary>
/// Transition status information
/// </summary>
public class TransitionStatusInfo
{
    public string Id { get; set; } = string.Empty;
    public TransitionType Type { get; set; }
    public float Progress { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Transition event arguments
/// </summary>
public class TransitionEventArgs : EventArgs
{
    public string TransitionId { get; }
    public TransitionStatus Status { get; }
    public DateTime Timestamp { get; }

    public TransitionEventArgs(string transitionId, TransitionStatus status)
    {
        TransitionId = transitionId;
        Status = status;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Transition types
/// </summary>
public enum TransitionType
{
    Color,
    Brightness,
    Effect,
    Complex,
    Custom
}

/// <summary>
/// Easing functions for smooth transitions
/// </summary>
public enum EasingFunction
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    EaseInCubic,
    EaseOutCubic,
    EaseInOutCubic,
    EaseInSine,
    EaseOutSine,
    EaseInOutSine,
    EaseInExpo,
    EaseOutExpo,
    EaseInOutExpo,
    EaseInBack,
    EaseOutBack,
    EaseInOutBack,
    EaseInElastic,
    EaseOutElastic,
    EaseInOutElastic,
    EaseInBounce,
    EaseOutBounce,
    EaseInOutBounce
}

/// <summary>
/// Transition status
/// </summary>
public enum TransitionStatus
{
    Started,
    Completed,
    Cancelled,
    Error
}
