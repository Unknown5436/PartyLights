using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;

namespace PartyLights.Services;

/// <summary>
/// Preset UI service for managing presets through the user interface
/// </summary>
public class PresetUiService : IDisposable
{
    private readonly ILogger<PresetUiService> _logger;
    private readonly AdvancedPresetManagementService _presetManagementService;
    private readonly AdvancedPresetExecutionEngine _executionEngine;
    private readonly ConcurrentDictionary<string, PresetUiState> _uiStates = new();
    private readonly Timer _uiUpdateTimer;
    private readonly object _lockObject = new();

    private const int UiUpdateIntervalMs = 100; // 10 FPS for UI updates
    private bool _isUpdating;

    // UI state management
    private readonly Dictionary<string, PresetEditorState> _editorStates = new();
    private readonly Dictionary<string, PresetPreviewState> _previewStates = new();
    private readonly Dictionary<string, PresetLibraryState> _libraryStates = new();

    public event EventHandler<PresetUiEventArgs>? PresetUiUpdated;
    public event EventHandler<PresetEditorEventArgs>? PresetEditorUpdated;
    public event EventHandler<PresetPreviewEventArgs>? PresetPreviewUpdated;
    public event EventHandler<PresetLibraryEventArgs>? PresetLibraryUpdated;

    public PresetUiService(
        ILogger<PresetUiService> logger,
        AdvancedPresetManagementService presetManagementService,
        AdvancedPresetExecutionEngine executionEngine)
    {
        _logger = logger;
        _presetManagementService = presetManagementService;
        _executionEngine = executionEngine;

        _uiUpdateTimer = new Timer(UpdateUiStates, null, UiUpdateIntervalMs, UiUpdateIntervalMs);
        _isUpdating = true;

        _logger.LogInformation("Preset UI service initialized");
    }

    /// <summary>
    /// Opens a preset editor
    /// </summary>
    public async Task<string> OpenPresetEditorAsync(PresetEditorRequest request)
    {
        try
        {
            var editorId = Guid.NewGuid().ToString();

            var editorState = new PresetEditorState
            {
                EditorId = editorId,
                PresetId = request.PresetId,
                Mode = request.Mode,
                IsActive = true,
                CreatedTime = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                HasUnsavedChanges = false,
                ValidationErrors = new List<string>(),
                PreviewEnabled = request.PreviewEnabled,
                AutoSaveEnabled = request.AutoSaveEnabled
            };

            // Load preset if editing existing
            if (!string.IsNullOrEmpty(request.PresetId))
            {
                var preset = _presetManagementService.GetPreset(request.PresetId);
                if (preset != null)
                {
                    editorState.Preset = preset;
                    editorState.OriginalPreset = ClonePreset(preset);
                }
            }
            else
            {
                // Create new preset
                editorState.Preset = new LightingPreset
                {
                    Id = string.Empty,
                    Name = "New Preset",
                    Description = string.Empty,
                    Category = PresetCategory.Custom,
                    Type = PresetType.Custom,
                    Metadata = new PresetMetadata
                    {
                        CreatedBy = "User",
                        CreatedAt = DateTime.UtcNow,
                        LastModified = DateTime.UtcNow,
                        Version = "1.0.0",
                        Tags = new List<string>()
                    },
                    Effects = new List<EffectConfiguration>(),
                    DeviceGroups = new List<DeviceGroupConfiguration>(),
                    AudioSettings = new AudioPresetSettings(),
                    ExecutionSettings = new PresetExecutionSettings(),
                    CustomParameters = new Dictionary<string, object>()
                };
            }

            lock (_lockObject)
            {
                _editorStates[editorId] = editorState;
            }

            PresetEditorUpdated?.Invoke(this, new PresetEditorEventArgs(editorId, PresetEditorAction.Opened));
            _logger.LogInformation("Opened preset editor: {EditorId}", editorId);

            return editorId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening preset editor");
            return string.Empty;
        }
    }

    /// <summary>
    /// Updates preset in editor
    /// </summary>
    public async Task<bool> UpdatePresetInEditorAsync(string editorId, PresetUpdateRequest request)
    {
        try
        {
            if (!_editorStates.TryGetValue(editorId, out var editorState))
            {
                _logger.LogWarning("Editor not found: {EditorId}", editorId);
                return false;
            }

            // Update preset
            var updateRequest = new PresetUpdateRequest
            {
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                Type = request.Type,
                Tags = request.Tags,
                Difficulty = request.Difficulty,
                EstimatedDuration = request.EstimatedDuration,
                Effects = request.Effects,
                DeviceGroups = request.DeviceGroups,
                AudioSettings = request.AudioSettings,
                ExecutionSettings = request.ExecutionSettings,
                CustomParameters = request.CustomParameters
            };

            var success = await _presetManagementService.UpdatePresetAsync(editorState.PresetId, updateRequest);

            if (success)
            {
                editorState.LastModified = DateTime.UtcNow;
                editorState.HasUnsavedChanges = false;

                // Reload preset
                var updatedPreset = _presetManagementService.GetPreset(editorState.PresetId);
                if (updatedPreset != null)
                {
                    editorState.Preset = updatedPreset;
                }
            }

            PresetEditorUpdated?.Invoke(this, new PresetEditorEventArgs(editorId, PresetEditorAction.Updated));
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating preset in editor: {EditorId}", editorId);
            return false;
        }
    }

    /// <summary>
    /// Saves preset from editor
    /// </summary>
    public async Task<bool> SavePresetFromEditorAsync(string editorId)
    {
        try
        {
            if (!_editorStates.TryGetValue(editorId, out var editorState))
            {
                _logger.LogWarning("Editor not found: {EditorId}", editorId);
                return false;
            }

            // Validate preset
            var validationResult = ValidatePreset(editorState.Preset);
            if (!validationResult.IsValid)
            {
                editorState.ValidationErrors = validationResult.Errors;
                PresetEditorUpdated?.Invoke(this, new PresetEditorEventArgs(editorId, PresetEditorAction.ValidationFailed));
                return false;
            }

            bool success;
            if (string.IsNullOrEmpty(editorState.PresetId))
            {
                // Create new preset
                var createRequest = new PresetCreationRequest
                {
                    Name = editorState.Preset.Name,
                    Description = editorState.Preset.Description,
                    Category = editorState.Preset.Category,
                    Type = editorState.Preset.Type,
                    CreatedBy = "User",
                    Tags = editorState.Preset.Metadata.Tags,
                    Difficulty = editorState.Preset.Metadata.Difficulty,
                    EstimatedDuration = editorState.Preset.Metadata.EstimatedDuration,
                    Effects = editorState.Preset.Effects,
                    DeviceGroups = editorState.Preset.DeviceGroups,
                    AudioSettings = editorState.Preset.AudioSettings,
                    ExecutionSettings = editorState.Preset.ExecutionSettings,
                    CustomParameters = editorState.Preset.CustomParameters
                };

                var presetId = await _presetManagementService.CreatePresetAsync(createRequest);
                success = !string.IsNullOrEmpty(presetId);

                if (success)
                {
                    editorState.PresetId = presetId;
                }
            }
            else
            {
                // Update existing preset
                var updateRequest = new PresetUpdateRequest
                {
                    Name = editorState.Preset.Name,
                    Description = editorState.Preset.Description,
                    Category = editorState.Preset.Category,
                    Type = editorState.Preset.Type,
                    Tags = editorState.Preset.Metadata.Tags,
                    Difficulty = editorState.Preset.Metadata.Difficulty,
                    EstimatedDuration = editorState.Preset.Metadata.EstimatedDuration,
                    Effects = editorState.Preset.Effects,
                    DeviceGroups = editorState.Preset.DeviceGroups,
                    AudioSettings = editorState.Preset.AudioSettings,
                    ExecutionSettings = editorState.Preset.ExecutionSettings,
                    CustomParameters = editorState.Preset.CustomParameters
                };

                success = await _presetManagementService.UpdatePresetAsync(editorState.PresetId, updateRequest);
            }

            if (success)
            {
                editorState.HasUnsavedChanges = false;
                editorState.ValidationErrors.Clear();
                PresetEditorUpdated?.Invoke(this, new PresetEditorEventArgs(editorId, PresetEditorAction.Saved));
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving preset from editor: {EditorId}", editorId);
            return false;
        }
    }

    /// <summary>
    /// Closes preset editor
    /// </summary>
    public async Task<bool> ClosePresetEditorAsync(string editorId)
    {
        try
        {
            if (!_editorStates.TryGetValue(editorId, out var editorState))
            {
                _logger.LogWarning("Editor not found: {EditorId}", editorId);
                return false;
            }

            // Check for unsaved changes
            if (editorState.HasUnsavedChanges)
            {
                // This would typically show a confirmation dialog
                // For now, we'll just log a warning
                _logger.LogWarning("Closing editor with unsaved changes: {EditorId}", editorId);
            }

            lock (_lockObject)
            {
                _editorStates.Remove(editorId);
            }

            PresetEditorUpdated?.Invoke(this, new PresetEditorEventArgs(editorId, PresetEditorAction.Closed));
            _logger.LogInformation("Closed preset editor: {EditorId}", editorId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing preset editor: {EditorId}", editorId);
            return false;
        }
    }

    /// <summary>
    /// Starts preset preview
    /// </summary>
    public async Task<string> StartPresetPreviewAsync(string presetId, PreviewSettings? settings = null)
    {
        try
        {
            var previewId = Guid.NewGuid().ToString();
            settings ??= new PreviewSettings();

            var previewState = new PresetPreviewState
            {
                PreviewId = previewId,
                PresetId = presetId,
                Settings = settings,
                IsActive = true,
                StartTime = DateTime.UtcNow,
                ExecutionId = string.Empty
            };

            // Get preset
            var preset = _presetManagementService.GetPreset(presetId);
            if (preset == null)
            {
                _logger.LogWarning("Preset not found for preview: {PresetId}", presetId);
                return string.Empty;
            }

            // Start execution
            var executionRequest = new PresetExecutionRequest
            {
                Preset = preset,
                Settings = new PresetExecutionSettings
                {
                    Duration = settings.Duration,
                    LoopEnabled = settings.LoopEnabled,
                    AutoStop = settings.AutoStop,
                    PreviewMode = true
                }
            };

            var executionId = await _executionEngine.ExecutePresetAsync(executionRequest);
            if (!string.IsNullOrEmpty(executionId))
            {
                previewState.ExecutionId = executionId;

                lock (_lockObject)
                {
                    _previewStates[previewId] = previewState;
                }

                PresetPreviewUpdated?.Invoke(this, new PresetPreviewEventArgs(previewId, PresetPreviewAction.Started));
                _logger.LogInformation("Started preset preview: {PresetId} ({PreviewId})", presetId, previewId);

                return previewId;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting preset preview: {PresetId}", presetId);
            return string.Empty;
        }
    }

    /// <summary>
    /// Stops preset preview
    /// </summary>
    public async Task<bool> StopPresetPreviewAsync(string previewId)
    {
        try
        {
            if (!_previewStates.TryGetValue(previewId, out var previewState))
            {
                _logger.LogWarning("Preview not found: {PreviewId}", previewId);
                return false;
            }

            // Stop execution
            if (!string.IsNullOrEmpty(previewState.ExecutionId))
            {
                await _executionEngine.StopExecutionAsync(previewState.ExecutionId);
            }

            previewState.IsActive = false;
            previewState.EndTime = DateTime.UtcNow;

            lock (_lockObject)
            {
                _previewStates.Remove(previewId);
            }

            PresetPreviewUpdated?.Invoke(this, new PresetPreviewEventArgs(previewId, PresetPreviewAction.Stopped));
            _logger.LogInformation("Stopped preset preview: {PreviewId}", previewId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping preset preview: {PreviewId}", previewId);
            return false;
        }
    }

    /// <summary>
    /// Gets preset library data
    /// </summary>
    public PresetLibraryData GetPresetLibraryData(PresetLibraryFilter? filter = null)
    {
        try
        {
            var presets = _presetManagementService.GetPresets(filter?.ToPresetFilter());

            var libraryData = new PresetLibraryData
            {
                Presets = presets.Select(p => new PresetLibraryItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Category = p.Category,
                    Type = p.Type,
                    Tags = p.Metadata.Tags,
                    Difficulty = p.Metadata.Difficulty,
                    CreatedBy = p.Metadata.CreatedBy,
                    CreatedAt = p.Metadata.CreatedAt,
                    LastModified = p.Metadata.LastModified,
                    EffectCount = p.Effects.Count,
                    DeviceGroupCount = p.DeviceGroups.Count,
                    IsExecutable = IsPresetExecutable(p),
                    PreviewAvailable = IsPresetPreviewAvailable(p)
                }).ToList(),

                Categories = presets.GroupBy(p => p.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),

                Types = presets.GroupBy(p => p.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),

                Tags = presets.SelectMany(p => p.Metadata.Tags)
                    .GroupBy(t => t)
                    .ToDictionary(g => g.Key, g => g.Count()),

                TotalCount = presets.Count(),
                FilteredCount = presets.Count()
            };

            return libraryData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting preset library data");
            return new PresetLibraryData();
        }
    }

    /// <summary>
    /// Gets editor state
    /// </summary>
    public PresetEditorState? GetEditorState(string editorId)
    {
        _editorStates.TryGetValue(editorId, out var state);
        return state;
    }

    /// <summary>
    /// Gets preview state
    /// </summary>
    public PresetPreviewState? GetPreviewState(string previewId)
    {
        _previewStates.TryGetValue(previewId, out var state);
        return state;
    }

    #region Private Methods

    private async void UpdateUiStates(object? state)
    {
        if (!_isUpdating)
        {
            return;
        }

        try
        {
            // Update editor states
            foreach (var editorEntry in _editorStates)
            {
                var editorId = editorEntry.Key;
                var editorState = editorEntry.Value;

                if (editorState.IsActive)
                {
                    await UpdateEditorState(editorId, editorState);
                }
            }

            // Update preview states
            foreach (var previewEntry in _previewStates)
            {
                var previewId = previewEntry.Key;
                var previewState = previewEntry.Value;

                if (previewState.IsActive)
                {
                    await UpdatePreviewState(previewId, previewState);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating UI states");
        }
    }

    private async Task UpdateEditorState(string editorId, PresetEditorState editorState)
    {
        try
        {
            // Update editor state based on current execution status
            if (!string.IsNullOrEmpty(editorState.PreviewExecutionId))
            {
                var executionStatus = _executionEngine.GetExecutionStatus(editorState.PreviewExecutionId);
                if (executionStatus != null)
                {
                    editorState.PreviewProgress = executionStatus.PhaseProgress;
                    editorState.PreviewPhase = executionStatus.CurrentPhase;
                }
            }

            PresetEditorUpdated?.Invoke(this, new PresetEditorEventArgs(editorId, PresetEditorAction.Updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating editor state: {EditorId}", editorId);
        }
    }

    private async Task UpdatePreviewState(string previewId, PresetPreviewState previewState)
    {
        try
        {
            // Update preview state based on execution status
            if (!string.IsNullOrEmpty(previewState.ExecutionId))
            {
                var executionStatus = _executionEngine.GetExecutionStatus(previewState.ExecutionId);
                if (executionStatus != null)
                {
                    previewState.Progress = executionStatus.PhaseProgress;
                    previewState.CurrentPhase = executionStatus.CurrentPhase;
                    previewState.ElapsedTime = executionStatus.ElapsedTime;

                    // Check if execution is complete
                    if (!executionStatus.IsActive)
                    {
                        previewState.IsActive = false;
                        previewState.EndTime = DateTime.UtcNow;

                        lock (_lockObject)
                        {
                            _previewStates.Remove(previewId);
                        }

                        PresetPreviewUpdated?.Invoke(this, new PresetPreviewEventArgs(previewId, PresetPreviewAction.Completed));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating preview state: {PreviewId}", previewId);
        }
    }

    private PresetValidationResult ValidatePreset(LightingPreset preset)
    {
        var result = new PresetValidationResult { IsValid = true, Errors = new List<string>() };

        // Validate name
        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            result.Errors.Add("Preset name is required");
            result.IsValid = false;
        }

        // Validate effects
        if (preset.Effects == null || !preset.Effects.Any())
        {
            result.Errors.Add("At least one effect is required");
            result.IsValid = false;
        }

        // Validate device groups
        if (preset.DeviceGroups == null || !preset.DeviceGroups.Any())
        {
            result.Errors.Add("At least one device group is required");
            result.IsValid = false;
        }

        // Validate effect configurations
        foreach (var effect in preset.Effects)
        {
            if (string.IsNullOrWhiteSpace(effect.Name))
            {
                result.Errors.Add($"Effect name is required for effect {preset.Effects.IndexOf(effect) + 1}");
                result.IsValid = false;
            }
        }

        return result;
    }

    private LightingPreset ClonePreset(LightingPreset preset)
    {
        // Simple clone implementation
        return new LightingPreset
        {
            Id = preset.Id,
            Name = preset.Name,
            Description = preset.Description,
            Category = preset.Category,
            Type = preset.Type,
            Metadata = preset.Metadata,
            Effects = preset.Effects.ToList(),
            DeviceGroups = preset.DeviceGroups.ToList(),
            AudioSettings = preset.AudioSettings,
            ExecutionSettings = preset.ExecutionSettings,
            CustomParameters = new Dictionary<string, object>(preset.CustomParameters)
        };
    }

    private bool IsPresetExecutable(LightingPreset preset)
    {
        // Check if preset has required components for execution
        return preset.Effects.Any() && preset.DeviceGroups.Any();
    }

    private bool IsPresetPreviewAvailable(LightingPreset preset)
    {
        // Check if preset can be previewed
        return IsPresetExecutable(preset) && preset.Effects.All(e => e.Type != EffectType.Custom);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isUpdating = false;
            _uiUpdateTimer?.Dispose();

            // Close all editors
            var editorIds = _editorStates.Keys.ToList();
            foreach (var editorId in editorIds)
            {
                ClosePresetEditorAsync(editorId).Wait(1000);
            }

            // Stop all previews
            var previewIds = _previewStates.Keys.ToList();
            foreach (var previewId in previewIds)
            {
                StopPresetPreviewAsync(previewId).Wait(1000);
            }

            _logger.LogInformation("Preset UI service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing preset UI service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Preset editor request
/// </summary>
public class PresetEditorRequest
{
    public string? PresetId { get; set; }
    public PresetEditorMode Mode { get; set; } = PresetEditorMode.Edit;
    public bool PreviewEnabled { get; set; } = true;
    public bool AutoSaveEnabled { get; set; } = false;
}

/// <summary>
/// Preview settings
/// </summary>
public class PreviewSettings
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(1);
    public bool LoopEnabled { get; set; } = false;
    public bool AutoStop { get; set; } = true;
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Preset library filter
/// </summary>
public class PresetLibraryFilter
{
    public string? Category { get; set; }
    public PresetType? Type { get; set; }
    public string? SearchTerm { get; set; }
    public string? CreatedBy { get; set; }
    public PresetDifficulty? Difficulty { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }

    public PresetFilter ToPresetFilter()
    {
        return new PresetFilter
        {
            Category = Category,
            Type = Type,
            SearchTerm = SearchTerm,
            CreatedBy = CreatedBy,
            Difficulty = Difficulty,
            CreatedAfter = CreatedAfter,
            CreatedBefore = CreatedBefore
        };
    }
}

/// <summary>
/// Preset UI state
/// </summary>
public class PresetUiState
{
    public string StateId { get; set; } = string.Empty;
    public PresetUiStateType Type { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, object> CustomData { get; set; } = new();
}

/// <summary>
/// Preset editor state
/// </summary>
public class PresetEditorState
{
    public string EditorId { get; set; } = string.Empty;
    public string PresetId { get; set; } = string.Empty;
    public PresetEditorMode Mode { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime LastModified { get; set; }
    public bool HasUnsavedChanges { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public bool PreviewEnabled { get; set; }
    public bool AutoSaveEnabled { get; set; }
    public LightingPreset Preset { get; set; } = new();
    public LightingPreset? OriginalPreset { get; set; }
    public string? PreviewExecutionId { get; set; }
    public float PreviewProgress { get; set; }
    public int PreviewPhase { get; set; }
}

/// <summary>
/// Preset preview state
/// </summary>
public class PresetPreviewState
{
    public string PreviewId { get; set; } = string.Empty;
    public string PresetId { get; set; } = string.Empty;
    public PreviewSettings Settings { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string ExecutionId { get; set; } = string.Empty;
    public float Progress { get; set; }
    public int CurrentPhase { get; set; }
    public TimeSpan ElapsedTime { get; set; }
}

/// <summary>
/// Preset library state
/// </summary>
public class PresetLibraryState
{
    public string LibraryId { get; set; } = string.Empty;
    public PresetLibraryFilter Filter { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime LastUpdated { get; set; }
    public PresetLibraryData Data { get; set; } = new();
}

/// <summary>
/// Preset library data
/// </summary>
public class PresetLibraryData
{
    public List<PresetLibraryItem> Presets { get; set; } = new();
    public Dictionary<PresetCategory, int> Categories { get; set; } = new();
    public Dictionary<PresetType, int> Types { get; set; } = new();
    public Dictionary<string, int> Tags { get; set; } = new();
    public int TotalCount { get; set; }
    public int FilteredCount { get; set; }
}

/// <summary>
/// Preset library item
/// </summary>
public class PresetLibraryItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PresetCategory Category { get; set; }
    public PresetType Type { get; set; }
    public List<string> Tags { get; set; } = new();
    public PresetDifficulty? Difficulty { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public int EffectCount { get; set; }
    public int DeviceGroupCount { get; set; }
    public bool IsExecutable { get; set; }
    public bool PreviewAvailable { get; set; }
}

/// <summary>
/// Preset validation result
/// </summary>
public class PresetValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Preset UI event arguments
/// </summary>
public class PresetUiEventArgs : EventArgs
{
    public string StateId { get; }
    public PresetUiAction Action { get; }
    public DateTime Timestamp { get; }

    public PresetUiEventArgs(string stateId, PresetUiAction action)
    {
        StateId = stateId;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Preset editor event arguments
/// </summary>
public class PresetEditorEventArgs : EventArgs
{
    public string EditorId { get; }
    public PresetEditorAction Action { get; }
    public DateTime Timestamp { get; }

    public PresetEditorEventArgs(string editorId, PresetEditorAction action)
    {
        EditorId = editorId;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Preset preview event arguments
/// </summary>
public class PresetPreviewEventArgs : EventArgs
{
    public string PreviewId { get; }
    public PresetPreviewAction Action { get; }
    public DateTime Timestamp { get; }

    public PresetPreviewEventArgs(string previewId, PresetPreviewAction action)
    {
        PreviewId = previewId;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Preset library event arguments
/// </summary>
public class PresetLibraryEventArgs : EventArgs
{
    public string LibraryId { get; }
    public PresetLibraryAction Action { get; }
    public DateTime Timestamp { get; }

    public PresetLibraryEventArgs(string libraryId, PresetLibraryAction action)
    {
        LibraryId = libraryId;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Preset editor modes
/// </summary>
public enum PresetEditorMode
{
    Create,
    Edit,
    Clone,
    Template
}

/// <summary>
/// Preset UI state types
/// </summary>
public enum PresetUiStateType
{
    Editor,
    Preview,
    Library,
    Collection
}

/// <summary>
/// Preset UI actions
/// </summary>
public enum PresetUiAction
{
    Created,
    Updated,
    Deleted,
    Opened,
    Closed
}

/// <summary>
/// Preset editor actions
/// </summary>
public enum PresetEditorAction
{
    Opened,
    Closed,
    Updated,
    Saved,
    ValidationFailed,
    PreviewStarted,
    PreviewStopped
}

/// <summary>
/// Preset preview actions
/// </summary>
public enum PresetPreviewAction
{
    Started,
    Stopped,
    Completed,
    Error
}

/// <summary>
/// Preset library actions
/// </summary>
public enum PresetLibraryAction
{
    Filtered,
    Sorted,
    Refreshed,
    Updated
}