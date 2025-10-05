using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using System.Numerics;

namespace PartyLights.Services;

/// <summary>
/// Advanced audio analysis service with sophisticated algorithms
/// </summary>
public class AdvancedAudioAnalysisService
{
    private readonly ILogger<AdvancedAudioAnalysisService> _logger;
    private readonly Queue<float[]> _audioBuffer = new();
    private readonly Queue<float[]> _spectralBuffer = new();
    private readonly List<float> _beatHistory = new();
    private readonly List<float> _tempoHistory = new();
    private readonly List<float> _energyHistory = new();
    private readonly List<float> _valenceHistory = new();

    private const int BufferSize = 1024;
    private const int FFTSize = 2048;
    private const int HopSize = 512;
    private const int SampleRate = 44100;
    private const int HistorySize = 100;

    private float _previousSpectralCentroid = 0;
    private float _previousSpectralRolloff = 0;
    private float _previousSpectralFlux = 0;
    private DateTime _lastBeatTime = DateTime.MinValue;
    private float _estimatedTempo = 120f;
    private int _beatCount = 0;
    private DateTime _tempoStartTime = DateTime.UtcNow;

    public AdvancedAudioAnalysisService(ILogger<AdvancedAudioAnalysisService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Performs comprehensive audio analysis on the input samples
    /// </summary>
    public AudioAnalysis AnalyzeAudio(float[] samples)
    {
        try
        {
            var analysis = new AudioAnalysis
            {
                Timestamp = DateTime.UtcNow
            };

            // Add samples to buffer
            _audioBuffer.Enqueue(samples);
            if (_audioBuffer.Count > BufferSize / samples.Length)
            {
                _audioBuffer.Dequeue();
            }

            // Perform FFT analysis
            var fftResult = PerformFFT(samples);
            _spectralBuffer.Enqueue(fftResult);
            if (_spectralBuffer.Count > 10)
            {
                _spectralBuffer.Dequeue();
            }

            // Basic features
            analysis.Volume = CalculateRMS(samples);
            analysis.FrequencyBands = CalculateFrequencyBands(fftResult);
            analysis.SpectralCentroid = CalculateSpectralCentroid(fftResult);
            analysis.SpectralRolloff = CalculateSpectralRolloff(fftResult);
            analysis.SpectralBandwidth = CalculateSpectralBandwidth(fftResult);
            analysis.SpectralContrast = CalculateSpectralContrast(fftResult);
            analysis.SpectralFlatness = CalculateSpectralFlatness(fftResult);
            analysis.SpectralFlux = CalculateSpectralFlux(fftResult);
            analysis.ZeroCrossingRate = CalculateZeroCrossingRate(samples);

            // MFCC features
            var mfcc = CalculateMFCC(fftResult);
            analysis.MFCC1 = mfcc.Length > 0 ? mfcc[0] : 0;
            analysis.MFCC2 = mfcc.Length > 1 ? mfcc[1] : 0;
            analysis.MFCC3 = mfcc.Length > 2 ? mfcc[2] : 0;

            // Beat detection
            var beatAnalysis = AnalyzeBeatDetection(fftResult, analysis.Volume);
            analysis.IsBeatDetected = beatAnalysis.IsBeatDetected;
            analysis.BeatConfidence = beatAnalysis.Confidence;
            analysis.BeatStrength = beatAnalysis.Strength;
            analysis.BeatTimes = beatAnalysis.BeatTimes;
            analysis.OnsetStrength = beatAnalysis.OnsetStrength;

            // Tempo estimation
            analysis.Tempo = EstimateTempo();
            analysis.RhythmRegularity = CalculateRhythmRegularity();
            analysis.RhythmComplexity = CalculateRhythmComplexity();

            // Advanced frequency analysis
            analysis.FrequencyBandsDetailed = CalculateDetailedFrequencyBands(fftResult);
            analysis.BassIntensity = CalculateBassIntensity(fftResult);
            analysis.MidIntensity = CalculateMidIntensity(fftResult);
            analysis.TrebleIntensity = CalculateTrebleIntensity(fftResult);
            analysis.SubBassIntensity = CalculateSubBassIntensity(fftResult);
            analysis.PresenceIntensity = CalculatePresenceIntensity(fftResult);

            // Dynamic range analysis
            analysis.DynamicRange = CalculateDynamicRange(samples);
            analysis.PeakLevel = CalculatePeakLevel(samples);
            analysis.RMSLevel = analysis.Volume;
            analysis.CrestFactor = analysis.PeakLevel / Math.Max(analysis.RMSLevel, 0.001f);
            analysis.CompressionRatio = CalculateCompressionRatio(samples);

            // Harmonic analysis
            var harmonicAnalysis = AnalyzeHarmonics(fftResult);
            analysis.Harmonicity = harmonicAnalysis.Harmonicity;
            analysis.Inharmonicity = harmonicAnalysis.Inharmonicity;
            analysis.FundamentalFrequency = harmonicAnalysis.FundamentalFrequency;
            analysis.HarmonicFrequencies = harmonicAnalysis.HarmonicFrequencies;
            analysis.HarmonicAmplitudes = harmonicAnalysis.HarmonicAmplitudes;

            // Temporal features
            var temporalAnalysis = AnalyzeTemporalFeatures(samples);
            analysis.AttackTime = temporalAnalysis.AttackTime;
            analysis.DecayTime = temporalAnalysis.DecayTime;
            analysis.SustainLevel = temporalAnalysis.SustainLevel;
            analysis.ReleaseTime = temporalAnalysis.ReleaseTime;
            analysis.TransientStrength = temporalAnalysis.TransientStrength;

            // Quality metrics
            analysis.SignalToNoiseRatio = CalculateSNR(samples);
            analysis.DistortionLevel = CalculateDistortionLevel(samples);
            analysis.ClippingLevel = CalculateClippingLevel(samples);
            analysis.IsClipping = analysis.ClippingLevel > 0.95f;
            analysis.AudioQuality = CalculateAudioQuality(analysis);

            // Mood and emotion analysis
            var moodAnalysis = AnalyzeMood(analysis);
            analysis.Energy = moodAnalysis.Energy;
            analysis.Valence = moodAnalysis.Valence;
            analysis.Arousal = moodAnalysis.Arousal;
            analysis.Dominance = moodAnalysis.Dominance;
            analysis.PredictedMood = moodAnalysis.PredictedMood;
            analysis.MoodConfidence = moodAnalysis.Confidence;

            // Genre classification
            var genreAnalysis = ClassifyGenre(analysis);
            analysis.PredictedGenre = genreAnalysis.PredictedGenre;
            analysis.GenreConfidence = genreAnalysis.Confidence;
            analysis.GenreProbabilities = genreAnalysis.Probabilities;

            // Update history for trend analysis
            UpdateHistory(analysis);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during advanced audio analysis");
            return new AudioAnalysis { Timestamp = DateTime.UtcNow };
        }
    }

    #region FFT and Spectral Analysis

    private float[] PerformFFT(float[] samples)
    {
        // Pad samples to FFT size
        var paddedSamples = new float[FFTSize];
        Array.Copy(samples, paddedSamples, Math.Min(samples.Length, FFTSize));

        // Apply window function (Blackman-Harris)
        ApplyBlackmanHarrisWindow(paddedSamples);

        // Perform FFT using System.Numerics
        var complexSamples = paddedSamples.Select(s => new Complex(s, 0)).ToArray();
        FFT(complexSamples);

        // Calculate magnitude spectrum
        var magnitudeSpectrum = new float[FFTSize / 2];
        for (int i = 0; i < magnitudeSpectrum.Length; i++)
        {
            magnitudeSpectrum[i] = (float)Math.Sqrt(complexSamples[i].Real * complexSamples[i].Real +
                                                   complexSamples[i].Imaginary * complexSamples[i].Imaginary);
        }

        return magnitudeSpectrum;
    }

    private void ApplyBlackmanHarrisWindow(float[] samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            var window = 0.35875f - 0.48829f * (float)Math.Cos(2 * Math.PI * i / (samples.Length - 1)) +
                         0.14128f * (float)Math.Cos(4 * Math.PI * i / (samples.Length - 1)) -
                         0.01168f * (float)Math.Cos(6 * Math.PI * i / (samples.Length - 1));
            samples[i] *= window;
        }
    }

    private void FFT(Complex[] samples)
    {
        int n = samples.Length;
        if (n <= 1) return;

        // Bit-reverse permutation
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            while ((j & bit) != 0)
            {
                j ^= bit;
                bit >>= 1;
            }
            j ^= bit;
            if (i < j)
            {
                (samples[i], samples[j]) = (samples[j], samples[i]);
            }
        }

        // Cooley-Tukey FFT
        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2 * Math.PI / len;
            Complex wlen = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (int i = 0; i < n; i += len)
            {
                Complex w = 1;
                for (int j = 0; j < len / 2; j++)
                {
                    Complex u = samples[i + j];
                    Complex v = samples[i + j + len / 2] * w;
                    samples[i + j] = u + v;
                    samples[i + j + len / 2] = u - v;
                    w *= wlen;
                }
            }
        }
    }

    #endregion

    #region Spectral Feature Calculations

    private float CalculateSpectralCentroid(float[] magnitudeSpectrum)
    {
        float weightedSum = 0;
        float magnitudeSum = 0;

        for (int i = 0; i < magnitudeSpectrum.Length; i++)
        {
            float frequency = (float)i * SampleRate / (2 * magnitudeSpectrum.Length);
            weightedSum += frequency * magnitudeSpectrum[i];
            magnitudeSum += magnitudeSpectrum[i];
        }

        return magnitudeSum > 0 ? weightedSum / magnitudeSum : 0;
    }

    private float CalculateSpectralRolloff(float[] magnitudeSpectrum)
    {
        float totalEnergy = magnitudeSpectrum.Sum();
        float threshold = 0.85f * totalEnergy;
        float cumulativeEnergy = 0;

        for (int i = 0; i < magnitudeSpectrum.Length; i++)
        {
            cumulativeEnergy += magnitudeSpectrum[i];
            if (cumulativeEnergy >= threshold)
            {
                return (float)i * SampleRate / (2 * magnitudeSpectrum.Length);
            }
        }

        return SampleRate / 2;
    }

    private float CalculateSpectralBandwidth(float[] magnitudeSpectrum)
    {
        float centroid = CalculateSpectralCentroid(magnitudeSpectrum);
        float weightedSum = 0;
        float magnitudeSum = 0;

        for (int i = 0; i < magnitudeSpectrum.Length; i++)
        {
            float frequency = (float)i * SampleRate / (2 * magnitudeSpectrum.Length);
            float diff = frequency - centroid;
            weightedSum += diff * diff * magnitudeSpectrum[i];
            magnitudeSum += magnitudeSpectrum[i];
        }

        return magnitudeSum > 0 ? (float)Math.Sqrt(weightedSum / magnitudeSum) : 0;
    }

    private float CalculateSpectralContrast(float[] magnitudeSpectrum)
    {
        // Divide spectrum into sub-bands and calculate contrast
        int numBands = 6;
        int bandSize = magnitudeSpectrum.Length / numBands;
        float contrast = 0;

        for (int band = 0; band < numBands; band++)
        {
            int start = band * bandSize;
            int end = Math.Min(start + bandSize, magnitudeSpectrum.Length);

            float peak = magnitudeSpectrum.Skip(start).Take(end - start).Max();
            float valley = magnitudeSpectrum.Skip(start).Take(end - start).Min();

            if (peak > 0)
            {
                contrast += (peak - valley) / peak;
            }
        }

        return contrast / numBands;
    }

    private float CalculateSpectralFlatness(float[] magnitudeSpectrum)
    {
        float geometricMean = 1;
        float arithmeticMean = 0;
        int validBins = 0;

        for (int i = 0; i < magnitudeSpectrum.Length; i++)
        {
            if (magnitudeSpectrum[i] > 0)
            {
                geometricMean *= (float)Math.Pow(magnitudeSpectrum[i], 1.0f / magnitudeSpectrum.Length);
                arithmeticMean += magnitudeSpectrum[i];
                validBins++;
            }
        }

        arithmeticMean /= validBins;
        return arithmeticMean > 0 ? geometricMean / arithmeticMean : 0;
    }

    private float CalculateSpectralFlux(float[] magnitudeSpectrum)
    {
        if (_previousSpectralFlux == 0)
        {
            _previousSpectralFlux = magnitudeSpectrum.Sum();
            return 0;
        }

        float currentSum = magnitudeSpectrum.Sum();
        float flux = Math.Abs(currentSum - _previousSpectralFlux);
        _previousSpectralFlux = currentSum;

        return flux;
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

    #endregion

    #region MFCC Calculation

    private float[] CalculateMFCC(float[] magnitudeSpectrum)
    {
        // Simplified MFCC calculation
        int numCoeffs = 13;
        var mfcc = new float[numCoeffs];

        // Apply mel-scale filterbank
        var melFilters = CreateMelFilterBank(magnitudeSpectrum.Length, numCoeffs);

        for (int i = 0; i < numCoeffs; i++)
        {
            float melEnergy = 0;
            for (int j = 0; j < magnitudeSpectrum.Length; j++)
            {
                melEnergy += magnitudeSpectrum[j] * melFilters[i][j];
            }

            // Log energy
            mfcc[i] = (float)Math.Log(Math.Max(melEnergy, 1e-10));
        }

        // Apply DCT (simplified)
        for (int i = 0; i < numCoeffs; i++)
        {
            float sum = 0;
            for (int j = 0; j < numCoeffs; j++)
            {
                sum += mfcc[j] * (float)Math.Cos(Math.PI * i * (j + 0.5) / numCoeffs);
            }
            mfcc[i] = sum;
        }

        return mfcc;
    }

    private float[][] CreateMelFilterBank(int fftSize, int numFilters)
    {
        var filters = new float[numFilters][];
        for (int i = 0; i < numFilters; i++)
        {
            filters[i] = new float[fftSize];
        }

        // Simplified mel-scale filterbank
        float melMax = 2595 * (float)Math.Log10(1 + (SampleRate / 2) / 700);

        for (int i = 0; i < numFilters; i++)
        {
            float melCenter = (i + 1) * melMax / (numFilters + 1);
            float freqCenter = 700 * ((float)Math.Pow(10, melCenter / 2595) - 1);
            int binCenter = (int)(freqCenter * fftSize / (SampleRate / 2));

            for (int j = 0; j < fftSize; j++)
            {
                float distance = Math.Abs(j - binCenter);
                if (distance < fftSize / (2 * numFilters))
                {
                    filters[i][j] = 1 - distance / (fftSize / (2 * numFilters));
                }
            }
        }

        return filters;
    }

    #endregion

    #region Beat Detection

    private (bool IsBeatDetected, float Confidence, float Strength, List<float> BeatTimes, float OnsetStrength) AnalyzeBeatDetection(float[] magnitudeSpectrum, float volume)
    {
        // Calculate onset strength
        float onsetStrength = CalculateOnsetStrength(magnitudeSpectrum);

        // Simple beat detection based on onset strength and volume
        bool isBeatDetected = false;
        float confidence = 0;
        float strength = 0;
        var beatTimes = new List<float>();

        if (onsetStrength > 0.3f && volume > 0.1f)
        {
            // Check if enough time has passed since last beat
            var timeSinceLastBeat = DateTime.UtcNow - _lastBeatTime;
            var expectedBeatInterval = 60000f / _estimatedTempo; // Convert BPM to ms

            if (timeSinceLastBeat.TotalMilliseconds > expectedBeatInterval * 0.5f)
            {
                isBeatDetected = true;
                confidence = Math.Min(onsetStrength * volume * 2, 1.0f);
                strength = onsetStrength;

                _lastBeatTime = DateTime.UtcNow;
                _beatCount++;

                // Update tempo estimation
                if (_beatCount > 1)
                {
                    var actualInterval = timeSinceLastBeat.TotalMilliseconds;
                    _estimatedTempo = 60000f / (float)actualInterval;
                }
            }
        }

        return (isBeatDetected, confidence, strength, beatTimes, onsetStrength);
    }

    private float CalculateOnsetStrength(float[] magnitudeSpectrum)
    {
        if (_spectralBuffer.Count < 2)
        {
            return 0;
        }

        var previousSpectrum = _spectralBuffer.ElementAt(_spectralBuffer.Count - 2);
        float onsetStrength = 0;

        for (int i = 0; i < Math.Min(magnitudeSpectrum.Length, previousSpectrum.Length); i++)
        {
            float diff = magnitudeSpectrum[i] - previousSpectrum[i];
            if (diff > 0)
            {
                onsetStrength += diff;
            }
        }

        return onsetStrength / magnitudeSpectrum.Length;
    }

    #endregion

    #region Tempo and Rhythm Analysis

    private float EstimateTempo()
    {
        if (_beatHistory.Count < 2)
        {
            return 120f; // Default tempo
        }

        // Calculate average beat interval
        float totalInterval = 0;
        int validIntervals = 0;

        for (int i = 1; i < _beatHistory.Count; i++)
        {
            float interval = _beatHistory[i] - _beatHistory[i - 1];
            if (interval > 0 && interval < 2000) // Reasonable beat interval (30-200 BPM)
            {
                totalInterval += interval;
                validIntervals++;
            }
        }

        if (validIntervals > 0)
        {
            float avgInterval = totalInterval / validIntervals;
            return 60000f / avgInterval; // Convert ms to BPM
        }

        return _estimatedTempo;
    }

    private float CalculateRhythmRegularity()
    {
        if (_beatHistory.Count < 3)
        {
            return 0.5f;
        }

        float totalVariance = 0;
        int validIntervals = 0;

        for (int i = 1; i < _beatHistory.Count; i++)
        {
            float interval = _beatHistory[i] - _beatHistory[i - 1];
            if (interval > 0 && interval < 2000)
            {
                float expectedInterval = 60000f / _estimatedTempo;
                float variance = Math.Abs(interval - expectedInterval) / expectedInterval;
                totalVariance += variance;
                validIntervals++;
            }
        }

        if (validIntervals > 0)
        {
            float avgVariance = totalVariance / validIntervals;
            return Math.Max(0, 1 - avgVariance); // Higher regularity = lower variance
        }

        return 0.5f;
    }

    private float CalculateRhythmComplexity()
    {
        // Simplified rhythm complexity based on beat pattern variation
        if (_beatHistory.Count < 4)
        {
            return 0.3f;
        }

        var intervals = new List<float>();
        for (int i = 1; i < _beatHistory.Count; i++)
        {
            float interval = _beatHistory[i] - _beatHistory[i - 1];
            if (interval > 0 && interval < 2000)
            {
                intervals.Add(interval);
            }
        }

        if (intervals.Count < 2)
        {
            return 0.3f;
        }

        // Calculate coefficient of variation
        float mean = intervals.Average();
        float variance = intervals.Select(x => (x - mean) * (x - mean)).Average();
        float stdDev = (float)Math.Sqrt(variance);

        return Math.Min(stdDev / mean, 1.0f);
    }

    #endregion

    #region Frequency Band Analysis

    private float[] CalculateFrequencyBands(float[] magnitudeSpectrum)
    {
        var bands = new float[8]; // 8 frequency bands

        // Define frequency ranges for each band
        var bandRanges = new[]
        {
            (0, 0.1f),      // Sub-bass
            (0.1f, 0.25f),  // Bass
            (0.25f, 0.5f),  // Low-mid
            (0.5f, 1.0f),   // Mid
            (1.0f, 2.0f),   // High-mid
            (2.0f, 4.0f),   // Presence
            (4.0f, 8.0f),   // Brilliance
            (8.0f, 1.0f)    // Air
        };

        for (int band = 0; band < bands.Length; band++)
        {
            int startBin = (int)(bandRanges[band].Item1 * magnitudeSpectrum.Length);
            int endBin = (int)(bandRanges[band].Item2 * magnitudeSpectrum.Length);

            float bandEnergy = 0;
            for (int i = startBin; i < Math.Min(endBin, magnitudeSpectrum.Length); i++)
            {
                bandEnergy += magnitudeSpectrum[i];
            }

            bands[band] = bandEnergy;
        }

        return bands;
    }

    private List<FrequencyBand> CalculateDetailedFrequencyBands(float[] magnitudeSpectrum)
    {
        var bands = new List<FrequencyBand>();

        // Define detailed frequency bands
        var bandDefinitions = new[]
        {
            ("Sub-Bass", 20, 60),
            ("Bass", 60, 250),
            ("Low-Mid", 250, 500),
            ("Mid", 500, 2000),
            ("High-Mid", 2000, 4000),
            ("Presence", 4000, 6000),
            ("Brilliance", 6000, 8000),
            ("Air", 8000, 20000)
        };

        foreach (var (name, freqLow, freqHigh) in bandDefinitions)
        {
            int lowBin = (int)(freqLow * magnitudeSpectrum.Length / (SampleRate / 2));
            int highBin = (int)(freqHigh * magnitudeSpectrum.Length / (SampleRate / 2));

            float intensity = 0;
            for (int i = lowBin; i < Math.Min(highBin, magnitudeSpectrum.Length); i++)
            {
                intensity += magnitudeSpectrum[i];
            }

            bands.Add(new FrequencyBand
            {
                Name = name,
                FrequencyLow = freqLow,
                FrequencyHigh = freqHigh,
                Intensity = intensity,
                Band = GetBandType(name)
            });
        }

        return bands;
    }

    private FrequencyBandType GetBandType(string name)
    {
        return name switch
        {
            "Sub-Bass" or "Bass" => FrequencyBandType.Low,
            "Low-Mid" or "Mid" or "High-Mid" => FrequencyBandType.Mid,
            "Presence" or "Brilliance" or "Air" => FrequencyBandType.High,
            _ => FrequencyBandType.Mid
        };
    }

    private float CalculateBassIntensity(float[] magnitudeSpectrum)
    {
        int lowBin = (int)(60 * magnitudeSpectrum.Length / (SampleRate / 2));
        int highBin = (int)(250 * magnitudeSpectrum.Length / (SampleRate / 2));

        float intensity = 0;
        for (int i = lowBin; i < Math.Min(highBin, magnitudeSpectrum.Length); i++)
        {
            intensity += magnitudeSpectrum[i];
        }

        return intensity;
    }

    private float CalculateMidIntensity(float[] magnitudeSpectrum)
    {
        int lowBin = (int)(250 * magnitudeSpectrum.Length / (SampleRate / 2));
        int highBin = (int)(4000 * magnitudeSpectrum.Length / (SampleRate / 2));

        float intensity = 0;
        for (int i = lowBin; i < Math.Min(highBin, magnitudeSpectrum.Length); i++)
        {
            intensity += magnitudeSpectrum[i];
        }

        return intensity;
    }

    private float CalculateTrebleIntensity(float[] magnitudeSpectrum)
    {
        int lowBin = (int)(4000 * magnitudeSpectrum.Length / (SampleRate / 2));
        int highBin = magnitudeSpectrum.Length;

        float intensity = 0;
        for (int i = lowBin; i < highBin; i++)
        {
            intensity += magnitudeSpectrum[i];
        }

        return intensity;
    }

    private float CalculateSubBassIntensity(float[] magnitudeSpectrum)
    {
        int lowBin = (int)(20 * magnitudeSpectrum.Length / (SampleRate / 2));
        int highBin = (int)(60 * magnitudeSpectrum.Length / (SampleRate / 2));

        float intensity = 0;
        for (int i = lowBin; i < Math.Min(highBin, magnitudeSpectrum.Length); i++)
        {
            intensity += magnitudeSpectrum[i];
        }

        return intensity;
    }

    private float CalculatePresenceIntensity(float[] magnitudeSpectrum)
    {
        int lowBin = (int)(4000 * magnitudeSpectrum.Length / (SampleRate / 2));
        int highBin = (int)(6000 * magnitudeSpectrum.Length / (SampleRate / 2));

        float intensity = 0;
        for (int i = lowBin; i < Math.Min(highBin, magnitudeSpectrum.Length); i++)
        {
            intensity += magnitudeSpectrum[i];
        }

        return intensity;
    }

    #endregion

    #region Dynamic Range and Quality Analysis

    private float CalculateRMS(float[] samples)
    {
        float sum = 0;
        foreach (float sample in samples)
        {
            sum += sample * sample;
        }
        return (float)Math.Sqrt(sum / samples.Length);
    }

    private float CalculatePeakLevel(float[] samples)
    {
        return samples.Max(Math.Abs);
    }

    private float CalculateDynamicRange(float[] samples)
    {
        float peak = CalculatePeakLevel(samples);
        float rms = CalculateRMS(samples);
        return peak > 0 ? 20 * (float)Math.Log10(peak / Math.Max(rms, 0.001f)) : 0;
    }

    private float CalculateCompressionRatio(float[] samples)
    {
        // Simplified compression ratio calculation
        float peak = CalculatePeakLevel(samples);
        float rms = CalculateRMS(samples);

        if (peak > 0 && rms > 0)
        {
            float ratio = peak / rms;
            return Math.Min(ratio, 20f); // Cap at 20:1
        }

        return 1f;
    }

    private float CalculateSNR(float[] samples)
    {
        // Simplified SNR calculation
        float signal = CalculateRMS(samples);
        float noise = CalculateRMS(samples.Take(samples.Length / 4).ToArray()); // Assume first quarter is noise
        return signal > 0 ? 20 * (float)Math.Log10(signal / Math.Max(noise, 0.001f)) : 0;
    }

    private float CalculateDistortionLevel(float[] samples)
    {
        // Simplified distortion calculation based on harmonic content
        float fundamental = CalculateRMS(samples);
        float harmonics = CalculateRMS(samples.Skip(samples.Length / 2).ToArray());
        return fundamental > 0 ? harmonics / fundamental : 0;
    }

    private float CalculateClippingLevel(float[] samples)
    {
        int clippedSamples = samples.Count(s => Math.Abs(s) > 0.95f);
        return (float)clippedSamples / samples.Length;
    }

    private float CalculateAudioQuality(AudioAnalysis analysis)
    {
        // Composite audio quality score
        float snrScore = Math.Min(analysis.SignalToNoiseRatio / 60f, 1f);
        float distortionScore = Math.Max(0, 1 - analysis.DistortionLevel);
        float clippingScore = Math.Max(0, 1 - analysis.ClippingLevel);
        float dynamicRangeScore = Math.Min(analysis.DynamicRange / 40f, 1f);

        return (snrScore + distortionScore + clippingScore + dynamicRangeScore) / 4f;
    }

    #endregion

    #region Harmonic Analysis

    private (float Harmonicity, float Inharmonicity, float FundamentalFrequency, List<float> HarmonicFrequencies, List<float> HarmonicAmplitudes) AnalyzeHarmonics(float[] magnitudeSpectrum)
    {
        // Find fundamental frequency (simplified)
        float fundamentalFreq = 0;
        float maxAmplitude = 0;
        int fundamentalBin = 0;

        for (int i = 1; i < magnitudeSpectrum.Length / 2; i++)
        {
            if (magnitudeSpectrum[i] > maxAmplitude)
            {
                maxAmplitude = magnitudeSpectrum[i];
                fundamentalBin = i;
                fundamentalFreq = (float)i * SampleRate / (2 * magnitudeSpectrum.Length);
            }
        }

        // Find harmonics
        var harmonicFrequencies = new List<float>();
        var harmonicAmplitudes = new List<float>();

        for (int harmonic = 2; harmonic <= 10; harmonic++)
        {
            int harmonicBin = fundamentalBin * harmonic;
            if (harmonicBin < magnitudeSpectrum.Length)
            {
                harmonicFrequencies.Add((float)harmonicBin * SampleRate / (2 * magnitudeSpectrum.Length));
                harmonicAmplitudes.Add(magnitudeSpectrum[harmonicBin]);
            }
        }

        // Calculate harmonicity (simplified)
        float harmonicEnergy = harmonicAmplitudes.Sum();
        float totalEnergy = magnitudeSpectrum.Sum();
        float harmonicity = totalEnergy > 0 ? harmonicEnergy / totalEnergy : 0;

        // Calculate inharmonicity (simplified)
        float inharmonicity = 0;
        if (harmonicFrequencies.Count > 1)
        {
            for (int i = 1; i < harmonicFrequencies.Count; i++)
            {
                float expectedFreq = fundamentalFreq * (i + 1);
                float actualFreq = harmonicFrequencies[i];
                if (expectedFreq > 0)
                {
                    inharmonicity += Math.Abs(actualFreq - expectedFreq) / expectedFreq;
                }
            }
            inharmonicity /= harmonicFrequencies.Count - 1;
        }

        return (harmonicity, inharmonicity, fundamentalFreq, harmonicFrequencies, harmonicAmplitudes);
    }

    #endregion

    #region Temporal Analysis

    private (float AttackTime, float DecayTime, float SustainLevel, float ReleaseTime, float TransientStrength) AnalyzeTemporalFeatures(float[] samples)
    {
        // Simplified ADSR analysis
        float peak = samples.Max(Math.Abs);
        float rms = CalculateRMS(samples);

        // Find attack time (time to reach 90% of peak)
        float attackThreshold = peak * 0.9f;
        int attackIndex = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            if (Math.Abs(samples[i]) >= attackThreshold)
            {
                attackIndex = i;
                break;
            }
        }
        float attackTime = (float)attackIndex / SampleRate * 1000; // Convert to ms

        // Find decay time (time from peak to sustain level)
        float sustainThreshold = peak * 0.6f;
        int decayIndex = attackIndex;
        for (int i = attackIndex; i < samples.Length; i++)
        {
            if (Math.Abs(samples[i]) <= sustainThreshold)
            {
                decayIndex = i;
                break;
            }
        }
        float decayTime = (float)(decayIndex - attackIndex) / SampleRate * 1000;

        // Sustain level
        float sustainLevel = rms;

        // Release time (simplified - time from last 90% to end)
        float releaseThreshold = peak * 0.1f;
        int releaseIndex = samples.Length - 1;
        for (int i = samples.Length - 1; i >= 0; i--)
        {
            if (Math.Abs(samples[i]) >= releaseThreshold)
            {
                releaseIndex = i;
                break;
            }
        }
        float releaseTime = (float)(samples.Length - releaseIndex) / SampleRate * 1000;

        // Transient strength
        float transientStrength = peak / Math.Max(rms, 0.001f);

        return (attackTime, decayTime, sustainLevel, releaseTime, transientStrength);
    }

    #endregion

    #region Mood and Genre Analysis

    private (float Energy, float Valence, float Arousal, float Dominance, string PredictedMood, float Confidence) AnalyzeMood(AudioAnalysis analysis)
    {
        // Energy calculation
        float energy = (analysis.Volume + analysis.SpectralCentroid / 10000f + analysis.Tempo / 200f) / 3f;
        energy = Math.Clamp(energy, 0, 1);

        // Valence calculation (positive/negative emotion)
        float valence = (analysis.SpectralCentroid / 10000f + analysis.SpectralRolloff / 10000f + (1 - analysis.ZeroCrossingRate)) / 3f;
        valence = Math.Clamp(valence, 0, 1);

        // Arousal calculation
        float arousal = (analysis.Volume + analysis.SpectralFlux + analysis.OnsetStrength) / 3f;
        arousal = Math.Clamp(arousal, 0, 1);

        // Dominance calculation
        float dominance = (analysis.BassIntensity + analysis.DynamicRange / 40f + analysis.TransientStrength / 10f) / 3f;
        dominance = Math.Clamp(dominance, 0, 1);

        // Predict mood based on energy and valence
        string predictedMood = PredictMood(energy, valence);
        float confidence = Math.Min(energy + valence + arousal + dominance, 1f) / 4f;

        return (energy, valence, arousal, dominance, predictedMood, confidence);
    }

    private string PredictMood(float energy, float valence)
    {
        if (energy > 0.7f && valence > 0.7f) return "Happy";
        if (energy > 0.7f && valence < 0.3f) return "Angry";
        if (energy < 0.3f && valence > 0.7f) return "Calm";
        if (energy < 0.3f && valence < 0.3f) return "Sad";
        if (energy > 0.5f) return "Energetic";
        if (valence > 0.5f) return "Positive";
        return "Neutral";
    }

    private (string PredictedGenre, float Confidence, Dictionary<string, float> Probabilities) ClassifyGenre(AudioAnalysis analysis)
    {
        var probabilities = new Dictionary<string, float>();

        // Electronic/Dance
        probabilities["Electronic"] = CalculateElectronicProbability(analysis);

        // Rock
        probabilities["Rock"] = CalculateRockProbability(analysis);

        // Classical
        probabilities["Classical"] = CalculateClassicalProbability(analysis);

        // Jazz
        probabilities["Jazz"] = CalculateJazzProbability(analysis);

        // Hip-Hop
        probabilities["Hip-Hop"] = CalculateHipHopProbability(analysis);

        // Find highest probability
        var bestGenre = probabilities.MaxBy(kvp => kvp.Value);
        string predictedGenre = bestGenre.Key ?? "Unknown";
        float confidence = bestGenre.Value;

        return (predictedGenre, confidence, probabilities);
    }

    private float CalculateElectronicProbability(AudioAnalysis analysis)
    {
        float score = 0;
        if (analysis.Tempo > 120) score += 0.3f;
        if (analysis.BassIntensity > 0.5f) score += 0.2f;
        if (analysis.SpectralCentroid > 5000) score += 0.2f;
        if (analysis.ZeroCrossingRate < 0.1f) score += 0.1f;
        if (analysis.Harmonicity < 0.3f) score += 0.2f;
        return Math.Min(score, 1f);
    }

    private float CalculateRockProbability(AudioAnalysis analysis)
    {
        float score = 0;
        if (analysis.Tempo > 80 && analysis.Tempo < 140) score += 0.3f;
        if (analysis.DistortionLevel > 0.1f) score += 0.3f;
        if (analysis.DynamicRange > 20) score += 0.2f;
        if (analysis.ZeroCrossingRate > 0.1f) score += 0.2f;
        return Math.Min(score, 1f);
    }

    private float CalculateClassicalProbability(AudioAnalysis analysis)
    {
        float score = 0;
        if (analysis.Tempo < 120) score += 0.3f;
        if (analysis.Harmonicity > 0.7f) score += 0.3f;
        if (analysis.DynamicRange > 30) score += 0.2f;
        if (analysis.DistortionLevel < 0.05f) score += 0.2f;
        return Math.Min(score, 1f);
    }

    private float CalculateJazzProbability(AudioAnalysis analysis)
    {
        float score = 0;
        if (analysis.Tempo > 60 && analysis.Tempo < 200) score += 0.2f;
        if (analysis.Harmonicity > 0.5f) score += 0.3f;
        if (analysis.RhythmComplexity > 0.5f) score += 0.3f;
        if (analysis.ZeroCrossingRate > 0.05f) score += 0.2f;
        return Math.Min(score, 1f);
    }

    private float CalculateHipHopProbability(AudioAnalysis analysis)
    {
        float score = 0;
        if (analysis.Tempo > 60 && analysis.Tempo < 120) score += 0.3f;
        if (analysis.BassIntensity > 0.6f) score += 0.3f;
        if (analysis.RhythmRegularity > 0.7f) score += 0.2f;
        if (analysis.ZeroCrossingRate < 0.2f) score += 0.2f;
        return Math.Min(score, 1f);
    }

    #endregion

    #region History Management

    private void UpdateHistory(AudioAnalysis analysis)
    {
        // Update beat history
        if (analysis.IsBeatDetected)
        {
            _beatHistory.Add((float)DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
            if (_beatHistory.Count > HistorySize)
            {
                _beatHistory.RemoveAt(0);
            }
        }

        // Update tempo history
        _tempoHistory.Add(analysis.Tempo);
        if (_tempoHistory.Count > HistorySize)
        {
            _tempoHistory.RemoveAt(0);
        }

        // Update energy history
        _energyHistory.Add(analysis.Energy);
        if (_energyHistory.Count > HistorySize)
        {
            _energyHistory.RemoveAt(0);
        }

        // Update valence history
        _valenceHistory.Add(analysis.Valence);
        if (_valenceHistory.Count > HistorySize)
        {
            _valenceHistory.RemoveAt(0);
        }
    }

    #endregion
}
