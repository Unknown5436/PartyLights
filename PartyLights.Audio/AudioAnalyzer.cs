using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Dsp;
using System.Collections.Concurrent;

namespace PartyLights.Audio;

/// <summary>
/// Real-time audio analyzer using FFT
/// </summary>
public class AudioAnalyzer
{
    private readonly ILogger<AudioAnalyzer> _logger;
    private readonly float[] _fftBuffer;
    private readonly float[] _windowBuffer;
    private readonly int _fftSize;
    private readonly int _sampleRate;

    public AudioAnalyzer(ILogger<AudioAnalyzer> logger, int fftSize = 1024, int sampleRate = 44100)
    {
        _logger = logger;
        _fftSize = fftSize;
        _sampleRate = sampleRate;
        _fftBuffer = new float[fftSize];
        _windowBuffer = new float[fftSize];
    }

    /// <summary>
    /// Analyzes audio samples and returns frequency analysis
    /// </summary>
    public AudioAnalysisResult AnalyzeSamples(float[] samples)
    {
        if (samples.Length != _fftSize)
        {
            throw new ArgumentException($"Sample array must be exactly {_fftSize} samples long");
        }

        try
        {
            // Copy samples to FFT buffer
            Array.Copy(samples, _fftBuffer, _fftSize);

            // Apply window function
            ApplyHanningWindow(_fftBuffer, _windowBuffer);

            // Perform FFT
            var fftResult = PerformFFT(_windowBuffer);

            // Calculate frequency bands
            var frequencyBands = CalculateFrequencyBands(fftResult);

            // Calculate volume (RMS)
            var volume = CalculateRMS(_fftBuffer);

            // Calculate spectral centroid
            var spectralCentroid = CalculateSpectralCentroid(fftResult);

            // Calculate zero crossing rate
            var zeroCrossingRate = CalculateZeroCrossingRate(_fftBuffer);

            return new AudioAnalysisResult
            {
                Volume = volume,
                FrequencyBands = frequencyBands,
                SpectralCentroid = spectralCentroid,
                ZeroCrossingRate = zeroCrossingRate,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing audio samples");
            throw;
        }
    }

    private void ApplyHanningWindow(float[] input, float[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            var window = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (input.Length - 1)));
            output[i] = input[i] * window;
        }
    }

    private Complex[] PerformFFT(float[] samples)
    {
        var fftResult = new Complex[samples.Length];

        // Convert to complex numbers
        for (int i = 0; i < samples.Length; i++)
        {
            fftResult[i] = new Complex(samples[i], 0);
        }

        // Perform FFT
        FastFourierTransform.FFT(true, (int)Math.Log2(samples.Length), fftResult);

        return fftResult;
    }

    private float[] CalculateFrequencyBands(Complex[] fftResult)
    {
        var bands = new float[8]; // 8 frequency bands
        var bandSize = fftResult.Length / 2 / bands.Length;

        for (int band = 0; band < bands.Length; band++)
        {
            float magnitude = 0;
            int startIndex = band * bandSize;
            int endIndex = Math.Min(startIndex + bandSize, fftResult.Length / 2);

            for (int i = startIndex; i < endIndex; i++)
            {
                magnitude += (float)Math.Sqrt(fftResult[i].X * fftResult[i].X + fftResult[i].Y * fftResult[i].Y);
            }

            bands[band] = magnitude / (endIndex - startIndex);
        }

        return bands;
    }

    private float CalculateRMS(float[] samples)
    {
        float sum = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        return (float)Math.Sqrt(sum / samples.Length);
    }

    private float CalculateSpectralCentroid(Complex[] fftResult)
    {
        float weightedSum = 0;
        float magnitudeSum = 0;

        for (int i = 0; i < fftResult.Length / 2; i++)
        {
            var magnitude = (float)Math.Sqrt(fftResult[i].X * fftResult[i].X + fftResult[i].Y * fftResult[i].Y);
            weightedSum += i * magnitude;
            magnitudeSum += magnitude;
        }

        return magnitudeSum > 0 ? weightedSum / magnitudeSum : 0;
    }

    private float CalculateZeroCrossingRate(float[] samples)
    {
        int crossings = 0;
        for (int i = 1; i < samples.Length; i++)
        {
            if ((samples[i] >= 0) != (samples[i - 1] >= 0))
            {
                crossings++;
            }
        }
        return (float)crossings / (samples.Length - 1);
    }
}

/// <summary>
/// Beat detection algorithm
/// </summary>
public class BeatDetector
{
    private readonly ILogger<BeatDetector> _logger;
    private readonly Queue<float> _volumeHistory;
    private readonly int _historySize;
    private float _volumeThreshold;
    private float _beatSensitivity;
    private DateTime _lastBeatTime;
    private float _averageVolume;

    public BeatDetector(ILogger<BeatDetector> logger, int historySize = 43) // ~1 second at 20fps
    {
        _logger = logger;
        _historySize = historySize;
        _volumeHistory = new Queue<float>();
        _volumeThreshold = 0.01f;
        _beatSensitivity = 0.3f;
        _lastBeatTime = DateTime.MinValue;
        _averageVolume = 0;
    }

    /// <summary>
    /// Detects beats in audio volume
    /// </summary>
    public BeatDetectionResult DetectBeat(float volume)
    {
        // Add volume to history
        _volumeHistory.Enqueue(volume);
        if (_volumeHistory.Count > _historySize)
        {
            _volumeHistory.Dequeue();
        }

        // Calculate average volume
        _averageVolume = _volumeHistory.Average();

        var now = DateTime.UtcNow;
        var timeSinceLastBeat = (now - _lastBeatTime).TotalSeconds;

        // Beat detection algorithm
        var beatDetected = false;
        var beatIntensity = 0f;

        if (volume > _volumeThreshold &&
            volume > _averageVolume * (1 + _beatSensitivity) &&
            timeSinceLastBeat > 0.2) // Minimum 200ms between beats
        {
            beatDetected = true;
            beatIntensity = Math.Min((volume - _averageVolume) / _averageVolume, 1.0f);
            _lastBeatTime = now;
        }

        return new BeatDetectionResult
        {
            BeatDetected = beatDetected,
            BeatIntensity = beatIntensity,
            AverageVolume = _averageVolume,
            TimeSinceLastBeat = timeSinceLastBeat
        };
    }

    /// <summary>
    /// Updates detection parameters
    /// </summary>
    public void UpdateParameters(float volumeThreshold, float beatSensitivity)
    {
        _volumeThreshold = Math.Clamp(volumeThreshold, 0.001f, 0.1f);
        _beatSensitivity = Math.Clamp(beatSensitivity, 0.1f, 2.0f);

        _logger.LogDebug("Updated beat detection parameters: Threshold={Threshold}, Sensitivity={Sensitivity}",
            _volumeThreshold, _beatSensitivity);
    }
}

/// <summary>
/// Tempo estimation algorithm
/// </summary>
public class TempoEstimator
{
    private readonly ILogger<TempoEstimator> _logger;
    private readonly Queue<DateTime> _beatTimes;
    private readonly int _maxBeatHistory = 20;
    private float _currentTempo = 120f;

    public TempoEstimator(ILogger<TempoEstimator> logger)
    {
        _logger = logger;
        _beatTimes = new Queue<DateTime>();
    }

    /// <summary>
    /// Estimates tempo based on beat intervals
    /// </summary>
    public float EstimateTempo(DateTime beatTime)
    {
        _beatTimes.Enqueue(beatTime);

        if (_beatTimes.Count > _maxBeatHistory)
        {
            _beatTimes.Dequeue();
        }

        if (_beatTimes.Count < 4)
        {
            return _currentTempo; // Not enough data
        }

        // Calculate average interval between beats
        var intervals = new List<double>();
        var beatArray = _beatTimes.ToArray();

        for (int i = 1; i < beatArray.Length; i++)
        {
            intervals.Add((beatArray[i] - beatArray[i - 1]).TotalSeconds);
        }

        // Calculate median interval to avoid outliers
        intervals.Sort();
        var medianInterval = intervals[intervals.Count / 2];

        // Convert to BPM
        var tempo = 60.0f / (float)medianInterval;

        // Smooth tempo changes
        _currentTempo = _currentTempo * 0.8f + tempo * 0.2f;

        return _currentTempo;
    }

    /// <summary>
    /// Gets the current estimated tempo
    /// </summary>
    public float GetCurrentTempo()
    {
        return _currentTempo;
    }
}

/// <summary>
/// Result of audio analysis
/// </summary>
public class AudioAnalysisResult
{
    public float Volume { get; set; }
    public float[] FrequencyBands { get; set; } = Array.Empty<float>();
    public float SpectralCentroid { get; set; }
    public float ZeroCrossingRate { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Result of beat detection
/// </summary>
public class BeatDetectionResult
{
    public bool BeatDetected { get; set; }
    public float BeatIntensity { get; set; }
    public float AverageVolume { get; set; }
    public double TimeSinceLastBeat { get; set; }
}
