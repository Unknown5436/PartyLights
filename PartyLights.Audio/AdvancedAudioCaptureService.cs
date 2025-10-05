using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using NAudio.Wave;
using System.Collections.Concurrent;

namespace PartyLights.Audio;

/// <summary>
/// Advanced audio capture service with real-time analysis
/// </summary>
public class AdvancedAudioCaptureService : IAudioCaptureService, IDisposable
{
    private readonly ILogger<AdvancedAudioCaptureService> _logger;
    private readonly AudioAnalyzer _audioAnalyzer;
    private readonly BeatDetector _beatDetector;
    private readonly TempoEstimator _tempoEstimator;

    private bool _isCapturing;
    private WasapiLoopbackCapture? _capture;
    private BufferedWaveProvider? _bufferedWaveProvider;
    private WaveFormat _waveFormat;
    private float[] _sampleBuffer;
    private int _sampleRate = 44100;
    private int _fftSize = 1024;
    private int _bufferSize = 4096;
    private float _volumeThreshold = 0.01f;
    private float _beatSensitivity = 0.3f;

    public AdvancedAudioCaptureService(ILogger<AdvancedAudioCaptureService> logger)
    {
        _logger = logger;
        _audioAnalyzer = new AudioAnalyzer(logger.CreateLogger<AudioAnalyzer>(), _fftSize, _sampleRate);
        _beatDetector = new BeatDetector(logger.CreateLogger<BeatDetector>());
        _tempoEstimator = new TempoEstimator(logger.CreateLogger<TempoEstimator>());

        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, 2); // Stereo
        _sampleBuffer = new float[_fftSize];
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

            _logger.LogInformation("Starting advanced audio capture");

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

            // Start capture
            _capture.StartRecording();
            _isCapturing = true;

            // Start analysis task
            _ = Task.Run(AnalysisLoop);

            _logger.LogInformation("Advanced audio capture started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start advanced audio capture");
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

            _logger.LogInformation("Stopping advanced audio capture");

            _isCapturing = false;
            _capture?.StopRecording();
            _capture?.Dispose();
            _capture = null;
            _bufferedWaveProvider = null;

            await Task.CompletedTask;
            _logger.LogInformation("Advanced audio capture stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping advanced audio capture");
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
        _logger.LogInformation("Starting advanced audio analysis loop");

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
                _logger.LogError(ex, "Error in advanced audio analysis loop");
                await Task.Delay(100);
            }
        }

        _logger.LogInformation("Advanced audio analysis loop stopped");
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

            // Perform audio analysis
            var analysisResult = _audioAnalyzer.AnalyzeSamples(samples);

            // Detect beats
            var beatResult = _beatDetector.DetectBeat(analysisResult.Volume);

            // Estimate tempo
            var tempo = beatResult.BeatDetected ?
                _tempoEstimator.EstimateTempo(DateTime.UtcNow) :
                _tempoEstimator.GetCurrentTempo();

            // Create comprehensive analysis
            var analysis = new AudioAnalysis
            {
                Volume = analysisResult.Volume,
                FrequencyBands = analysisResult.FrequencyBands,
                BeatIntensity = beatResult.BeatIntensity,
                Tempo = tempo,
                SpectralCentroid = analysisResult.SpectralCentroid,
                Timestamp = analysisResult.Timestamp
            };

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing audio");
            return null;
        }
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
        _volumeThreshold = Math.Clamp(volumeThreshold, 0.001f, 0.1f);
        _beatSensitivity = Math.Clamp(beatSensitivity, 0.1f, 2.0f);

        _beatDetector.UpdateParameters(_volumeThreshold, _beatSensitivity);

        _logger.LogDebug("Updated analysis parameters: VolumeThreshold={VolumeThreshold}, BeatSensitivity={BeatSensitivity}",
            _volumeThreshold, _beatSensitivity);
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
            VolumeThreshold = _volumeThreshold,
            BeatSensitivity = _beatSensitivity,
            CurrentTempo = _tempoEstimator.GetCurrentTempo()
        };
    }

    public void Dispose()
    {
        StopCaptureAsync().Wait();
        _capture?.Dispose();
        _bufferedWaveProvider = null;
    }
}

/// <summary>
/// Audio statistics
/// </summary>
public class AudioStatistics
{
    public bool IsCapturing { get; set; }
    public int SampleRate { get; set; }
    public int FftSize { get; set; }
    public int BufferSize { get; set; }
    public float VolumeThreshold { get; set; }
    public float BeatSensitivity { get; set; }
    public float CurrentTempo { get; set; }
}

/// <summary>
/// Represents an audio device
/// </summary>
public class AudioDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsEnabled { get; set; }
    public int Channels { get; set; }
    public int SampleRate { get; set; }
    public int BitsPerSample { get; set; }
}
