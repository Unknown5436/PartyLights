using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using NAudio.Wave;
using NAudio.Dsp;
using System.Collections.Concurrent;

namespace PartyLights.Services;

/// <summary>
/// Service for audio capture and analysis
/// </summary>
public class AudioCaptureService : IAudioCaptureService
{
    private readonly ILogger<AudioCaptureService> _logger;
    private readonly ConcurrentQueue<AudioAnalysis> _analysisQueue = new();
    private readonly object _lockObject = new();

    private bool _isCapturing;
    private WasapiLoopbackCapture? _capture;
    private BufferedWaveProvider? _bufferedWaveProvider;
    private WaveFormat _waveFormat;
    private float[] _fftBuffer;
    private float[] _windowBuffer;
    private int _sampleRate = 44100;
    private int _fftSize = 1024;
    private int _bufferSize = 4096;
    private float _volumeThreshold = 0.01f;
    private DateTime _lastBeatTime = DateTime.MinValue;
    private float _beatSensitivity = 0.3f;

    public AudioCaptureService(ILogger<AudioCaptureService> logger)
    {
        _logger = logger;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, 2); // Stereo
        _fftBuffer = new float[_fftSize];
        _windowBuffer = new float[_fftSize];
    }

    public bool IsCapturing => _isCapturing;
    public AudioAnalysis? CurrentAnalysis { get; private set; }

    public event EventHandler<AudioAnalysisEventArgs>? AnalysisUpdated;

    public async Task<bool> StartCaptureAsync()
    {
        try
        {
            if (_isCapturing)
            {
                _logger.LogWarning("Audio capture is already running");
                return true;
            }

            _logger.LogInformation("Starting audio capture");

            // Initialize WASAPI loopback capture
            _capture = new WasapiLoopbackCapture();
            _capture.WaveFormat = _waveFormat;

            // Initialize buffered wave provider
            _bufferedWaveProvider = new BufferedWaveProvider(_waveFormat)
            {
                BufferLength = _bufferSize * 4,
                DiscardOnBufferOverflow = true
            };

            // Set up data available event
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;

            // Start capture
            _capture.StartRecording();
            _isCapturing = true;

            // Start analysis task
            _ = Task.Run(AnalysisLoop);

            _logger.LogInformation("Audio capture started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start audio capture");
            return false;
        }
    }

    public async Task StopCaptureAsync()
    {
        try
        {
            if (!_isCapturing)
            {
                _logger.LogWarning("Audio capture is not running");
                return;
            }

            _logger.LogInformation("Stopping audio capture");

            _isCapturing = false;
            _capture?.StopRecording();
            _capture?.Dispose();
            _capture = null;
            _bufferedWaveProvider = null;

            await Task.CompletedTask;
            _logger.LogInformation("Audio capture stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping audio capture");
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_bufferedWaveProvider == null || !_isCapturing)
            return;

        try
        {
            // Add data to buffer
            _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio data");
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Audio capture stopped due to error");
        }
        else
        {
            _logger.LogInformation("Audio capture stopped");
        }
    }

    private async Task AnalysisLoop()
    {
        _logger.LogInformation("Starting audio analysis loop");

        while (_isCapturing)
        {
            try
            {
                if (_bufferedWaveProvider != null && _bufferedWaveProvider.BufferedBytes >= _fftSize * 4)
                {
                    var analysis = await AnalyzeAudioAsync();
                    if (analysis != null)
                    {
                        CurrentAnalysis = analysis;
                        AnalysisUpdated?.Invoke(this, new AudioAnalysisEventArgs(analysis));
                    }
                }

                await Task.Delay(50); // 20 FPS analysis
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in audio analysis loop");
                await Task.Delay(100);
            }
        }

        _logger.LogInformation("Audio analysis loop stopped");
    }

    private async Task<AudioAnalysis?> AnalyzeAudioAsync()
    {
        if (_bufferedWaveProvider == null)
            return null;

        try
        {
            // Read samples from buffer
            var samples = new float[_fftSize];
            var bytesRead = _bufferedWaveProvider.Read(samples, 0, _fftSize);

            if (bytesRead < _fftSize * 4)
                return null;

            // Convert bytes to float samples
            for (int i = 0; i < _fftSize; i++)
            {
                _fftBuffer[i] = samples[i];
            }

            // Apply window function (Hanning window)
            ApplyWindowFunction(_fftBuffer, _windowBuffer);

            // Perform FFT
            var fftResult = new Complex[_fftSize];
            for (int i = 0; i < _fftSize; i++)
            {
                fftResult[i] = new Complex(_windowBuffer[i], 0);
            }

            FastFourierTransform.FFT(true, (int)Math.Log2(_fftSize), fftResult);

            // Calculate frequency bands
            var frequencyBands = CalculateFrequencyBands(fftResult);

            // Calculate volume (RMS)
            var volume = CalculateRMS(_fftBuffer);

            // Detect beats
            var beatIntensity = DetectBeat(volume);

            // Calculate spectral centroid
            var spectralCentroid = CalculateSpectralCentroid(fftResult);

            // Estimate tempo (simplified)
            var tempo = EstimateTempo(beatIntensity);

            var analysis = new AudioAnalysis
            {
                Volume = volume,
                FrequencyBands = frequencyBands,
                BeatIntensity = beatIntensity,
                Tempo = tempo,
                SpectralCentroid = spectralCentroid,
                Timestamp = DateTime.UtcNow
            };

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing audio");
            return null;
        }
    }

    private void ApplyWindowFunction(float[] input, float[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            // Hanning window
            var window = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (input.Length - 1)));
            output[i] = input[i] * window;
        }
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

    private float DetectBeat(float volume)
    {
        var now = DateTime.UtcNow;
        var timeSinceLastBeat = (now - _lastBeatTime).TotalSeconds;

        // Simple beat detection based on volume threshold and timing
        if (volume > _volumeThreshold && timeSinceLastBeat > 0.2) // Minimum 200ms between beats
        {
            _lastBeatTime = now;
            return Math.Min(volume * _beatSensitivity, 1.0f);
        }

        return 0;
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

    private float EstimateTempo(float beatIntensity)
    {
        // Simplified tempo estimation
        // In a real implementation, this would use more sophisticated algorithms
        var now = DateTime.UtcNow;
        var timeSinceLastBeat = (now - _lastBeatTime).TotalSeconds;

        if (beatIntensity > 0 && timeSinceLastBeat > 0.1)
        {
            return 60.0f / (float)timeSinceLastBeat; // BPM
        }

        return 120.0f; // Default tempo
    }

    /// <summary>
    /// Gets available audio devices
    /// </summary>
    public static List<AudioDevice> GetAvailableAudioDevices()
    {
        var devices = new List<AudioDevice>();

        try
        {
            // Get WASAPI devices
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                devices.Add(new AudioDevice
                {
                    Id = i.ToString(),
                    Name = capabilities.ProductName,
                    Type = "Output"
                });
            }

            // Get DirectSound devices
            for (int i = 0; i < DirectSoundOut.DeviceCount; i++)
            {
                var capabilities = DirectSoundOut.GetCapabilities(i);
                devices.Add(new AudioDevice
                {
                    Id = $"DS{i}",
                    Name = capabilities.Description,
                    Type = "DirectSound"
                });
            }
        }
        catch (Exception)
        {
            // Device enumeration failed
        }

        return devices;
    }

    /// <summary>
    /// Sets the audio device for capture
    /// </summary>
    public async Task<bool> SetAudioDeviceAsync(string deviceId)
    {
        try
        {
            if (_isCapturing)
            {
                await StopCaptureAsync();
            }

            // TODO: Implement device selection
            // This would require recreating the capture with the selected device
            _logger.LogInformation("Audio device set to: {DeviceId}", deviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting audio device");
            return false;
        }
    }

    /// <summary>
    /// Updates analysis parameters
    /// </summary>
    public void UpdateAnalysisParameters(float volumeThreshold, float beatSensitivity)
    {
        _volumeThreshold = Math.Clamp(volumeThreshold, 0.001f, 0.1f);
        _beatSensitivity = Math.Clamp(beatSensitivity, 0.1f, 2.0f);

        _logger.LogDebug("Updated analysis parameters: VolumeThreshold={VolumeThreshold}, BeatSensitivity={BeatSensitivity}",
            _volumeThreshold, _beatSensitivity);
    }

    public void Dispose()
    {
        StopCaptureAsync().Wait();
        _capture?.Dispose();
        _bufferedWaveProvider = null;
    }
}

/// <summary>
/// Represents an audio device
/// </summary>
public class AudioDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
