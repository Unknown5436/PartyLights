using Microsoft.Extensions.Logging;
using NAudio.Dsp;
using System.Collections.Concurrent;

namespace PartyLights.Audio;

/// <summary>
/// Advanced audio analysis pipeline with multiple analysis modules
/// </summary>
public class AudioAnalysisPipeline
{
    private readonly ILogger<AudioAnalysisPipeline> _logger;
    private readonly ConcurrentQueue<AudioFrame> _frameQueue;
    private readonly List<IAudioAnalyzer> _analyzers;
    private readonly AudioAnalysisConfig _config;

    private bool _isProcessing;
    private Task? _processingTask;
    private CancellationTokenSource? _cancellationTokenSource;

    public AudioAnalysisPipeline(ILogger<AudioAnalysisPipeline> logger, AudioAnalysisConfig config)
    {
        _logger = logger;
        _config = config;
        _frameQueue = new ConcurrentQueue<AudioFrame>();
        _analyzers = new List<IAudioAnalyzer>();

        InitializeAnalyzers();
    }

    public event EventHandler<AudioAnalysisResultEventArgs>? AnalysisCompleted;

    /// <summary>
    /// Starts the audio analysis pipeline
    /// </summary>
    public void Start()
    {
        if (_isProcessing)
        {
            _logger.LogWarning("Audio analysis pipeline is already running");
            return;
        }

        _logger.LogInformation("Starting audio analysis pipeline");

        _cancellationTokenSource = new CancellationTokenSource();
        _isProcessing = true;

        _processingTask = Task.Run(ProcessAudioFrames, _cancellationTokenSource.Token);

        _logger.LogInformation("Audio analysis pipeline started");
    }

    /// <summary>
    /// Stops the audio analysis pipeline
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isProcessing)
        {
            _logger.LogWarning("Audio analysis pipeline is not running");
            return;
        }

        _logger.LogInformation("Stopping audio analysis pipeline");

        _isProcessing = false;
        _cancellationTokenSource?.Cancel();

        if (_processingTask != null)
        {
            await _processingTask;
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _logger.LogInformation("Audio analysis pipeline stopped");
    }

    /// <summary>
    /// Adds an audio frame for analysis
    /// </summary>
    public void AddAudioFrame(AudioFrame frame)
    {
        if (_isProcessing)
        {
            _frameQueue.Enqueue(frame);
        }
    }

    private void InitializeAnalyzers()
    {
        // Add various analysis modules
        _analyzers.Add(new FrequencyAnalyzer(_logger.CreateLogger<FrequencyAnalyzer>(), _config));
        _analyzers.Add(new BeatAnalyzer(_logger.CreateLogger<BeatAnalyzer>(), _config));
        _analyzers.Add(new SpectralAnalyzer(_logger.CreateLogger<SpectralAnalyzer>(), _config));
        _analyzers.Add(new RhythmAnalyzer(_logger.CreateLogger<RhythmAnalyzer>(), _config));
        _analyzers.Add(new MoodAnalyzer(_logger.CreateLogger<MoodAnalyzer>(), _config));

        _logger.LogInformation("Initialized {Count} audio analyzers", _analyzers.Count);
    }

    private async Task ProcessAudioFrames()
    {
        _logger.LogInformation("Audio frame processing started");

        while (_isProcessing && !_cancellationTokenSource!.Token.IsCancellationRequested)
        {
            try
            {
                if (_frameQueue.TryDequeue(out var frame))
                {
                    var result = await AnalyzeFrameAsync(frame);
                    if (result != null)
                    {
                        AnalysisCompleted?.Invoke(this, new AudioAnalysisResultEventArgs(result));
                    }
                }
                else
                {
                    await Task.Delay(10, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audio frame");
                await Task.Delay(100);
            }
        }

        _logger.LogInformation("Audio frame processing stopped");
    }

    private async Task<AudioAnalysisResult?> AnalyzeFrameAsync(AudioFrame frame)
    {
        try
        {
            var result = new AudioAnalysisResult
            {
                Timestamp = frame.Timestamp,
                FrameId = frame.Id
            };

            // Run all analyzers in parallel
            var analysisTasks = _analyzers.Select(analyzer => analyzer.AnalyzeAsync(frame));
            var analysisResults = await Task.WhenAll(analysisTasks);

            // Combine results
            foreach (var analysisResult in analysisResults)
            {
                result.Combine(analysisResult);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing audio frame");
            return null;
        }
    }
}

/// <summary>
/// Interface for audio analyzers
/// </summary>
public interface IAudioAnalyzer
{
    Task<AudioAnalysisResult> AnalyzeAsync(AudioFrame frame);
}

/// <summary>
/// Frequency domain analyzer
/// </summary>
public class FrequencyAnalyzer : IAudioAnalyzer
{
    private readonly ILogger<FrequencyAnalyzer> _logger;
    private readonly AudioAnalysisConfig _config;
    private readonly float[] _fftBuffer;
    private readonly float[] _windowBuffer;

    public FrequencyAnalyzer(ILogger<FrequencyAnalyzer> logger, AudioAnalysisConfig config)
    {
        _logger = logger;
        _config = config;
        _fftBuffer = new float[config.FftSize];
        _windowBuffer = new float[config.FftSize];
    }

    public async Task<AudioAnalysisResult> AnalyzeAsync(AudioFrame frame)
    {
        var result = new AudioAnalysisResult();

        try
        {
            // Copy samples to FFT buffer
            Array.Copy(frame.Samples, _fftBuffer, Math.Min(frame.Samples.Length, _fftBuffer.Length));

            // Apply window function
            ApplyBlackmanHarrisWindow(_fftBuffer, _windowBuffer);

            // Perform FFT
            var fftResult = PerformFFT(_windowBuffer);

            // Calculate frequency bands
            result.FrequencyBands = CalculateFrequencyBands(fftResult);

            // Calculate spectral centroid
            result.SpectralCentroid = CalculateSpectralCentroid(fftResult);

            // Calculate spectral rolloff
            result.SpectralRolloff = CalculateSpectralRolloff(fftResult);

            // Calculate spectral flux
            result.SpectralFlux = CalculateSpectralFlux(fftResult);

            // Calculate zero crossing rate
            result.ZeroCrossingRate = CalculateZeroCrossingRate(_fftBuffer);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in frequency analysis");
        }

        return result;
    }

    private void ApplyBlackmanHarrisWindow(float[] input, float[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            var window = 0.35875f - 0.48829f * (float)Math.Cos(2 * Math.PI * i / (input.Length - 1)) +
                         0.14128f * (float)Math.Cos(4 * Math.PI * i / (input.Length - 1)) -
                         0.01168f * (float)Math.Cos(6 * Math.PI * i / (input.Length - 1));
            output[i] = input[i] * window;
        }
    }

    private Complex[] PerformFFT(float[] samples)
    {
        var fftResult = new Complex[samples.Length];

        for (int i = 0; i < samples.Length; i++)
        {
            fftResult[i] = new Complex(samples[i], 0);
        }

        FastFourierTransform.FFT(true, (int)Math.Log2(samples.Length), fftResult);
        return fftResult;
    }

    private float[] CalculateFrequencyBands(Complex[] fftResult)
    {
        var bands = new float[12]; // 12 frequency bands
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

    private float CalculateSpectralRolloff(Complex[] fftResult)
    {
        float totalEnergy = 0;
        var magnitudes = new float[fftResult.Length / 2];

        for (int i = 0; i < fftResult.Length / 2; i++)
        {
            magnitudes[i] = (float)Math.Sqrt(fftResult[i].X * fftResult[i].X + fftResult[i].Y * fftResult[i].Y);
            totalEnergy += magnitudes[i];
        }

        float cumulativeEnergy = 0;
        float threshold = totalEnergy * 0.85f; // 85% rolloff

        for (int i = 0; i < magnitudes.Length; i++)
        {
            cumulativeEnergy += magnitudes[i];
            if (cumulativeEnergy >= threshold)
            {
                return (float)i / magnitudes.Length;
            }
        }

        return 1.0f;
    }

    private float CalculateSpectralFlux(Complex[] fftResult)
    {
        // Simplified spectral flux calculation
        float flux = 0;
        for (int i = 0; i < fftResult.Length / 2; i++)
        {
            var magnitude = (float)Math.Sqrt(fftResult[i].X * fftResult[i].X + fftResult[i].Y * fftResult[i].Y);
            flux += magnitude;
        }
        return flux / (fftResult.Length / 2);
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
/// Beat detection analyzer
/// </summary>
public class BeatAnalyzer : IAudioAnalyzer
{
    private readonly ILogger<BeatAnalyzer> _logger;
    private readonly AudioAnalysisConfig _config;
    private readonly Queue<float> _volumeHistory;
    private readonly Queue<DateTime> _beatTimes;
    private DateTime _lastBeatTime = DateTime.MinValue;

    public BeatAnalyzer(ILogger<BeatAnalyzer> logger, AudioAnalysisConfig config)
    {
        _logger = logger;
        _config = config;
        _volumeHistory = new Queue<float>();
        _beatTimes = new Queue<DateTime>();
    }

    public async Task<AudioAnalysisResult> AnalyzeAsync(AudioFrame frame)
    {
        var result = new AudioAnalysisResult();

        try
        {
            // Calculate RMS volume
            var volume = CalculateRMS(frame.Samples);
            result.Volume = volume;

            // Add to volume history
            _volumeHistory.Enqueue(volume);
            if (_volumeHistory.Count > _config.BeatHistorySize)
            {
                _volumeHistory.Dequeue();
            }

            // Detect beats
            var beatDetected = DetectBeat(volume);
            result.BeatDetected = beatDetected;

            if (beatDetected)
            {
                _beatTimes.Enqueue(DateTime.UtcNow);
                if (_beatTimes.Count > _config.TempoHistorySize)
                {
                    _beatTimes.Dequeue();
                }

                result.BeatIntensity = CalculateBeatIntensity(volume);
                result.Tempo = EstimateTempo();
            }
            else
            {
                result.BeatIntensity = 0;
                result.Tempo = EstimateTempo();
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in beat analysis");
        }

        return result;
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

    private bool DetectBeat(float volume)
    {
        if (_volumeHistory.Count < _config.BeatHistorySize)
            return false;

        var averageVolume = _volumeHistory.Average();
        var variance = _volumeHistory.Select(v => (v - averageVolume) * (v - averageVolume)).Average();
        var threshold = averageVolume + Math.Sqrt(variance) * _config.BeatSensitivity;

        var timeSinceLastBeat = (DateTime.UtcNow - _lastBeatTime).TotalSeconds;

        if (volume > threshold && timeSinceLastBeat > _config.MinBeatInterval)
        {
            _lastBeatTime = DateTime.UtcNow;
            return true;
        }

        return false;
    }

    private float CalculateBeatIntensity(float volume)
    {
        if (_volumeHistory.Count < 2)
            return 0;

        var averageVolume = _volumeHistory.Average();
        return Math.Min((volume - averageVolume) / averageVolume, 1.0f);
    }

    private float EstimateTempo()
    {
        if (_beatTimes.Count < 2)
            return 120.0f; // Default tempo

        var intervals = new List<double>();
        var beatArray = _beatTimes.ToArray();

        for (int i = 1; i < beatArray.Length; i++)
        {
            intervals.Add((beatArray[i] - beatArray[i - 1]).TotalSeconds);
        }

        intervals.Sort();
        var medianInterval = intervals[intervals.Count / 2];

        return 60.0f / (float)medianInterval;
    }
}

/// <summary>
/// Spectral analysis analyzer
/// </summary>
public class SpectralAnalyzer : IAudioAnalyzer
{
    private readonly ILogger<SpectralAnalyzer> _logger;
    private readonly AudioAnalysisConfig _config;

    public SpectralAnalyzer(ILogger<SpectralAnalyzer> logger, AudioAnalysisConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task<AudioAnalysisResult> AnalyzeAsync(AudioFrame frame)
    {
        var result = new AudioAnalysisResult();

        try
        {
            // Calculate spectral features
            result.SpectralBandwidth = CalculateSpectralBandwidth(frame.Samples);
            result.SpectralContrast = CalculateSpectralContrast(frame.Samples);
            result.SpectralFlatness = CalculateSpectralFlatness(frame.Samples);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in spectral analysis");
        }

        return result;
    }

    private float CalculateSpectralBandwidth(float[] samples)
    {
        // Simplified spectral bandwidth calculation
        var variance = 0f;
        var mean = samples.Average();

        foreach (var sample in samples)
        {
            variance += (sample - mean) * (sample - mean);
        }

        return (float)Math.Sqrt(variance / samples.Length);
    }

    private float CalculateSpectralContrast(float[] samples)
    {
        // Calculate contrast between high and low frequencies
        var lowFreq = samples.Take(samples.Length / 4).Average();
        var highFreq = samples.Skip(3 * samples.Length / 4).Average();

        return Math.Abs(highFreq - lowFreq);
    }

    private float CalculateSpectralFlatness(float[] samples)
    {
        // Simplified spectral flatness calculation
        var geometricMean = 1f;
        var arithmeticMean = 0f;

        foreach (var sample in samples)
        {
            var absSample = Math.Abs(sample);
            if (absSample > 0)
            {
                geometricMean *= (float)Math.Pow(absSample, 1.0 / samples.Length);
            }
            arithmeticMean += absSample;
        }

        arithmeticMean /= samples.Length;

        return arithmeticMean > 0 ? geometricMean / arithmeticMean : 0;
    }
}

/// <summary>
/// Rhythm analysis analyzer
/// </summary>
public class RhythmAnalyzer : IAudioAnalyzer
{
    private readonly ILogger<RhythmAnalyzer> _logger;
    private readonly AudioAnalysisConfig _config;
    private readonly Queue<float> _rhythmHistory;

    public RhythmAnalyzer(ILogger<RhythmAnalyzer> logger, AudioAnalysisConfig config)
    {
        _logger = logger;
        _config = config;
        _rhythmHistory = new Queue<float>();
    }

    public async Task<AudioAnalysisResult> AnalyzeAsync(AudioFrame frame)
    {
        var result = new AudioAnalysisResult();

        try
        {
            // Calculate rhythm features
            result.RhythmRegularity = CalculateRhythmRegularity(frame.Samples);
            result.RhythmComplexity = CalculateRhythmComplexity(frame.Samples);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in rhythm analysis");
        }

        return result;
    }

    private float CalculateRhythmRegularity(float[] samples)
    {
        // Calculate regularity based on zero crossing patterns
        var zeroCrossings = new List<int>();

        for (int i = 1; i < samples.Length; i++)
        {
            if ((samples[i] >= 0) != (samples[i - 1] >= 0))
            {
                zeroCrossings.Add(i);
            }
        }

        if (zeroCrossings.Count < 2)
            return 0;

        var intervals = new List<int>();
        for (int i = 1; i < zeroCrossings.Count; i++)
        {
            intervals.Add(zeroCrossings[i] - zeroCrossings[i - 1]);
        }

        var meanInterval = intervals.Average();
        var variance = intervals.Select(i => (i - meanInterval) * (i - meanInterval)).Average();

        return variance > 0 ? 1.0f / (1.0f + (float)Math.Sqrt(variance)) : 1.0f;
    }

    private float CalculateRhythmComplexity(float[] samples)
    {
        // Calculate complexity based on frequency content
        var highFreqContent = samples.Skip(samples.Length / 2).Sum(s => Math.Abs(s));
        var totalContent = samples.Sum(s => Math.Abs(s));

        return totalContent > 0 ? highFreqContent / totalContent : 0;
    }
}

/// <summary>
/// Mood analysis analyzer
/// </summary>
public class MoodAnalyzer : IAudioAnalyzer
{
    private readonly ILogger<MoodAnalyzer> _logger;
    private readonly AudioAnalysisConfig _config;

    public MoodAnalyzer(ILogger<MoodAnalyzer> logger, AudioAnalysisConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task<AudioAnalysisResult> AnalyzeAsync(AudioFrame frame)
    {
        var result = new AudioAnalysisResult();

        try
        {
            // Calculate mood features
            result.Energy = CalculateEnergy(frame.Samples);
            result.Valence = CalculateValence(frame.Samples);
            result.Arousal = CalculateArousal(frame.Samples);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in mood analysis");
        }

        return result;
    }

    private float CalculateEnergy(float[] samples)
    {
        return samples.Sum(s => s * s) / samples.Length;
    }

    private float CalculateValence(float[] samples)
    {
        // Simplified valence calculation based on spectral centroid
        var centroid = samples.Select((s, i) => i * Math.Abs(s)).Sum() / samples.Sum(s => Math.Abs(s));
        return (float)centroid / samples.Length;
    }

    private float CalculateArousal(float[] samples)
    {
        // Simplified arousal calculation based on high frequency content
        var highFreq = samples.Skip(samples.Length / 2).Sum(s => Math.Abs(s));
        var total = samples.Sum(s => Math.Abs(s));
        return total > 0 ? highFreq / total : 0;
    }
}
