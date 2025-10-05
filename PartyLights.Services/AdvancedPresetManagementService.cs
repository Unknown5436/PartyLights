using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PartyLights.Services;

/// <summary>
/// Advanced preset management service with comprehensive features
/// </summary>
public class AdvancedPresetManagementService : IDisposable
{
    private readonly ILogger<AdvancedPresetManagementService> _logger;
    private readonly IConfigurationService _configurationService;
    private readonly ConcurrentDictionary<string, LightingPreset> _presets = new();
    private readonly ConcurrentDictionary<string, PresetCollection> _collections = new();
    private readonly ConcurrentDictionary<string, PresetTemplate> _templates = new();
    private readonly Timer _autoSaveTimer;
    private readonly object _lockObject = new();

    private const int AutoSaveIntervalMs = 30000; // 30 seconds
    private bool _isManaging;
    private string _presetsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PartyLights", "Presets");
    private string _collectionsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PartyLights", "Collections");
    private string _templatesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PartyLights", "Templates");

    public event EventHandler<PresetEventArgs>? PresetCreated;
    public event EventHandler<PresetEventArgs>? PresetUpdated;
    public event EventHandler<PresetEventArgs>? PresetDeleted;
    public event EventHandler<PresetEventArgs>? PresetExecuted;
    public event EventHandler<CollectionEventArgs>? CollectionCreated;
    public event EventHandler<CollectionEventArgs>? CollectionUpdated;
    public event EventHandler<CollectionEventArgs>? CollectionDeleted;
    public event EventHandler<TemplateEventArgs>? TemplateCreated;
    public event EventHandler<TemplateEventArgs>? TemplateUpdated;
    public event EventHandler<TemplateEventArgs>? TemplateDeleted;

    public AdvancedPresetManagementService(
        ILogger<AdvancedPresetManagementService> logger,
        IConfigurationService configurationService)
    {
        _logger = logger;
        _configurationService = configurationService;

        _autoSaveTimer = new Timer(AutoSavePresets, null, AutoSaveIntervalMs, AutoSaveIntervalMs);
        _isManaging = true;

        InitializeDirectories();
        LoadPresetsFromStorage();

        _logger.LogInformation("Advanced preset management service initialized");
    }

    /// <summary>
    /// Creates a new lighting preset
    /// </summary>
    public async Task<string> CreatePresetAsync(PresetCreationRequest request)
    {
        try
        {
            var presetId = Guid.NewGuid().ToString();

            var preset = new LightingPreset
            {
                Id = presetId,
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                Type = request.Type,
                Metadata = new PresetMetadata
                {
                    CreatedBy = request.CreatedBy ?? "User",
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Version = "1.0.0",
                    Tags = request.Tags ?? new List<string>(),
                    Difficulty = request.Difficulty,
                    EstimatedDuration = request.EstimatedDuration,
                    DeviceCompatibility = request.DeviceCompatibility ?? new List<string>(),
                    AudioCompatibility = request.AudioCompatibility ?? new List<string>()
                },
                Effects = request.Effects ?? new List<EffectConfiguration>(),
                DeviceGroups = request.DeviceGroups ?? new List<DeviceGroupConfiguration>(),
                AudioSettings = request.AudioSettings ?? new AudioPresetSettings(),
                ExecutionSettings = request.ExecutionSettings ?? new PresetExecutionSettings(),
                CustomParameters = request.CustomParameters ?? new Dictionary<string, object>()
            };

            _presets[presetId] = preset;

            await SavePresetToStorage(preset);

            PresetCreated?.Invoke(this, new PresetEventArgs(presetId, PresetAction.Created));
            _logger.LogInformation("Created preset: {PresetName} ({PresetId})", preset.Name, presetId);

            return presetId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating preset: {PresetName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Updates an existing preset
    /// </summary>
    public async Task<bool> UpdatePresetAsync(string presetId, PresetUpdateRequest request)
    {
        try
        {
            if (!_presets.TryGetValue(presetId, out var preset))
            {
                _logger.LogWarning("Preset not found: {PresetId}", presetId);
                return false;
            }

            // Update preset properties
            if (!string.IsNullOrEmpty(request.Name))
                preset.Name = request.Name;

            if (!string.IsNullOrEmpty(request.Description))
                preset.Description = request.Description;

            if (request.Category.HasValue)
                preset.Category = request.Category.Value;

            if (request.Type.HasValue)
                preset.Type = request.Type.Value;

            if (request.Effects != null)
                preset.Effects = request.Effects;

            if (request.DeviceGroups != null)
                preset.DeviceGroups = request.DeviceGroups;

            if (request.AudioSettings != null)
                preset.AudioSettings = request.AudioSettings;

            if (request.ExecutionSettings != null)
                preset.ExecutionSettings = request.ExecutionSettings;

            if (request.CustomParameters != null)
                preset.CustomParameters = request.CustomParameters;

            // Update metadata
            preset.Metadata.LastModified = DateTime.UtcNow;
            preset.Metadata.Version = IncrementVersion(preset.Metadata.Version);

            if (request.Tags != null)
                preset.Metadata.Tags = request.Tags;

            if (request.Difficulty.HasValue)
                preset.Metadata.Difficulty = request.Difficulty.Value;

            if (request.EstimatedDuration.HasValue)
                preset.Metadata.EstimatedDuration = request.EstimatedDuration.Value;

            await SavePresetToStorage(preset);

            PresetUpdated?.Invoke(this, new PresetEventArgs(presetId, PresetAction.Updated));
            _logger.LogInformation("Updated preset: {PresetName} ({PresetId})", preset.Name, presetId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating preset: {PresetId}", presetId);
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
            if (!_presets.TryRemove(presetId, out var preset))
            {
                _logger.LogWarning("Preset not found: {PresetId}", presetId);
                return false;
            }

            await DeletePresetFromStorage(presetId);

            PresetDeleted?.Invoke(this, new PresetEventArgs(presetId, PresetAction.Deleted));
            _logger.LogInformation("Deleted preset: {PresetName} ({PresetId})", preset.Name, presetId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting preset: {PresetId}", presetId);
            return false;
        }
    }

    /// <summary>
    /// Gets a preset by ID
    /// </summary>
    public LightingPreset? GetPreset(string presetId)
    {
        _presets.TryGetValue(presetId, out var preset);
        return preset;
    }

    /// <summary>
    /// Gets all presets with optional filtering
    /// </summary>
    public IEnumerable<LightingPreset> GetPresets(PresetFilter? filter = null)
    {
        var presets = _presets.Values.AsEnumerable();

        if (filter != null)
        {
            if (!string.IsNullOrEmpty(filter.Category))
                presets = presets.Where(p => p.Category == filter.Category);

            if (filter.Type.HasValue)
                presets = presets.Where(p => p.Type == filter.Type.Value);

            if (!string.IsNullOrEmpty(filter.SearchTerm))
                presets = presets.Where(p =>
                    p.Name.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    p.Metadata.Tags.Any(t => t.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase)));

            if (filter.CreatedBy != null)
                presets = presets.Where(p => p.Metadata.CreatedBy == filter.CreatedBy);

            if (filter.Difficulty.HasValue)
                presets = presets.Where(p => p.Metadata.Difficulty == filter.Difficulty.Value);

            if (filter.CreatedAfter.HasValue)
                presets = presets.Where(p => p.Metadata.CreatedAt >= filter.CreatedAfter.Value);

            if (filter.CreatedBefore.HasValue)
                presets = presets.Where(p => p.Metadata.CreatedAt <= filter.CreatedBefore.Value);
        }

        return presets.OrderBy(p => p.Name);
    }

    /// <summary>
    /// Creates a preset collection
    /// </summary>
    public async Task<string> CreateCollectionAsync(CollectionCreationRequest request)
    {
        try
        {
            var collectionId = Guid.NewGuid().ToString();

            var collection = new PresetCollection
            {
                Id = collectionId,
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                PresetIds = request.PresetIds ?? new List<string>(),
                Metadata = new PresetMetadata
                {
                    CreatedBy = request.CreatedBy ?? "User",
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Version = "1.0.0",
                    Tags = request.Tags ?? new List<string>()
                },
                ExecutionOrder = request.ExecutionOrder ?? ExecutionOrder.Sequential,
                TransitionSettings = request.TransitionSettings ?? new TransitionSettings(),
                CustomParameters = request.CustomParameters ?? new Dictionary<string, object>()
            };

            _collections[collectionId] = collection;

            await SaveCollectionToStorage(collection);

            CollectionCreated?.Invoke(this, new CollectionEventArgs(collectionId, CollectionAction.Created));
            _logger.LogInformation("Created collection: {CollectionName} ({CollectionId})", collection.Name, collectionId);

            return collectionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating collection: {CollectionName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Creates a preset template
    /// </summary>
    public async Task<string> CreateTemplateAsync(TemplateCreationRequest request)
    {
        try
        {
            var templateId = Guid.NewGuid().ToString();

            var template = new PresetTemplate
            {
                Id = templateId,
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                Type = request.Type,
                TemplateData = request.TemplateData ?? new Dictionary<string, object>(),
                RequiredParameters = request.RequiredParameters ?? new List<string>(),
                OptionalParameters = request.OptionalParameters ?? new List<string>(),
                DefaultValues = request.DefaultValues ?? new Dictionary<string, object>(),
                ValidationRules = request.ValidationRules ?? new Dictionary<string, object>(),
                Metadata = new PresetMetadata
                {
                    CreatedBy = request.CreatedBy ?? "User",
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    Version = "1.0.0",
                    Tags = request.Tags ?? new List<string>()
                }
            };

            _templates[templateId] = template;

            await SaveTemplateToStorage(template);

            TemplateCreated?.Invoke(this, new TemplateEventArgs(templateId, TemplateAction.Created));
            _logger.LogInformation("Created template: {TemplateName} ({TemplateId})", template.Name, templateId);

            return templateId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template: {TemplateName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Generates a preset from a template
    /// </summary>
    public async Task<string> GeneratePresetFromTemplateAsync(string templateId, Dictionary<string, object> parameters)
    {
        try
        {
            if (!_templates.TryGetValue(templateId, out var template))
            {
                _logger.LogWarning("Template not found: {TemplateId}", templateId);
                return string.Empty;
            }

            // Validate required parameters
            foreach (var requiredParam in template.RequiredParameters)
            {
                if (!parameters.ContainsKey(requiredParam))
                {
                    _logger.LogError("Missing required parameter: {Parameter}", requiredParam);
                    return string.Empty;
                }
            }

            // Apply template data with parameters
            var presetData = new Dictionary<string, object>(template.TemplateData);
            foreach (var param in parameters)
            {
                presetData[param.Key] = param.Value;
            }

            // Apply default values for missing optional parameters
            foreach (var defaultParam in template.DefaultValues)
            {
                if (!presetData.ContainsKey(defaultParam.Key))
                {
                    presetData[defaultParam.Key] = defaultParam.Value;
                }
            }

            // Create preset from template
            var presetRequest = new PresetCreationRequest
            {
                Name = parameters.GetValueOrDefault("Name", $"Generated from {template.Name}").ToString() ?? "Generated Preset",
                Description = parameters.GetValueOrDefault("Description", template.Description).ToString() ?? string.Empty,
                Category = Enum.TryParse<PresetCategory>(parameters.GetValueOrDefault("Category", template.Category.ToString()).ToString(), out var category) ? category : PresetCategory.Custom,
                Type = Enum.TryParse<PresetType>(parameters.GetValueOrDefault("Type", template.Type.ToString()).ToString(), out var type) ? type : PresetType.Custom,
                CreatedBy = "Template Generator",
                Tags = new List<string> { "Generated", "Template" },
                CustomParameters = presetData
            };

            var presetId = await CreatePresetAsync(presetRequest);

            if (!string.IsNullOrEmpty(presetId))
            {
                _logger.LogInformation("Generated preset from template: {TemplateName} -> {PresetId}", template.Name, presetId);
            }

            return presetId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating preset from template: {TemplateId}", templateId);
            return string.Empty;
        }
    }

    /// <summary>
    /// Imports presets from a file
    /// </summary>
    public async Task<ImportResult> ImportPresetsAsync(string filePath, ImportOptions? options = null)
    {
        try
        {
            options ??= new ImportOptions();
            var result = new ImportResult();

            if (!File.Exists(filePath))
            {
                result.Success = false;
                result.ErrorMessage = "File not found";
                return result;
            }

            var fileContent = await File.ReadAllTextAsync(filePath);
            var importData = JsonSerializer.Deserialize<PresetImportData>(fileContent);

            if (importData == null)
            {
                result.Success = false;
                result.ErrorMessage = "Invalid file format";
                return result;
            }

            // Import presets
            foreach (var presetData in importData.Presets)
            {
                try
                {
                    var presetRequest = new PresetCreationRequest
                    {
                        Name = presetData.Name,
                        Description = presetData.Description,
                        Category = presetData.Category,
                        Type = presetData.Type,
                        CreatedBy = options.CreatedBy ?? "Import",
                        Tags = presetData.Tags ?? new List<string>(),
                        Effects = presetData.Effects,
                        DeviceGroups = presetData.DeviceGroups,
                        AudioSettings = presetData.AudioSettings,
                        ExecutionSettings = presetData.ExecutionSettings,
                        CustomParameters = presetData.CustomParameters
                    };

                    var presetId = await CreatePresetAsync(presetRequest);
                    if (!string.IsNullOrEmpty(presetId))
                    {
                        result.ImportedPresets.Add(presetId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error importing preset: {PresetName}", presetData.Name);
                    result.FailedPresets.Add(presetData.Name);
                }
            }

            // Import collections
            foreach (var collectionData in importData.Collections)
            {
                try
                {
                    var collectionRequest = new CollectionCreationRequest
                    {
                        Name = collectionData.Name,
                        Description = collectionData.Description,
                        Category = collectionData.Category,
                        CreatedBy = options.CreatedBy ?? "Import",
                        Tags = collectionData.Tags ?? new List<string>(),
                        PresetIds = collectionData.PresetIds,
                        ExecutionOrder = collectionData.ExecutionOrder,
                        TransitionSettings = collectionData.TransitionSettings,
                        CustomParameters = collectionData.CustomParameters
                    };

                    var collectionId = await CreateCollectionAsync(collectionRequest);
                    if (!string.IsNullOrEmpty(collectionId))
                    {
                        result.ImportedCollections.Add(collectionId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error importing collection: {CollectionName}", collectionData.Name);
                    result.FailedCollections.Add(collectionData.Name);
                }
            }

            result.Success = true;
            _logger.LogInformation("Imported {PresetCount} presets and {CollectionCount} collections",
                result.ImportedPresets.Count, result.ImportedCollections.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing presets from file: {FilePath}", filePath);
            return new ImportResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Exports presets to a file
    /// </summary>
    public async Task<bool> ExportPresetsAsync(string filePath, ExportOptions? options = null)
    {
        try
        {
            options ??= new ExportOptions();

            var exportData = new PresetExportData
            {
                ExportDate = DateTime.UtcNow,
                ExportVersion = "1.0.0",
                ExportedBy = options.ExportedBy ?? "User"
            };

            // Export presets
            var presetsToExport = options.PresetIds?.Select(id => GetPreset(id)).Where(p => p != null).ToList()
                                ?? _presets.Values.ToList();

            exportData.Presets = presetsToExport.Select(p => new PresetExportItem
            {
                Name = p.Name,
                Description = p.Description,
                Category = p.Category,
                Type = p.Type,
                Tags = p.Metadata.Tags,
                Effects = p.Effects,
                DeviceGroups = p.DeviceGroups,
                AudioSettings = p.AudioSettings,
                ExecutionSettings = p.ExecutionSettings,
                CustomParameters = p.CustomParameters
            }).ToList();

            // Export collections
            var collectionsToExport = options.CollectionIds?.Select(id => _collections.GetValueOrDefault(id)).Where(c => c != null).ToList()
                                   ?? _collections.Values.ToList();

            exportData.Collections = collectionsToExport.Select(c => new CollectionExportItem
            {
                Name = c.Name,
                Description = c.Description,
                Category = c.Category,
                Tags = c.Metadata.Tags,
                PresetIds = c.PresetIds,
                ExecutionOrder = c.ExecutionOrder,
                TransitionSettings = c.TransitionSettings,
                CustomParameters = c.CustomParameters
            }).ToList();

            var jsonContent = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, jsonContent);

            _logger.LogInformation("Exported {PresetCount} presets and {CollectionCount} collections to {FilePath}",
                exportData.Presets.Count, exportData.Collections.Count, filePath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting presets to file: {FilePath}", filePath);
            return false;
        }
    }

    #region Private Methods

    private void InitializeDirectories()
    {
        Directory.CreateDirectory(_presetsDirectory);
        Directory.CreateDirectory(_collectionsDirectory);
        Directory.CreateDirectory(_templatesDirectory);
    }

    private async void LoadPresetsFromStorage()
    {
        try
        {
            await LoadPresetsFromDirectory(_presetsDirectory);
            await LoadCollectionsFromDirectory(_collectionsDirectory);
            await LoadTemplatesFromDirectory(_templatesDirectory);

            _logger.LogInformation("Loaded {PresetCount} presets, {CollectionCount} collections, {TemplateCount} templates",
                _presets.Count, _collections.Count, _templates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading presets from storage");
        }
    }

    private async Task LoadPresetsFromDirectory(string directory)
    {
        try
        {
            var files = Directory.GetFiles(directory, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var preset = JsonSerializer.Deserialize<LightingPreset>(content);
                    if (preset != null)
                    {
                        _presets[preset.Id] = preset;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading preset from file: {FilePath}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading presets from directory: {Directory}", directory);
        }
    }

    private async Task LoadCollectionsFromDirectory(string directory)
    {
        try
        {
            var files = Directory.GetFiles(directory, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var collection = JsonSerializer.Deserialize<PresetCollection>(content);
                    if (collection != null)
                    {
                        _collections[collection.Id] = collection;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading collection from file: {FilePath}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading collections from directory: {Directory}", directory);
        }
    }

    private async Task LoadTemplatesFromDirectory(string directory)
    {
        try
        {
            var files = Directory.GetFiles(directory, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var template = JsonSerializer.Deserialize<PresetTemplate>(content);
                    if (template != null)
                    {
                        _templates[template.Id] = template;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading template from file: {FilePath}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading templates from directory: {Directory}", directory);
        }
    }

    private async Task SavePresetToStorage(LightingPreset preset)
    {
        try
        {
            var filePath = Path.Combine(_presetsDirectory, $"{preset.Id}.json");
            var content = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving preset to storage: {PresetId}", preset.Id);
        }
    }

    private async Task SaveCollectionToStorage(PresetCollection collection)
    {
        try
        {
            var filePath = Path.Combine(_collectionsDirectory, $"{collection.Id}.json");
            var content = JsonSerializer.Serialize(collection, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving collection to storage: {CollectionId}", collection.Id);
        }
    }

    private async Task SaveTemplateToStorage(PresetTemplate template)
    {
        try
        {
            var filePath = Path.Combine(_templatesDirectory, $"{template.Id}.json");
            var content = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving template to storage: {TemplateId}", template.Id);
        }
    }

    private async Task DeletePresetFromStorage(string presetId)
    {
        try
        {
            var filePath = Path.Combine(_presetsDirectory, $"{presetId}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting preset from storage: {PresetId}", presetId);
        }
    }

    private async void AutoSavePresets(object? state)
    {
        if (!_isManaging)
        {
            return;
        }

        try
        {
            // Auto-save logic would go here
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auto-save");
        }
    }

    private string IncrementVersion(string currentVersion)
    {
        try
        {
            var parts = currentVersion.Split('.');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var patch))
            {
                return $"{parts[0]}.{parts[1]}.{patch + 1}";
            }
            return "1.0.1";
        }
        catch
        {
            return "1.0.1";
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isManaging = false;
            _autoSaveTimer?.Dispose();

            _logger.LogInformation("Advanced preset management service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing advanced preset management service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Preset creation request
/// </summary>
public class PresetCreationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PresetCategory Category { get; set; } = PresetCategory.Custom;
    public PresetType Type { get; set; } = PresetType.Custom;
    public string? CreatedBy { get; set; }
    public List<string>? Tags { get; set; }
    public PresetDifficulty? Difficulty { get; set; }
    public TimeSpan? EstimatedDuration { get; set; }
    public List<string>? DeviceCompatibility { get; set; }
    public List<string>? AudioCompatibility { get; set; }
    public List<EffectConfiguration>? Effects { get; set; }
    public List<DeviceGroupConfiguration>? DeviceGroups { get; set; }
    public AudioPresetSettings? AudioSettings { get; set; }
    public PresetExecutionSettings? ExecutionSettings { get; set; }
    public Dictionary<string, object>? CustomParameters { get; set; }
}

/// <summary>
/// Preset update request
/// </summary>
public class PresetUpdateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public PresetCategory? Category { get; set; }
    public PresetType? Type { get; set; }
    public List<string>? Tags { get; set; }
    public PresetDifficulty? Difficulty { get; set; }
    public TimeSpan? EstimatedDuration { get; set; }
    public List<EffectConfiguration>? Effects { get; set; }
    public List<DeviceGroupConfiguration>? DeviceGroups { get; set; }
    public AudioPresetSettings? AudioSettings { get; set; }
    public PresetExecutionSettings? ExecutionSettings { get; set; }
    public Dictionary<string, object>? CustomParameters { get; set; }
}

/// <summary>
/// Preset filter for searching
/// </summary>
public class PresetFilter
{
    public string? Category { get; set; }
    public PresetType? Type { get; set; }
    public string? SearchTerm { get; set; }
    public string? CreatedBy { get; set; }
    public PresetDifficulty? Difficulty { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
}

/// <summary>
/// Collection creation request
/// </summary>
public class CollectionCreationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PresetCategory Category { get; set; } = PresetCategory.Custom;
    public string? CreatedBy { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? PresetIds { get; set; }
    public ExecutionOrder? ExecutionOrder { get; set; }
    public TransitionSettings? TransitionSettings { get; set; }
    public Dictionary<string, object>? CustomParameters { get; set; }
}

/// <summary>
/// Template creation request
/// </summary>
public class TemplateCreationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PresetCategory Category { get; set; } = PresetCategory.Custom;
    public PresetType Type { get; set; } = PresetType.Custom;
    public string? CreatedBy { get; set; }
    public List<string>? Tags { get; set; }
    public Dictionary<string, object>? TemplateData { get; set; }
    public List<string>? RequiredParameters { get; set; }
    public List<string>? OptionalParameters { get; set; }
    public Dictionary<string, object>? DefaultValues { get; set; }
    public Dictionary<string, object>? ValidationRules { get; set; }
}

/// <summary>
/// Import options
/// </summary>
public class ImportOptions
{
    public string? CreatedBy { get; set; }
    public bool OverwriteExisting { get; set; } = false;
    public bool ValidateBeforeImport { get; set; } = true;
}

/// <summary>
/// Export options
/// </summary>
public class ExportOptions
{
    public string? ExportedBy { get; set; }
    public List<string>? PresetIds { get; set; }
    public List<string>? CollectionIds { get; set; }
    public bool IncludeTemplates { get; set; } = false;
}

/// <summary>
/// Import result
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> ImportedPresets { get; set; } = new();
    public List<string> ImportedCollections { get; set; } = new();
    public List<string> FailedPresets { get; set; } = new();
    public List<string> FailedCollections { get; set; } = new();
}

/// <summary>
/// Preset import data
/// </summary>
public class PresetImportData
{
    public List<PresetImportItem> Presets { get; set; } = new();
    public List<CollectionImportItem> Collections { get; set; } = new();
}

/// <summary>
/// Preset import item
/// </summary>
public class PresetImportItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PresetCategory Category { get; set; }
    public PresetType Type { get; set; }
    public List<string>? Tags { get; set; }
    public List<EffectConfiguration>? Effects { get; set; }
    public List<DeviceGroupConfiguration>? DeviceGroups { get; set; }
    public AudioPresetSettings? AudioSettings { get; set; }
    public PresetExecutionSettings? ExecutionSettings { get; set; }
    public Dictionary<string, object>? CustomParameters { get; set; }
}

/// <summary>
/// Collection import item
/// </summary>
public class CollectionImportItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PresetCategory Category { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? PresetIds { get; set; }
    public ExecutionOrder? ExecutionOrder { get; set; }
    public TransitionSettings? TransitionSettings { get; set; }
    public Dictionary<string, object>? CustomParameters { get; set; }
}

/// <summary>
/// Preset export data
/// </summary>
public class PresetExportData
{
    public DateTime ExportDate { get; set; }
    public string ExportVersion { get; set; } = "1.0.0";
    public string ExportedBy { get; set; } = string.Empty;
    public List<PresetExportItem> Presets { get; set; } = new();
    public List<CollectionExportItem> Collections { get; set; } = new();
}

/// <summary>
/// Preset export item
/// </summary>
public class PresetExportItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PresetCategory Category { get; set; }
    public PresetType Type { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<EffectConfiguration> Effects { get; set; } = new();
    public List<DeviceGroupConfiguration> DeviceGroups { get; set; } = new();
    public AudioPresetSettings AudioSettings { get; set; } = new();
    public PresetExecutionSettings ExecutionSettings { get; set; } = new();
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Collection export item
/// </summary>
public class CollectionExportItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PresetCategory Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> PresetIds { get; set; } = new();
    public ExecutionOrder ExecutionOrder { get; set; }
    public TransitionSettings TransitionSettings { get; set; } = new();
    public Dictionary<string, object> CustomParameters { get; set; } = new();
}

/// <summary>
/// Preset event arguments
/// </summary>
public class PresetEventArgs : EventArgs
{
    public string PresetId { get; }
    public PresetAction Action { get; }
    public DateTime Timestamp { get; }

    public PresetEventArgs(string presetId, PresetAction action)
    {
        PresetId = presetId;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Collection event arguments
/// </summary>
public class CollectionEventArgs : EventArgs
{
    public string CollectionId { get; }
    public CollectionAction Action { get; }
    public DateTime Timestamp { get; }

    public CollectionEventArgs(string collectionId, CollectionAction action)
    {
        CollectionId = collectionId;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Template event arguments
/// </summary>
public class TemplateEventArgs : EventArgs
{
    public string TemplateId { get; }
    public TemplateAction Action { get; }
    public DateTime Timestamp { get; }

    public TemplateEventArgs(string templateId, TemplateAction action)
    {
        TemplateId = templateId;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Preset actions
/// </summary>
public enum PresetAction
{
    Created,
    Updated,
    Deleted,
    Executed,
    Imported,
    Exported
}

/// <summary>
/// Collection actions
/// </summary>
public enum CollectionAction
{
    Created,
    Updated,
    Deleted,
    Executed
}

/// <summary>
/// Template actions
/// </summary>
public enum TemplateAction
{
    Created,
    Updated,
    Deleted,
    Used
}
