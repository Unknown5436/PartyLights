using PartyLights.Core.Models;

namespace PartyLights.Audio;

/// <summary>
/// Configuration for audio analysis pipeline
/// </summary>
public class AudioAnalysisConfig
{
    public int FftSize { get; set; } = 1024;
    public int SampleRate { get; set; } = 44100;
    public int BeatHistorySize { get; set; } = 43; // ~1 second at 20fps
    public int TempoHistorySize { get; set; } = 20;
    public float BeatSensitivity { get; set; } = 1.5f;
    public double MinBeatInterval { get; set; } = 0.2; // 200ms minimum between beats
    public int FrequencyBands { get; set; } = 12;
    public bool EnableSpectralAnalysis { get; set; } = true;
    public bool EnableRhythmAnalysis { get; set; } = true;
    public bool EnableMoodAnalysis { get; set; } = true;
}

/// <summary>
/// Represents a frame of audio data
/// </summary>
public class AudioFrame
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public float[] Samples { get; set; } = Array.Empty<float>();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int SampleRate { get; set; } = 44100;
    public int Channels { get; set; } = 2;
}

/// <summary>
/// Comprehensive audio analysis result
/// </summary>
public class AudioAnalysisResult
{
    public string FrameId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Basic audio features
    public float Volume { get; set; }
    public float[] FrequencyBands { get; set; } = Array.Empty<float>();
    public float SpectralCentroid { get; set; }
    public float SpectralRolloff { get; set; }
    public float SpectralFlux { get; set; }
    public float ZeroCrossingRate { get; set; }

    // Beat and rhythm features
    public bool BeatDetected { get; set; }
    public float BeatIntensity { get; set; }
    public float Tempo { get; set; }
    public float RhythmRegularity { get; set; }
    public float RhythmComplexity { get; set; }

    // Spectral features
    public float SpectralBandwidth { get; set; }
    public float SpectralContrast { get; set; }
    public float SpectralFlatness { get; set; }

    // Mood features
    public float Energy { get; set; }
    public float Valence { get; set; }
    public float Arousal { get; set; }

    // Derived features
    public AudioMood Mood { get; set; } = AudioMood.Neutral;
    public AudioGenre Genre { get; set; } = AudioGenre.Unknown;
    public float Intensity { get; set; }
    public float Complexity { get; set; }

    /// <summary>
    /// Combines this result with another analysis result
    /// </summary>
    public void Combine(AudioAnalysisResult other)
    {
        // Update values from other result (non-zero values take precedence)
        if (other.Volume > 0) Volume = other.Volume;
        if (other.FrequencyBands.Length > 0) FrequencyBands = other.FrequencyBands;
        if (other.SpectralCentroid > 0) SpectralCentroid = other.SpectralCentroid;
        if (other.SpectralRolloff > 0) SpectralRolloff = other.SpectralRolloff;
        if (other.SpectralFlux > 0) SpectralFlux = other.SpectralFlux;
        if (other.ZeroCrossingRate > 0) ZeroCrossingRate = other.ZeroCrossingRate;

        if (other.BeatDetected) BeatDetected = other.BeatDetected;
        if (other.BeatIntensity > 0) BeatIntensity = other.BeatIntensity;
        if (other.Tempo > 0) Tempo = other.Tempo;
        if (other.RhythmRegularity > 0) RhythmRegularity = other.RhythmRegularity;
        if (other.RhythmComplexity > 0) RhythmComplexity = other.RhythmComplexity;

        if (other.SpectralBandwidth > 0) SpectralBandwidth = other.SpectralBandwidth;
        if (other.SpectralContrast > 0) SpectralContrast = other.SpectralContrast;
        if (other.SpectralFlatness > 0) SpectralFlatness = other.SpectralFlatness;

        if (other.Energy > 0) Energy = other.Energy;
        if (other.Valence > 0) Valence = other.Valence;
        if (other.Arousal > 0) Arousal = other.Arousal;

        // Calculate derived features
        CalculateDerivedFeatures();
    }

    private void CalculateDerivedFeatures()
    {
        // Calculate intensity based on volume and energy
        Intensity = Math.Min(Volume * 100 + Energy * 50, 1.0f);

        // Calculate complexity based on spectral features
        Complexity = (SpectralBandwidth + SpectralContrast + RhythmComplexity) / 3.0f;

        // Determine mood based on valence and arousal
        Mood = DetermineMood(Valence, Arousal);

        // Determine genre based on tempo and spectral features
        Genre = DetermineGenre(Tempo, SpectralCentroid, RhythmRegularity);
    }

    private AudioMood DetermineMood(float valence, float arousal)
    {
        if (arousal > 0.7f)
        {
            return valence > 0.5f ? AudioMood.Excited : AudioMood.Angry;
        }
        else if (arousal < 0.3f)
        {
            return valence > 0.5f ? AudioMood.Calm : AudioMood.Sad;
        }
        else
        {
            return valence > 0.5f ? AudioMood.Happy : AudioMood.Neutral;
        }
    }

    private AudioGenre DetermineGenre(float tempo, float spectralCentroid, float rhythmRegularity)
    {
        if (tempo > 140 && rhythmRegularity > 0.7f)
        {
            return AudioGenre.Electronic;
        }
        else if (tempo > 120 && spectralCentroid > 0.6f)
        {
            return AudioGenre.Rock;
        }
        else if (tempo < 100 && rhythmRegularity < 0.5f)
        {
            return AudioGenre.Jazz;
        }
        else if (tempo > 100 && tempo < 130 && spectralCentroid < 0.4f)
        {
            return AudioGenre.Pop;
        }
        else
        {
            return AudioGenre.Unknown;
        }
    }

    /// <summary>
    /// Gets a summary of the analysis result
    /// </summary>
    public string GetSummary()
    {
        return $"Volume: {Volume:F2}, Tempo: {Tempo:F1} BPM, Mood: {Mood}, Genre: {Genre}, Intensity: {Intensity:F2}";
    }
}

/// <summary>
/// Audio mood classification
/// </summary>
public enum AudioMood
{
    Neutral,
    Happy,
    Sad,
    Excited,
    Calm,
    Angry
}

/// <summary>
/// Audio genre classification
/// </summary>
public enum AudioGenre
{
    Unknown,
    Electronic,
    Rock,
    Jazz,
    Pop,
    Classical,
    HipHop,
    Country
}

/// <summary>
/// Event args for audio analysis completion
/// </summary>
public class AudioAnalysisResultEventArgs : EventArgs
{
    public AudioAnalysisResult Result { get; }

    public AudioAnalysisResultEventArgs(AudioAnalysisResult result)
    {
        Result = result;
    }
}

/// <summary>
/// Audio feature extractor for specific lighting effects
/// </summary>
public class AudioFeatureExtractor
{
    /// <summary>
    /// Extracts features for beat-sync lighting
    /// </summary>
    public static BeatSyncFeatures ExtractBeatSyncFeatures(AudioAnalysisResult result)
    {
        return new BeatSyncFeatures
        {
            BeatDetected = result.BeatDetected,
            BeatIntensity = result.BeatIntensity,
            Tempo = result.Tempo,
            Volume = result.Volume,
            Energy = result.Energy
        };
    }

    /// <summary>
    /// Extracts features for frequency visualization
    /// </summary>
    public static FrequencyVisualizationFeatures ExtractFrequencyFeatures(AudioAnalysisResult result)
    {
        return new FrequencyVisualizationFeatures
        {
            FrequencyBands = result.FrequencyBands,
            SpectralCentroid = result.SpectralCentroid,
            SpectralRolloff = result.SpectralRolloff,
            SpectralFlux = result.SpectralFlux,
            BandCount = result.FrequencyBands.Length
        };
    }

    /// <summary>
    /// Extracts features for mood-based lighting
    /// </summary>
    public static MoodLightingFeatures ExtractMoodFeatures(AudioAnalysisResult result)
    {
        return new MoodLightingFeatures
        {
            Mood = result.Mood,
            Valence = result.Valence,
            Arousal = result.Arousal,
            Energy = result.Energy,
            Intensity = result.Intensity
        };
    }

    /// <summary>
    /// Extracts features for spectrum analyzer
    /// </summary>
    public static SpectrumAnalyzerFeatures ExtractSpectrumFeatures(AudioAnalysisResult result)
    {
        return new SpectrumAnalyzerFeatures
        {
            FrequencyBands = result.FrequencyBands,
            SpectralCentroid = result.SpectralCentroid,
            SpectralBandwidth = result.SpectralBandwidth,
            SpectralContrast = result.SpectralContrast,
            ZeroCrossingRate = result.ZeroCrossingRate
        };
    }

    /// <summary>
    /// Extracts features for party mode
    /// </summary>
    public static PartyModeFeatures ExtractPartyFeatures(AudioAnalysisResult result)
    {
        return new PartyModeFeatures
        {
            BeatDetected = result.BeatDetected,
            BeatIntensity = result.BeatIntensity,
            Tempo = result.Tempo,
            Energy = result.Energy,
            Complexity = result.Complexity,
            Mood = result.Mood,
            Genre = result.Genre
        };
    }
}

/// <summary>
/// Features for beat-sync lighting
/// </summary>
public class BeatSyncFeatures
{
    public bool BeatDetected { get; set; }
    public float BeatIntensity { get; set; }
    public float Tempo { get; set; }
    public float Volume { get; set; }
    public float Energy { get; set; }
}

/// <summary>
/// Features for frequency visualization
/// </summary>
public class FrequencyVisualizationFeatures
{
    public float[] FrequencyBands { get; set; } = Array.Empty<float>();
    public float SpectralCentroid { get; set; }
    public float SpectralRolloff { get; set; }
    public float SpectralFlux { get; set; }
    public int BandCount { get; set; }
}

/// <summary>
/// Features for mood-based lighting
/// </summary>
public class MoodLightingFeatures
{
    public AudioMood Mood { get; set; }
    public float Valence { get; set; }
    public float Arousal { get; set; }
    public float Energy { get; set; }
    public float Intensity { get; set; }
}

/// <summary>
/// Features for spectrum analyzer
/// </summary>
public class SpectrumAnalyzerFeatures
{
    public float[] FrequencyBands { get; set; } = Array.Empty<float>();
    public float SpectralCentroid { get; set; }
    public float SpectralBandwidth { get; set; }
    public float SpectralContrast { get; set; }
    public float ZeroCrossingRate { get; set; }
}

/// <summary>
/// Features for party mode
/// </summary>
public class PartyModeFeatures
{
    public bool BeatDetected { get; set; }
    public float BeatIntensity { get; set; }
    public float Tempo { get; set; }
    public float Energy { get; set; }
    public float Complexity { get; set; }
    public AudioMood Mood { get; set; }
    public AudioGenre Genre { get; set; }
}
