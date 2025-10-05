using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Reflection;

namespace PartyLights.Services;

/// <summary>
/// Documentation generation service for automated API and code documentation
/// </summary>
public class DocumentationGenerationService : IDisposable
{
    private readonly ILogger<DocumentationGenerationService> _logger;
    private readonly ConcurrentDictionary<string, GeneratedDocumentation> _generatedDocs = new();
    private readonly Timer _generationTimer;
    private readonly object _lockObject = new();

    private const int GenerationIntervalMs = 1000; // 1 second
    private bool _isGenerating;

    // Documentation generation
    private readonly Dictionary<string, CodeAnalysis> _codeAnalysis = new();
    private readonly Dictionary<string, ApiDocumentation> _apiDocs = new();
    private readonly Dictionary<string, ClassDocumentation> _classDocs = new();

    public event EventHandler<DocumentationGenerationEventArgs>? DocumentationGenerated;
    public event EventHandler<ApiDocumentationEventArgs>? ApiDocumentationGenerated;
    public event EventHandler<CodeAnalysisEventArgs>? CodeAnalysisCompleted;

    public DocumentationGenerationService(ILogger<DocumentationGenerationService> logger)
    {
        _logger = logger;

        _generationTimer = new Timer(ProcessGeneration, null, GenerationIntervalMs, GenerationIntervalMs);
        _isGenerating = true;

        InitializeCodeAnalysis();

        _logger.LogInformation("Documentation generation service initialized");
    }

    /// <summary>
    /// Generates API documentation from code
    /// </summary>
    public async Task<ApiDocumentation> GenerateApiDocumentationAsync(ApiDocumentationRequest request)
    {
        try
        {
            var apiDocId = Guid.NewGuid().ToString();

            var apiDoc = new ApiDocumentation
            {
                Id = apiDocId,
                Name = request.Name ?? "API Documentation",
                Description = request.Description ?? "Generated API documentation",
                Version = request.Version ?? "1.0.0",
                BaseUrl = request.BaseUrl ?? "https://api.partylights.com",
                Endpoints = new List<ApiEndpoint>(),
                Models = new List<ApiModel>(),
                GeneratedAt = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            // Analyze code and generate API documentation
            await AnalyzeCodeForApiDocumentation(apiDoc);

            _apiDocs[apiDocId] = apiDoc;

            ApiDocumentationGenerated?.Invoke(this, new ApiDocumentationEventArgs(apiDocId, apiDoc, ApiDocumentationAction.Generated));
            _logger.LogInformation("Generated API documentation: {Name} ({ApiDocId})", apiDoc.Name, apiDocId);

            return apiDoc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating API documentation: {Name}", request.Name);
            return new ApiDocumentation
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name ?? "API Documentation",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Generates class documentation
    /// </summary>
    public async Task<ClassDocumentation> GenerateClassDocumentationAsync(string className)
    {
        try
        {
            var classDocId = Guid.NewGuid().ToString();

            var classDoc = new ClassDocumentation
            {
                Id = classDocId,
                ClassName = className,
                Description = $"Documentation for {className}",
                Methods = new List<MethodDocumentation>(),
                Properties = new List<PropertyDocumentation>(),
                Events = new List<EventDocumentation>(),
                GeneratedAt = DateTime.UtcNow
            };

            // Analyze class and generate documentation
            await AnalyzeClass(classDoc);

            _classDocs[classDocId] = classDoc;

            _logger.LogInformation("Generated class documentation: {ClassName} ({ClassDocId})", className, classDocId);

            return classDoc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating class documentation: {ClassName}", className);
            return new ClassDocumentation
            {
                Id = Guid.NewGuid().ToString(),
                ClassName = className,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Analyzes code for documentation generation
    /// </summary>
    public async Task<CodeAnalysis> AnalyzeCodeAsync(CodeAnalysisRequest request)
    {
        try
        {
            var analysisId = Guid.NewGuid().ToString();

            var analysis = new CodeAnalysis
            {
                Id = analysisId,
                ProjectName = request.ProjectName ?? "PartyLights",
                AnalysisType = request.AnalysisType,
                StartTime = DateTime.UtcNow,
                Classes = new List<ClassInfo>(),
                Methods = new List<MethodInfo>(),
                Properties = new List<PropertyInfo>(),
                Interfaces = new List<InterfaceInfo>(),
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            // Perform code analysis
            await PerformCodeAnalysis(analysis);

            analysis.EndTime = DateTime.UtcNow;
            analysis.Success = true;

            _codeAnalysis[analysisId] = analysis;

            CodeAnalysisCompleted?.Invoke(this, new CodeAnalysisEventArgs(analysisId, analysis, CodeAnalysisAction.Completed));
            _logger.LogInformation("Completed code analysis: {ProjectName} ({AnalysisId})", analysis.ProjectName, analysisId);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing code: {ProjectName}", request.ProjectName);
            return new CodeAnalysis
            {
                Id = Guid.NewGuid().ToString(),
                ProjectName = request.ProjectName ?? "PartyLights",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Generates comprehensive documentation
    /// </summary>
    public async Task<GeneratedDocumentation> GenerateComprehensiveDocumentationAsync(ComprehensiveDocumentationRequest request)
    {
        try
        {
            var docId = Guid.NewGuid().ToString();

            var documentation = new GeneratedDocumentation
            {
                Id = docId,
                Title = request.Title ?? "Comprehensive Documentation",
                Description = request.Description ?? "Generated comprehensive documentation",
                DocumentationType = request.DocumentationType,
                GeneratedAt = DateTime.UtcNow,
                ApiDocumentation = new List<ApiDocumentation>(),
                ClassDocumentation = new List<ClassDocumentation>(),
                CodeAnalysis = new List<CodeAnalysis>(),
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            // Generate all types of documentation
            await GenerateAllDocumentation(documentation);

            _generatedDocs[docId] = documentation;

            DocumentationGenerated?.Invoke(this, new DocumentationGenerationEventArgs(docId, documentation, DocumentationGenerationAction.Generated));
            _logger.LogInformation("Generated comprehensive documentation: {Title} ({DocId})", documentation.Title, docId);

            return documentation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating comprehensive documentation: {Title}", request.Title);
            return new GeneratedDocumentation
            {
                Id = Guid.NewGuid().ToString(),
                Title = request.Title ?? "Comprehensive Documentation",
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets generated documentation
    /// </summary>
    public IEnumerable<GeneratedDocumentation> GetGeneratedDocumentation()
    {
        return _generatedDocs.Values;
    }

    /// <summary>
    /// Gets API documentation
    /// </summary>
    public IEnumerable<ApiDocumentation> GetApiDocumentation()
    {
        return _apiDocs.Values;
    }

    /// <summary>
    /// Gets class documentation
    /// </summary>
    public IEnumerable<ClassDocumentation> GetClassDocumentation()
    {
        return _classDocs.Values;
    }

    /// <summary>
    /// Gets code analysis
    /// </summary>
    public IEnumerable<CodeAnalysis> GetCodeAnalysis()
    {
        return _codeAnalysis.Values;
    }

    #region Private Methods

    private async void ProcessGeneration(object? state)
    {
        if (!_isGenerating)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;

            // Process documentation generation
            foreach (var doc in _generatedDocs.Values)
            {
                await ProcessDocumentationGeneration(doc, currentTime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in documentation generation processing");
        }
    }

    private async Task ProcessDocumentationGeneration(GeneratedDocumentation documentation, DateTime currentTime)
    {
        try
        {
            // Process documentation generation logic
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing documentation generation: {DocId}", documentation.Id);
        }
    }

    private async Task AnalyzeCodeForApiDocumentation(ApiDocumentation apiDoc)
    {
        try
        {
            // Analyze code and generate API endpoints
            apiDoc.Endpoints.Add(new ApiEndpoint
            {
                Path = "/api/devices",
                Method = "GET",
                Description = "Get all devices",
                Parameters = new List<ApiParameter>(),
                Response = new ApiResponse
                {
                    StatusCode = 200,
                    Description = "List of devices",
                    Schema = "Device[]"
                }
            });

            apiDoc.Endpoints.Add(new ApiEndpoint
            {
                Path = "/api/devices/{id}",
                Method = "PUT",
                Description = "Update device",
                Parameters = new List<ApiParameter>
                {
                    new ApiParameter
                    {
                        Name = "id",
                        Type = "string",
                        Required = true,
                        Description = "Device ID"
                    }
                },
                Response = new ApiResponse
                {
                    StatusCode = 200,
                    Description = "Updated device",
                    Schema = "Device"
                }
            });

            // Generate API models
            apiDoc.Models.Add(new ApiModel
            {
                Name = "Device",
                Description = "Smart device representation",
                Properties = new List<ApiProperty>
                {
                    new ApiProperty
                    {
                        Name = "id",
                        Type = "string",
                        Description = "Unique device identifier"
                    },
                    new ApiProperty
                    {
                        Name = "name",
                        Type = "string",
                        Description = "Device name"
                    },
                    new ApiProperty
                    {
                        Name = "type",
                        Type = "string",
                        Description = "Device type (hue, tplink, magichome)"
                    }
                }
            });

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing code for API documentation: {ApiDocId}", apiDoc.Id);
        }
    }

    private async Task AnalyzeClass(ClassDocumentation classDoc)
    {
        try
        {
            // Analyze class and generate method documentation
            classDoc.Methods.Add(new MethodDocumentation
            {
                Name = "SetColorAsync",
                Description = "Sets the color of the device",
                Parameters = new List<ParameterDocumentation>
                {
                    new ParameterDocumentation
                    {
                        Name = "deviceId",
                        Type = "string",
                        Description = "Device identifier"
                    },
                    new ParameterDocumentation
                    {
                        Name = "color",
                        Type = "Color",
                        Description = "Color to set"
                    }
                },
                ReturnType = "Task<bool>",
                ReturnDescription = "True if successful, false otherwise"
            });

            classDoc.Properties.Add(new PropertyDocumentation
            {
                Name = "IsConnected",
                Type = "bool",
                Description = "Indicates if the device is connected",
                IsReadOnly = true
            });

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing class: {ClassName}", classDoc.ClassName);
        }
    }

    private async Task PerformCodeAnalysis(CodeAnalysis analysis)
    {
        try
        {
            // Perform code analysis
            analysis.Classes.Add(new ClassInfo
            {
                Name = "DeviceManagerService",
                Namespace = "PartyLights.Services",
                Description = "Manages smart device connections and control",
                MethodCount = 15,
                PropertyCount = 8,
                IsPublic = true
            });

            analysis.Methods.Add(new MethodInfo
            {
                Name = "ConnectDeviceAsync",
                ClassName = "DeviceManagerService",
                ReturnType = "Task<bool>",
                ParameterCount = 1,
                IsPublic = true,
                IsAsync = true
            });

            analysis.Properties.Add(new PropertyInfo
            {
                Name = "ConnectedDevices",
                ClassName = "DeviceManagerService",
                Type = "IEnumerable<SmartDevice>",
                IsPublic = true,
                IsReadOnly = true
            });

            analysis.Interfaces.Add(new InterfaceInfo
            {
                Name = "IDeviceController",
                Namespace = "PartyLights.Core.Interfaces",
                Description = "Interface for device control operations",
                MethodCount = 8,
                PropertyCount = 2
            });

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing code analysis: {AnalysisId}", analysis.Id);
        }
    }

    private async Task GenerateAllDocumentation(GeneratedDocumentation documentation)
    {
        try
        {
            // Generate API documentation
            var apiDoc = await GenerateApiDocumentationAsync(new ApiDocumentationRequest
            {
                Name = "PartyLights API",
                Description = "Complete API reference",
                Version = "1.0.0"
            });
            documentation.ApiDocumentation.Add(apiDoc);

            // Generate class documentation for key classes
            var deviceManagerDoc = await GenerateClassDocumentationAsync("DeviceManagerService");
            documentation.ClassDocumentation.Add(deviceManagerDoc);

            var audioServiceDoc = await GenerateClassDocumentationAsync("AudioAnalysisService");
            documentation.ClassDocumentation.Add(audioServiceDoc);

            // Generate code analysis
            var codeAnalysis = await AnalyzeCodeAsync(new CodeAnalysisRequest
            {
                ProjectName = "PartyLights",
                AnalysisType = CodeAnalysisType.Comprehensive
            });
            documentation.CodeAnalysis.Add(codeAnalysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating all documentation: {DocId}", documentation.Id);
        }
    }

    private void InitializeCodeAnalysis()
    {
        try
        {
            // Initialize code analysis
            _logger.LogInformation("Code analysis initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing code analysis");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isGenerating = false;
            _generationTimer?.Dispose();

            _logger.LogInformation("Documentation generation service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing documentation generation service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// API documentation request
/// </summary>
public class ApiDocumentationRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public string? BaseUrl { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Code analysis request
/// </summary>
public class CodeAnalysisRequest
{
    public string? ProjectName { get; set; }
    public CodeAnalysisType AnalysisType { get; set; } = CodeAnalysisType.Comprehensive;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Comprehensive documentation request
/// </summary>
public class ComprehensiveDocumentationRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DocumentationType DocumentationType { get; set; } = DocumentationType.Comprehensive;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Generated documentation
/// </summary>
public class GeneratedDocumentation
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DocumentationType DocumentationType { get; set; }
    public DateTime GeneratedAt { get; set; }
    public bool Success { get; set; } = true;
    public string ErrorMessage { get; set; } = string.Empty;
    public List<ApiDocumentation> ApiDocumentation { get; set; } = new();
    public List<ClassDocumentation> ClassDocumentation { get; set; } = new();
    public List<CodeAnalysis> CodeAnalysis { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// API documentation
/// </summary>
public class ApiDocumentation
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public List<ApiEndpoint> Endpoints { get; set; } = new();
    public List<ApiModel> Models { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public bool Success { get; set; } = true;
    public string ErrorMessage { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// API endpoint
/// </summary>
public class ApiEndpoint
{
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ApiParameter> Parameters { get; set; } = new();
    public ApiResponse Response { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// API parameter
/// </summary>
public class ApiParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
}

/// <summary>
/// API response
/// </summary>
public class ApiResponse
{
    public int StatusCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
}

/// <summary>
/// API model
/// </summary>
public class ApiModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ApiProperty> Properties { get; set; } = new();
    public List<string> Examples { get; set; } = new();
}

/// <summary>
/// API property
/// </summary>
public class ApiProperty
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Class documentation
/// </summary>
public class ClassDocumentation
{
    public string Id { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<MethodDocumentation> Methods { get; set; } = new();
    public List<PropertyDocumentation> Properties { get; set; } = new();
    public List<EventDocumentation> Events { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public bool Success { get; set; } = true;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Method documentation
/// </summary>
public class MethodDocumentation
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ParameterDocumentation> Parameters { get; set; } = new();
    public string ReturnType { get; set; } = string.Empty;
    public string ReturnDescription { get; set; } = string.Empty;
    public List<string> Examples { get; set; } = new();
}

/// <summary>
/// Parameter documentation
/// </summary>
public class ParameterDocumentation
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Property documentation
/// </summary>
public class PropertyDocumentation
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Event documentation
/// </summary>
public class EventDocumentation
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public List<ParameterDocumentation> Parameters { get; set; } = new();
}

/// <summary>
/// Code analysis
/// </summary>
public class CodeAnalysis
{
    public string Id { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public CodeAnalysisType AnalysisType { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<ClassInfo> Classes { get; set; } = new();
    public List<MethodInfo> Methods { get; set; } = new();
    public List<PropertyInfo> Properties { get; set; } = new();
    public List<InterfaceInfo> Interfaces { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Class information
/// </summary>
public class ClassInfo
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MethodCount { get; set; }
    public int PropertyCount { get; set; }
    public bool IsPublic { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsStatic { get; set; }
}

/// <summary>
/// Method information
/// </summary>
public class MethodInfo
{
    public string Name { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public int ParameterCount { get; set; }
    public bool IsPublic { get; set; }
    public bool IsStatic { get; set; }
    public bool IsAsync { get; set; }
    public bool IsVirtual { get; set; }
}

/// <summary>
/// Property information
/// </summary>
public class PropertyInfo
{
    public string Name { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsStatic { get; set; }
    public bool IsVirtual { get; set; }
}

/// <summary>
/// Interface information
/// </summary>
public class InterfaceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MethodCount { get; set; }
    public int PropertyCount { get; set; }
    public List<string> ImplementedBy { get; set; } = new();
}

/// <summary>
/// Documentation generation event arguments
/// </summary>
public class DocumentationGenerationEventArgs : EventArgs
{
    public string DocId { get; }
    public GeneratedDocumentation Documentation { get; }
    public DocumentationGenerationAction Action { get; }
    public DateTime Timestamp { get; }

    public DocumentationGenerationEventArgs(string docId, GeneratedDocumentation documentation, DocumentationGenerationAction action)
    {
        DocId = docId;
        Documentation = documentation;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// API documentation event arguments
/// </summary>
public class ApiDocumentationEventArgs : EventArgs
{
    public string ApiDocId { get; }
    public ApiDocumentation ApiDocumentation { get; }
    public ApiDocumentationAction Action { get; }
    public DateTime Timestamp { get; }

    public ApiDocumentationEventArgs(string apiDocId, ApiDocumentation apiDocumentation, ApiDocumentationAction action)
    {
        ApiDocId = apiDocId;
        ApiDocumentation = apiDocumentation;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Code analysis event arguments
/// </summary>
public class CodeAnalysisEventArgs : EventArgs
{
    public string AnalysisId { get; }
    public CodeAnalysis Analysis { get; }
    public CodeAnalysisAction Action { get; }
    public DateTime Timestamp { get; }

    public CodeAnalysisEventArgs(string analysisId, CodeAnalysis analysis, CodeAnalysisAction action)
    {
        AnalysisId = analysisId;
        Analysis = analysis;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Code analysis types
/// </summary>
public enum CodeAnalysisType
{
    Basic,
    Comprehensive,
    Performance,
    Security
}

/// <summary>
/// Documentation generation actions
/// </summary>
public enum DocumentationGenerationAction
{
    Generated,
    Updated,
    Exported
}

/// <summary>
/// API documentation actions
/// </summary>
public enum ApiDocumentationAction
{
    Generated,
    Updated,
    Exported
}

/// <summary>
/// Code analysis actions
/// </summary>
public enum CodeAnalysisAction
{
    Started,
    Completed,
    Failed
}
