using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;

namespace PartyLights.Services;

/// <summary>
/// Spotify audio analysis service for track analysis and mood detection
/// </summary>
public class SpotifyAudioAnalysisService : ISpotifyAudioAnalysisService
{
    private readonly ILogger<SpotifyAudioAnalysisService> _logger;
    private readonly ISpotifyWebApiService _spotifyApiService;
    private readonly Dictionary<string, SpotifyAudioFeatures> _audioFeaturesCache = new();
    private readonly Dictionary<string, Dictionary<string, float>> _moodCache = new();

    public SpotifyAudioAnalysisService(
        ILogger<SpotifyAudioAnalysisService> logger,
        ISpotifyWebApiService spotifyApiService)
    {
        _logger = logger;
        _spotifyApiService = spotifyApiService;
    }

    /// <summary>
    /// Analyzes a track and returns audio features
    /// </summary>
    public async Task<SpotifyAudioFeatures?> AnalyzeTrackAsync(string trackId)
    {
        try
        {
            // Check cache first
            if (_audioFeaturesCache.TryGetValue(trackId, out var cachedFeatures))
            {
                return cachedFeatures;
            }

            var features = await _spotifyApiService.GetTrackAudioFeaturesAsync(trackId);
            if (features != null)
            {
                _audioFeaturesCache[trackId] = features;
            }

            return features;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing track: {TrackId}", trackId);
            return null;
        }
    }

    /// <summary>
    /// Gets track mood based on audio features
    /// </summary>
    public async Task<Dictionary<string, float>> GetTrackMoodAsync(string trackId)
    {
        try
        {
            // Check cache first
            if (_moodCache.TryGetValue(trackId, out var cachedMood))
            {
                return cachedMood;
            }

            var features = await AnalyzeTrackAsync(trackId);
            if (features == null)
            {
                return new Dictionary<string, float>();
            }

            var mood = CalculateMoodFromFeatures(features);
            _moodCache[trackId] = mood;

            return mood;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting track mood: {TrackId}", trackId);
            return new Dictionary<string, float>();
        }
    }

    /// <summary>
    /// Gets track genre based on audio features and metadata
    /// </summary>
    public async Task<string> GetTrackGenreAsync(string trackId)
    {
        try
        {
            var track = await _spotifyApiService.GetTrackAsync(trackId);
            if (track?.Genres?.Any() == true)
            {
                return track.Genres.First();
            }

            // Fallback to genre detection based on audio features
            var features = await AnalyzeTrackAsync(trackId);
            if (features != null)
            {
                return DetectGenreFromFeatures(features);
            }

            return "Unknown";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting track genre: {TrackId}", trackId);
            return "Unknown";
        }
    }

    /// <summary>
    /// Gets track energy level (0-1)
    /// </summary>
    public async Task<float> GetTrackEnergyLevelAsync(string trackId)
    {
        try
        {
            var features = await AnalyzeTrackAsync(trackId);
            return features?.Energy ?? 0.5f;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting track energy level: {TrackId}", trackId);
            return 0.5f;
        }
    }

    /// <summary>
    /// Gets track danceability (0-1)
    /// </summary>
    public async Task<float> GetTrackDanceabilityAsync(string trackId)
    {
        try
        {
            var features = await AnalyzeTrackAsync(trackId);
            return features?.Danceability ?? 0.5f;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting track danceability: {TrackId}", trackId);
            return 0.5f;
        }
    }

    /// <summary>
    /// Determines if a track is suitable for lighting effects
    /// </summary>
    public async Task<bool> IsTrackSuitableForLightingAsync(string trackId)
    {
        try
        {
            var features = await AnalyzeTrackAsync(trackId);
            if (features == null)
            {
                return false;
            }

            // Criteria for lighting suitability:
            // - Energy > 0.3 (not too slow)
            // - Danceability > 0.2 (some rhythm)
            // - Not too instrumental (speechiness < 0.8)
            // - Not too acoustic (acousticness < 0.9)

            return features.Energy > 0.3f &&
                   features.Danceability > 0.2f &&
                   features.Speechiness < 0.8f &&
                   features.Acousticness < 0.9f;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking track suitability: {TrackId}", trackId);
            return false;
        }
    }

    /// <summary>
    /// Gets recommended presets based on track analysis
    /// </summary>
    public async Task<IEnumerable<string>> GetRecommendedPresetsAsync(string trackId)
    {
        try
        {
            var features = await AnalyzeTrackAsync(trackId);
            if (features == null)
            {
                return new[] { "Static", "VolumeReactive" };
            }

            var recommendations = new List<string>();

            // High energy tracks
            if (features.Energy > 0.7f)
            {
                recommendations.AddRange(new[] { "PartyMode", "BeatSync", "Strobe" });
            }

            // High danceability tracks
            if (features.Danceability > 0.7f)
            {
                recommendations.AddRange(new[] { "BeatSync", "Disco", "Rainbow" });
            }

            // High valence (positive mood) tracks
            if (features.Valence > 0.7f)
            {
                recommendations.AddRange(new[] { "Rainbow", "Fire", "Ambient" });
            }

            // Low valence (negative mood) tracks
            if (features.Valence < 0.3f)
            {
                recommendations.AddRange(new[] { "MoodLighting", "Ambient", "Water" });
            }

            // High tempo tracks
            if (features.Tempo > 120)
            {
                recommendations.AddRange(new[] { "BeatSync", "PartyMode", "Strobe" });
            }

            // Low tempo tracks
            if (features.Tempo < 80)
            {
                recommendations.AddRange(new[] { "Ambient", "MoodLighting", "Water" });
            }

            // Always include volume reactive as fallback
            recommendations.Add("VolumeReactive");

            // Remove duplicates and return
            return recommendations.Distinct();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommended presets: {TrackId}", trackId);
            return new[] { "VolumeReactive", "Static" };
        }
    }

    #region Private Methods

    /// <summary>
    /// Calculates mood from audio features
    /// </summary>
    private Dictionary<string, float> CalculateMoodFromFeatures(SpotifyAudioFeatures features)
    {
        var mood = new Dictionary<string, float>();

        // Energy level (0-1)
        mood["Energy"] = features.Energy;

        // Valence (positivity/negativity) (0-1)
        mood["Valence"] = features.Valence;

        // Danceability (0-1)
        mood["Danceability"] = features.Danceability;

        // Arousal (combination of energy and valence)
        mood["Arousal"] = (features.Energy + features.Valence) / 2;

        // Calmness (inverse of arousal)
        mood["Calmness"] = 1 - mood["Arousal"];

        // Intensity (combination of energy and loudness)
        var normalizedLoudness = Math.Max(0, (features.Loudness + 60) / 60); // Normalize loudness (-60 to 0 dB)
        mood["Intensity"] = (features.Energy + normalizedLoudness) / 2;

        // Happiness (combination of valence and energy)
        mood["Happiness"] = (features.Valence + features.Energy) / 2;

        // Sadness (inverse of happiness)
        mood["Sadness"] = 1 - mood["Happiness"];

        // Aggressiveness (high energy, low valence)
        mood["Aggressiveness"] = features.Energy * (1 - features.Valence);

        // Relaxation (low energy, high valence)
        mood["Relaxation"] = (1 - features.Energy) * features.Valence;

        return mood;
    }

    /// <summary>
    /// Detects genre based on audio features
    /// </summary>
    private string DetectGenreFromFeatures(SpotifyAudioFeatures features)
    {
        // Simple genre detection based on audio features
        // This is a basic implementation - in practice, you'd use more sophisticated ML models

        if (features.Energy > 0.8f && features.Danceability > 0.7f)
        {
            return "Electronic/Dance";
        }

        if (features.Acousticness > 0.7f)
        {
            return "Acoustic/Folk";
        }

        if (features.Instrumentalness > 0.7f)
        {
            return "Instrumental";
        }

        if (features.Speechiness > 0.5f)
        {
            return "Hip-Hop/Rap";
        }

        if (features.Tempo > 140)
        {
            return "Electronic";
        }

        if (features.Tempo < 80)
        {
            return "Ballad/Slow";
        }

        if (features.Valence > 0.7f && features.Energy > 0.6f)
        {
            return "Pop";
        }

        if (features.Valence < 0.3f)
        {
            return "Alternative/Indie";
        }

        return "Rock";
    }

    #endregion
}
