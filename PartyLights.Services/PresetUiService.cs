using Microsoft.Extensions.Logging;
using PartyLights.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PartyLights.Services;

/// <summary>
/// Service for managing presets through the UI
/// </summary>
public class PresetUiService : INotifyPropertyChanged
{
    private readonly ILogger<PresetUiService> _logger;
    private readonly PresetManagementService _presetManagementService;
    private readonly PresetExecutionEngine _presetExecutionEngine;
    private readonly ObservableCollection<LightingPreset> _presets = new();
    private readonly ObservableCollection<PresetCollection> _collections = new();
    private readonly ObservableCollection<PresetTemplate> _templates = new();
    private LightingPreset? _selectedPreset;
    private PresetCollection? _selectedCollection;
    private PresetExecutionContext? _activeExecution;
    private bool _isExecuting;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<PresetUiEventArgs>? PresetSelected;
    public event EventHandler<PresetUiEventArgs>? PresetExecuted;
    public event EventHandler<PresetUiEventArgs>? PresetStopped;

    public PresetUiService(
        ILogger<PresetUiService> logger,
        PresetManagementService presetManagementService,
        PresetExecutionEngine presetExecutionEngine)
    {
        _logger = logger;
        _presetManagementService = presetManagementService;
        _presetExecutionEngine = presetExecutionEngine;

        // Subscribe to preset management events
        _presetManagementService.PresetCreated += OnPresetCreated;
        _presetManagementService.PresetUpdated += OnPresetUpdated;
        _presetManagementService.PresetDeleted += OnPresetDeleted;
        _presetManagementService.PresetStarted += OnPresetStarted;
        _presetManagementService.PresetStopped += OnPresetStopped;

        // Subscribe to execution engine events
        _presetExecutionEngine.PresetExecutionStarted += OnPresetExecutionStarted;
        _presetExecutionEngine.PresetExecutionStopped += OnPresetExecutionStopped;
        _presetExecutionEngine.PresetExecutionError += OnPresetExecutionError;

        // Load initial data
        _ = LoadPresetsAsync();
        _ = LoadCollectionsAsync();
        _ = LoadTemplatesAsync();
    }

    #region Properties

    public ObservableCollection<LightingPreset> Presets => _presets;
    public ObservableCollection<PresetCollection> Collections => _collections;
    public ObservableCollection<PresetTemplate> Templates => _templates;

    public LightingPreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (_selectedPreset != value)
            {
                _selectedPreset = value;
                OnPropertyChanged();
                PresetSelected?.Invoke(this, new PresetUiEventArgs(value));
            }
        }
    }

    public PresetCollection? SelectedCollection
    {
        get => _selectedCollection;
        set
        {
            if (_selectedCollection != value)
            {
                _selectedCollection = value;
                OnPropertyChanged();
            }
        }
    }

    public PresetExecutionContext? ActiveExecution
    {
        get => _activeExecution;
        private set
        {
            if (_activeExecution != value)
            {
                _activeExecution = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting != value)
            {
                _isExecuting = value;
                OnPropertyChanged();
            }
        }
    }

    public IEnumerable<LightingPreset> BuiltInPresets => _presets.Where(p => p.IsBuiltIn);
    public IEnumerable<LightingPreset> UserPresets => _presets.Where(p => !p.IsBuiltIn);
    public IEnumerable<LightingPreset> EnabledPresets => _presets.Where(p => p.IsEnabled);

    #endregion

    #region Preset Management

    /// <summary>
    /// Creates a new preset
    /// </summary>
    public async Task<LightingPreset?> CreatePresetAsync(string name, string description, PresetType type, Dictionary<string, object> parameters)
    {
        try
        {
            var preset = new LightingPreset
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description,
                Type = type,
                Parameters = parameters,
                IsBuiltIn = false,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                Author = "User"
            };

            var createdPreset = await _presetManagementService.CreatePresetAsync(preset);
            return createdPreset;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating preset: {PresetName}", name);
            return null;
        }
    }

    /// <summary>
    /// Updates an existing preset
    /// </summary>
    public async Task<bool> UpdatePresetAsync(LightingPreset preset)
    {
        try
        {
            return await _presetManagementService.UpdatePresetAsync(preset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating preset: {PresetId}", preset.Id);
            return false;
        }
    }

    /// <summary>
    /// Deletes a preset
    /// </summary>
    public async Task<bool> DeletePresetAsync(string presetId)
    {
        try
        {
            var success = await _presetManagementService.DeletePresetAsync(presetId);
            if (success && SelectedPreset?.Id == presetId)
            {
                SelectedPreset = null;
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting preset: {PresetId}", presetId);
            return false;
        }
    }

    /// <summary>
    /// Duplicates a preset
    /// </summary>
    public async Task<LightingPreset?> DuplicatePresetAsync(string presetId, string newName)
    {
        try
        {
            var originalPreset = await _presetManagementService.GetPresetAsync(presetId);
            if (originalPreset == null)
            {
                return null;
            }

            var duplicatedPreset = new LightingPreset
            {
                Id = Guid.NewGuid().ToString(),
                Name = newName,
                Description = $"Copy of {originalPreset.Name}",
                Type = originalPreset.Type,
                Parameters = new Dictionary<string, object>(originalPreset.Parameters),
                DeviceIds = new List<string>(originalPreset.DeviceIds),
                DeviceGroupIds = new List<string>(originalPreset.DeviceGroupIds),
                IsBuiltIn = false,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                Author = "User",
                Metadata = new PresetMetadata
                {
                    Category = originalPreset.Metadata.Category,
                    Difficulty = originalPreset.Metadata.Difficulty,
                    EnergyLevel = originalPreset.Metadata.EnergyLevel,
                    CompatibleGenres = new List<string>(originalPreset.Metadata.CompatibleGenres),
                    CompatibleMoods = new List<string>(originalPreset.Metadata.CompatibleMoods),
                    EstimatedBPM = originalPreset.Metadata.EstimatedBPM,
                    ColorScheme = originalPreset.Metadata.ColorScheme,
                    RequiresBeatDetection = originalPreset.Metadata.RequiresBeatDetection,
                    RequiresFrequencyAnalysis = originalPreset.Metadata.RequiresFrequencyAnalysis,
                    RequiresMoodDetection = originalPreset.Metadata.RequiresMoodDetection
                }
            };

            return await _presetManagementService.CreatePresetAsync(duplicatedPreset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating preset: {PresetId}", presetId);
            return null;
        }
    }

    /// <summary>
    /// Creates a preset from a template
    /// </summary>
    public async Task<LightingPreset?> CreatePresetFromTemplateAsync(string templateId, string name, string description)
    {
        try
        {
            return await _presetManagementService.CreatePresetFromTemplateAsync(templateId, name, description);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating preset from template: {TemplateId}", templateId);
            return null;
        }
    }

    #endregion

    #region Preset Execution

    /// <summary>
    /// Starts executing a preset
    /// </summary>
    public async Task<bool> StartPresetAsync(string presetId, PresetExecutionSettings? settings = null)
    {
        try
        {
            var preset = await _presetManagementService.GetPresetAsync(presetId);
            if (preset == null)
            {
                return false;
            }

            var context = new PresetExecutionContext
            {
                PresetId = presetId,
                TargetDeviceIds = preset.DeviceIds.ToList(),
                TargetDeviceGroupIds = preset.DeviceGroupIds.ToList(),
                RuntimeParameters = new Dictionary<string, object>(preset.Parameters),
                Settings = settings ?? new PresetExecutionSettings()
            };

            var success = await _presetExecutionEngine.StartPresetExecutionAsync(context);
            if (success)
            {
                ActiveExecution = context;
                IsExecuting = true;
                PresetExecuted?.Invoke(this, new PresetUiEventArgs(preset));
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting preset: {PresetId}", presetId);
            return false;
        }
    }

    /// <summary>
    /// Stops executing a preset
    /// </summary>
    public async Task<bool> StopPresetAsync(string presetId)
    {
        try
        {
            var success = await _presetExecutionEngine.StopPresetExecutionAsync(presetId);
            if (success)
            {
                if (ActiveExecution?.PresetId == presetId)
                {
                    ActiveExecution = null;
                    IsExecuting = false;
                }

                var preset = await _presetManagementService.GetPresetAsync(presetId);
                if (preset != null)
                {
                    PresetStopped?.Invoke(this, new PresetUiEventArgs(preset));
                }
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping preset: {PresetId}", presetId);
            return false;
        }
    }

    /// <summary>
    /// Stops all preset executions
    /// </summary>
    public async Task<bool> StopAllPresetsAsync()
    {
        try
        {
            var success = await _presetExecutionEngine.StopAllPresetExecutionsAsync();
            if (success)
            {
                ActiveExecution = null;
                IsExecuting = false;
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping all presets");
            return false;
        }
    }

    /// <summary>
    /// Gets active preset executions
    /// </summary>
    public async Task<IEnumerable<PresetExecutionContext>> GetActiveExecutionsAsync()
    {
        return await _presetExecutionEngine.GetActiveExecutionsAsync();
    }

    #endregion

    #region Preset Collections

    /// <summary>
    /// Creates a preset collection
    /// </summary>
    public async Task<PresetCollection?> CreateCollectionAsync(string name, string description, PresetCategory category, List<string> presetIds)
    {
        try
        {
            var collection = new PresetCollection
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description,
                PresetIds = presetIds,
                Category = category,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                Author = "User"
            };

            return await _presetManagementService.CreateCollectionAsync(collection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating collection: {CollectionName}", name);
            return null;
        }
    }

    /// <summary>
    /// Gets presets in a collection
    /// </summary>
    public async Task<IEnumerable<LightingPreset>> GetPresetsInCollectionAsync(string collectionId)
    {
        return await _presetManagementService.GetPresetsInCollectionAsync(collectionId);
    }

    #endregion

    #region Preset Filtering and Search

    /// <summary>
    /// Filters presets by type
    /// </summary>
    public IEnumerable<LightingPreset> FilterPresetsByType(PresetType type)
    {
        return _presets.Where(p => p.Type == type);
    }

    /// <summary>
    /// Filters presets by category
    /// </summary>
    public IEnumerable<LightingPreset> FilterPresetsByCategory(string category)
    {
        return _presets.Where(p => p.Metadata.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Searches presets by name or description
    /// </summary>
    public IEnumerable<LightingPreset> SearchPresets(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return _presets;
        }

        var term = searchTerm.ToLowerInvariant();
        return _presets.Where(p =>
            p.Name.ToLowerInvariant().Contains(term) ||
            p.Description.ToLowerInvariant().Contains(term) ||
            p.Tags.Any(tag => tag.ToLowerInvariant().Contains(term))
        );
    }

    /// <summary>
    /// Gets presets by energy level
    /// </summary>
    public IEnumerable<LightingPreset> GetPresetsByEnergyLevel(int energyLevel)
    {
        return _presets.Where(p => p.Metadata.EnergyLevel == energyLevel);
    }

    /// <summary>
    /// Gets presets by difficulty
    /// </summary>
    public IEnumerable<LightingPreset> GetPresetsByDifficulty(int difficulty)
    {
        return _presets.Where(p => p.Metadata.Difficulty == difficulty);
    }

    #endregion

    #region Preset Statistics

    /// <summary>
    /// Gets preset statistics
    /// </summary>
    public PresetStatistics GetPresetStatistics()
    {
        return new PresetStatistics
        {
            TotalPresets = _presets.Count,
            BuiltInPresets = _presets.Count(p => p.IsBuiltIn),
            UserPresets = _presets.Count(p => !p.IsBuiltIn),
            EnabledPresets = _presets.Count(p => p.IsEnabled),
            PresetsByType = _presets.GroupBy(p => p.Type).ToDictionary(g => g.Key, g => g.Count()),
            PresetsByCategory = _presets.GroupBy(p => p.Metadata.Category).ToDictionary(g => g.Key, g => g.Count()),
            AverageEnergyLevel = _presets.Any() ? _presets.Average(p => p.Metadata.EnergyLevel) : 0,
            AverageDifficulty = _presets.Any() ? _presets.Average(p => p.Metadata.Difficulty) : 0
        };
    }

    #endregion

    #region Private Methods

    private async Task LoadPresetsAsync()
    {
        try
        {
            var presets = await _presetManagementService.GetAllPresetsAsync();

            _presets.Clear();
            foreach (var preset in presets)
            {
                _presets.Add(preset);
            }

            _logger.LogInformation("Loaded {Count} presets", _presets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading presets");
        }
    }

    private async Task LoadCollectionsAsync()
    {
        try
        {
            var collections = await _presetManagementService.GetAllCollectionsAsync();

            _collections.Clear();
            foreach (var collection in collections)
            {
                _collections.Add(collection);
            }

            _logger.LogInformation("Loaded {Count} collections", _collections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading collections");
        }
    }

    private async Task LoadTemplatesAsync()
    {
        try
        {
            var templates = await _presetManagementService.GetAllTemplatesAsync();

            _templates.Clear();
            foreach (var template in templates)
            {
                _templates.Add(template);
            }

            _logger.LogInformation("Loaded {Count} templates", _templates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading templates");
        }
    }

    private void OnPresetCreated(object? sender, PresetEventArgs e)
    {
        _presets.Add(e.Preset);
    }

    private void OnPresetUpdated(object? sender, PresetEventArgs e)
    {
        var existingPreset = _presets.FirstOrDefault(p => p.Id == e.Preset.Id);
        if (existingPreset != null)
        {
            var index = _presets.IndexOf(existingPreset);
            _presets[index] = e.Preset;
        }
    }

    private void OnPresetDeleted(object? sender, PresetEventArgs e)
    {
        var presetToRemove = _presets.FirstOrDefault(p => p.Id == e.Preset.Id);
        if (presetToRemove != null)
        {
            _presets.Remove(presetToRemove);
        }
    }

    private void OnPresetStarted(object? sender, PresetExecutionEventArgs e)
    {
        // Update UI state if needed
    }

    private void OnPresetStopped(object? sender, PresetExecutionEventArgs e)
    {
        // Update UI state if needed
    }

    private void OnPresetExecutionStarted(object? sender, PresetExecutionEventArgs e)
    {
        ActiveExecution = e.Context;
        IsExecuting = true;
    }

    private void OnPresetExecutionStopped(object? sender, PresetExecutionEventArgs e)
    {
        if (ActiveExecution?.ExecutionId == e.Context.ExecutionId)
        {
            ActiveExecution = null;
            IsExecuting = false;
        }
    }

    private void OnPresetExecutionError(object? sender, PresetExecutionErrorEventArgs e)
    {
        _logger.LogError("Preset execution error: {PresetId} - {ErrorMessage}", e.PresetId, e.ErrorMessage);
    }

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

/// <summary>
/// Preset statistics
/// </summary>
public class PresetStatistics
{
    public int TotalPresets { get; set; }
    public int BuiltInPresets { get; set; }
    public int UserPresets { get; set; }
    public int EnabledPresets { get; set; }
    public Dictionary<PresetType, int> PresetsByType { get; set; } = new();
    public Dictionary<string, int> PresetsByCategory { get; set; } = new();
    public double AverageEnergyLevel { get; set; }
    public double AverageDifficulty { get; set; }
}

/// <summary>
/// Event arguments for preset UI events
/// </summary>
public class PresetUiEventArgs : EventArgs
{
    public LightingPreset? Preset { get; }
    public DateTime EventTime { get; }

    public PresetUiEventArgs(LightingPreset? preset)
    {
        Preset = preset;
        EventTime = DateTime.UtcNow;
    }
}
