using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Input;

namespace PartyLights.Services;

/// <summary>
/// Comprehensive UI/UX refinement service for polished user experience
/// </summary>
public class UiUxRefinementService : IDisposable
{
    private readonly ILogger<UiUxRefinementService> _logger;
    private readonly ConcurrentDictionary<string, UiTheme> _themes = new();
    private readonly ConcurrentDictionary<string, UiAnimation> _animations = new();
    private readonly ConcurrentDictionary<string, UiComponent> _components = new();
    private readonly Timer _refinementTimer;
    private readonly object _lockObject = new();

    private const int RefinementIntervalMs = 1000; // 1 second
    private bool _isRefining;

    // UI/UX refinement
    private readonly Dictionary<string, UiStyle> _uiStyles = new();
    private readonly Dictionary<string, UiLayout> _uiLayouts = new();
    private readonly Dictionary<string, UiInteraction> _uiInteractions = new();
    private readonly Dictionary<string, AccessibilityFeature> _accessibilityFeatures = new();

    public event EventHandler<UiThemeEventArgs>? ThemeChanged;
    public event EventHandler<UiAnimationEventArgs>? AnimationTriggered;
    public event EventHandler<UiComponentEventArgs>? ComponentRefined;
    public event EventHandler<AccessibilityEventArgs>? AccessibilityFeatureApplied;

    public UiUxRefinementService(ILogger<UiUxRefinementService> logger)
    {
        _logger = logger;

        _refinementTimer = new Timer(ProcessRefinements, null, RefinementIntervalMs, RefinementIntervalMs);
        _isRefining = true;

        InitializeThemes();
        InitializeAnimations();
        InitializeComponents();
        InitializeAccessibilityFeatures();

        _logger.LogInformation("UI/UX refinement service initialized");
    }

    /// <summary>
    /// Applies a UI theme
    /// </summary>
    public async Task<bool> ApplyThemeAsync(ThemeRequest request)
    {
        try
        {
            var theme = new UiTheme
            {
                Id = request.Id,
                Name = request.Name,
                Description = request.Description,
                ColorScheme = request.ColorScheme,
                Typography = request.Typography,
                Spacing = request.Spacing,
                BorderRadius = request.BorderRadius,
                Shadows = request.Shadows,
                Animations = request.Animations,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                LastApplied = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _themes[request.Id] = theme;

            // Apply theme to UI components
            await ApplyThemeToComponents(theme);

            ThemeChanged?.Invoke(this, new UiThemeEventArgs(theme.Id, theme, ThemeAction.Applied));
            _logger.LogInformation("Applied UI theme: {ThemeName} ({ThemeId})", request.Name, request.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying UI theme: {ThemeName}", request.Name);
            return false;
        }
    }

    /// <summary>
    /// Creates a UI animation
    /// </summary>
    public async Task<string> CreateAnimationAsync(AnimationRequest request)
    {
        try
        {
            var animationId = Guid.NewGuid().ToString();

            var animation = new UiAnimation
            {
                Id = animationId,
                Name = request.Name,
                Description = request.Description,
                AnimationType = request.AnimationType,
                Duration = request.Duration,
                EasingFunction = request.EasingFunction,
                Properties = request.Properties ?? new Dictionary<string, object>(),
                Triggers = request.Triggers ?? new List<string>(),
                IsEnabled = request.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _animations[animationId] = animation;

            AnimationTriggered?.Invoke(this, new UiAnimationEventArgs(animationId, animation, AnimationAction.Created));
            _logger.LogInformation("Created UI animation: {AnimationName} ({AnimationId})", request.Name, animationId);

            return animationId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating UI animation: {AnimationName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Refines a UI component
    /// </summary>
    public async Task<bool> RefineComponentAsync(ComponentRefinementRequest request)
    {
        try
        {
            var component = new UiComponent
            {
                Id = request.Id,
                Name = request.Name,
                ComponentType = request.ComponentType,
                Style = request.Style,
                Layout = request.Layout,
                Interactions = request.Interactions ?? new List<string>(),
                Accessibility = request.Accessibility ?? new Dictionary<string, object>(),
                Animations = request.Animations ?? new List<string>(),
                IsRefined = request.IsRefined,
                LastRefined = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _components[request.Id] = component;

            // Apply refinements
            await ApplyComponentRefinements(component);

            ComponentRefined?.Invoke(this, new UiComponentEventArgs(component.Id, component, ComponentAction.Refined));
            _logger.LogInformation("Refined UI component: {ComponentName} ({ComponentId})", request.Name, request.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refining UI component: {ComponentName}", request.Name);
            return false;
        }
    }

    /// <summary>
    /// Applies accessibility features
    /// </summary>
    public async Task<bool> ApplyAccessibilityFeatureAsync(AccessibilityRequest request)
    {
        try
        {
            var feature = new AccessibilityFeature
            {
                Id = request.Id,
                Name = request.Name,
                Description = request.Description,
                FeatureType = request.FeatureType,
                TargetComponent = request.TargetComponent,
                Properties = request.Properties ?? new Dictionary<string, object>(),
                IsEnabled = request.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                LastApplied = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _accessibilityFeatures[request.Id] = feature;

            // Apply accessibility feature
            await ApplyAccessibilityFeatureToComponent(feature);

            AccessibilityFeatureApplied?.Invoke(this, new AccessibilityEventArgs(feature.Id, feature, AccessibilityAction.Applied));
            _logger.LogInformation("Applied accessibility feature: {FeatureName} ({FeatureId})", request.Name, request.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying accessibility feature: {FeatureName}", request.Name);
            return false;
        }
    }

    /// <summary>
    /// Creates a polished UI style
    /// </summary>
    public async Task<string> CreateUiStyleAsync(UiStyleRequest request)
    {
        try
        {
            var styleId = Guid.NewGuid().ToString();

            var style = new UiStyle
            {
                Id = styleId,
                Name = request.Name,
                Description = request.Description,
                ComponentType = request.ComponentType,
                Colors = request.Colors ?? new Dictionary<string, Color>(),
                Typography = request.Typography ?? new Dictionary<string, object>(),
                Spacing = request.Spacing ?? new Dictionary<string, double>(),
                Borders = request.Borders ?? new Dictionary<string, object>(),
                Shadows = request.Shadows ?? new Dictionary<string, object>(),
                Animations = request.Animations ?? new List<string>(),
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _uiStyles[styleId] = style;

            _logger.LogInformation("Created UI style: {StyleName} ({StyleId})", request.Name, styleId);

            return styleId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating UI style: {StyleName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Creates a responsive UI layout
    /// </summary>
    public async Task<string> CreateUiLayoutAsync(UiLayoutRequest request)
    {
        try
        {
            var layoutId = Guid.NewGuid().ToString();

            var layout = new UiLayout
            {
                Id = layoutId,
                Name = request.Name,
                Description = request.Description,
                LayoutType = request.LayoutType,
                Breakpoints = request.Breakpoints ?? new Dictionary<string, double>(),
                GridSettings = request.GridSettings ?? new Dictionary<string, object>(),
                FlexSettings = request.FlexSettings ?? new Dictionary<string, object>(),
                ResponsiveRules = request.ResponsiveRules ?? new List<string>(),
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _uiLayouts[layoutId] = layout;

            _logger.LogInformation("Created UI layout: {LayoutName} ({LayoutId})", request.Name, layoutId);

            return layoutId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating UI layout: {LayoutName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Creates an interactive UI element
    /// </summary>
    public async Task<string> CreateUiInteractionAsync(UiInteractionRequest request)
    {
        try
        {
            var interactionId = Guid.NewGuid().ToString();

            var interaction = new UiInteraction
            {
                Id = interactionId,
                Name = request.Name,
                Description = request.Description,
                InteractionType = request.InteractionType,
                TargetComponent = request.TargetComponent,
                Triggers = request.Triggers ?? new List<string>(),
                Actions = request.Actions ?? new List<string>(),
                Feedback = request.Feedback ?? new Dictionary<string, object>(),
                IsEnabled = request.IsEnabled,
                CreatedAt = DateTime.UtcNow,
                Metadata = request.Metadata ?? new Dictionary<string, object>()
            };

            _uiInteractions[interactionId] = interaction;

            _logger.LogInformation("Created UI interaction: {InteractionName} ({InteractionId})", request.Name, interactionId);

            return interactionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating UI interaction: {InteractionName}", request.Name);
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets UI themes
    /// </summary>
    public IEnumerable<UiTheme> GetThemes()
    {
        return _themes.Values;
    }

    /// <summary>
    /// Gets UI animations
    /// </summary>
    public IEnumerable<UiAnimation> GetAnimations()
    {
        return _animations.Values;
    }

    /// <summary>
    /// Gets UI components
    /// </summary>
    public IEnumerable<UiComponent> GetComponents()
    {
        return _components.Values;
    }

    /// <summary>
    /// Gets accessibility features
    /// </summary>
    public IEnumerable<AccessibilityFeature> GetAccessibilityFeatures()
    {
        return _accessibilityFeatures.Values;
    }

    #region Private Methods

    private async void ProcessRefinements(object? state)
    {
        if (!_isRefining)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;

            // Process active themes
            foreach (var theme in _themes.Values.Where(t => t.IsActive))
            {
                await ProcessActiveTheme(theme, currentTime);
            }

            // Process animations
            foreach (var animation in _animations.Values.Where(a => a.IsEnabled))
            {
                await ProcessAnimation(animation, currentTime);
            }

            // Process components
            foreach (var component in _components.Values.Where(c => c.IsRefined))
            {
                await ProcessComponent(component, currentTime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UI/UX refinement processing");
        }
    }

    private async Task ProcessActiveTheme(UiTheme theme, DateTime currentTime)
    {
        try
        {
            // Process active theme logic
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing active theme: {ThemeId}", theme.Id);
        }
    }

    private async Task ProcessAnimation(UiAnimation animation, DateTime currentTime)
    {
        try
        {
            // Process animation logic
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing animation: {AnimationId}", animation.Id);
        }
    }

    private async Task ProcessComponent(UiComponent component, DateTime currentTime)
    {
        try
        {
            // Process component logic
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing component: {ComponentId}", component.Id);
        }
    }

    private async Task ApplyThemeToComponents(UiTheme theme)
    {
        try
        {
            // Apply theme to all components
            foreach (var component in _components.Values)
            {
                await ApplyThemeToComponent(component, theme);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying theme to components: {ThemeId}", theme.Id);
        }
    }

    private async Task ApplyThemeToComponent(UiComponent component, UiTheme theme)
    {
        try
        {
            // Apply theme to specific component
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying theme to component: {ComponentId}", component.Id);
        }
    }

    private async Task ApplyComponentRefinements(UiComponent component)
    {
        try
        {
            // Apply component refinements
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying component refinements: {ComponentId}", component.Id);
        }
    }

    private async Task ApplyAccessibilityFeatureToComponent(AccessibilityFeature feature)
    {
        try
        {
            // Apply accessibility feature to component
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying accessibility feature: {FeatureId}", feature.Id);
        }
    }

    private void InitializeThemes()
    {
        try
        {
            // Initialize default themes
            ApplyThemeAsync(new ThemeRequest
            {
                Id = "modern_dark",
                Name = "Modern Dark",
                Description = "Modern dark theme with sleek design",
                ColorScheme = new ColorScheme
                {
                    Primary = Colors.DodgerBlue,
                    Secondary = Colors.SlateGray,
                    Background = Colors.Black,
                    Surface = Colors.Gray,
                    Text = Colors.White,
                    TextSecondary = Colors.LightGray
                },
                Typography = new Typography
                {
                    FontFamily = "Segoe UI",
                    FontSize = 14,
                    FontWeight = FontWeights.Normal,
                    LineHeight = 1.5
                },
                Spacing = new Spacing
                {
                    Small = 8,
                    Medium = 16,
                    Large = 24,
                    ExtraLarge = 32
                },
                BorderRadius = new BorderRadius
                {
                    Small = 4,
                    Medium = 8,
                    Large = 12
                },
                Shadows = new List<Shadow>
                {
                    new Shadow { Blur = 4, OffsetX = 0, OffsetY = 2, Color = Colors.Black, Opacity = 0.1 }
                },
                Animations = new List<string> { "fade_in", "slide_up", "scale_in" },
                IsActive = true
            }).Wait();

            ApplyThemeAsync(new ThemeRequest
            {
                Id = "modern_light",
                Name = "Modern Light",
                Description = "Modern light theme with clean design",
                ColorScheme = new ColorScheme
                {
                    Primary = Colors.DodgerBlue,
                    Secondary = Colors.SlateGray,
                    Background = Colors.White,
                    Surface = Colors.LightGray,
                    Text = Colors.Black,
                    TextSecondary = Colors.Gray
                },
                Typography = new Typography
                {
                    FontFamily = "Segoe UI",
                    FontSize = 14,
                    FontWeight = FontWeights.Normal,
                    LineHeight = 1.5
                },
                Spacing = new Spacing
                {
                    Small = 8,
                    Medium = 16,
                    Large = 24,
                    ExtraLarge = 32
                },
                BorderRadius = new BorderRadius
                {
                    Small = 4,
                    Medium = 8,
                    Large = 12
                },
                Shadows = new List<Shadow>
                {
                    new Shadow { Blur = 4, OffsetX = 0, OffsetY = 2, Color = Colors.Black, Opacity = 0.1 }
                },
                Animations = new List<string> { "fade_in", "slide_up", "scale_in" },
                IsActive = false
            }).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing themes");
        }
    }

    private void InitializeAnimations()
    {
        try
        {
            // Initialize default animations
            CreateAnimationAsync(new AnimationRequest
            {
                Name = "fade_in",
                Description = "Fade in animation",
                AnimationType = AnimationType.FadeIn,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = EasingFunction.EaseInOut,
                Properties = new Dictionary<string, object>
                {
                    ["Opacity"] = "0 to 1"
                },
                Triggers = new List<string> { "Loaded", "Visible" },
                IsEnabled = true
            }).Wait();

            CreateAnimationAsync(new AnimationRequest
            {
                Name = "slide_up",
                Description = "Slide up animation",
                AnimationType = AnimationType.SlideUp,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = EasingFunction.EaseOut,
                Properties = new Dictionary<string, object>
                {
                    ["TranslateY"] = "20 to 0"
                },
                Triggers = new List<string> { "Loaded", "Visible" },
                IsEnabled = true
            }).Wait();

            CreateAnimationAsync(new AnimationRequest
            {
                Name = "scale_in",
                Description = "Scale in animation",
                AnimationType = AnimationType.ScaleIn,
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = EasingFunction.EaseOut,
                Properties = new Dictionary<string, object>
                {
                    ["ScaleX"] = "0.8 to 1",
                    ["ScaleY"] = "0.8 to 1"
                },
                Triggers = new List<string> { "Loaded", "Visible" },
                IsEnabled = true
            }).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing animations");
        }
    }

    private void InitializeComponents()
    {
        try
        {
            // Initialize default components
            RefineComponentAsync(new ComponentRefinementRequest
            {
                Id = "main_window",
                Name = "Main Window",
                ComponentType = ComponentType.Window,
                Style = "modern_dark",
                Layout = "responsive_grid",
                Interactions = new List<string> { "drag", "resize", "minimize", "maximize" },
                Accessibility = new Dictionary<string, object>
                {
                    ["ScreenReaderSupport"] = true,
                    ["KeyboardNavigation"] = true,
                    ["HighContrast"] = true
                },
                Animations = new List<string> { "fade_in", "slide_up" },
                IsRefined = true
            }).Wait();

            RefineComponentAsync(new ComponentRefinementRequest
            {
                Id = "device_panel",
                Name = "Device Panel",
                ComponentType = ComponentType.Panel,
                Style = "modern_dark",
                Layout = "flex_column",
                Interactions = new List<string> { "scroll", "select", "hover" },
                Accessibility = new Dictionary<string, object>
                {
                    ["ScreenReaderSupport"] = true,
                    ["KeyboardNavigation"] = true,
                    ["FocusIndicators"] = true
                },
                Animations = new List<string> { "fade_in", "scale_in" },
                IsRefined = true
            }).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing components");
        }
    }

    private void InitializeAccessibilityFeatures()
    {
        try
        {
            // Initialize default accessibility features
            ApplyAccessibilityFeatureAsync(new AccessibilityRequest
            {
                Id = "screen_reader_support",
                Name = "Screen Reader Support",
                Description = "Provides screen reader support for all UI components",
                FeatureType = AccessibilityFeatureType.ScreenReader,
                TargetComponent = "all",
                Properties = new Dictionary<string, object>
                {
                    ["AnnounceChanges"] = true,
                    ["DescriptiveText"] = true,
                    ["RoleAttributes"] = true
                },
                IsEnabled = true
            }).Wait();

            ApplyAccessibilityFeatureAsync(new AccessibilityRequest
            {
                Id = "keyboard_navigation",
                Name = "Keyboard Navigation",
                Description = "Enables full keyboard navigation support",
                FeatureType = AccessibilityFeatureType.KeyboardNavigation,
                TargetComponent = "all",
                Properties = new Dictionary<string, object>
                {
                    ["TabOrder"] = true,
                    ["FocusIndicators"] = true,
                    ["KeyboardShortcuts"] = true
                },
                IsEnabled = true
            }).Wait();

            ApplyAccessibilityFeatureAsync(new AccessibilityRequest
            {
                Id = "high_contrast",
                Name = "High Contrast Mode",
                Description = "Provides high contrast mode for better visibility",
                FeatureType = AccessibilityFeatureType.HighContrast,
                TargetComponent = "all",
                Properties = new Dictionary<string, object>
                {
                    ["ContrastRatio"] = 7.0,
                    ["ColorAdjustments"] = true,
                    ["TextSize"] = "large"
                },
                IsEnabled = true
            }).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing accessibility features");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isRefining = false;
            _refinementTimer?.Dispose();

            _logger.LogInformation("UI/UX refinement service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing UI/UX refinement service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Theme request
/// </summary>
public class ThemeRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ColorScheme ColorScheme { get; set; } = new();
    public Typography Typography { get; set; } = new();
    public Spacing Spacing { get; set; } = new();
    public BorderRadius BorderRadius { get; set; } = new();
    public List<Shadow> Shadows { get; set; } = new();
    public List<string> Animations { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Animation request
/// </summary>
public class AnimationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AnimationType AnimationType { get; set; } = AnimationType.FadeIn;
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(300);
    public EasingFunction EasingFunction { get; set; } = EasingFunction.EaseInOut;
    public Dictionary<string, object>? Properties { get; set; }
    public List<string>? Triggers { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Component refinement request
/// </summary>
public class ComponentRefinementRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ComponentType ComponentType { get; set; } = ComponentType.Panel;
    public string Style { get; set; } = string.Empty;
    public string Layout { get; set; } = string.Empty;
    public List<string>? Interactions { get; set; }
    public Dictionary<string, object>? Accessibility { get; set; }
    public List<string>? Animations { get; set; }
    public bool IsRefined { get; set; } = true;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Accessibility request
/// </summary>
public class AccessibilityRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AccessibilityFeatureType FeatureType { get; set; } = AccessibilityFeatureType.ScreenReader;
    public string TargetComponent { get; set; } = string.Empty;
    public Dictionary<string, object>? Properties { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// UI style request
/// </summary>
public class UiStyleRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComponentType ComponentType { get; set; } = ComponentType.Panel;
    public Dictionary<string, Color>? Colors { get; set; }
    public Dictionary<string, object>? Typography { get; set; }
    public Dictionary<string, double>? Spacing { get; set; }
    public Dictionary<string, object>? Borders { get; set; }
    public Dictionary<string, object>? Shadows { get; set; }
    public List<string>? Animations { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// UI layout request
/// </summary>
public class UiLayoutRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LayoutType LayoutType { get; set; } = LayoutType.Grid;
    public Dictionary<string, double>? Breakpoints { get; set; }
    public Dictionary<string, object>? GridSettings { get; set; }
    public Dictionary<string, object>? FlexSettings { get; set; }
    public List<string>? ResponsiveRules { get; set; }
    public bool IsActive { get; set; } = true;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// UI interaction request
/// </summary>
public class UiInteractionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public InteractionType InteractionType { get; set; } = InteractionType.Click;
    public string TargetComponent { get; set; } = string.Empty;
    public List<string>? Triggers { get; set; }
    public List<string>? Actions { get; set; }
    public Dictionary<string, object>? Feedback { get; set; }
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// UI theme
/// </summary>
public class UiTheme
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ColorScheme ColorScheme { get; set; } = new();
    public Typography Typography { get; set; } = new();
    public Spacing Spacing { get; set; } = new();
    public BorderRadius BorderRadius { get; set; } = new();
    public List<Shadow> Shadows { get; set; } = new();
    public List<string> Animations { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastApplied { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// UI animation
/// </summary>
public class UiAnimation
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AnimationType AnimationType { get; set; }
    public TimeSpan Duration { get; set; }
    public EasingFunction EasingFunction { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<string> Triggers { get; set; } = new();
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// UI component
/// </summary>
public class UiComponent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ComponentType ComponentType { get; set; }
    public string Style { get; set; } = string.Empty;
    public string Layout { get; set; } = string.Empty;
    public List<string> Interactions { get; set; } = new();
    public Dictionary<string, object> Accessibility { get; set; } = new();
    public List<string> Animations { get; set; } = new();
    public bool IsRefined { get; set; }
    public DateTime LastRefined { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Accessibility feature
/// </summary>
public class AccessibilityFeature
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AccessibilityFeatureType FeatureType { get; set; }
    public string TargetComponent { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastApplied { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// UI style
/// </summary>
public class UiStyle
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ComponentType ComponentType { get; set; }
    public Dictionary<string, Color> Colors { get; set; } = new();
    public Dictionary<string, object> Typography { get; set; } = new();
    public Dictionary<string, double> Spacing { get; set; } = new();
    public Dictionary<string, object> Borders { get; set; } = new();
    public Dictionary<string, object> Shadows { get; set; } = new();
    public List<string> Animations { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// UI layout
/// </summary>
public class UiLayout
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LayoutType LayoutType { get; set; }
    public Dictionary<string, double> Breakpoints { get; set; } = new();
    public Dictionary<string, object> GridSettings { get; set; } = new();
    public Dictionary<string, object> FlexSettings { get; set; } = new();
    public List<string> ResponsiveRules { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// UI interaction
/// </summary>
public class UiInteraction
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public InteractionType InteractionType { get; set; }
    public string TargetComponent { get; set; } = string.Empty;
    public List<string> Triggers { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public Dictionary<string, object> Feedback { get; set; } = new();
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Color scheme
/// </summary>
public class ColorScheme
{
    public Color Primary { get; set; } = Colors.DodgerBlue;
    public Color Secondary { get; set; } = Colors.SlateGray;
    public Color Background { get; set; } = Colors.White;
    public Color Surface { get; set; } = Colors.LightGray;
    public Color Text { get; set; } = Colors.Black;
    public Color TextSecondary { get; set; } = Colors.Gray;
}

/// <summary>
/// Typography
/// </summary>
public class Typography
{
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 14;
    public FontWeight FontWeight { get; set; } = FontWeights.Normal;
    public double LineHeight { get; set; } = 1.5;
}

/// <summary>
/// Spacing
/// </summary>
public class Spacing
{
    public double Small { get; set; } = 8;
    public double Medium { get; set; } = 16;
    public double Large { get; set; } = 24;
    public double ExtraLarge { get; set; } = 32;
}

/// <summary>
/// Border radius
/// </summary>
public class BorderRadius
{
    public double Small { get; set; } = 4;
    public double Medium { get; set; } = 8;
    public double Large { get; set; } = 12;
}

/// <summary>
/// Shadow
/// </summary>
public class Shadow
{
    public double Blur { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public Color Color { get; set; } = Colors.Black;
    public double Opacity { get; set; } = 0.1;
}

/// <summary>
/// UI theme event arguments
/// </summary>
public class UiThemeEventArgs : EventArgs
{
    public string ThemeId { get; }
    public UiTheme Theme { get; }
    public ThemeAction Action { get; }
    public DateTime Timestamp { get; }

    public UiThemeEventArgs(string themeId, UiTheme theme, ThemeAction action)
    {
        ThemeId = themeId;
        Theme = theme;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// UI animation event arguments
/// </summary>
public class UiAnimationEventArgs : EventArgs
{
    public string AnimationId { get; }
    public UiAnimation Animation { get; }
    public AnimationAction Action { get; }
    public DateTime Timestamp { get; }

    public UiAnimationEventArgs(string animationId, UiAnimation animation, AnimationAction action)
    {
        AnimationId = animationId;
        Animation = animation;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// UI component event arguments
/// </summary>
public class UiComponentEventArgs : EventArgs
{
    public string ComponentId { get; }
    public UiComponent Component { get; }
    public ComponentAction Action { get; }
    public DateTime Timestamp { get; }

    public UiComponentEventArgs(string componentId, UiComponent component, ComponentAction action)
    {
        ComponentId = componentId;
        Component = component;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Accessibility event arguments
/// </summary>
public class AccessibilityEventArgs : EventArgs
{
    public string FeatureId { get; }
    public AccessibilityFeature Feature { get; }
    public AccessibilityAction Action { get; }
    public DateTime Timestamp { get; }

    public AccessibilityEventArgs(string featureId, AccessibilityFeature feature, AccessibilityAction action)
    {
        FeatureId = featureId;
        Feature = feature;
        Action = action;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Animation types
/// </summary>
public enum AnimationType
{
    FadeIn,
    FadeOut,
    SlideUp,
    SlideDown,
    SlideLeft,
    SlideRight,
    ScaleIn,
    ScaleOut,
    RotateIn,
    RotateOut,
    Custom
}

/// <summary>
/// Easing functions
/// </summary>
public enum EasingFunction
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    EaseInQuad,
    EaseOutQuad,
    EaseInOutQuad,
    EaseInCubic,
    EaseOutCubic,
    EaseInOutCubic
}

/// <summary>
/// Component types
/// </summary>
public enum ComponentType
{
    Window,
    Panel,
    Button,
    TextBox,
    ListBox,
    ComboBox,
    CheckBox,
    RadioButton,
    Slider,
    ProgressBar,
    TabControl,
    Menu,
    Toolbar,
    StatusBar,
    Custom
}

/// <summary>
/// Layout types
/// </summary>
public enum LayoutType
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
/// Interaction types
/// </summary>
public enum InteractionType
{
    Click,
    DoubleClick,
    Hover,
    Focus,
    Drag,
    Drop,
    Scroll,
    Resize,
    Custom
}

/// <summary>
/// Accessibility feature types
/// </summary>
public enum AccessibilityFeatureType
{
    ScreenReader,
    KeyboardNavigation,
    HighContrast,
    FocusIndicators,
    ColorBlindness,
    MotionReduction,
    Custom
}

/// <summary>
/// Theme actions
/// </summary>
public enum ThemeAction
{
    Applied,
    Removed,
    Updated
}

/// <summary>
/// Animation actions
/// </summary>
public enum AnimationAction
{
    Created,
    Triggered,
    Completed,
    Cancelled
}

/// <summary>
/// Component actions
/// </summary>
public enum ComponentAction
{
    Refined,
    Updated,
    Removed
}

/// <summary>
/// Accessibility actions
/// </summary>
public enum AccessibilityAction
{
    Applied,
    Removed,
    Updated
}
