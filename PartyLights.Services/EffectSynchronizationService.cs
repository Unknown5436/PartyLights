using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;

namespace PartyLights.Services;

/// <summary>
/// Service for synchronizing multiple lighting effects in real-time
/// </summary>
public class EffectSynchronizationService : IDisposable
{
    private readonly ILogger<EffectSynchronizationService> _logger;
    private readonly RealTimeEffectProcessingEngine _effectEngine;
    private readonly SmoothTransitionManager _transitionManager;
    private readonly IAdvancedDeviceControlService _deviceControlService;
    private readonly ConcurrentDictionary<string, SynchronizedEffectGroup> _activeGroups = new();
    private readonly Timer _synchronizationTimer;
    private readonly object _lockObject = new();

    private const int SynchronizationIntervalMs = 16; // ~60 FPS
    private bool _isSynchronizing;
    private DateTime _lastSyncTime = DateTime.MinValue;
    private float _globalBeatPhase = 0f;
    private float _globalTempo = 120f;

    public event EventHandler<SynchronizationEventArgs>? SynchronizationStarted;
    public event EventHandler<SynchronizationEventArgs>? SynchronizationStopped;
    public event EventHandler<SynchronizationEventArgs>? SynchronizationError;
    public event EventHandler<EffectGroupEventArgs>? EffectGroupCreated;
    public event EventHandler<EffectGroupEventArgs>? EffectGroupRemoved;

    public EffectSynchronizationService(
        ILogger<EffectSynchronizationService> logger,
        RealTimeEffectProcessingEngine effectEngine,
        SmoothTransitionManager transitionManager,
        IAdvancedDeviceControlService deviceControlService)
    {
        _logger = logger;
        _effectEngine = effectEngine;
        _transitionManager = transitionManager;
        _deviceControlService = deviceControlService;

        _synchronizationTimer = new Timer(SynchronizeEffects, null, SynchronizationIntervalMs, SynchronizationIntervalMs);
        _isSynchronizing = true;

        _logger.LogInformation("Effect synchronization service initialized");
    }

    /// <summary>
    /// Creates a synchronized group of effects
    /// </summary>
    public async Task<string> CreateEffectGroupAsync(EffectGroupRequest request)
    {
        try
        {
            var groupId = Guid.NewGuid().ToString();

            var group = new SynchronizedEffectGroup
            {
                Id = groupId,
                Request = request,
                CreatedTime = DateTime.UtcNow,
                IsActive = true,
                Effects = new List<string>()
            };

            // Create individual effects
            foreach (var effectRequest in request.Effects)
            {
                var effectId = await _effectEngine.StartEffectAsync(effectRequest);
                if (!string.IsNullOrEmpty(effectId))
                {
                    group.Effects.Add(effectId);
                }
            }

            lock (_lockObject)
            {
                _activeGroups[groupId] = group;
            }

            EffectGroupCreated?.Invoke(this, new EffectGroupEventArgs(groupId, EffectGroupStatus.Created));
            _logger.LogInformation("Created synchronized effect group: {GroupId}", groupId);

            return groupId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating effect group");
            SynchronizationError?.Invoke(this, new SynchronizationEventArgs("Failed to create effect group", ex.Message));
            return string.Empty;
        }
    }

    /// <summary>
    /// Removes a synchronized effect group
    /// </summary>
    public async Task<bool> RemoveEffectGroupAsync(string groupId)
    {
        try
        {
            lock (_lockObject)
            {
                if (_activeGroups.TryGetValue(groupId, out var group))
                {
                    // Stop all effects in the group
                    foreach (var effectId in group.Effects)
                    {
                        await _effectEngine.StopEffectAsync(effectId);
                    }

                    group.IsActive = false;
                    _activeGroups.Remove(groupId);

                    EffectGroupRemoved?.Invoke(this, new EffectGroupEventArgs(groupId, EffectGroupStatus.Removed));
                    _logger.LogInformation("Removed synchronized effect group: {GroupId}", groupId);

                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing effect group: {GroupId}", groupId);
            SynchronizationError?.Invoke(this, new SynchronizationEventArgs($"Failed to remove effect group {groupId}", ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Updates synchronization parameters for a group
    /// </summary>
    public async Task<bool> UpdateGroupSynchronizationAsync(string groupId, SynchronizationUpdateRequest updateRequest)
    {
        try
        {
            lock (_lockObject)
            {
                if (_activeGroups.TryGetValue(groupId, out var group))
                {
                    // Update synchronization parameters
                    if (updateRequest.Tempo.HasValue)
                    {
                        group.Request.Tempo = updateRequest.Tempo.Value;
                        _globalTempo = updateRequest.Tempo.Value;
                    }

                    if (updateRequest.PhaseOffset.HasValue)
                    {
                        group.Request.PhaseOffset = updateRequest.PhaseOffset.Value;
                    }

                    if (updateRequest.IntensityMultiplier.HasValue)
                    {
                        group.Request.IntensityMultiplier = updateRequest.IntensityMultiplier.Value;
                    }

                    if (updateRequest.ColorShift.HasValue)
                    {
                        group.Request.ColorShift = updateRequest.ColorShift.Value;
                    }

                    _logger.LogInformation("Updated synchronization for group: {GroupId}", groupId);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating group synchronization: {GroupId}", groupId);
            return false;
        }
    }

    /// <summary>
    /// Gets status of all synchronized effect groups
    /// </summary>
    public IEnumerable<EffectGroupStatusInfo> GetActiveGroups()
    {
        lock (_lockObject)
        {
            return _activeGroups.Values.Select(g => new EffectGroupStatusInfo
            {
                Id = g.Id,
                Name = g.Request.Name,
                EffectCount = g.Effects.Count,
                IsActive = g.IsActive,
                CreatedTime = g.CreatedTime,
                Tempo = g.Request.Tempo,
                PhaseOffset = g.Request.PhaseOffset
            }).ToList();
        }
    }

    /// <summary>
    /// Starts global synchronization
    /// </summary>
    public async Task<bool> StartSynchronizationAsync()
    {
        try
        {
            if (_isSynchronizing)
            {
                _logger.LogWarning("Synchronization is already running");
                return true;
            }

            _isSynchronizing = true;
            SynchronizationStarted?.Invoke(this, new SynchronizationEventArgs("Global synchronization started"));
            _logger.LogInformation("Started global effect synchronization");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting synchronization");
            SynchronizationError?.Invoke(this, new SynchronizationEventArgs("Failed to start synchronization", ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Stops global synchronization
    /// </summary>
    public async Task StopSynchronizationAsync()
    {
        try
        {
            if (!_isSynchronizing)
            {
                _logger.LogWarning("Synchronization is not running");
                return;
            }

            _isSynchronizing = false;

            // Stop all active groups
            var groupIds = _activeGroups.Keys.ToList();
            foreach (var groupId in groupIds)
            {
                await RemoveEffectGroupAsync(groupId);
            }

            SynchronizationStopped?.Invoke(this, new SynchronizationEventArgs("Global synchronization stopped"));
            _logger.LogInformation("Stopped global effect synchronization");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping synchronization");
            SynchronizationError?.Invoke(this, new SynchronizationEventArgs("Failed to stop synchronization", ex.Message));
        }
    }

    #region Private Methods

    private async void SynchronizeEffects(object? state)
    {
        if (!_isSynchronizing)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;
            var deltaTime = (float)(currentTime - _lastSyncTime).TotalMilliseconds;
            _lastSyncTime = currentTime;

            // Update global beat phase
            UpdateGlobalBeatPhase(deltaTime);

            // Synchronize all active groups
            lock (_lockObject)
            {
                foreach (var groupEntry in _activeGroups)
                {
                    var groupId = groupEntry.Key;
                    var group = groupEntry.Value;

                    if (group.IsActive)
                    {
                        SynchronizeGroup(group, deltaTime);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in synchronization loop");
            SynchronizationError?.Invoke(this, new SynchronizationEventArgs("Synchronization loop error", ex.Message));
        }
    }

    private void UpdateGlobalBeatPhase(float deltaTime)
    {
        // Calculate beat phase based on global tempo
        var beatInterval = 60000f / _globalTempo; // Convert BPM to ms
        _globalBeatPhase += deltaTime / beatInterval;

        // Keep phase between 0 and 1
        while (_globalBeatPhase >= 1f)
        {
            _globalBeatPhase -= 1f;
        }
    }

    private void SynchronizeGroup(SynchronizedEffectGroup group, float deltaTime)
    {
        try
        {
            var request = group.Request;

            // Calculate group-specific phase
            var groupPhase = (_globalBeatPhase + request.PhaseOffset) % 1f;

            // Apply synchronization to each effect in the group
            foreach (var effectId in group.Effects)
            {
                ApplyEffectSynchronization(effectId, groupPhase, request);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing group: {GroupId}", group.Id);
        }
    }

    private void ApplyEffectSynchronization(string effectId, float phase, EffectGroupRequest request)
    {
        try
        {
            // Create synchronization update request
            var updateRequest = new EffectUpdateRequest
            {
                IntensityMultiplier = request.IntensityMultiplier,
                ColorSpeed = CalculateSynchronizedColorSpeed(phase, request),
                BaseHue = CalculateSynchronizedHue(phase, request),
                CustomParameters = new Dictionary<string, object>
                {
                    ["SyncPhase"] = phase,
                    ["BeatPhase"] = _globalBeatPhase,
                    ["Tempo"] = request.Tempo
                }
            };

            // Apply update to effect
            _effectEngine.UpdateEffectAsync(effectId, updateRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying effect synchronization: {EffectId}", effectId);
        }
    }

    private float CalculateSynchronizedColorSpeed(float phase, EffectGroupRequest request)
    {
        // Synchronize color speed with beat phase
        var baseSpeed = request.ColorSpeed;
        var syncMultiplier = 1f + (float)Math.Sin(phase * 2 * Math.PI) * 0.5f;
        return baseSpeed * syncMultiplier;
    }

    private float CalculateSynchronizedHue(float phase, EffectGroupRequest request)
    {
        // Synchronize hue with beat phase
        var baseHue = request.BaseHue;
        var phaseShift = phase * 360f;
        var colorShift = request.ColorShift;

        return (baseHue + phaseShift + colorShift) % 360f;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isSynchronizing = false;
            _synchronizationTimer?.Dispose();

            // Stop all active groups
            var groupIds = _activeGroups.Keys.ToList();
            foreach (var groupId in groupIds)
            {
                RemoveEffectGroupAsync(groupId).Wait(1000);
            }

            _logger.LogInformation("Effect synchronization service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing effect synchronization service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Request to create a synchronized effect group
/// </summary>
public class EffectGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public List<EffectRequest> Effects { get; set; } = new();
    public float Tempo { get; set; } = 120f;
    public float PhaseOffset { get; set; } = 0f;
    public float IntensityMultiplier { get; set; } = 1f;
    public float ColorSpeed { get; set; } = 1f;
    public float BaseHue { get; set; } = 0f;
    public float ColorShift { get; set; } = 0f;
    public SynchronizationMode Mode { get; set; } = SynchronizationMode.BeatSync;
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Request to update synchronization parameters
/// </summary>
public class SynchronizationUpdateRequest
{
    public float? Tempo { get; set; }
    public float? PhaseOffset { get; set; }
    public float? IntensityMultiplier { get; set; }
    public float? ColorShift { get; set; }
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Synchronized effect group
/// </summary>
public class SynchronizedEffectGroup
{
    public string Id { get; set; } = string.Empty;
    public EffectGroupRequest Request { get; set; } = new();
    public DateTime CreatedTime { get; set; }
    public bool IsActive { get; set; }
    public List<string> Effects { get; set; } = new();
}

/// <summary>
/// Effect group status information
/// </summary>
public class EffectGroupStatusInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int EffectCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedTime { get; set; }
    public float Tempo { get; set; }
    public float PhaseOffset { get; set; }
}

/// <summary>
/// Effect group event arguments
/// </summary>
public class EffectGroupEventArgs : EventArgs
{
    public string GroupId { get; }
    public EffectGroupStatus Status { get; }
    public DateTime Timestamp { get; }

    public EffectGroupEventArgs(string groupId, EffectGroupStatus status)
    {
        GroupId = groupId;
        Status = status;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Synchronization modes
/// </summary>
public enum SynchronizationMode
{
    BeatSync,
    TempoSync,
    PhaseSync,
    FreeSync,
    Custom
}

/// <summary>
/// Effect group status
/// </summary>
public enum EffectGroupStatus
{
    Created,
    Removed,
    Updated,
    Error
}
