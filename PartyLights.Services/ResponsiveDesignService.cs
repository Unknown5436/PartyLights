using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PartyLights.Services;

/// <summary>
/// Responsive design service for adaptive UI layouts
/// </summary>
public class ResponsiveDesignService : IDisposable
{
    private readonly ILogger<ResponsiveDesignService> _logger;
    private readonly ConcurrentDictionary<string, ResponsiveLayout> _layouts = new();
    private readonly ConcurrentDictionary<string, Breakpoint> _breakpoints = new();
    private readonly Timer _responsiveTimer;
    private readonly object _lockObject = new();

    private const int ResponsiveIntervalMs = 500; // 500ms
    private bool _isResponsive;

    // Responsive design
    private readonly Dictionary<string, ResponsiveRule> _responsiveRules = new();
    private readonly Dictionary<string, AdaptiveComponent> _adaptiveComponents = new();
    private Size _currentScreenSize;
    private string _currentBreakpoint = "desktop";

    public event EventHandler<ResponsiveLayoutEventArgs>? LayoutChanged;
    public event EventHandler<BreakpointEventArgs>? BreakpointChanged;
    public event EventHandler<AdaptiveComponentEventArgs>? ComponentAdapted;

    public ResponsiveDesignService(ILogger<ResponsiveDesignService> logger)
    {
        _logger = logger;

        _responsiveTimer = new Timer(ProcessResponsiveDesign, null, ResponsiveIntervalMs, ResponsiveIntervalMs);
        _isResponsive = true;

        InitializeBreakpoints();
        InitializeResponsiveRules();
        InitializeAdaptiveComponents();

        _logger.LogInformation("Responsive design service initialized");
    }

    /// <summary>
    /// Creates a responsive layout
    /// </summary>
    public async Task<string> CreateResponsiveLayoutAsync(ResponsiveLayoutRequest request)
    {
        try
        {
            var layoutId = Guid.NewGuid().ToString();

            var layout = new ResponsiveLayout
            {
                Id = layoutId,
                Name = request.Name,
                Description = request.Description,
                LayoutType = request.LayoutType,
                Breakpoints = request.Breakpoints ?? new Dictionary<string, ResponsiveBreakpoint>(),
                Components = request.Components ?? new List<ResponsiveComponent>(),
                Rules = request.Rules ?? new List<string>(),
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _layouts[layoutId] = layout;

            LayoutChanged?.Invoke(this, new ResponsiveLayoutEventArgs(layoutId, layout, LayoutAction.Created));
            _logger.LogInformation("Created responsive layout: {LayoutName} ({LayoutId})", request.Name, layoutId);

            return layoutId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating responsive layout: {LayoutName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Updates screen size and triggers responsive adjustments
    /// </summary>
    public async Task<bool> UpdateScreenSizeAsync(Size newSize)
    {
        try
        {
            var previousSize = _currentScreenSize;
            var previousBreakpoint = _currentBreakpoint;

            _currentScreenSize = newSize;

            // Determine current breakpoint
            var newBreakpoint = DetermineBreakpoint(newSize);

            if (newBreakpoint != _currentBreakpoint)
            {
                _currentBreakpoint = newBreakpoint;

                // Trigger breakpoint change
                BreakpointChanged?.Invoke(this, new BreakpointEventArgs(newBreakpoint, previousBreakpoint, newSize));

                // Apply responsive adjustments
                await ApplyResponsiveAdjustments(newBreakpoint);

                _logger.LogInformation("Screen size updated: {Width}x{Height}, Breakpoint: {Breakpoint}",
                    newSize.Width, newSize.Height, newBreakpoint);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating screen size");
            return false;
        }
    }

    /// <summary>
    /// Adapts a component for the current breakpoint
    /// </summary>
    public async Task<bool> AdaptComponentAsync(string componentId, string breakpoint)
    {
        try
        {
            if (!_adaptiveComponents.TryGetValue(componentId, out var component))
            {
                _logger.LogWarning("Adaptive component not found: {ComponentId}", componentId);
                return false;
            }

            // Get breakpoint-specific properties
            var breakpointProperties = GetBreakpointProperties(component, breakpoint);

            // Apply adaptive properties
            await ApplyAdaptiveProperties(component, breakpointProperties);

            ComponentAdapted?.Invoke(this, new AdaptiveComponentEventArgs(componentId, component, breakpoint));
            _logger.LogInformation("Adapted component {ComponentId} for breakpoint {Breakpoint}", componentId, breakpoint);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adapting component {ComponentId} for breakpoint {Breakpoint}", componentId, breakpoint);
            return false;
        }
    }

    /// <summary>
    /// Applies responsive rules
    /// </summary>
    public async Task<bool> ApplyResponsiveRulesAsync(string breakpoint)
    {
        try
        {
            var applicableRules = _responsiveRules.Values.Where(r => r.IsEnabled &&
                (r.TargetBreakpoints.Contains(breakpoint) || r.TargetBreakpoints.Contains("all")));

            foreach (var rule in applicableRules)
            {
                await ApplyResponsiveRule(rule, breakpoint);
            }

            _logger.LogInformation("Applied {RuleCount} responsive rules for breakpoint {Breakpoint}",
                applicableRules.Count(), breakpoint);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying responsive rules for breakpoint {Breakpoint}", breakpoint);
            return false;
        }
    }

    /// <summary>
    /// Creates a responsive breakpoint
    /// </summary>
    public async Task<string> CreateBreakpointAsync(ResponsiveBreakpointRequest request)
    {
        try
        {
            var breakpointId = Guid.NewGuid().ToString();

            var breakpoint = new Breakpoint
            {
                Id = breakpointId,
                Name = request.Name,
                Description = request.Description,
                MinWidth = request.MinWidth,
                MaxWidth = request.MaxWidth,
                MinHeight = request.MinHeight,
                MaxHeight = request.MaxHeight,
                Orientation = request.Orientation,
                Properties = request.Properties ?? new Dictionary<string, object>(),
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _breakpoints[breakpointId] = breakpoint;

            _logger.LogInformation("Created responsive breakpoint: {BreakpointName} ({BreakpointId})", request.Name, breakpointId);

            return breakpointId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating responsive breakpoint: {BreakpointName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets responsive layouts
    /// </summary>
    public IEnumerable<ResponsiveLayout> GetResponsiveLayouts()
    {
        return _layouts.Values;
    }

    /// <summary>
    /// Gets breakpoints
    /// </summary>
    public IEnumerable<Breakpoint> GetBreakpoints()
    {
        return _breakpoints.Values;
    }

    /// <summary>
    /// Gets current breakpoint
    /// </summary>
    public string GetCurrentBreakpoint()
    {
        return _currentBreakpoint;
    }

    /// <summary>
    /// Gets current screen size
    /// </summary>
    public Size GetCurrentScreenSize()
    {
        return _currentScreenSize;
    }

    #region Private Methods

    private async void ProcessResponsiveDesign(object? state)
    {
        if (!_isResponsive)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;

            // Process active layouts
            foreach (var layout in _layouts.Values.Where(l => l.IsActive))
            {
                await ProcessResponsiveLayout(layout, currentTime);
            }

            // Process adaptive components
            foreach (var component in _adaptiveComponents.Values.Where(c => c.IsActive))
            {
                await ProcessAdaptiveComponent(component, currentTime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in responsive design processing");
        }
    }

    private async Task ProcessResponsiveLayout(ResponsiveLayout layout, DateTime currentTime)
    {
        try
        {
            // Process responsive layout logic
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing responsive layout: {LayoutId}", layout.Id);
        }
    }

    private async Task ProcessAdaptiveComponent(AdaptiveComponent component, DateTime currentTime)
    {
        try
        {
            // Process adaptive component logic
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing adaptive component: {ComponentId}", component.Id);
        }
    }

    private string DetermineBreakpoint(Size screenSize)
    {
        try
        {
            // Check breakpoints in order of specificity
            var breakpoints = _breakpoints.Values
                .Where(b => b.IsActive)
                .OrderByDescending(b => b.MinWidth)
                .ThenByDescending(b => b.MinHeight);

            foreach (var breakpoint in breakpoints)
            {
                if (screenSize.Width >= breakpoint.MinWidth &&
                    screenSize.Width <= breakpoint.MaxWidth &&
                    screenSize.Height >= breakpoint.MinHeight &&
                    screenSize.Height <= breakpoint.MaxHeight)
                {
                    return breakpoint.Name;
                }
            }

            // Default to desktop if no breakpoint matches
            return "desktop";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining breakpoint for size {Width}x{Height}", screenSize.Width, screenSize.Height);
            return "desktop";
        }
    }

    private async Task ApplyResponsiveAdjustments(string breakpoint)
    {
        try
        {
            // Apply responsive adjustments for the new breakpoint
            await ApplyResponsiveRulesAsync(breakpoint);

            // Adapt all active components
            foreach (var component in _adaptiveComponents.Values.Where(c => c.IsActive))
            {
                await AdaptComponentAsync(component.Id, breakpoint);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying responsive adjustments for breakpoint {Breakpoint}", breakpoint);
        }
    }

    private Dictionary<string, object> GetBreakpointProperties(AdaptiveComponent component, string breakpoint)
    {
        try
        {
            if (component.BreakpointProperties.TryGetValue(breakpoint, out var properties))
            {
                return properties;
            }

            // Return default properties if breakpoint-specific properties not found
            return component.DefaultProperties;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting breakpoint properties for component {ComponentId} and breakpoint {Breakpoint}",
                component.Id, breakpoint);
            return component.DefaultProperties;
        }
    }

    private async Task ApplyAdaptiveProperties(AdaptiveComponent component, Dictionary<string, object> properties)
    {
        try
        {
            // Apply adaptive properties to component
            foreach (var property in properties)
            {
                // Apply property based on type
                await ApplyPropertyToComponent(component, property.Key, property.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying adaptive properties to component {ComponentId}", component.Id);
        }
    }

    private async Task ApplyPropertyToComponent(AdaptiveComponent component, string propertyName, object value)
    {
        try
        {
            // Apply property to component based on property name and value
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying property {PropertyName} to component {ComponentId}", propertyName, component.Id);
        }
    }

    private async Task ApplyResponsiveRule(ResponsiveRule rule, string breakpoint)
    {
        try
        {
            // Apply responsive rule based on rule type
            switch (rule.RuleType)
            {
                case ResponsiveRuleType.Layout:
                    await ApplyLayoutRule(rule, breakpoint);
                    break;
                case ResponsiveRuleType.Spacing:
                    await ApplySpacingRule(rule, breakpoint);
                    break;
                case ResponsiveRuleType.Typography:
                    await ApplyTypographyRule(rule, breakpoint);
                    break;
                case ResponsiveRuleType.Visibility:
                    await ApplyVisibilityRule(rule, breakpoint);
                    break;
                case ResponsiveRuleType.Custom:
                    await ApplyCustomRule(rule, breakpoint);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying responsive rule {RuleId} for breakpoint {Breakpoint}", rule.Id, breakpoint);
        }
    }

    private async Task ApplyLayoutRule(ResponsiveRule rule, string breakpoint)
    {
        try
        {
            // Apply layout rule
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying layout rule {RuleId}", rule.Id);
        }
    }

    private async Task ApplySpacingRule(ResponsiveRule rule, string breakpoint)
    {
        try
        {
            // Apply spacing rule
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying spacing rule {RuleId}", rule.Id);
        }
    }

    private async Task ApplyTypographyRule(ResponsiveRule rule, string breakpoint)
    {
        try
        {
            // Apply typography rule
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying typography rule {RuleId}", rule.Id);
        }
    }

    private async Task ApplyVisibilityRule(ResponsiveRule rule, string breakpoint)
    {
        try
        {
            // Apply visibility rule
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying visibility rule {RuleId}", rule.Id);
        }
    }

    private async Task ApplyCustomRule(ResponsiveRule rule, string breakpoint)
    {
        try
        {
            // Apply custom rule
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying custom rule {RuleId}", rule.Id);
        }
    }

    private void InitializeBreakpoints()
    {
        try
        {
            // Initialize default breakpoints
            CreateBreakpointAsync(new ResponsiveBreakpointRequest
            {
                Name = "mobile",
                Description = "Mobile devices (phones)",
                MinWidth = 0,
                MaxWidth = 767,
                MinHeight = 0,
                MaxHeight = 1024,
                Orientation = Orientation.Portrait,
                Properties = new Dictionary<string, object>
                {
                    ["GridColumns"] = 1,
                    ["Spacing"] = 8,
                    ["FontSize"] = 14
                },
                IsActive = true
            }).Wait();

            CreateBreakpointAsync(new ResponsiveBreakpointRequest
            {
                Name = "tablet",
                Description = "Tablet devices",
                MinWidth = 768,
                MaxWidth = 1023,
                MinHeight = 0,
                MaxHeight = 1366,
                Orientation = Orientation.Landscape,
                Properties = new Dictionary<string, object>
                {
                    ["GridColumns"] = 2,
                    ["Spacing"] = 16,
                    ["FontSize"] = 16
                },
                IsActive = true
            }).Wait();

            CreateBreakpointAsync(new ResponsiveBreakpointRequest
            {
                Name = "desktop",
                Description = "Desktop devices",
                MinWidth = 1024,
                MaxWidth = 1920,
                MinHeight = 0,
                MaxHeight = 1080,
                Orientation = Orientation.Landscape,
                Properties = new Dictionary<string, object>
                {
                    ["GridColumns"] = 3,
                    ["Spacing"] = 24,
                    ["FontSize"] = 18
                },
                IsActive = true
            }).Wait();

            CreateBreakpointAsync(new ResponsiveBreakpointRequest
            {
                Name = "large_desktop",
                Description = "Large desktop devices",
                MinWidth = 1921,
                MaxWidth = 3840,
                MinHeight = 0,
                MaxHeight = 2160,
                Orientation = Orientation.Landscape,
                Properties = new Dictionary<string, object>
                {
                    ["GridColumns"] = 4,
                    ["Spacing"] = 32,
                    ["FontSize"] = 20
                },
                IsActive = true
            }).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing breakpoints");
        }
    }

    private void InitializeResponsiveRules()
    {
        try
        {
            // Initialize responsive rules
            _responsiveRules["mobile_layout"] = new ResponsiveRule
            {
                Id = "mobile_layout",
                Name = "Mobile Layout",
                Description = "Single column layout for mobile devices",
                RuleType = ResponsiveRuleType.Layout,
                TargetBreakpoints = new List<string> { "mobile" },
                Properties = new Dictionary<string, object>
                {
                    ["LayoutType"] = "StackPanel",
                    ["Orientation"] = "Vertical",
                    ["Spacing"] = 8
                },
                IsEnabled = true
            };

            _responsiveRules["tablet_layout"] = new ResponsiveRule
            {
                Id = "tablet_layout",
                Name = "Tablet Layout",
                Description = "Two column layout for tablet devices",
                RuleType = ResponsiveRuleType.Layout,
                TargetBreakpoints = new List<string> { "tablet" },
                Properties = new Dictionary<string, object>
                {
                    ["LayoutType"] = "Grid",
                    ["Columns"] = 2,
                    ["Spacing"] = 16
                },
                IsEnabled = true
            };

            _responsiveRules["desktop_layout"] = new ResponsiveRule
            {
                Id = "desktop_layout",
                Name = "Desktop Layout",
                Description = "Multi column layout for desktop devices",
                RuleType = ResponsiveRuleType.Layout,
                TargetBreakpoints = new List<string> { "desktop", "large_desktop" },
                Properties = new Dictionary<string, object>
                {
                    ["LayoutType"] = "Grid",
                    ["Columns"] = 3,
                    ["Spacing"] = 24
                },
                IsEnabled = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing responsive rules");
        }
    }

    private void InitializeAdaptiveComponents()
    {
        try
        {
            // Initialize adaptive components
            _adaptiveComponents["main_panel"] = new AdaptiveComponent
            {
                Id = "main_panel",
                Name = "Main Panel",
                ComponentType = ComponentType.Panel,
                DefaultProperties = new Dictionary<string, object>
                {
                    ["Width"] = 800,
                    ["Height"] = 600,
                    ["Margin"] = new Thickness(16),
                    ["Padding"] = new Thickness(16)
                },
                BreakpointProperties = new Dictionary<string, Dictionary<string, object>>
                {
                    ["mobile"] = new Dictionary<string, object>
                    {
                        ["Width"] = 320,
                        ["Height"] = 480,
                        ["Margin"] = new Thickness(8),
                        ["Padding"] = new Thickness(8)
                    },
                    ["tablet"] = new Dictionary<string, object>
                    {
                        ["Width"] = 600,
                        ["Height"] = 400,
                        ["Margin"] = new Thickness(12),
                        ["Padding"] = new Thickness(12)
                    },
                    ["desktop"] = new Dictionary<string, object>
                    {
                        ["Width"] = 800,
                        ["Height"] = 600,
                        ["Margin"] = new Thickness(16),
                        ["Padding"] = new Thickness(16)
                    },
                    ["large_desktop"] = new Dictionary<string, object>
                    {
                        ["Width"] = 1200,
                        ["Height"] = 800,
                        ["Margin"] = new Thickness(24),
                        ["Padding"] = new Thickness(24)
                    }
                },
                IsActive = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing adaptive components");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isResponsive = false;
            _responsiveTimer?.Dispose();

            _logger.LogInformation("Responsive design service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing responsive design service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Responsive layout request
/// </summary>
public class ResponsiveLayoutRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ResponsiveLayoutType LayoutType { get; set; } = ResponsiveLayoutType.Grid;
    public Dictionary<string, ResponsiveBreakpoint>? Breakpoints { get; set; }
    public List<ResponsiveComponent>? Components { get; set; }
    public List<string>? Rules { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Responsive breakpoint request
/// </summary>
public class ResponsiveBreakpointRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double MinWidth { get; set; } = 0;
    public double MaxWidth { get; set; } = double.MaxValue;
    public double MinHeight { get; set; } = 0;
    public double MaxHeight { get; set; } = double.MaxValue;
    public Orientation Orientation { get; set; } = Orientation.Landscape;
    public Dictionary<string, object>? Properties { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Responsive layout
/// </summary>
public class ResponsiveLayout
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ResponsiveLayoutType LayoutType { get; set; }
    public Dictionary<string, ResponsiveBreakpoint> Breakpoints { get; set; } = new();
    public List<ResponsiveComponent> Components { get; set; } = new();
    public List<string> Rules { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Responsive breakpoint
/// </summary>
public class ResponsiveBreakpoint
{
    public string Name { get; set; } = string.Empty;
    public double MinWidth { get; set; }
    public double MaxWidth { get; set; }
    public double MinHeight { get; set; }
    public double MaxHeight { get; set; }
    public Orientation Orientation { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Responsive component
/// </summary>
public class ResponsiveComponent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ComponentType ComponentType { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public Dictionary<string, Dictionary<string, object>> BreakpointProperties { get; set; } = new();
}

/// <summary>
/// Breakpoint
/// </summary>
public class Breakpoint
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double MinWidth { get; set; }
    public double MaxWidth { get; set; }
    public double MinHeight { get; set; }
    public double MaxHeight { get; set; }
    public Orientation Orientation { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Responsive rule
/// </summary>
public class ResponsiveRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ResponsiveRuleType RuleType { get; set; }
    public List<string> TargetBreakpoints { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Adaptive component
/// </summary>
public class AdaptiveComponent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ComponentType ComponentType { get; set; }
    public Dictionary<string, object> DefaultProperties { get; set; } = new();
    public Dictionary<string, Dictionary<string, object>> BreakpointProperties { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Responsive layout event arguments
/// </summary>
public class ResponsiveLayoutEventArgs : EventArgs
{
    public string LayoutId { get; }
    public ResponsiveLayout Layout { get; }
    public LayoutAction Action { get; }
    public DateTime Timestamp { get; }

    public ResponsiveLayoutEventArgs(string layoutId, ResponsiveLayout layout, LayoutAction action)
    {
        LayoutId = layoutId;
        Layout = layout;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Breakpoint event arguments
/// </summary>
public class BreakpointEventArgs : EventArgs
{
    public string CurrentBreakpoint { get; }
    public string PreviousBreakpoint { get; }
    public Size ScreenSize { get; }
    public DateTime Timestamp { get; }

    public BreakpointEventArgs(string currentBreakpoint, string previousBreakpoint, Size screenSize)
    {
        CurrentBreakpoint = currentBreakpoint;
        PreviousBreakpoint = previousBreakpoint;
        ScreenSize = screenSize;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Adaptive component event arguments
/// </summary>
public class AdaptiveComponentEventArgs : EventArgs
{
    public string ComponentId { get; }
    public AdaptiveComponent Component { get; }
    public string Breakpoint { get; }
    public DateTime Timestamp { get; }

    public AdaptiveComponentEventArgs(string componentId, AdaptiveComponent component, string breakpoint)
    {
        ComponentId = componentId;
        Component = component;
        Breakpoint = breakpoint;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Responsive layout types
/// </summary>
public enum ResponsiveLayoutType
{
    Grid,
    StackPanel,
    DockPanel,
    WrapPanel,
    Canvas,
    UniformGrid,
    Custom
}

/// <summary>
/// Responsive rule types
/// </summary>
public enum ResponsiveRuleType
{
    Layout,
    Spacing,
    Typography,
    Visibility,
    Custom
}

/// <summary>
/// Layout actions
/// </summary>
public enum LayoutAction
{
    Created,
    Updated,
    Removed
}
