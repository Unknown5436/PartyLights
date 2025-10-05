using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;

namespace PartyLights.Services;

/// <summary>
/// Advanced preset execution engine with comprehensive features
/// </summary>
public class AdvancedPresetExecutionEngine : IDisposable
{
    private readonly ILogger<AdvancedPresetExecutionEngine> _logger;
    private readonly RealTimeEffectProcessingEngine _effectEngine;
    private readonly SmoothTransitionManager _transitionManager;
    private readonly EffectSynchronizationService _synchronizationService;
    private readonly IAdvancedDeviceControlService _deviceControlService;
    private readonly ConcurrentDictionary<string, PresetExecutionContext> _activeExecutions = new();
    private readonly Timer _executionTimer;
    private readonly object _lockObject = new();

    private const int ExecutionUpdateIntervalMs = 16; // ~60 FPS
    private bool _isExecuting;

    // Execution state management
    private readonly Dictionary<string, ExecutionState> _executionStates = new();
    private readonly Dictionary<string, EffectSequence> _effectSequences = new();
    private readonly Dictionary<string, TransitionSequence> _transitionSequences = new();

    public event EventHandler<PresetExecutionEventArgs>? PresetExecutionStarted;
    public event EventHandler<PresetExecutionEventArgs>? PresetExecutionCompleted;
    public event EventHandler<PresetExecutionEventArgs>? PresetExecutionPaused;
    public event EventHandler<PresetExecutionEventArgs>? PresetExecutionResumed;
    public event EventHandler<PresetExecutionEventArgs>? PresetExecutionStopped;
    public event EventHandler<PresetExecutionErrorEventArgs>? PresetExecutionError;

    public AdvancedPresetExecutionEngine(
        ILogger<AdvancedPresetExecutionEngine> logger,
        RealTimeEffectProcessingEngine effectEngine,
        SmoothTransitionManager transitionManager,
        EffectSynchronizationService synchronizationService,
        IAdvancedDeviceControlService deviceControlService)
    {
        _logger = logger;
        _effectEngine = effectEngine;
        _transitionManager = transitionManager;
        _synchronizationService = synchronizationService;
        _deviceControlService = deviceControlService;

        _executionTimer = new Timer(ProcessExecutions, null, ExecutionUpdateIntervalMs, ExecutionUpdateIntervalMs);
        _isExecuting = true;

        _logger.LogInformation("Advanced preset execution engine initialized");
    }

    /// <summary>
    /// Executes a lighting preset
    /// </summary>
    public async Task<string> ExecutePresetAsync(PresetExecutionRequest request)
    {
        try
        {
            var executionId = Guid.NewGuid().ToString();

            var context = new PresetExecutionContext
            {
                Id = executionId,
                Preset = request.Preset,
                Settings = request.Settings,
                StartTime = DateTime.UtcNow,
                IsActive = true,
                CurrentPhase = 0,
                PhaseProgress = 0f,
                Effects = new List<string>(),
                Transitions = new List<string>()
            };

            // Initialize execution state
            var executionState = new ExecutionState
            {
                ExecutionId = executionId,
                Preset = request.Preset,
                Settings = request.Settings,
                StartTime = DateTime.UtcNow,
                IsActive = true,
                CurrentPhase = 0,
                PhaseProgress = 0f,
                CompletedPhases = 0,
                TotalPhases = CalculateTotalPhases(request.Preset)
            };

            // Create effect sequence
            var effectSequence = CreateEffectSequence(request.Preset, request.Settings);

            // Create transition sequence
            var transitionSequence = CreateTransitionSequence(request.Preset, request.Settings);

            lock (_lockObject)
            {
                _activeExecutions[executionId] = context;
                _executionStates[executionId] = executionState;
                _effectSequences[executionId] = effectSequence;
                _transitionSequences[executionId] = transitionSequence;
            }

            // Start execution
            await StartExecutionPhase(executionId, 0);

            PresetExecutionStarted?.Invoke(this, new PresetExecutionEventArgs(executionId, PresetExecutionStatus.Started));
            _logger.LogInformation("Started preset execution: {PresetName} ({ExecutionId})", request.Preset.Name, executionId);

            return executionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing preset: {PresetName}", request.Preset.Name);
            PresetExecutionError?.Invoke(this, new PresetExecutionErrorEventArgs("Failed to execute preset", ex.Message));
            return string.Empty;
        }
    }

    /// <summary>
    /// Executes a preset collection
    /// </summary>
    public async Task<string> ExecuteCollectionAsync(CollectionExecutionRequest request)
    {
        try
        {
            var executionId = Guid.NewGuid().ToString();

            var context = new PresetExecutionContext
            {
                Id = executionId,
                Collection = request.Collection,
                Settings = request.Settings,
                StartTime = DateTime.UtcNow,
                IsActive = true,
                CurrentPhase = 0,
                PhaseProgress = 0f,
                Effects = new List<string>(),
                Transitions = new List<string>()
            };

            // Initialize execution state
            var executionState = new ExecutionState
            {
                ExecutionId = executionId,
                Collection = request.Collection,
                Settings = request.Settings,
                StartTime = DateTime.UtcNow,
                IsActive = true,
                CurrentPhase = 0,
                PhaseProgress = 0f,
                CompletedPhases = 0,
                TotalPhases = CalculateCollectionPhases(request.Collection)
            };

            // Create collection effect sequence
            var effectSequence = CreateCollectionEffectSequence(request.Collection, request.Settings);

            // Create collection transition sequence
            var transitionSequence = CreateCollectionTransitionSequence(request.Collection, request.Settings);

            lock (_lockObject)
            {
                _activeExecutions[executionId] = context;
                _executionStates[executionId] = executionState;
                _effectSequences[executionId] = effectSequence;
                _transitionSequences[executionId] = transitionSequence;
            }

            // Start execution
            await StartExecutionPhase(executionId, 0);

            PresetExecutionStarted?.Invoke(this, new PresetExecutionEventArgs(executionId, PresetExecutionStatus.Started));
            _logger.LogInformation("Started collection execution: {CollectionName} ({ExecutionId})", request.Collection.Name, executionId);

            return executionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing collection: {CollectionName}", request.Collection.Name);
            PresetExecutionError?.Invoke(this, new PresetExecutionErrorEventArgs("Failed to execute collection", ex.Message));
            return string.Empty;
        }
    }

    /// <summary>
    /// Pauses a preset execution
    /// </summary>
    public async Task<bool> PauseExecutionAsync(string executionId)
    {
        try
        {
            lock (_lockObject)
            {
                if (_executionStates.TryGetValue(executionId, out var state))
                {
                    state.IsPaused = true;
                    state.PauseTime = DateTime.UtcNow;
                }
            }

            // Pause all active effects
            if (_activeExecutions.TryGetValue(executionId, out var context))
            {
                foreach (var effectId in context.Effects)
                {
                    await _effectEngine.StopEffectAsync(effectId);
                }
            }

            PresetExecutionPaused?.Invoke(this, new PresetExecutionEventArgs(executionId, PresetExecutionStatus.Paused));
            _logger.LogInformation("Paused execution: {ExecutionId}", executionId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing execution: {ExecutionId}", executionId);
            return false;
        }
    }

    /// <summary>
    /// Resumes a paused preset execution
    /// </summary>
    public async Task<bool> ResumeExecutionAsync(string executionId)
    {
        try
        {
            lock (_lockObject)
            {
                if (_executionStates.TryGetValue(executionId, out var state))
                {
                    state.IsPaused = false;
                    state.ResumeTime = DateTime.UtcNow;
                }
            }

            // Resume execution from current phase
            await StartExecutionPhase(executionId, GetCurrentPhase(executionId));

            PresetExecutionResumed?.Invoke(this, new PresetExecutionEventArgs(executionId, PresetExecutionStatus.Resumed));
            _logger.LogInformation("Resumed execution: {ExecutionId}", executionId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming execution: {ExecutionId}", executionId);
            return false;
        }
    }

    /// <summary>
    /// Stops a preset execution
    /// </summary>
    public async Task<bool> StopExecutionAsync(string executionId)
    {
        try
        {
            lock (_lockObject)
            {
                if (_executionStates.TryGetValue(executionId, out var state))
                {
                    state.IsActive = false;
                    state.EndTime = DateTime.UtcNow;
                }
            }

            // Stop all active effects
            if (_activeExecutions.TryGetValue(executionId, out var context))
            {
                foreach (var effectId in context.Effects)
                {
                    await _effectEngine.StopEffectAsync(effectId);
                }
            }

            // Clean up execution state
            _activeExecutions.TryRemove(executionId, out _);
            _executionStates.Remove(executionId);
            _effectSequences.Remove(executionId);
            _transitionSequences.Remove(executionId);

            PresetExecutionStopped?.Invoke(this, new PresetExecutionEventArgs(executionId, PresetExecutionStatus.Stopped));
            _logger.LogInformation("Stopped execution: {ExecutionId}", executionId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping execution: {ExecutionId}", executionId);
            return false;
        }
    }

    /// <summary>
    /// Gets execution status
    /// </summary>
    public ExecutionStatusInfo? GetExecutionStatus(string executionId)
    {
        lock (_lockObject)
        {
            if (_executionStates.TryGetValue(executionId, out var state))
            {
                return new ExecutionStatusInfo
                {
                    ExecutionId = executionId,
                    IsActive = state.IsActive,
                    IsPaused = state.IsPaused,
                    CurrentPhase = state.CurrentPhase,
                    PhaseProgress = state.PhaseProgress,
                    CompletedPhases = state.CompletedPhases,
                    TotalPhases = state.TotalPhases,
                    StartTime = state.StartTime,
                    ElapsedTime = DateTime.UtcNow - state.StartTime,
                    PresetName = state.Preset?.Name ?? state.Collection?.Name ?? "Unknown"
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all active executions
    /// </summary>
    public IEnumerable<ExecutionStatusInfo> GetActiveExecutions()
    {
        lock (_lockObject)
        {
            return _executionStates.Values
                .Where(s => s.IsActive)
                .Select(s => new ExecutionStatusInfo
                {
                    ExecutionId = s.ExecutionId,
                    IsActive = s.IsActive,
                    IsPaused = s.IsPaused,
                    CurrentPhase = s.CurrentPhase,
                    PhaseProgress = s.PhaseProgress,
                    CompletedPhases = s.CompletedPhases,
                    TotalPhases = s.TotalPhases,
                    StartTime = s.StartTime,
                    ElapsedTime = DateTime.UtcNow - s.StartTime,
                    PresetName = s.Preset?.Name ?? s.Collection?.Name ?? "Unknown"
                })
                .ToList();
        }
    }

    #region Private Methods

    private async void ProcessExecutions(object? state)
    {
        if (!_isExecuting)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;

            lock (_lockObject)
            {
                foreach (var executionEntry in _executionStates)
                {
                    var executionId = executionEntry.Key;
                    var executionState = executionEntry.Value;

                    if (!executionState.IsActive || executionState.IsPaused)
                    {
                        continue;
                    }

                    try
                    {
                        await ProcessExecutionPhase(executionId, executionState, currentTime);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing execution phase: {ExecutionId}", executionId);
                        await StopExecutionAsync(executionId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in execution processing loop");
        }
    }

    private async Task ProcessExecutionPhase(string executionId, ExecutionState state, DateTime currentTime)
    {
        // Update phase progress
        var phaseDuration = GetPhaseDuration(executionId, state.CurrentPhase);
        var phaseElapsed = (currentTime - state.PhaseStartTime).TotalMilliseconds;
        state.PhaseProgress = Math.Min((float)(phaseElapsed / phaseDuration), 1f);

        // Check if phase is complete
        if (state.PhaseProgress >= 1f)
        {
            await CompleteExecutionPhase(executionId, state);
        }
        else
        {
            // Update effects for current phase
            await UpdatePhaseEffects(executionId, state);
        }
    }

    private async Task StartExecutionPhase(string executionId, int phaseIndex)
    {
        try
        {
            if (!_executionStates.TryGetValue(executionId, out var state))
            {
                return;
            }

            state.CurrentPhase = phaseIndex;
            state.PhaseProgress = 0f;
            state.PhaseStartTime = DateTime.UtcNow;

            // Start effects for this phase
            await StartPhaseEffects(executionId, phaseIndex);

            _logger.LogDebug("Started execution phase {Phase} for {ExecutionId}", phaseIndex, executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting execution phase: {ExecutionId}, Phase: {Phase}", executionId, phaseIndex);
        }
    }

    private async Task CompleteExecutionPhase(string executionId, ExecutionState state)
    {
        try
        {
            state.CompletedPhases++;

            // Check if execution is complete
            if (state.CompletedPhases >= state.TotalPhases)
            {
                await CompleteExecution(executionId);
            }
            else
            {
                // Start next phase
                await StartExecutionPhase(executionId, state.CurrentPhase + 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing execution phase: {ExecutionId}", executionId);
        }
    }

    private async Task CompleteExecution(string executionId)
    {
        try
        {
            if (_executionStates.TryGetValue(executionId, out var state))
            {
                state.IsActive = false;
                state.EndTime = DateTime.UtcNow;
            }

            // Stop all effects
            if (_activeExecutions.TryGetValue(executionId, out var context))
            {
                foreach (var effectId in context.Effects)
                {
                    await _effectEngine.StopEffectAsync(effectId);
                }
            }

            // Clean up execution state
            _activeExecutions.TryRemove(executionId, out _);
            _executionStates.Remove(executionId);
            _effectSequences.Remove(executionId);
            _transitionSequences.Remove(executionId);

            PresetExecutionCompleted?.Invoke(this, new PresetExecutionEventArgs(executionId, PresetExecutionStatus.Completed));
            _logger.LogInformation("Completed execution: {ExecutionId}", executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing execution: {ExecutionId}", executionId);
        }
    }

    private async Task StartPhaseEffects(string executionId, int phaseIndex)
    {
        try
        {
            if (!_effectSequences.TryGetValue(executionId, out var sequence))
            {
                return;
            }

            var phaseEffects = sequence.Phases.ElementAtOrDefault(phaseIndex);
            if (phaseEffects == null)
            {
                return;
            }

            foreach (var effectConfig in phaseEffects.Effects)
            {
                var effectRequest = new EffectRequest
                {
                    EffectType = effectConfig.Type,
                    TargetDevices = effectConfig.TargetDevices,
                    IntensityMultiplier = effectConfig.IntensityMultiplier,
                    ColorSpeed = effectConfig.ColorSpeed,
                    BaseHue = effectConfig.BaseHue,
                    TransitionDurationMs = effectConfig.TransitionDurationMs,
                    MaxDurationMs = effectConfig.MaxDurationMs,
                    LoopEnabled = effectConfig.LoopEnabled,
                    CustomParameters = effectConfig.CustomParameters
                };

                var effectId = await _effectEngine.StartEffectAsync(effectRequest);
                if (!string.IsNullOrEmpty(effectId))
                {
                    if (_activeExecutions.TryGetValue(executionId, out var context))
                    {
                        context.Effects.Add(effectId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting phase effects: {ExecutionId}, Phase: {Phase}", executionId, phaseIndex);
        }
    }

    private async Task UpdatePhaseEffects(string executionId, ExecutionState state)
    {
        try
        {
            // Update effect parameters based on phase progress
            if (_activeExecutions.TryGetValue(executionId, out var context))
            {
                foreach (var effectId in context.Effects)
                {
                    var updateRequest = new EffectUpdateRequest
                    {
                        IntensityMultiplier = CalculatePhaseIntensity(state),
                        ColorSpeed = CalculatePhaseColorSpeed(state),
                        BaseHue = CalculatePhaseHue(state),
                        CustomParameters = new Dictionary<string, object>
                        {
                            ["PhaseProgress"] = state.PhaseProgress,
                            ["CurrentPhase"] = state.CurrentPhase
                        }
                    };

                    await _effectEngine.UpdateEffectAsync(effectId, updateRequest);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating phase effects: {ExecutionId}", executionId);
        }
    }

    private EffectSequence CreateEffectSequence(LightingPreset preset, PresetExecutionSettings settings)
    {
        var sequence = new EffectSequence
        {
            ExecutionId = Guid.NewGuid().ToString(),
            Phases = new List<EffectPhase>()
        };

        // Create phases based on preset effects
        foreach (var effect in preset.Effects)
        {
            var phase = new EffectPhase
            {
                PhaseIndex = sequence.Phases.Count,
                Effects = new List<EffectConfiguration> { effect },
                Duration = effect.MaxDurationMs > 0 ? TimeSpan.FromMilliseconds(effect.MaxDurationMs) : TimeSpan.FromSeconds(10),
                TransitionDuration = TimeSpan.FromMilliseconds(effect.TransitionDurationMs)
            };

            sequence.Phases.Add(phase);
        }

        return sequence;
    }

    private EffectSequence CreateCollectionEffectSequence(PresetCollection collection, PresetExecutionSettings settings)
    {
        var sequence = new EffectSequence
        {
            ExecutionId = Guid.NewGuid().ToString(),
            Phases = new List<EffectPhase>()
        };

        // Create phases based on collection execution order
        switch (collection.ExecutionOrder)
        {
            case ExecutionOrder.Sequential:
                CreateSequentialPhases(sequence, collection);
                break;
            case ExecutionOrder.Parallel:
                CreateParallelPhases(sequence, collection);
                break;
            case ExecutionOrder.Random:
                CreateRandomPhases(sequence, collection);
                break;
            case ExecutionOrder.Custom:
                CreateCustomPhases(sequence, collection);
                break;
        }

        return sequence;
    }

    private TransitionSequence CreateTransitionSequence(LightingPreset preset, PresetExecutionSettings settings)
    {
        var sequence = new TransitionSequence
        {
            ExecutionId = Guid.NewGuid().ToString(),
            Transitions = new List<TransitionConfiguration>()
        };

        // Create transitions between effects
        for (int i = 0; i < preset.Effects.Count - 1; i++)
        {
            var transition = new TransitionConfiguration
            {
                FromEffect = preset.Effects[i],
                ToEffect = preset.Effects[i + 1],
                Duration = TimeSpan.FromMilliseconds(preset.Effects[i].TransitionDurationMs),
                EasingFunction = EasingFunction.EaseInOut
            };

            sequence.Transitions.Add(transition);
        }

        return sequence;
    }

    private TransitionSequence CreateCollectionTransitionSequence(PresetCollection collection, PresetExecutionSettings settings)
    {
        var sequence = new TransitionSequence
        {
            ExecutionId = Guid.NewGuid().ToString(),
            Transitions = new List<TransitionConfiguration>()
        };

        // Create transitions based on collection settings
        foreach (var transitionSetting in collection.TransitionSettings.Transitions)
        {
            var transition = new TransitionConfiguration
            {
                Duration = transitionSetting.Duration,
                EasingFunction = transitionSetting.EasingFunction,
                CustomParameters = transitionSetting.CustomParameters
            };

            sequence.Transitions.Add(transition);
        }

        return sequence;
    }

    private void CreateSequentialPhases(EffectSequence sequence, PresetCollection collection)
    {
        // Create sequential phases for each preset in the collection
        foreach (var presetId in collection.PresetIds)
        {
            // This would typically load the preset and create phases
            // For now, it's a placeholder implementation
        }
    }

    private void CreateParallelPhases(EffectSequence sequence, PresetCollection collection)
    {
        // Create parallel phases for all presets in the collection
        var parallelPhase = new EffectPhase
        {
            PhaseIndex = 0,
            Effects = new List<EffectConfiguration>(),
            Duration = TimeSpan.FromMinutes(5), // Default duration
            TransitionDuration = TimeSpan.FromMilliseconds(500)
        };

        sequence.Phases.Add(parallelPhase);
    }

    private void CreateRandomPhases(EffectSequence sequence, PresetCollection collection)
    {
        // Create random phases for presets in the collection
        var random = new Random();
        var shuffledPresets = collection.PresetIds.OrderBy(x => random.Next()).ToList();

        foreach (var presetId in shuffledPresets)
        {
            // This would typically load the preset and create phases
            // For now, it's a placeholder implementation
        }
    }

    private void CreateCustomPhases(EffectSequence sequence, PresetCollection collection)
    {
        // Create custom phases based on collection custom parameters
        // This would typically use custom execution logic
    }

    private int CalculateTotalPhases(LightingPreset preset)
    {
        return preset.Effects.Count;
    }

    private int CalculateCollectionPhases(PresetCollection collection)
    {
        return collection.ExecutionOrder switch
        {
            ExecutionOrder.Sequential => collection.PresetIds.Count,
            ExecutionOrder.Parallel => 1,
            ExecutionOrder.Random => collection.PresetIds.Count,
            ExecutionOrder.Custom => collection.CustomParameters.GetValueOrDefault("PhaseCount", 1).ToString()?.ParseInt() ?? 1,
            _ => 1
        };
    }

    private double GetPhaseDuration(string executionId, int phaseIndex)
    {
        if (_effectSequences.TryGetValue(executionId, out var sequence))
        {
            var phase = sequence.Phases.ElementAtOrDefault(phaseIndex);
            return phase?.Duration.TotalMilliseconds ?? 10000; // Default 10 seconds
        }

        return 10000; // Default 10 seconds
    }

    private int GetCurrentPhase(string executionId)
    {
        if (_executionStates.TryGetValue(executionId, out var state))
        {
            return state.CurrentPhase;
        }

        return 0;
    }

    private float CalculatePhaseIntensity(ExecutionState state)
    {
        // Calculate intensity based on phase progress
        return 1f; // Placeholder implementation
    }

    private float CalculatePhaseColorSpeed(ExecutionState state)
    {
        // Calculate color speed based on phase progress
        return 1f; // Placeholder implementation
    }

    private float CalculatePhaseHue(ExecutionState state)
    {
        // Calculate hue based on phase progress
        return 0f; // Placeholder implementation
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isExecuting = false;
            _executionTimer?.Dispose();

            // Stop all active executions
            var executionIds = _executionStates.Keys.ToList();
            foreach (var executionId in executionIds)
            {
                StopExecutionAsync(executionId).Wait(1000);
            }

            _logger.LogInformation("Advanced preset execution engine disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing advanced preset execution engine");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Preset execution request
/// </summary>
public class PresetExecutionRequest
{
    public LightingPreset Preset { get; set; } = new();
    public PresetExecutionSettings Settings { get; set; } = new();
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Collection execution request
/// </summary>
public class CollectionExecutionRequest
{
    public PresetCollection Collection { get; set; } = new();
    public PresetExecutionSettings Settings { get; set; } = new();
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Execution state
/// </summary>
public class ExecutionState
{
    public string ExecutionId { get; set; } = string.Empty;
    public LightingPreset? Preset { get; set; }
    public PresetCollection? Collection { get; set; }
    public PresetExecutionSettings Settings { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime PhaseStartTime { get; set; }
    public DateTime? PauseTime { get; set; }
    public DateTime? ResumeTime { get; set; }
    public bool IsActive { get; set; }
    public bool IsPaused { get; set; }
    public int CurrentPhase { get; set; }
    public float PhaseProgress { get; set; }
    public int CompletedPhases { get; set; }
    public int TotalPhases { get; set; }
}

/// <summary>
/// Effect sequence
/// </summary>
public class EffectSequence
{
    public string ExecutionId { get; set; } = string.Empty;
    public List<EffectPhase> Phases { get; set; } = new();
}

/// <summary>
/// Effect phase
/// </summary>
public class EffectPhase
{
    public int PhaseIndex { get; set; }
    public List<EffectConfiguration> Effects { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public TimeSpan TransitionDuration { get; set; }
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Transition sequence
/// </summary>
public class TransitionSequence
{
    public string ExecutionId { get; set; } = string.Empty;
    public List<TransitionConfiguration> Transitions { get; set; } = new();
}

/// <summary>
/// Transition configuration
/// </summary>
public class TransitionConfiguration
{
    public EffectConfiguration? FromEffect { get; set; }
    public EffectConfiguration? ToEffect { get; set; }
    public TimeSpan Duration { get; set; }
    public EasingFunction EasingFunction { get; set; } = EasingFunction.EaseInOut;
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Execution status information
/// </summary>
public class ExecutionStatusInfo
{
    public string ExecutionId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsPaused { get; set; }
    public int CurrentPhase { get; set; }
    public float PhaseProgress { get; set; }
    public int CompletedPhases { get; set; }
    public int TotalPhases { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public string PresetName { get; set; } = string.Empty;
}

/// <summary>
/// Preset execution event arguments
/// </summary>
public class PresetExecutionEventArgs : EventArgs
{
    public string ExecutionId { get; }
    public PresetExecutionStatus Status { get; }
    public DateTime Timestamp { get; }

    public PresetExecutionEventArgs(string executionId, PresetExecutionStatus status)
    {
        ExecutionId = executionId;
        Status = status;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Preset execution error event arguments
/// </summary>
public class PresetExecutionErrorEventArgs : EventArgs
{
    public string ErrorMessage { get; }
    public string Details { get; }
    public DateTime Timestamp { get; }

    public PresetExecutionErrorEventArgs(string errorMessage, string details)
    {
        ErrorMessage = errorMessage;
        Details = details;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Preset execution status
/// </summary>
public enum PresetExecutionStatus
{
    Started,
    Paused,
    Resumed,
    Stopped,
    Completed,
    Error
}

/// <summary>
/// Extension methods
/// </summary>
public static class Extensions
{
    public static int ParseInt(this string? value)
    {
        return int.TryParse(value, out var result) ? result : 0;
    }
}
