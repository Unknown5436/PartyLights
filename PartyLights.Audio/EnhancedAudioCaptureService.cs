using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using NAudio.Wave;
using System.Collections.Concurrent;

namespace PartyLights.Audio;

/// <summary>
/// Enhanced audio capture service with advanced analysis pipeline
/// </summary>
public class EnhancedAudioCaptureService : IAudioCaptureService, IDisposable
{
    private readonly ILogger<EnhancedAudioCaptureService> _logger;
    private readonly AudioAnalysisPipeline _analysisPipeline;
    private readonly AudioAnalysisConfig _config;

    private bool _isCapturing;
    private WasapiLoopbackCapture? _capture;
    private BufferedWaveProvider? _bufferedWaveProvider;
    private WaveFormat _waveFormat;
    private float[] _sampleBuffer;
    private int _sampleRate = 44100;
    private int _fftSize = 1024;
    private int _bufferSize = 4096;
    private int _frameCount = 0;

    public EnhancedAudioCaptureService(ILogger<EnhancedAudioCaptureService> logger)
    {
        _logger = logger;
        _config = new AudioAnalysisConfig
        {
            FftSize = _fftSize,
            SampleRate = _sampleRate,
            BeatSensitivity = 1.5f,
            MinBeatInterval = 0.2,
            FrequencyBands = 12,
            EnableSpectralAnalysis = true,
            EnableRhythmAnalysis = true,
            EnableMoodAnalysis = true
        };

        _analysisPipeline = new AudioAnalysisPipeline(logger.CreateLogger<AudioAnalysisPipeline>(), _config);
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, 2); // Stereo
        _sampleBuffer = new float[_fftSize];

        // Subscribe to analysis pipeline events
        _analysisPipeline.AnalysisCompleted += OnAnalysisCompleted;
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
                _logger.LogWarning("Enhanced audio capture is already running");
                return true;
            }

            _logger.LogInformation("Starting enhanced audio capture with analysis pipeline");

            // Initialize WASAPI loopback capture
            _capture = new WasapiLoopbackCapture();
            _capture.WaveFormat = _waveFormat;

            // Initialize buffered wave provider
            _bufferedWaveProvider = new BufferedWaveProvider(_waveFormat)
            {
                BufferLength = _bufferSize * 4,
                DiscardOnBufferOverflow = true
            };

            // Set up event handlers
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;

            // Start analysis pipeline
            _analysisPipeline.Start();

            // Start capture
            _capture.StartRecording();
            _isCapturing = true;

            _logger.LogInformation("Enhanced audio capture started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start enhanced audio capture");
            return false;
        }
    }

    public async Task StopCaptureAsync()
    {
        try
        {
            if (!_isCapturing)
            {
                _logger.LogWarning("Enhanced audio capture is not running");
                return;
            }

            _logger.LogInformation("Stopping enhanced audio capture");

            _isCapturing = false;
            _capture?.StopRecording();
            _capture?.Dispose();
            _capture = null;
            _bufferedWaveProvider = null;

            // Stop analysis pipeline
            await _analysisPipeline.StopAsync();

            await Task.CompletedTask;
            _logger.LogInformation("Enhanced audio capture stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping enhanced audio capture");
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

            // Process audio frames
            ProcessAudioFrames();
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
            _logger.LogError(e.Exception, "Enhanced audio capture stopped due to error");
        }
        else
        {
            _logger.LogInformation("Enhanced audio capture stopped");
        }
    }

    private void ProcessAudioFrames()
    {
        if (_bufferedWaveProvider == null)
            return;

        try
        {
            // Process frames while we have enough data
            while (_bufferedWaveProvider.BufferedBytes >= _fftSize * 4)
            {
                // Read samples from buffer
                var samples = new float[_fftSize];
                var bytesRead = _bufferedWaveProvider.Read(samples, 0, _fftSize);

                if (bytesRead < _fftSize * 4)
                    break;

                // Create audio frame
                var frame = new AudioFrame
                {
                    Id = $"frame_{++_frameCount}",
                    Samples = samples,
                    Timestamp = DateTime.UtcNow,
                    SampleRate = _sampleRate,
                    Channels = 2
                };

                // Add frame to analysis pipeline
                _analysisPipeline.AddAudioFrame(frame);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio frames");
        }
    }

    private void OnAnalysisCompleted(object? sender, AudioAnalysisResultEventArgs e)
    {
        try
        {
            // Convert enhanced analysis result to legacy format for compatibility
            var legacyAnalysis = ConvertToLegacyAnalysis(e.Result);
            CurrentAnalysis = legacyAnalysis;

            // Fire legacy event
            AnalysisUpdated?.Invoke(this, new AudioAnalysisEventArgs(legacyAnalysis));

            _logger.LogDebug("Audio analysis completed: {Summary}", e.Result.GetSummary());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling analysis completion");
        }
    }

    private AudioAnalysis ConvertToLegacyAnalysis(AudioAnalysisResult result)
    {
        return new AudioAnalysis
        {
            Volume = result.Volume,
            FrequencyBands = result.FrequencyBands,
            BeatIntensity = result.BeatIntensity,
            Tempo = result.Tempo,
            SpectralCentroid = result.SpectralCentroid,
            Timestamp = result.Timestamp
        };
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
                    Type = "WASAPI"
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
        _config.BeatSensitivity = Math.Clamp(beatSensitivity, 0.1f, 3.0f);
        _logger.LogDebug("Updated analysis parameters: BeatSensitivity={BeatSensitivity}", _config.BeatSensitivity);
    }

    /// <summary>
    /// Gets current audio statistics
    /// </summary>
    public AudioStatistics GetAudioStatistics()
    {
        return new AudioStatistics
        {
            IsCapturing = _isCapturing,
            SampleRate = _sampleRate,
            FftSize = _fftSize,
            BufferSize = _bufferSize,
            VolumeThreshold = 0.01f, // Legacy compatibility
            BeatSensitivity = _config.BeatSensitivity,
            CurrentTempo = CurrentAnalysis?.Tempo ?? 120.0f
        };
    }

    /// <summary>
    /// Gets the current analysis configuration
    /// </summary>
    public AudioAnalysisConfig GetAnalysisConfig()
    {
        return _config;
    }

    /// <summary>
    /// Updates the analysis configuration
    /// </summary>
    public void UpdateAnalysisConfig(AudioAnalysisConfig config)
    {
        _config.FftSize = config.FftSize;
        _config.SampleRate = config.SampleRate;
        _config.BeatSensitivity = config.BeatSensitivity;
        _config.MinBeatInterval = config.MinBeatInterval;
        _config.FrequencyBands = config.FrequencyBands;
        _config.EnableSpectralAnalysis = config.EnableSpectralAnalysis;
        _config.EnableRhythmAnalysis = config.EnableRhythmAnalysis;
        _config.EnableMoodAnalysis = config.EnableMoodAnalysis;

        _logger.LogInformation("Analysis configuration updated");
    }

    public void Dispose()
    {
        StopCaptureAsync().Wait();
        _capture?.Dispose();
        _bufferedWaveProvider = null;
        _analysisPipeline?.StopAsync().Wait();
    }
}
