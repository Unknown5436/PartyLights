using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PartyLights.Services;

/// <summary>
/// Comprehensive preset management service
/// </summary>
public class PresetManagementService
{
    private readonly ILogger<PresetManagementService> _logger;
    private readonly IAdvancedDeviceControlService _deviceControlService;
    private readonly ConcurrentDictionary<string, LightingPreset> _presets = new();
    private readonly ConcurrentDictionary<string, PresetCollection> _collections = new();
    private readonly ConcurrentDictionary<string, PresetTemplate> _templates = new();
    private readonly ConcurrentDictionary<string, PresetExecutionContext> _activeExecutions = new();
    private readonly string _presetsFilePath;
    private readonly string _collectionsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public event EventHandler<PresetEventArgs>? PresetCreated;
    public event EventHandler<PresetEventArgs>? PresetUpdated;
    public event EventHandler<PresetEventArgs>? PresetDeleted;
    public event EventHandler<PresetExecutionEventArgs>? PresetStarted;
    public event EventHandler<PresetExecutionEventArgs>? PresetStopped;

    public PresetManagementService(
        ILogger<PresetManagementService> logger,
        IAdvancedDeviceControlService deviceControlService)
    {
        _logger = logger;
        _deviceControlService = deviceControlService;

        _presetsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PartyLights", "presets.json");
        _collectionsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PartyLights", "collections.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        InitializeBuiltInPresets();
        LoadPresetsFromFile();
        LoadCollectionsFromFile();
    }

    #region Preset Management

    /// <summary>
    /// Creates a new preset
    /// </summary>
    public async Task<LightingPreset> CreatePresetAsync(LightingPreset preset)
    {
        try
        {
            _logger.LogInformation("Creating new preset: {PresetName}", preset.Name);

            // Generate ID if not provided
            if (string.IsNullOrEmpty(preset.Id))
            {
                preset.Id = Guid.NewGuid().ToString();
            }

            // Set timestamps
            preset.CreatedAt = DateTime.UtcNow;
            preset.LastModified = DateTime.UtcNow;

            // Validate preset
            var validationResult = ValidatePreset(preset);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Preset validation failed: {string.Join(", ", validationResult.Errors)}");
            }

            _presets.TryAdd(preset.Id, preset);
            await SavePresetsToFileAsync();

            PresetCreated?.Invoke(this, new PresetEventArgs(preset));
            _logger.LogInformation("Preset created successfully: {PresetId}", preset.Id);

            return preset;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating preset: {PresetName}", preset.Name);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing preset
    /// </summary>
    public async Task<bool> UpdatePresetAsync(LightingPreset preset)
    {
        try
        {
            _logger.LogInformation("Updating preset: {PresetId}", preset.Id);

            if (!_presets.ContainsKey(preset.Id))
            {
                _logger.LogWarning("Preset not found: {PresetId}", preset.Id);
                return false;
            }

            // Validate preset
            var validationResult = ValidatePreset(preset);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Preset validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                return false;
            }

            preset.LastModified = DateTime.UtcNow;
            _presets.AddOrUpdate(preset.Id, preset, (key, existing) => preset);
            await SavePresetsToFileAsync();

            PresetUpdated?.Invoke(this, new PresetEventArgs(preset));
            _logger.LogInformation("Preset updated successfully: {PresetId}", preset.Id);

            return true;
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
            _logger.LogInformation("Deleting preset: {PresetId}", presetId);

            if (!_presets.TryRemove(presetId, out var preset))
            {
                _logger.LogWarning("Preset not found: {PresetId}", presetId);
                return false;
            }

            // Stop any active executions of this preset
            await StopPresetExecutionsAsync(presetId);

            await SavePresetsToFileAsync();

            PresetDeleted?.Invoke(this, new PresetEventArgs(preset));
            _logger.LogInformation("Preset deleted successfully: {PresetId}", presetId);

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
    public async Task<LightingPreset?> GetPresetAsync(string presetId)
    {
        await Task.CompletedTask;
        _presets.TryGetValue(presetId, out var preset);
        return preset;
    }

    /// <summary>
    /// Gets all presets
    /// </summary>
    public async Task<IEnumerable<LightingPreset>> GetAllPresetsAsync()
    {
        await Task.CompletedTask;
        return _presets.Values.ToList();
    }

    /// <summary>
    /// Gets presets by type
    /// </summary>
    public async Task<IEnumerable<LightingPreset>> GetPresetsByTypeAsync(PresetType type)
    {
        await Task.CompletedTask;
        return _presets.Values.Where(p => p.Type == type).ToList();
    }

    /// <summary>
    /// Gets presets by category
    /// </summary>
    public async Task<IEnumerable<LightingPreset>> GetPresetsByCategoryAsync(string category)
    {
        await Task.CompletedTask;
        return _presets.Values.Where(p => p.Metadata.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Gets built-in presets
    /// </summary>
    public async Task<IEnumerable<LightingPreset>> GetBuiltInPresetsAsync()
    {
        await Task.CompletedTask;
        return _presets.Values.Where(p => p.IsBuiltIn).ToList();
    }

    /// <summary>
    /// Gets user-created presets
    /// </summary>
    public async Task<IEnumerable<LightingPreset>> GetUserPresetsAsync()
    {
        await Task.CompletedTask;
        return _presets.Values.Where(p => !p.IsBuiltIn).ToList();
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
            _logger.LogInformation("Starting preset execution: {PresetId}", presetId);

            var preset = await GetPresetAsync(presetId);
            if (preset == null)
            {
                _logger.LogWarning("Preset not found: {PresetId}", presetId);
                return false;
            }

            if (!preset.IsEnabled)
            {
                _logger.LogWarning("Preset is disabled: {PresetId}", presetId);
                return false;
            }

            // Stop any existing executions of this preset
            await StopPresetExecutionsAsync(presetId);

            // Create execution context
            var executionId = Guid.NewGuid().ToString();
            var context = new PresetExecutionContext
            {
                PresetId = presetId,
                TargetDeviceIds = preset.DeviceIds.ToList(),
                TargetDeviceGroupIds = preset.DeviceGroupIds.ToList(),
                RuntimeParameters = new Dictionary<string, object>(preset.Parameters),
                StartedAt = DateTime.UtcNow,
                IsActive = true,
                ExecutionId = executionId,
                Settings = settings ?? new PresetExecutionSettings()
            };

            _activeExecutions.TryAdd(executionId, context);

            // Apply preset to devices
            var success = await ApplyPresetToDevicesAsync(preset, context);
            if (success)
            {
                PresetStarted?.Invoke(this, new PresetExecutionEventArgs(context));
                _logger.LogInformation("Preset execution started successfully: {ExecutionId}", executionId);
            }
            else
            {
                _activeExecutions.TryRemove(executionId, out _);
                _logger.LogWarning("Failed to apply preset to devices: {PresetId}", presetId);
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
    public async Task<bool> StopPresetAsync(string executionId)
    {
        try
        {
            _logger.LogInformation("Stopping preset execution: {ExecutionId}", executionId);

            if (!_activeExecutions.TryRemove(executionId, out var context))
            {
                _logger.LogWarning("Preset execution not found: {ExecutionId}", executionId);
                return false;
            }

            context.IsActive = false;

            // Stop effects on target devices
            await StopEffectsOnDevicesAsync(context);

            PresetStopped?.Invoke(this, new PresetExecutionEventArgs(context));
            _logger.LogInformation("Preset execution stopped successfully: {ExecutionId}", executionId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping preset execution: {ExecutionId}", executionId);
            return false;
        }
    }

    /// <summary>
    /// Stops all executions of a specific preset
    /// </summary>
    public async Task<bool> StopPresetExecutionsAsync(string presetId)
    {
        try
        {
            var executions = _activeExecutions.Values.Where(e => e.PresetId == presetId).ToList();
            var tasks = executions.Select(e => StopPresetAsync(e.ExecutionId));
            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping preset executions: {PresetId}", presetId);
            return false;
        }
    }

    /// <summary>
    /// Stops all active preset executions
    /// </summary>
    public async Task<bool> StopAllPresetExecutionsAsync()
    {
        try
        {
            var executions = _activeExecutions.Values.ToList();
            var tasks = executions.Select(e => StopPresetAsync(e.ExecutionId));
            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping all preset executions");
            return false;
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

    #region Preset Collections

    /// <summary>
    /// Creates a preset collection
    /// </summary>
    public async Task<PresetCollection> CreateCollectionAsync(PresetCollection collection)
    {
        try
        {
            _logger.LogInformation("Creating preset collection: {CollectionName}", collection.Name);

            if (string.IsNullOrEmpty(collection.Id))
            {
                collection.Id = Guid.NewGuid().ToString();
            }

            collection.CreatedAt = DateTime.UtcNow;
            collection.LastModified = DateTime.UtcNow;

            _collections.TryAdd(collection.Id, collection);
            await SaveCollectionsToFileAsync();

            _logger.LogInformation("Preset collection created successfully: {CollectionId}", collection.Id);
            return collection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating preset collection: {CollectionName}", collection.Name);
            throw;
        }
    }

    /// <summary>
    /// Gets all preset collections
    /// </summary>
    public async Task<IEnumerable<PresetCollection>> GetAllCollectionsAsync()
    {
        await Task.CompletedTask;
        return _collections.Values.ToList();
    }

    /// <summary>
    /// Gets presets in a collection
    /// </summary>
    public async Task<IEnumerable<LightingPreset>> GetPresetsInCollectionAsync(string collectionId)
    {
        try
        {
            if (!_collections.TryGetValue(collectionId, out var collection))
            {
                return Enumerable.Empty<LightingPreset>();
            }

            var presets = new List<LightingPreset>();
            foreach (var presetId in collection.PresetIds)
            {
                var preset = await GetPresetAsync(presetId);
                if (preset != null)
                {
                    presets.Add(preset);
                }
            }

            return presets;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting presets in collection: {CollectionId}", collectionId);
            return Enumerable.Empty<LightingPreset>();
        }
    }

    #endregion

    #region Preset Templates

    /// <summary>
    /// Gets all preset templates
    /// </summary>
    public async Task<IEnumerable<PresetTemplate>> GetAllTemplatesAsync()
    {
        await Task.CompletedTask;
        return _templates.Values.ToList();
    }

    /// <summary>
    /// Gets templates by type
    /// </summary>
    public async Task<IEnumerable<PresetTemplate>> GetTemplatesByTypeAsync(PresetType type)
    {
        await Task.CompletedTask;
        return _templates.Values.Where(t => t.Type == type).ToList();
    }

    /// <summary>
    /// Creates a preset from a template
    /// </summary>
    public async Task<LightingPreset> CreatePresetFromTemplateAsync(string templateId, string name, string description)
    {
        try
        {
            if (!_templates.TryGetValue(templateId, out var template))
            {
                throw new ArgumentException($"Template not found: {templateId}");
            }

            var preset = new LightingPreset
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description,
                Type = template.Type,
                Parameters = new Dictionary<string, object>(template.DefaultParameters),
                IsBuiltIn = false,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                Author = "User",
                Metadata = new PresetMetadata
                {
                    Category = template.Category.ToString(),
                    Difficulty = template.Metadata.Difficulty,
                    EnergyLevel = template.Metadata.EnergyLevel,
                    CompatibleGenres = new List<string>(template.Metadata.CompatibleGenres),
                    CompatibleMoods = new List<string>(template.Metadata.CompatibleMoods),
                    EstimatedBPM = template.Metadata.EstimatedBPM,
                    ColorScheme = template.Metadata.ColorScheme,
                    RequiresBeatDetection = template.Metadata.RequiresBeatDetection,
                    RequiresFrequencyAnalysis = template.Metadata.RequiresFrequencyAnalysis,
                    RequiresMoodDetection = template.Metadata.RequiresMoodDetection
                }
            };

            return await CreatePresetAsync(preset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating preset from template: {TemplateId}", templateId);
            throw;
        }
    }

    #endregion

    #region Private Methods

    private void InitializeBuiltInPresets()
    {
        try
        {
            // Beat Sync Preset
            var beatSyncPreset = new LightingPreset
            {
                Id = "builtin_beatsync",
                Name = "Beat Sync",
                Description = "Synchronizes lights with detected beats",
                Type = PresetType.BeatSync,
                IsBuiltIn = true,
                IsDefault = true,
                Parameters = new Dictionary<string, object>
                {
                    ["beatSensitivity"] = 0.7f,
                    ["flashDuration"] = 200,
                    ["colorCycleSpeed"] = 1.0f,
                    ["brightnessMultiplier"] = 1.0f
                },
                Metadata = new PresetMetadata
                {
                    Category = "Music",
                    Difficulty = 2,
                    EnergyLevel = 4,
                    CompatibleGenres = new List<string> { "Electronic", "Pop", "Rock", "Hip-Hop" },
                    CompatibleMoods = new List<string> { "Excited", "Energetic", "Happy" },
                    RequiresBeatDetection = true,
                    ColorScheme = "Dynamic"
                }
            };
            _presets.TryAdd(beatSyncPreset.Id, beatSyncPreset);

            // Frequency Visualization Preset
            var freqVizPreset = new LightingPreset
            {
                Id = "builtin_frequencyviz",
                Name = "Frequency Visualization",
                Description = "Visualizes audio frequencies with different colors",
                Type = PresetType.FrequencyVisualization,
                IsBuiltIn = true,
                IsDefault = true,
                Parameters = new Dictionary<string, object>
                {
                    ["lowFreqColor"] = "Red",
                    ["midFreqColor"] = "Green",
                    ["highFreqColor"] = "Blue",
                    ["sensitivity"] = 0.8f,
                    ["smoothing"] = 0.3f
                },
                Metadata = new PresetMetadata
                {
                    Category = "Music",
                    Difficulty = 1,
                    EnergyLevel = 3,
                    CompatibleGenres = new List<string> { "Electronic", "Ambient", "Classical" },
                    CompatibleMoods = new List<string> { "Calm", "Focused", "Neutral" },
                    RequiresFrequencyAnalysis = true,
                    ColorScheme = "RGB Spectrum"
                }
            };
            _presets.TryAdd(freqVizPreset.Id, freqVizPreset);

            // Volume Reactive Preset
            var volumePreset = new LightingPreset
            {
                Id = "builtin_volumereactive",
                Name = "Volume Reactive",
                Description = "Changes brightness based on audio volume",
                Type = PresetType.VolumeReactive,
                IsBuiltIn = true,
                IsDefault = true,
                Parameters = new Dictionary<string, object>
                {
                    ["volumeSensitivity"] = 0.6f,
                    ["minBrightness"] = 10,
                    ["maxBrightness"] = 255,
                    ["smoothing"] = 0.5f,
                    ["colorTemperature"] = 4000
                },
                Metadata = new PresetMetadata
                {
                    Category = "General",
                    Difficulty = 1,
                    EnergyLevel = 2,
                    CompatibleGenres = new List<string> { "Any" },
                    CompatibleMoods = new List<string> { "Any" },
                    ColorScheme = "Warm White"
                }
            };
            _presets.TryAdd(volumePreset.Id, volumePreset);

            // Mood Lighting Preset
            var moodPreset = new LightingPreset
            {
                Id = "builtin_moodlighting",
                Name = "Mood Lighting",
                Description = "Changes colors based on detected mood",
                Type = PresetType.MoodLighting,
                IsBuiltIn = true,
                IsDefault = true,
                Parameters = new Dictionary<string, object>
                {
                    ["moodSensitivity"] = 0.8f,
                    ["transitionSpeed"] = 2.0f,
                    ["brightnessAdjustment"] = true,
                    ["colorIntensity"] = 0.9f
                },
                Metadata = new PresetMetadata
                {
                    Category = "Ambient",
                    Difficulty = 2,
                    EnergyLevel = 3,
                    CompatibleGenres = new List<string> { "Any" },
                    CompatibleMoods = new List<string> { "Any" },
                    RequiresMoodDetection = true,
                    ColorScheme = "Mood-Based"
                }
            };
            _presets.TryAdd(moodPreset.Id, moodPreset);

            // Party Mode Preset
            var partyPreset = new LightingPreset
            {
                Id = "builtin_partymode",
                Name = "Party Mode",
                Description = "High-energy effects for parties",
                Type = PresetType.PartyMode,
                IsBuiltIn = true,
                IsDefault = true,
                Parameters = new Dictionary<string, object>
                {
                    ["effectSpeed"] = 3.0f,
                    ["colorCycleSpeed"] = 2.0f,
                    ["strobeEnabled"] = true,
                    ["strobeFrequency"] = 0.5f,
                    ["rainbowEnabled"] = true,
                    ["intensity"] = 1.0f
                },
                Metadata = new PresetMetadata
                {
                    Category = "Party",
                    Difficulty = 3,
                    EnergyLevel = 5,
                    CompatibleGenres = new List<string> { "Electronic", "Dance", "Pop" },
                    CompatibleMoods = new List<string> { "Excited", "Energetic", "Happy" },
                    RequiresBeatDetection = true,
                    RequiresFrequencyAnalysis = true,
                    ColorScheme = "Rainbow"
                }
            };
            _presets.TryAdd(partyPreset.Id, partyPreset);

            // Initialize templates
            InitializeTemplates();

            _logger.LogInformation("Built-in presets initialized: {Count} presets", _presets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing built-in presets");
        }
    }

    private void InitializeTemplates()
    {
        // Beat Sync Template
        var beatSyncTemplate = new PresetTemplate
        {
            Id = "template_beatsync",
            Name = "Beat Sync Template",
            Description = "Template for creating beat-synchronized lighting effects",
            Type = PresetType.BeatSync,
            Category = PresetCategory.Music,
            DefaultParameters = new Dictionary<string, object>
            {
                ["beatSensitivity"] = 0.7f,
                ["flashDuration"] = 200,
                ["colorCycleSpeed"] = 1.0f,
                ["brightnessMultiplier"] = 1.0f
            },
            Metadata = new PresetMetadata
            {
                Category = "Music",
                Difficulty = 2,
                EnergyLevel = 4,
                RequiresBeatDetection = true,
                ColorScheme = "Dynamic"
            }
        };
        _templates.TryAdd(beatSyncTemplate.Id, beatSyncTemplate);

        // Frequency Visualization Template
        var freqTemplate = new PresetTemplate
        {
            Id = "template_frequencyviz",
            Name = "Frequency Visualization Template",
            Description = "Template for creating frequency-based lighting effects",
            Type = PresetType.FrequencyVisualization,
            Category = PresetCategory.Music,
            DefaultParameters = new Dictionary<string, object>
            {
                ["lowFreqColor"] = "Red",
                ["midFreqColor"] = "Green",
                ["highFreqColor"] = "Blue",
                ["sensitivity"] = 0.8f,
                ["smoothing"] = 0.3f
            },
            Metadata = new PresetMetadata
            {
                Category = "Music",
                Difficulty = 1,
                EnergyLevel = 3,
                RequiresFrequencyAnalysis = true,
                ColorScheme = "RGB Spectrum"
            }
        };
        _templates.TryAdd(freqTemplate.Id, freqTemplate);
    }

    private async Task<bool> ApplyPresetToDevicesAsync(LightingPreset preset, PresetExecutionContext context)
    {
        try
        {
            var success = true;

            // Apply to individual devices
            if (context.TargetDeviceIds.Any())
            {
                foreach (var deviceId in context.TargetDeviceIds)
                {
                    var deviceSuccess = await ApplyPresetToDeviceAsync(preset, deviceId, context);
                    if (!deviceSuccess)
                    {
                        success = false;
                    }
                }
            }

            // Apply to device groups
            if (context.TargetDeviceGroupIds.Any())
            {
                foreach (var groupId in context.TargetDeviceGroupIds)
                {
                    var groupSuccess = await ApplyPresetToDeviceGroupAsync(preset, groupId, context);
                    if (!groupSuccess)
                    {
                        success = false;
                    }
                }
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying preset to devices");
            return false;
        }
    }

    private async Task<bool> ApplyPresetToDeviceAsync(LightingPreset preset, string deviceId, PresetExecutionContext context)
    {
        try
        {
            // This would integrate with the device control service
            // For now, we'll simulate the application
            _logger.LogDebug("Applying preset {PresetId} to device {DeviceId}", preset.Id, deviceId);

            // Apply preset parameters based on type
            switch (preset.Type)
            {
                case PresetType.BeatSync:
                    await ApplyBeatSyncPresetAsync(deviceId, preset.Parameters, context.Settings);
                    break;
                case PresetType.FrequencyVisualization:
                    await ApplyFrequencyVisualizationPresetAsync(deviceId, preset.Parameters, context.Settings);
                    break;
                case PresetType.VolumeReactive:
                    await ApplyVolumeReactivePresetAsync(deviceId, preset.Parameters, context.Settings);
                    break;
                case PresetType.MoodLighting:
                    await ApplyMoodLightingPresetAsync(deviceId, preset.Parameters, context.Settings);
                    break;
                case PresetType.PartyMode:
                    await ApplyPartyModePresetAsync(deviceId, preset.Parameters, context.Settings);
                    break;
                default:
                    _logger.LogWarning("Unknown preset type: {PresetType}", preset.Type);
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying preset to device: {DeviceId}", deviceId);
            return false;
        }
    }

    private async Task<bool> ApplyPresetToDeviceGroupAsync(LightingPreset preset, string groupId, PresetExecutionContext context)
    {
        try
        {
            _logger.LogDebug("Applying preset {PresetId} to device group {GroupId}", preset.Id, groupId);

            // Get devices in group and apply preset to each
            var groups = await _deviceControlService.GetDeviceGroupsAsync();
            var group = groups.FirstOrDefault(g => g.Id == groupId);

            if (group != null)
            {
                foreach (var deviceId in group.DeviceIds)
                {
                    await ApplyPresetToDeviceAsync(preset, deviceId, context);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying preset to device group: {GroupId}", groupId);
            return false;
        }
    }

    private async Task ApplyBeatSyncPresetAsync(string deviceId, Dictionary<string, object> parameters, PresetExecutionSettings settings)
    {
        // Simulate beat sync application
        _logger.LogDebug("Applying beat sync preset to device: {DeviceId}", deviceId);
        await Task.Delay(100); // Simulate processing
    }

    private async Task ApplyFrequencyVisualizationPresetAsync(string deviceId, Dictionary<string, object> parameters, PresetExecutionSettings settings)
    {
        // Simulate frequency visualization application
        _logger.LogDebug("Applying frequency visualization preset to device: {DeviceId}", deviceId);
        await Task.Delay(100); // Simulate processing
    }

    private async Task ApplyVolumeReactivePresetAsync(string deviceId, Dictionary<string, object> parameters, PresetExecutionSettings settings)
    {
        // Simulate volume reactive application
        _logger.LogDebug("Applying volume reactive preset to device: {DeviceId}", deviceId);
        await Task.Delay(100); // Simulate processing
    }

    private async Task ApplyMoodLightingPresetAsync(string deviceId, Dictionary<string, object> parameters, PresetExecutionSettings settings)
    {
        // Simulate mood lighting application
        _logger.LogDebug("Applying mood lighting preset to device: {DeviceId}", deviceId);
        await Task.Delay(100); // Simulate processing
    }

    private async Task ApplyPartyModePresetAsync(string deviceId, Dictionary<string, object> parameters, PresetExecutionSettings settings)
    {
        // Simulate party mode application
        _logger.LogDebug("Applying party mode preset to device: {DeviceId}", deviceId);
        await Task.Delay(100); // Simulate processing
    }

    private async Task StopEffectsOnDevicesAsync(PresetExecutionContext context)
    {
        try
        {
            // Stop effects on individual devices
            foreach (var deviceId in context.TargetDeviceIds)
            {
                _logger.LogDebug("Stopping effects on device: {DeviceId}", deviceId);
                // This would integrate with device control service
            }

            // Stop effects on device groups
            foreach (var groupId in context.TargetDeviceGroupIds)
            {
                _logger.LogDebug("Stopping effects on device group: {GroupId}", groupId);
                // This would integrate with device control service
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping effects on devices");
        }
    }

    private PresetValidationResult ValidatePreset(LightingPreset preset)
    {
        var result = new PresetValidationResult();

        if (string.IsNullOrEmpty(preset.Name))
        {
            result.Errors.Add("Preset name is required");
        }

        if (string.IsNullOrEmpty(preset.Description))
        {
            result.Warnings.Add("Preset description is recommended");
        }

        if (!preset.DeviceIds.Any() && !preset.DeviceGroupIds.Any())
        {
            result.Warnings.Add("Preset has no target devices or groups");
        }

        // Validate parameters based on preset type
        ValidatePresetParameters(preset, result);

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private void ValidatePresetParameters(LightingPreset preset, PresetValidationResult result)
    {
        switch (preset.Type)
        {
            case PresetType.BeatSync:
                ValidateBeatSyncParameters(preset.Parameters, result);
                break;
            case PresetType.FrequencyVisualization:
                ValidateFrequencyVisualizationParameters(preset.Parameters, result);
                break;
            case PresetType.VolumeReactive:
                ValidateVolumeReactiveParameters(preset.Parameters, result);
                break;
            case PresetType.MoodLighting:
                ValidateMoodLightingParameters(preset.Parameters, result);
                break;
            case PresetType.PartyMode:
                ValidatePartyModeParameters(preset.Parameters, result);
                break;
        }
    }

    private void ValidateBeatSyncParameters(Dictionary<string, object> parameters, PresetValidationResult result)
    {
        if (!parameters.ContainsKey("beatSensitivity"))
        {
            result.Errors.Add("Beat sensitivity parameter is required for Beat Sync preset");
        }
    }

    private void ValidateFrequencyVisualizationParameters(Dictionary<string, object> parameters, PresetValidationResult result)
    {
        if (!parameters.ContainsKey("lowFreqColor") || !parameters.ContainsKey("highFreqColor"))
        {
            result.Errors.Add("Low and high frequency colors are required for Frequency Visualization preset");
        }
    }

    private void ValidateVolumeReactiveParameters(Dictionary<string, object> parameters, PresetValidationResult result)
    {
        if (!parameters.ContainsKey("volumeSensitivity"))
        {
            result.Errors.Add("Volume sensitivity parameter is required for Volume Reactive preset");
        }
    }

    private void ValidateMoodLightingParameters(Dictionary<string, object> parameters, PresetValidationResult result)
    {
        if (!parameters.ContainsKey("moodSensitivity"))
        {
            result.Errors.Add("Mood sensitivity parameter is required for Mood Lighting preset");
        }
    }

    private void ValidatePartyModeParameters(Dictionary<string, object> parameters, PresetValidationResult result)
    {
        if (!parameters.ContainsKey("effectSpeed"))
        {
            result.Errors.Add("Effect speed parameter is required for Party Mode preset");
        }
    }

    private async Task LoadPresetsFromFileAsync()
    {
        try
        {
            if (File.Exists(_presetsFilePath))
            {
                var json = await File.ReadAllTextAsync(_presetsFilePath);
                var presets = JsonSerializer.Deserialize<Dictionary<string, LightingPreset>>(json, _jsonOptions);

                if (presets != null)
                {
                    foreach (var preset in presets.Values)
                    {
                        if (!preset.IsBuiltIn) // Don't override built-in presets
                        {
                            _presets.TryAdd(preset.Id, preset);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading presets from file");
        }
    }

    private async Task SavePresetsToFileAsync()
    {
        try
        {
            var userPresets = _presets.Values.Where(p => !p.IsBuiltIn).ToDictionary(p => p.Id, p => p);
            var json = JsonSerializer.Serialize(userPresets, _jsonOptions);

            var directory = Path.GetDirectoryName(_presetsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_presetsFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving presets to file");
        }
    }

    private async Task LoadCollectionsFromFileAsync()
    {
        try
        {
            if (File.Exists(_collectionsFilePath))
            {
                var json = await File.ReadAllTextAsync(_collectionsFilePath);
                var collections = JsonSerializer.Deserialize<Dictionary<string, PresetCollection>>(json, _jsonOptions);

                if (collections != null)
                {
                    foreach (var collection in collections.Values)
                    {
                        _collections.TryAdd(collection.Id, collection);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading collections from file");
        }
    }

    private async Task SaveCollectionsToFileAsync()
    {
        try
        {
            var collections = _collections.Values.ToDictionary(c => c.Id, c => c);
            var json = JsonSerializer.Serialize(collections, _jsonOptions);

            var directory = Path.GetDirectoryName(_collectionsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_collectionsFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving collections to file");
        }
    }

    #endregion
}

/// <summary>
/// Preset validation result
/// </summary>
public class PresetValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Event arguments for preset events
/// </summary>
public class PresetEventArgs : EventArgs
{
    public LightingPreset Preset { get; }
    public DateTime EventTime { get; }

    public PresetEventArgs(LightingPreset preset)
    {
        Preset = preset;
        EventTime = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for preset execution events
/// </summary>
public class PresetExecutionEventArgs : EventArgs
{
    public PresetExecutionContext Context { get; }
    public DateTime EventTime { get; }

    public PresetExecutionEventArgs(PresetExecutionContext context)
    {
        Context = context;
        EventTime = DateTime.UtcNow;
    }
}
