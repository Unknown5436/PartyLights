using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PartyLights.Services;

/// <summary>
/// Real-time visualization service for advanced audio visualizations
/// </summary>
public class RealTimeVisualizationService : IDisposable
{
    private readonly ILogger<RealTimeVisualizationService> _logger;
    private readonly ConcurrentDictionary<string, VisualizationRenderer> _renderers = new();
    private readonly Timer _renderingTimer;
    private readonly object _lockObject = new();

    private const int RenderingIntervalMs = 16; // ~60 FPS
    private bool _isRendering;

    // Rendering state management
    private readonly Dictionary<string, RenderingState> _renderingStates = new();
    private readonly Dictionary<string, BitmapData> _bitmapCache = new();
    private readonly Dictionary<string, ColorPalette> _colorPalettes = new();

    public event EventHandler<VisualizationRenderedEventArgs>? VisualizationRendered;
    public event EventHandler<VisualizationErrorEventArgs>? VisualizationError;

    public RealTimeVisualizationService(ILogger<RealTimeVisualizationService> logger)
    {
        _logger = logger;

        _renderingTimer = new Timer(RenderVisualizations, null, RenderingIntervalMs, RenderingIntervalMs);
        _isRendering = true;

        InitializeColorPalettes();

        _logger.LogInformation("Real-time visualization service initialized");
    }

    /// <summary>
    /// Creates a visualization renderer
    /// </summary>
    public async Task<string> CreateRendererAsync(VisualizationRendererRequest request)
    {
        try
        {
            var rendererId = Guid.NewGuid().ToString();

            var renderer = new VisualizationRenderer
            {
                Id = rendererId,
                Type = request.Type,
                Width = request.Width,
                Height = request.Height,
                ColorScheme = request.ColorScheme,
                Settings = request.Settings,
                IsActive = true,
                CreatedTime = DateTime.UtcNow,
                LastRenderTime = DateTime.UtcNow
            };

            var renderingState = new RenderingState
            {
                RendererId = rendererId,
                Type = request.Type,
                Width = request.Width,
                Height = request.Height,
                ColorScheme = request.ColorScheme,
                Settings = request.Settings,
                IsActive = true,
                LastRenderTime = DateTime.UtcNow,
                FrameCount = 0,
                AudioData = new AudioAnalysis(),
                PreviousFrame = new BitmapData(),
                CurrentFrame = new BitmapData()
            };

            lock (_lockObject)
            {
                _renderers[rendererId] = renderer;
                _renderingStates[rendererId] = renderingState;
            }

            _logger.LogInformation("Created visualization renderer: {RendererId}", rendererId);
            return rendererId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating visualization renderer");
            return string.Empty;
        }
    }

    /// <summary>
    /// Updates audio data for a renderer
    /// </summary>
    public async Task<bool> UpdateAudioDataAsync(string rendererId, AudioAnalysis audioData)
    {
        try
        {
            if (!_renderingStates.TryGetValue(rendererId, out var state))
            {
                _logger.LogWarning("Renderer not found: {RendererId}", rendererId);
                return false;
            }

            state.AudioData = audioData;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating audio data: {RendererId}", rendererId);
            return false;
        }
    }

    /// <summary>
    /// Renders a visualization frame
    /// </summary>
    public async Task<BitmapSource?> RenderFrameAsync(string rendererId)
    {
        try
        {
            if (!_renderingStates.TryGetValue(rendererId, out var state))
            {
                _logger.LogWarning("Renderer not found: {RendererId}", rendererId);
                return null;
            }

            var bitmap = await RenderVisualizationFrame(state);
            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering frame: {RendererId}", rendererId);
            VisualizationError?.Invoke(this, new VisualizationErrorEventArgs(rendererId, ex.Message));
            return null;
        }
    }

    /// <summary>
    /// Destroys a visualization renderer
    /// </summary>
    public async Task<bool> DestroyRendererAsync(string rendererId)
    {
        try
        {
            if (!_renderers.TryRemove(rendererId, out var renderer))
            {
                _logger.LogWarning("Renderer not found: {RendererId}", rendererId);
                return false;
            }

            // Clean up rendering state
            _renderingStates.Remove(rendererId);
            _bitmapCache.Remove(rendererId);

            _logger.LogInformation("Destroyed visualization renderer: {RendererId}", rendererId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error destroying renderer: {RendererId}", rendererId);
            return false;
        }
    }

    /// <summary>
    /// Gets renderer state
    /// </summary>
    public RenderingState? GetRendererState(string rendererId)
    {
        _renderingStates.TryGetValue(rendererId, out var state);
        return state;
    }

    /// <summary>
    /// Gets all active renderers
    /// </summary>
    public IEnumerable<VisualizationRenderer> GetActiveRenderers()
    {
        return _renderers.Values.Where(r => r.IsActive);
    }

    #region Private Methods

    private async void RenderVisualizations(object? state)
    {
        if (!_isRendering)
        {
            return;
        }

        try
        {
            var currentTime = DateTime.UtcNow;

            foreach (var rendererEntry in _renderers)
            {
                var rendererId = rendererEntry.Key;
                var renderer = rendererEntry.Value;

                if (renderer.IsActive)
                {
                    try
                    {
                        await RenderVisualization(rendererId, renderer, currentTime);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error rendering visualization: {RendererId}", rendererId);
                        VisualizationError?.Invoke(this, new VisualizationErrorEventArgs(rendererId, ex.Message));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in visualization rendering loop");
        }
    }

    private async Task RenderVisualization(string rendererId, VisualizationRenderer renderer, DateTime currentTime)
    {
        try
        {
            if (!_renderingStates.TryGetValue(rendererId, out var state))
            {
                return;
            }

            // Render based on type
            BitmapSource? bitmap = null;
            switch (state.Type)
            {
                case VisualizationType.SpectrumAnalyzer:
                    bitmap = await RenderSpectrumAnalyzer(state);
                    break;
                case VisualizationType.Waveform:
                    bitmap = await RenderWaveform(state);
                    break;
                case VisualizationType.FrequencyBands:
                    bitmap = await RenderFrequencyBands(state);
                    break;
                case VisualizationType.BeatVisualizer:
                    bitmap = await RenderBeatVisualizer(state);
                    break;
                case VisualizationType.MoodVisualizer:
                    bitmap = await RenderMoodVisualizer(state);
                    break;
                case VisualizationType.VolumeMeter:
                    bitmap = await RenderVolumeMeter(state);
                    break;
            }

            if (bitmap != null)
            {
                state.FrameCount++;
                state.LastRenderTime = currentTime;

                VisualizationRendered?.Invoke(this, new VisualizationRenderedEventArgs(rendererId, bitmap));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering visualization: {RendererId}", rendererId);
        }
    }

    private async Task<BitmapSource?> RenderVisualizationFrame(RenderingState state)
    {
        try
        {
            BitmapSource? bitmap = null;
            switch (state.Type)
            {
                case VisualizationType.SpectrumAnalyzer:
                    bitmap = await RenderSpectrumAnalyzer(state);
                    break;
                case VisualizationType.Waveform:
                    bitmap = await RenderWaveform(state);
                    break;
                case VisualizationType.FrequencyBands:
                    bitmap = await RenderFrequencyBands(state);
                    break;
                case VisualizationType.BeatVisualizer:
                    bitmap = await RenderBeatVisualizer(state);
                    break;
                case VisualizationType.MoodVisualizer:
                    bitmap = await RenderMoodVisualizer(state);
                    break;
                case VisualizationType.VolumeMeter:
                    bitmap = await RenderVolumeMeter(state);
                    break;
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering visualization frame");
            return null;
        }
    }

    private async Task<BitmapSource> RenderSpectrumAnalyzer(RenderingState state)
    {
        try
        {
            var width = state.Width;
            var height = state.Height;
            var audioData = state.AudioData;

            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.Lock();

            try
            {
                var pixels = new byte[width * height * 4];
                var colorPalette = GetColorPalette(state.ColorScheme);

                // Clear background
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = 0;     // B
                    pixels[i + 1] = 0; // G
                    pixels[i + 2] = 0; // R
                    pixels[i + 3] = 255; // A
                }

                // Render spectrum bars
                if (audioData.FrequencyBandsDetailed != null)
                {
                    var barWidth = width / audioData.FrequencyBandsDetailed.Count;

                    for (int i = 0; i < audioData.FrequencyBandsDetailed.Count; i++)
                    {
                        var band = audioData.FrequencyBandsDetailed[i];
                        var barHeight = (int)(band.Intensity * height);
                        var color = GetFrequencyColor(band.FrequencyLow, band.FrequencyHigh, band.Intensity, colorPalette);

                        var startX = i * barWidth;
                        var startY = height - barHeight;

                        for (int x = startX; x < startX + barWidth && x < width; x++)
                        {
                            for (int y = startY; y < height; y++)
                            {
                                var pixelIndex = (y * width + x) * 4;
                                if (pixelIndex < pixels.Length - 3)
                                {
                                    pixels[pixelIndex] = color.B;     // B
                                    pixels[pixelIndex + 1] = color.G; // G
                                    pixels[pixelIndex + 2] = color.R; // R
                                    pixels[pixelIndex + 3] = 255;     // A
                                }
                            }
                        }
                    }
                }

                // Copy pixels to bitmap
                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            }
            finally
            {
                bitmap.Unlock();
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering spectrum analyzer");
            return CreateEmptyBitmap(state.Width, state.Height);
        }
    }

    private async Task<BitmapSource> RenderWaveform(RenderingState state)
    {
        try
        {
            var width = state.Width;
            var height = state.Height;
            var audioData = state.AudioData;

            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.Lock();

            try
            {
                var pixels = new byte[width * height * 4];
                var colorPalette = GetColorPalette(state.ColorScheme);

                // Clear background
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = 0;     // B
                    pixels[i + 1] = 0; // G
                    pixels[i + 2] = 0; // R
                    pixels[i + 3] = 255; // A
                }

                // Render waveform
                var centerY = height / 2;
                var amplitude = audioData.Volume * height / 2;

                for (int x = 0; x < width; x++)
                {
                    var phase = (float)x / width * Math.PI * 2;
                    var y = centerY + (float)Math.Sin(phase) * amplitude;
                    var color = GetAmplitudeColor(audioData.Volume, colorPalette);

                    // Draw waveform line
                    for (int yOffset = -2; yOffset <= 2; yOffset++)
                    {
                        var pixelY = (int)y + yOffset;
                        if (pixelY >= 0 && pixelY < height)
                        {
                            var pixelIndex = (pixelY * width + x) * 4;
                            if (pixelIndex < pixels.Length - 3)
                            {
                                pixels[pixelIndex] = color.B;     // B
                                pixels[pixelIndex + 1] = color.G; // G
                                pixels[pixelIndex + 2] = color.R; // R
                                pixels[pixelIndex + 3] = 255;     // A
                            }
                        }
                    }
                }

                // Copy pixels to bitmap
                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            }
            finally
            {
                bitmap.Unlock();
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering waveform");
            return CreateEmptyBitmap(state.Width, state.Height);
        }
    }

    private async Task<BitmapSource> RenderFrequencyBands(RenderingState state)
    {
        try
        {
            var width = state.Width;
            var height = state.Height;
            var audioData = state.AudioData;

            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.Lock();

            try
            {
                var pixels = new byte[width * height * 4];
                var colorPalette = GetColorPalette(state.ColorScheme);

                // Clear background
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = 0;     // B
                    pixels[i + 1] = 0; // G
                    pixels[i + 2] = 0; // R
                    pixels[i + 3] = 255; // A
                }

                // Render frequency bands
                if (audioData.FrequencyBandsDetailed != null)
                {
                    var bandHeight = height / audioData.FrequencyBandsDetailed.Count;

                    for (int i = 0; i < audioData.FrequencyBandsDetailed.Count; i++)
                    {
                        var band = audioData.FrequencyBandsDetailed[i];
                        var bandWidth = (int)(band.Intensity * width);
                        var color = GetFrequencyColor(band.FrequencyLow, band.FrequencyHigh, band.Intensity, colorPalette);

                        var startY = i * bandHeight;
                        var startX = 0;

                        for (int x = startX; x < startX + bandWidth && x < width; x++)
                        {
                            for (int y = startY; y < startY + bandHeight && y < height; y++)
                            {
                                var pixelIndex = (y * width + x) * 4;
                                if (pixelIndex < pixels.Length - 3)
                                {
                                    pixels[pixelIndex] = color.B;     // B
                                    pixels[pixelIndex + 1] = color.G; // G
                                    pixels[pixelIndex + 2] = color.R; // R
                                    pixels[pixelIndex + 3] = 255;     // A
                                }
                            }
                        }
                    }
                }

                // Copy pixels to bitmap
                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            }
            finally
            {
                bitmap.Unlock();
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering frequency bands");
            return CreateEmptyBitmap(state.Width, state.Height);
        }
    }

    private async Task<BitmapSource> RenderBeatVisualizer(RenderingState state)
    {
        try
        {
            var width = state.Width;
            var height = state.Height;
            var audioData = state.AudioData;

            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.Lock();

            try
            {
                var pixels = new byte[width * height * 4];
                var colorPalette = GetColorPalette(state.ColorScheme);

                // Clear background
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = 0;     // B
                    pixels[i + 1] = 0; // G
                    pixels[i + 2] = 0; // R
                    pixels[i + 3] = 255; // A
                }

                // Render beat visualization
                if (audioData.IsBeatDetected)
                {
                    var centerX = width / 2;
                    var centerY = height / 2;
                    var radius = (int)(audioData.BeatStrength * Math.Min(width, height) / 2);
                    var color = GetBeatColor(audioData.BeatStrength, colorPalette);

                    // Draw beat circle
                    for (int x = 0; x < width; x++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            var distance = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
                            if (distance <= radius)
                            {
                                var pixelIndex = (y * width + x) * 4;
                                if (pixelIndex < pixels.Length - 3)
                                {
                                    pixels[pixelIndex] = color.B;     // B
                                    pixels[pixelIndex + 1] = color.G; // G
                                    pixels[pixelIndex + 2] = color.R; // R
                                    pixels[pixelIndex + 3] = 255;     // A
                                }
                            }
                        }
                    }
                }

                // Copy pixels to bitmap
                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            }
            finally
            {
                bitmap.Unlock();
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering beat visualizer");
            return CreateEmptyBitmap(state.Width, state.Height);
        }
    }

    private async Task<BitmapSource> RenderMoodVisualizer(RenderingState state)
    {
        try
        {
            var width = state.Width;
            var height = state.Height;
            var audioData = state.AudioData;

            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.Lock();

            try
            {
                var pixels = new byte[width * height * 4];
                var colorPalette = GetColorPalette(state.ColorScheme);

                // Clear background
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = 0;     // B
                    pixels[i + 1] = 0; // G
                    pixels[i + 2] = 0; // R
                    pixels[i + 3] = 255; // A
                }

                // Render mood visualization
                var color = GetMoodColor(audioData.Energy, audioData.Valence, colorPalette);
                var intensity = Math.Max(audioData.Energy, audioData.Arousal);

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var pixelIndex = (y * width + x) * 4;
                        if (pixelIndex < pixels.Length - 3)
                        {
                            pixels[pixelIndex] = (byte)(color.B * intensity);     // B
                            pixels[pixelIndex + 1] = (byte)(color.G * intensity); // G
                            pixels[pixelIndex + 2] = (byte)(color.R * intensity); // R
                            pixels[pixelIndex + 3] = 255;                         // A
                        }
                    }
                }

                // Copy pixels to bitmap
                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            }
            finally
            {
                bitmap.Unlock();
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering mood visualizer");
            return CreateEmptyBitmap(state.Width, state.Height);
        }
    }

    private async Task<BitmapSource> RenderVolumeMeter(RenderingState state)
    {
        try
        {
            var width = state.Width;
            var height = state.Height;
            var audioData = state.AudioData;

            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            bitmap.Lock();

            try
            {
                var pixels = new byte[width * height * 4];
                var colorPalette = GetColorPalette(state.ColorScheme);

                // Clear background
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = 0;     // B
                    pixels[i + 1] = 0; // G
                    pixels[i + 2] = 0; // R
                    pixels[i + 3] = 255; // A
                }

                // Render volume meter
                var volumeWidth = (int)(audioData.Volume * width);
                var peakWidth = (int)(audioData.PeakLevel * width);
                var volumeColor = GetVolumeColor(audioData.Volume, colorPalette);
                var peakColor = GetPeakColor(audioData.PeakLevel, colorPalette);

                // Draw volume bar
                for (int x = 0; x < volumeWidth && x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var pixelIndex = (y * width + x) * 4;
                        if (pixelIndex < pixels.Length - 3)
                        {
                            pixels[pixelIndex] = volumeColor.B;     // B
                            pixels[pixelIndex + 1] = volumeColor.G; // G
                            pixels[pixelIndex + 2] = volumeColor.R; // R
                            pixels[pixelIndex + 3] = 255;           // A
                        }
                    }
                }

                // Draw peak indicator
                if (peakWidth > volumeWidth)
                {
                    for (int x = volumeWidth; x < peakWidth && x < width; x++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            var pixelIndex = (y * width + x) * 4;
                            if (pixelIndex < pixels.Length - 3)
                            {
                                pixels[pixelIndex] = peakColor.B;     // B
                                pixels[pixelIndex + 1] = peakColor.G; // G
                                pixels[pixelIndex + 2] = peakColor.R; // R
                                pixels[pixelIndex + 3] = 255;         // A
                            }
                        }
                    }
                }

                // Copy pixels to bitmap
                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            }
            finally
            {
                bitmap.Unlock();
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering volume meter");
            return CreateEmptyBitmap(state.Width, state.Height);
        }
    }

    private BitmapSource CreateEmptyBitmap(int width, int height)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.Lock();

        try
        {
            var pixels = new byte[width * height * 4];
            bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        }
        finally
        {
            bitmap.Unlock();
        }

        return bitmap;
    }

    private ColorPalette GetColorPalette(ColorScheme colorScheme)
    {
        _colorPalettes.TryGetValue(colorScheme.ToString(), out var palette);
        return palette ?? _colorPalettes["Rainbow"];
    }

    private Color GetFrequencyColor(float freqLow, float freqHigh, float intensity, ColorPalette palette)
    {
        var centerFreq = (freqLow + freqHigh) / 2;
        var hue = (centerFreq / 20000f) * 360f;
        var saturation = Math.Min(intensity * 2f, 1f);
        var value = Math.Min(intensity * 1.5f, 1f);

        return ColorFromHSV(hue, saturation, value);
    }

    private Color GetAmplitudeColor(float amplitude, ColorPalette palette)
    {
        var intensity = Math.Abs(amplitude);
        var hue = intensity * 120f;
        var saturation = 1f;
        var value = Math.Min(intensity * 2f, 1f);

        return ColorFromHSV(hue, saturation, value);
    }

    private Color GetBeatColor(float beatStrength, ColorPalette palette)
    {
        var hue = beatStrength * 240f;
        var saturation = 1f;
        var value = Math.Min(beatStrength * 1.5f, 1f);

        return ColorFromHSV(hue, saturation, value);
    }

    private Color GetMoodColor(float energy, float valence, ColorPalette palette)
    {
        var hue = valence * 120f;
        var saturation = energy;
        var value = Math.Max(energy, valence);

        return ColorFromHSV(hue, saturation, value);
    }

    private Color GetVolumeColor(float volume, ColorPalette palette)
    {
        var hue = volume * 120f;
        var saturation = 1f;
        var value = Math.Min(volume * 1.5f, 1f);

        return ColorFromHSV(hue, saturation, value);
    }

    private Color GetPeakColor(float peakLevel, ColorPalette palette)
    {
        var hue = peakLevel > 0.9f ? 0f : peakLevel > 0.7f ? 60f : 120f;
        var saturation = 1f;
        var value = Math.Min(peakLevel * 1.2f, 1f);

        return ColorFromHSV(hue, saturation, value);
    }

    private Color ColorFromHSV(float hue, float saturation, float value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs((hue / 60f) % 2 - 1));
        var m = value - c;

        float r, g, b;
        if (hue < 60)
        {
            r = c; g = x; b = 0;
        }
        else if (hue < 120)
        {
            r = x; g = c; b = 0;
        }
        else if (hue < 180)
        {
            r = 0; g = c; b = x;
        }
        else if (hue < 240)
        {
            r = 0; g = x; b = c;
        }
        else if (hue < 300)
        {
            r = x; g = 0; b = c;
        }
        else
        {
            r = c; g = 0; b = x;
        }

        return Color.FromRgb(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255)
        );
    }

    private void InitializeColorPalettes()
    {
        // Rainbow palette
        _colorPalettes["Rainbow"] = new ColorPalette
        {
            Name = "Rainbow",
            Colors = new List<Color>
            {
                Colors.Red,
                Colors.Orange,
                Colors.Yellow,
                Colors.Green,
                Colors.Cyan,
                Colors.Blue,
                Colors.Purple
            }
        };

        // Fire palette
        _colorPalettes["Fire"] = new ColorPalette
        {
            Name = "Fire",
            Colors = new List<Color>
            {
                Colors.Black,
                Colors.DarkRed,
                Colors.Red,
                Colors.Orange,
                Colors.Yellow,
                Colors.White
            }
        };

        // Ocean palette
        _colorPalettes["Ocean"] = new ColorPalette
        {
            Name = "Ocean",
            Colors = new List<Color>
            {
                Colors.DarkBlue,
                Colors.Blue,
                Colors.Cyan,
                Colors.LightBlue,
                Colors.White
            }
        };

        // Monochrome palette
        _colorPalettes["Monochrome"] = new ColorPalette
        {
            Name = "Monochrome",
            Colors = new List<Color>
            {
                Colors.Black,
                Colors.Gray,
                Colors.White
            }
        };
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            _isRendering = false;
            _renderingTimer?.Dispose();

            // Destroy all renderers
            var rendererIds = _renderers.Keys.ToList();
            foreach (var rendererId in rendererIds)
            {
                DestroyRendererAsync(rendererId).Wait(1000);
            }

            _logger.LogInformation("Real-time visualization service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing real-time visualization service");
        }
    }

    #endregion
}

#region Data Models

/// <summary>
/// Visualization renderer request
/// </summary>
public class VisualizationRendererRequest
{
    public VisualizationType Type { get; set; } = VisualizationType.SpectrumAnalyzer;
    public int Width { get; set; } = 400;
    public int Height { get; set; } = 200;
    public ColorScheme ColorScheme { get; set; } = ColorScheme.Rainbow;
    public Dictionary<string, object> Settings { get; set; } = new();
}

/// <summary>
/// Visualization renderer
/// </summary>
public class VisualizationRenderer
{
    public string Id { get; set; } = string.Empty;
    public VisualizationType Type { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public ColorScheme ColorScheme { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime LastRenderTime { get; set; }
}

/// <summary>
/// Rendering state
/// </summary>
public class RenderingState
{
    public string RendererId { get; set; } = string.Empty;
    public VisualizationType Type { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public ColorScheme ColorScheme { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime LastRenderTime { get; set; }
    public int FrameCount { get; set; }
    public AudioAnalysis AudioData { get; set; } = new();
    public BitmapData PreviousFrame { get; set; } = new();
    public BitmapData CurrentFrame { get; set; } = new();
}

/// <summary>
/// Bitmap data
/// </summary>
public class BitmapData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] Pixels { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Color palette
/// </summary>
public class ColorPalette
{
    public string Name { get; set; } = string.Empty;
    public List<Color> Colors { get; set; } = new();
}

/// <summary>
/// Visualization rendered event arguments
/// </summary>
public class VisualizationRenderedEventArgs : EventArgs
{
    public string RendererId { get; }
    public BitmapSource Bitmap { get; }
    public DateTime Timestamp { get; }

    public VisualizationRenderedEventArgs(string rendererId, BitmapSource bitmap)
    {
        RendererId = rendererId;
        Bitmap = bitmap;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Visualization error event arguments
/// </summary>
public class VisualizationErrorEventArgs : EventArgs
{
    public string RendererId { get; }
    public string ErrorMessage { get; }
    public DateTime Timestamp { get; }

    public VisualizationErrorEventArgs(string rendererId, string errorMessage)
    {
        RendererId = rendererId;
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }
}
