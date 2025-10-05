using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Windows.Media;

namespace PartyLights.Services;

/// <summary>
/// Advanced control service for sophisticated UI controls
/// </summary>
public class AdvancedControlService : IDisposable
{
    private readonly ILogger<AdvancedControlService> _logger;
    private readonly ConcurrentDictionary<string, AdvancedControl> _controls = new();
    private readonly Timer _controlUpdateTimer;
    private readonly object _lockObject = new();

    private const int ControlUpdateIntervalMs = 16; // ~60 FPS
    private bool _isUpdating;

    // Control state management
    private readonly Dictionary<string, ControlState> _controlStates = new();
    private readonly Dictionary<string, AnimationState> _animationStates = new();
    private readonly Dictionary<string, InteractionState> _interactionStates = new();

    public event EventHandler<ControlEventArgs>? ControlCreated;
    public event EventHandler<ControlEventArgs>? ControlUpdated;
    public event EventHandler<ControlEventArgs>? ControlDestroyed;
    public event EventHandler<ControlInteractionEventArgs>? ControlInteraction;
    public event EventHandler<ControlAnimationEventArgs>? ControlAnimation;

    public AdvancedControlService(ILogger<AdvancedControlService> logger)
    {
        _logger = logger;

        _controlUpdateTimer = new Timer(UpdateControls, null, ControlUpdateIntervalMs, ControlUpdateIntervalMs);
        _isUpdating = true;

        _logger.LogInformation("Advanced control service initialized");
    }

    /// <summary>
    /// Creates an advanced color picker control
    /// </summary>
    public async Task<string> CreateColorPickerAsync(ColorPickerRequest request)
    {
        try
        {
            var controlId = Guid.NewGuid().ToString();

            var control = new AdvancedControl
            {
                Id = controlId,
                Type = ControlType.ColorPicker,
                Name = request.Name,
                IsActive = true,
                CreatedTime = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                Properties = new Dictionary<string, object>
                {
                    ["Width"] = request.Width,
                    ["Height"] = request.Height,
                    ["ShowAlpha"] = request.ShowAlpha,
                    ["ShowHex"] = request.ShowHex,
                    ["ShowHSV"] = request.ShowHSV,
                    ["ShowRGB"] = request.ShowRGB,
                    ["DefaultColor"] = request.DefaultColor,
                    ["ColorHistory"] = request.ColorHistory,
                    ["MaxHistorySize"] = request.MaxHistorySize,
                    ["Theme"] = request.Theme
                }
            };

            var controlState = new ControlState
            {
                ControlId = controlId,
                Type = ControlType.ColorPicker,
                Width = request.Width,
                Height = request.Height,
                IsActive = true,
                LastUpdateTime = DateTime.UtcNow,
                CurrentColor = request.DefaultColor,
                ColorHistory = request.ColorHistory ?? new List<Color>(),
                MaxHistorySize = request.MaxHistorySize
            };

            lock (_lockObject)
            {
                _controls[controlId] = control;
                _controlStates[controlId] = controlState;
            }

            ControlCreated?.Invoke(this, new ControlEventArgs(controlId, ControlAction.Created));
            _logger.LogInformation("Created color picker: {ControlName} ({ControlId})", request.Name, controlId);

            return controlId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating color picker: {ControlName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Creates an advanced slider control
    /// </summary>
    public async Task<string> CreateSliderAsync(SliderRequest request)
    {
        try
        {
            var controlId = Guid.NewGuid().ToString();

            var control = new AdvancedControl
            {
                Id = controlId,
                Type = ControlType.Slider,
                Name = request.Name,
                IsActive = true,
                CreatedTime = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                Properties = new Dictionary<string, object>
                {
                    ["Width"] = request.Width,
                    ["Height"] = request.Height,
                    ["MinValue"] = request.MinValue,
                    ["MaxValue"] = request.MaxValue,
                    ["DefaultValue"] = request.DefaultValue,
                    ["StepSize"] = request.StepSize,
                    ["Orientation"] = request.Orientation,
                    ["ShowValue"] = request.ShowValue,
                    ["ShowTicks"] = request.ShowTicks,
                    ["TickFrequency"] = request.TickFrequency,
                    ["Theme"] = request.Theme
                }
            };

            var controlState = new ControlState
            {
                ControlId = controlId,
                Type = ControlType.Slider,
                Width = request.Width,
                Height = request.Height,
                IsActive = true,
                LastUpdateTime = DateTime.UtcNow,
                MinValue = request.MinValue,
                MaxValue = request.MaxValue,
                CurrentValue = request.DefaultValue,
                StepSize = request.StepSize
            };

            lock (_lockObject)
            {
                _controls[controlId] = control;
                _controlStates[controlId] = controlState;
            }

            ControlCreated?.Invoke(this, new ControlEventArgs(controlId, ControlAction.Created));
            _logger.LogInformation("Created slider: {ControlName} ({ControlId})", request.Name, controlId);

            return controlId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating slider: {ControlName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Creates an advanced button control
    /// </summary>
    public async Task<string> CreateButtonAsync(ButtonRequest request)
    {
        try
        {
            var controlId = Guid.NewGuid().ToString();

            var control = new AdvancedControl
            {
                Id = controlId,
                Type = ControlType.Button,
                Name = request.Name,
                IsActive = true,
                CreatedTime = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                Properties = new Dictionary<string, object>
                {
                    ["Width"] = request.Width,
                    ["Height"] = request.Height,
                    ["Text"] = request.Text,
                    ["Icon"] = request.Icon,
                    ["ButtonType"] = request.ButtonType,
                    ["IsEnabled"] = request.IsEnabled,
                    ["ShowTooltip"] = request.ShowTooltip,
                    ["TooltipText"] = request.TooltipText,
                    ["AnimationType"] = request.AnimationType,
                    ["Theme"] = request.Theme
                }
            };

            var controlState = new ControlState
            {
                ControlId = controlId,
                Type = ControlType.Button,
                Width = request.Width,
                Height = request.Height,
                IsActive = true,
                LastUpdateTime = DateTime.UtcNow,
                IsEnabled = request.IsEnabled,
                Text = request.Text,
                Icon = request.Icon
            };

            lock (_lockObject)
            {
                _controls[controlId] = control;
                _controlStates[controlId] = controlState;
            }

            ControlCreated?.Invoke(this, new ControlEventArgs(controlId, ControlAction.Created));
            _logger.LogInformation("Created button: {ControlName} ({ControlId})", request.Name, controlId);

            return controlId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating button: {ControlName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Creates an advanced list control
    /// </summary>
    public async Task<string> CreateListAsync(ListRequest request)
    {
        try
        {
            var controlId = Guid.NewGuid().ToString();

            var control = new AdvancedControl
            {
                Id = controlId,
                Type = ControlType.List,
                Name = request.Name,
                IsActive = true,
                CreatedTime = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                Properties = new Dictionary<string, object>
                {
                    ["Width"] = request.Width,
                    ["Height"] = request.Height,
                    ["Items"] = request.Items,
                    ["SelectionMode"] = request.SelectionMode,
                    ["ShowCheckboxes"] = request.ShowCheckboxes,
                    ["ShowIcons"] = request.ShowIcons,
                    ["ShowTooltips"] = request.ShowTooltips,
                    ["Sortable"] = request.Sortable,
                    ["Filterable"] = request.Filterable,
                    ["Theme"] = request.Theme
                }
            };

            var controlState = new ControlState
            {
                ControlId = controlId,
                Type = ControlType.List,
                Width = request.Width,
                Height = request.Height,
                IsActive = true,
                LastUpdateTime = DateTime.UtcNow,
                Items = request.Items ?? new List<ListItem>(),
                SelectedItems = new List<string>()
            };

            lock (_lockObject)
            {
                _controls[controlId] = control;
                _controlStates[controlId] = controlState;
            }

            ControlCreated?.Invoke(this, new ControlEventArgs(controlId, ControlAction.Created));
            _logger.LogInformation("Created list: {ControlName} ({ControlId})", request.Name, controlId);

            return controlId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating list: {ControlName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Updates control value
    /// </summary>
    public async Task<bool> UpdateControlValueAsync(string controlId, object value)
    {
        try
        {
            if (!_controlStates.TryGetValue(controlId, out var state))
            {
                _logger.LogWarning("Control not found: {ControlId}", controlId);
                return false;
            }

            // Update control state based on type
            switch (state.Type)
            {
                case ControlType.ColorPicker:
                    if (value is Color color)
                    {
                        state.CurrentColor = color;
                        AddToColorHistory(state, color);
                    }
                    break;
                case ControlType.Slider:
                    if (value is double doubleValue)
                    {
                        state.CurrentValue = Math.Max(state.MinValue, Math.Min(state.MaxValue, doubleValue));
                    }
                    break;
                case ControlType.Button:
                    // Button values are typically boolean (pressed/not pressed)
                    if (value is bool boolValue)
                    {
                        state.IsPressed = boolValue;
                    }
                    break;
                case ControlType.List:
                    if (value is List<string> selectedItems)
                    {
                        state.SelectedItems = selectedItems;
                    }
                    break;
            }

            state.LastUpdateTime = DateTime.UtcNow;

            ControlUpdated?.Invoke(this, new ControlEventArgs(controlId, ControlAction.Updated));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating control value: {ControlId}", controlId);
            return false;
        }
    }

    /// <summary>
    /// Handles control interaction
    /// </summary>
    public async Task<bool> HandleControlInteractionAsync(string controlId, InteractionType interactionType, object? data = null)
    {
        try
        {
            if (!_controlStates.TryGetValue(controlId, out var state))
            {
                _logger.LogWarning("Control not found: {ControlId}", controlId);
                return false;
            }

            var interactionState = new InteractionState
            {
                ControlId = controlId,
                InteractionType = interactionType,
                Data = data,
                Timestamp = DateTime.UtcNow,
                IsHandled = false
            };

            // Handle interaction based on type
            switch (interactionType)
            {
                case InteractionType.Click:
                    await HandleClickInteraction(state, interactionState);
                    break;
                case InteractionType.DoubleClick:
                    await HandleDoubleClickInteraction(state, interactionState);
                    break;
                case InteractionType.Drag:
                    await HandleDragInteraction(state, interactionState);
                    break;
                case InteractionType.Hover:
                    await HandleHoverInteraction(state, interactionState);
                    break;
                case InteractionType.KeyPress:
                    await HandleKeyPressInteraction(state, interactionState);
                    break;
            }

            interactionState.IsHandled = true;
            ControlInteraction?.Invoke(this, new ControlInteractionEventArgs(controlId, interactionType, data));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling control interaction: {ControlId}", controlId);
            return false;
        }
    }

    /// <summary>
    /// Starts control animation
    /// </summary>
    public async Task<bool> StartControlAnimationAsync(string controlId, AnimationRequest request)
    {
        try
        {
            if (!_controlStates.TryGetValue(controlId, out var state))
            {
                _logger.LogWarning("Control not found: {ControlId}", controlId);
                return false;
            }

            var animationState = new AnimationState
            {
                ControlId = controlId,
                AnimationType = request.AnimationType,
                Duration = request.Duration,
                StartTime = DateTime.UtcNow,
                IsActive = true,
                Progress = 0f,
                FromValue = request.FromValue,
                ToValue = request.ToValue,
                EasingFunction = request.EasingFunction
            };

            lock (_lockObject)
            {
                _animationStates[controlId] = animationState;
            }

            ControlAnimation?.Invoke(this, new ControlAnimationEventArgs(controlId, AnimationAction.Started));
            _logger.LogInformation("Started animation for control: {ControlId}", controlId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting control animation: {ControlId}", controlId);
            return false;
        }
    }

    /// <summary>
    /// Gets control state
    /// </summary>
    public ControlState? GetControlState(string controlId)
    {
        _controlStates.TryGetValue(controlId, out var state);
        return state;
    }

    /// <summary>
    /// Gets all active controls
    /// </summary>
    public IEnumerable<AdvancedControl> GetActiveControls()
    {
        return _controls.Values.Where(c => c.IsActive);
    }

    /// <summary>
    /// Destroys a control
    /// </summary>
    public async Task<bool> DestroyControlAsync(string controlId)
    {
        try
        {
            if (!_controls.TryRemove(controlId, out var control))
            {
                _logger.LogWarning("Control not found: {ControlId}", controlId);
                return false;
            }

            // Clean up control state
            _controlStates.Remove(controlId);
            _animationStates.Remove(controlId);
            _interactionStates.Remove(controlId);

            ControlDestroyed?.Invoke(this, new ControlEventArgs(controlId, ControlAction.Destroyed));
            _logger.LogInformation("Destroyed control: {ControlName} ({ControlId})", control.Name, controlId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error destroying control: {ControlId}", controlId);
            return false;
        }
    }

    #region Private Methods

    private async void UpdateControls(object? state)
    {
        if (!_isUpdating)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;

            // Update control animations
            foreach (var animationEntry in _animationStates)
            {
                var controlId = animationEntry.Key;
                var animationState = animationEntry.Value;

                if (animationState.IsActive)
                {
                    await UpdateControlAnimation(controlId, animationState, currentTime);
                }
            }

            // Update control states
            foreach (var controlEntry in _controlStates)
            {
                var controlId = controlEntry.Key;
                var controlState = controlEntry.Value;

                if (controlState.IsActive)
                {
                    await UpdateControlState(controlId, controlState, currentTime);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating controls");
        }
    }

    private async Task UpdateControlAnimation(string controlId, AnimationState animationState, DateTime currentTime)
    {
        try
        {
            var elapsed = (currentTime - animationState.StartTime).TotalMilliseconds;
            var progress = Math.Min((float)(elapsed / animationState.Duration.TotalMilliseconds), 1f);

            animationState.Progress = progress;

            // Apply easing function
            var easedProgress = ApplyEasingFunction(progress, animationState.EasingFunction);

            // Update control value based on animation
            if (_controlStates.TryGetValue(controlId, out var controlState))
            {
                switch (animationState.AnimationType)
                {
                    case AnimationType.ColorTransition:
                        if (animationState.FromValue is Color fromColor && animationState.ToValue is Color toColor)
                        {
                            controlState.CurrentColor = InterpolateColor(fromColor, toColor, easedProgress);
                        }
                        break;
                    case AnimationType.ValueTransition:
                        if (animationState.FromValue is double fromValue && animationState.ToValue is double toValue)
                        {
                            controlState.CurrentValue = fromValue + (toValue - fromValue) * easedProgress;
                        }
                        break;
                    case AnimationType.ScaleTransition:
                        if (animationState.FromValue is double fromScale && animationState.ToValue is double toScale)
                        {
                            controlState.Scale = fromScale + (toScale - fromScale) * easedProgress;
                        }
                        break;
                }
            }

            // Check if animation is complete
            if (progress >= 1f)
            {
                animationState.IsActive = false;
                ControlAnimation?.Invoke(this, new ControlAnimationEventArgs(controlId, AnimationAction.Completed));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating control animation: {ControlId}", controlId);
        }
    }

    private async Task UpdateControlState(string controlId, ControlState controlState, DateTime currentTime)
    {
        try
        {
            // Update control state based on current values
            controlState.LastUpdateTime = currentTime;

            // This would typically update UI elements, trigger events, etc.
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating control state: {ControlId}", controlId);
        }
    }

    private async Task HandleClickInteraction(ControlState state, InteractionState interactionState)
    {
        try
        {
            switch (state.Type)
            {
                case ControlType.Button:
                    state.IsPressed = !state.IsPressed;
                    break;
                case ControlType.Slider:
                    // Handle slider click to set value
                    if (interactionState.Data is double clickValue)
                    {
                        state.CurrentValue = Math.Max(state.MinValue, Math.Min(state.MaxValue, clickValue));
                    }
                    break;
                case ControlType.List:
                    // Handle list item selection
                    if (interactionState.Data is string itemId)
                    {
                        if (state.SelectedItems.Contains(itemId))
                        {
                            state.SelectedItems.Remove(itemId);
                        }
                        else
                        {
                            state.SelectedItems.Add(itemId);
                        }
                    }
                    break;
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling click interaction");
        }
    }

    private async Task HandleDoubleClickInteraction(ControlState state, InteractionState interactionState)
    {
        try
        {
            // Handle double-click interactions
            switch (state.Type)
            {
                case ControlType.List:
                    // Double-click to select item
                    if (interactionState.Data is string itemId)
                    {
                        state.SelectedItems.Clear();
                        state.SelectedItems.Add(itemId);
                    }
                    break;
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling double-click interaction");
        }
    }

    private async Task HandleDragInteraction(ControlState state, InteractionState interactionState)
    {
        try
        {
            // Handle drag interactions
            switch (state.Type)
            {
                case ControlType.Slider:
                    // Handle slider drag
                    if (interactionState.Data is double dragValue)
                    {
                        state.CurrentValue = Math.Max(state.MinValue, Math.Min(state.MaxValue, dragValue));
                    }
                    break;
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling drag interaction");
        }
    }

    private async Task HandleHoverInteraction(ControlState state, InteractionState interactionState)
    {
        try
        {
            // Handle hover interactions
            state.IsHovered = true;
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling hover interaction");
        }
    }

    private async Task HandleKeyPressInteraction(ControlState state, InteractionState interactionState)
    {
        try
        {
            // Handle key press interactions
            if (interactionState.Data is string key)
            {
                switch (key)
                {
                    case "Enter":
                        if (state.Type == ControlType.Button)
                        {
                            state.IsPressed = !state.IsPressed;
                        }
                        break;
                    case "Escape":
                        // Handle escape key
                        break;
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling key press interaction");
        }
    }

    private void AddToColorHistory(ControlState state, Color color)
    {
        try
        {
            if (state.ColorHistory == null)
            {
                state.ColorHistory = new List<Color>();
            }

            // Remove duplicate if exists
            state.ColorHistory.RemoveAll(c => c == color);

            // Add to beginning
            state.ColorHistory.Insert(0, color);

            // Limit history size
            if (state.ColorHistory.Count > state.MaxHistorySize)
            {
                state.ColorHistory.RemoveAt(state.ColorHistory.Count - 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding to color history");
        }
    }

    private float ApplyEasingFunction(float progress, EasingFunction easingFunction)
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

    private Color InterpolateColor(Color fromColor, Color toColor, float progress)
    {
        var r = (byte)(fromColor.R + (toColor.R - fromColor.R) * progress);
        var g = (byte)(fromColor.G + (toColor.G - fromColor.G) * progress);
        var b = (byte)(fromColor.B + (toColor.B - fromColor.B) * progress);
        var a = (byte)(fromColor.A + (toColor.A - fromColor.A) * progress);

        return Color.FromArgb(a, r, g, b);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isUpdating = false;
            _controlUpdateTimer?.Dispose();

            // Destroy all controls
            var controlIds = _controls.Keys.ToList();
            foreach (var controlId in controlIds)
            {
                DestroyControlAsync(controlId).Wait(1000);
            }

            _logger.LogInformation("Advanced control service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing advanced control service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Color picker request
/// </summary>
public class ColorPickerRequest
{
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; } = 200;
    public int Height { get; set; } = 150;
    public bool ShowAlpha { get; set; } = true;
    public bool ShowHex { get; set; } = true;
    public bool ShowHSV { get; set; } = true;
    public bool ShowRGB { get; set; } = true;
    public Color DefaultColor { get; set; } = Colors.White;
    public List<Color>? ColorHistory { get; set; }
    public int MaxHistorySize { get; set; } = 20;
    public string Theme { get; set; } = "Default";
}

/// <summary>
/// Slider request
/// </summary>
public class SliderRequest
{
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; } = 200;
    public int Height { get; set; } = 30;
    public double MinValue { get; set; } = 0.0;
    public double MaxValue { get; set; } = 100.0;
    public double DefaultValue { get; set; } = 50.0;
    public double StepSize { get; set; } = 1.0;
    public SliderOrientation Orientation { get; set; } = SliderOrientation.Horizontal;
    public bool ShowValue { get; set; } = true;
    public bool ShowTicks { get; set; } = false;
    public double TickFrequency { get; set; } = 10.0;
    public string Theme { get; set; } = "Default";
}

/// <summary>
/// Button request
/// </summary>
public class ButtonRequest
{
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; } = 100;
    public int Height { get; set; } = 30;
    public string Text { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public ButtonType ButtonType { get; set; } = ButtonType.Primary;
    public bool IsEnabled { get; set; } = true;
    public bool ShowTooltip { get; set; } = false;
    public string? TooltipText { get; set; }
    public AnimationType AnimationType { get; set; } = AnimationType.ScaleTransition;
    public string Theme { get; set; } = "Default";
}

/// <summary>
/// List request
/// </summary>
public class ListRequest
{
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; } = 200;
    public int Height { get; set; } = 300;
    public List<ListItem>? Items { get; set; }
    public SelectionMode SelectionMode { get; set; } = SelectionMode.Single;
    public bool ShowCheckboxes { get; set; } = false;
    public bool ShowIcons { get; set; } = true;
    public bool ShowTooltips { get; set; } = true;
    public bool Sortable { get; set; } = false;
    public bool Filterable { get; set; } = false;
    public string Theme { get; set; } = "Default";
}

/// <summary>
/// Animation request
/// </summary>
public class AnimationRequest
{
    public AnimationType AnimationType { get; set; } = AnimationType.ValueTransition;
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(300);
    public object? FromValue { get; set; }
    public object? ToValue { get; set; }
    public EasingFunction EasingFunction { get; set; } = EasingFunction.EaseInOut;
}

/// <summary>
/// Advanced control
/// </summary>
public class AdvancedControl
{
    public string Id { get; set; } = string.Empty;
    public ControlType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Control state
/// </summary>
public class ControlState
{
    public string ControlId { get; set; } = string.Empty;
    public ControlType Type { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsPressed { get; set; }
    public bool IsHovered { get; set; }
    public Color CurrentColor { get; set; } = Colors.White;
    public double CurrentValue { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double StepSize { get; set; } = 1.0;
    public double Scale { get; set; } = 1.0;
    public string Text { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public List<Color>? ColorHistory { get; set; }
    public int MaxHistorySize { get; set; } = 20;
    public List<ListItem> Items { get; set; } = new();
    public List<string> SelectedItems { get; set; } = new();
}

/// <summary>
/// Animation state
/// </summary>
public class AnimationState
{
    public string ControlId { get; set; } = string.Empty;
    public AnimationType AnimationType { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime StartTime { get; set; }
    public bool IsActive { get; set; }
    public float Progress { get; set; }
    public object? FromValue { get; set; }
    public object? ToValue { get; set; }
    public EasingFunction EasingFunction { get; set; }
}

/// <summary>
/// Interaction state
/// </summary>
public class InteractionState
{
    public string ControlId { get; set; } = string.Empty;
    public InteractionType InteractionType { get; set; }
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsHandled { get; set; }
}

/// <summary>
/// List item
/// </summary>
public class ListItem
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Tooltip { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsSelected { get; set; }
    public Dictionary<string, object> CustomData { get; set; } = new();
}

/// <summary>
/// Control event arguments
/// </summary>
public class ControlEventArgs : EventArgs
{
    public string ControlId { get; }
    public ControlAction Action { get; }
    public DateTime Timestamp { get; }

    public ControlEventArgs(string controlId, ControlAction action)
    {
        ControlId = controlId;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Control interaction event arguments
/// </summary>
public class ControlInteractionEventArgs : EventArgs
{
    public string ControlId { get; }
    public InteractionType InteractionType { get; }
    public object? Data { get; }
    public DateTime Timestamp { get; }

    public ControlInteractionEventArgs(string controlId, InteractionType interactionType, object? data)
    {
        ControlId = controlId;
        InteractionType = interactionType;
        Data = data;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Control animation event arguments
/// </summary>
public class ControlAnimationEventArgs : EventArgs
{
    public string ControlId { get; }
    public AnimationAction Action { get; }
    public DateTime Timestamp { get; }

    public ControlAnimationEventArgs(string controlId, AnimationAction action)
    {
        ControlId = controlId;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Control types
/// </summary>
public enum ControlType
{
    ColorPicker,
    Slider,
    Button,
    List,
    ComboBox,
    CheckBox,
    RadioButton,
    TextBox,
    Custom
}

/// <summary>
/// Slider orientations
/// </summary>
public enum SliderOrientation
{
    Horizontal,
    Vertical
}

/// <summary>
/// Button types
/// </summary>
public enum ButtonType
{
    Primary,
    Secondary,
    Success,
    Warning,
    Danger,
    Info,
    Custom
}

/// <summary>
/// Selection modes
/// </summary>
public enum SelectionMode
{
    Single,
    Multiple,
    Extended
}

/// <summary>
/// Animation types
/// </summary>
public enum AnimationType
{
    ColorTransition,
    ValueTransition,
    ScaleTransition,
    OpacityTransition,
    RotationTransition,
    Custom
}

/// <summary>
/// Interaction types
/// </summary>
public enum InteractionType
{
    Click,
    DoubleClick,
    Drag,
    Hover,
    KeyPress,
    Focus,
    Blur,
    Custom
}

/// <summary>
/// Control actions
/// </summary>
public enum ControlAction
{
    Created,
    Updated,
    Destroyed,
    Enabled,
    Disabled
}

/// <summary>
/// Animation actions
/// </summary>
public enum AnimationAction
{
    Started,
    Completed,
    Cancelled,
    Paused,
    Resumed
}
