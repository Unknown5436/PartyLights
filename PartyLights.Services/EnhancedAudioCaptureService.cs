using Microsoft.Extensions.Logging;
using PartyLights.Core.Interfaces;
using PartyLights.Core.Models;
using NAudio.Wave;
using NAudio.Dsp;
using System.Collections.Concurrent;

namespace PartyLights.Services;

/// <summary>
/// Enhanced audio capture service with advanced analysis capabilities
/// </summary>
public class EnhancedAudioCaptureService : IAudioCaptureService, IDisposable
{
    private readonly ILogger<EnhancedAudioCaptureService> _logger;
    private readonly AdvancedAudioAnalysisService _advancedAnalysisService;
    private readonly ConcurrentQueue<AudioAnalysis> _analysisQueue = new();
    private readonly object _lockObject = new();

    private WasapiLoopbackCapture? _capture;
    private BufferedWaveProvider? _bufferedProvider;
    private WaveFormat? _waveFormat;
    private bool _isCapturing;
    private AudioAnalysis? _currentAnalysis;
    private DateTime _lastAnalysisTime = DateTime.MinValue;
    private readonly float[] _audioBuffer = new float[4096];
    private int _bufferIndex = 0;

    public bool IsCapturing => _isCapturing;
    public AudioAnalysis? CurrentAnalysis => _currentAnalysis;

    public event EventHandler<AudioAnalysisEventArgs>? AnalysisUpdated;

    public EnhancedAudioCaptureService(
        ILogger<EnhancedAudioCaptureService> logger,
        AdvancedAudioAnalysisService advancedAnalysisService)
    {
        _logger = logger;
        _advancedAnalysisService = advancedAnalysisService;
    }

    /// <summary>
    /// Starts audio capture with advanced analysis
    /// </summary>
    public async Task<bool> StartCaptureAsync()
    {
        try
        {
            if (_isCapturing)
            {
                _logger.LogWarning("Audio capture is already running");
                return true;
            }

            _logger.LogInformation("Starting enhanced audio capture");

            // Initialize WASAPI loopback capture
            _capture = new WasapiLoopbackCapture();
            _waveFormat = _capture.WaveFormat;

            // Create buffered provider for processing
            _bufferedProvider = new BufferedWaveProvider(_waveFormat)
            {
                BufferLength = _waveFormat.AverageBytesPerSecond * 2, // 2 seconds buffer
                DiscardOnBufferOverflow = true
            };

            // Set up data available event
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;

            // Start capture
            _capture.StartRecording();
            _isCapturing = true;

            // Start analysis processing task
            _ = Task.Run(ProcessAudioAnalysis);

            _logger.LogInformation("Enhanced audio capture started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting enhanced audio capture");
            return false;
        }
    }

    /// <summary>
    /// Stops audio capture
    /// </summary>
    public async Task StopCaptureAsync()
    {
        try
        {
            if (!_isCapturing)
            {
                _logger.LogWarning("Audio capture is not running");
                return;
            }

            _logger.LogInformation("Stopping enhanced audio capture");

            _isCapturing = false;
            _capture?.StopRecording();

            // Wait for capture to stop
            await Task.Delay(100);

            _logger.LogInformation("Enhanced audio capture stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping enhanced audio capture");
        }
    }

    /// <summary>
    /// Gets the current audio analysis result
    /// </summary>
    public AudioAnalysis? GetCurrentAudioAnalysis()
    {
        lock (_lockObject)
        {
            return _currentAnalysis;
        }
    }

    #region Private Methods

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            if (!_isCapturing || _bufferedProvider == null)
            {
                return;
            }

            // Add data to buffer
            _bufferedProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

            // Convert bytes to float samples
            ConvertBytesToFloatSamples(e.Buffer, e.BytesRecorded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio data");
        }
    }

    private void ConvertBytesToFloatSamples(byte[] buffer, int bytesRecorded)
    {
        if (_waveFormat == null) return;

        int samplesPerFrame = _waveFormat.Channels;
        int bytesPerSample = _waveFormat.BitsPerSample / 8;
        int sampleCount = bytesRecorded / (samplesPerFrame * bytesPerSample);

        for (int i = 0; i < sampleCount && _bufferIndex < _audioBuffer.Length; i++)
        {
            float sample = 0;

            if (_waveFormat.BitsPerSample == 16)
            {
                int byteIndex = i * samplesPerFrame * bytesPerSample;
                short sampleValue = BitConverter.ToInt16(buffer, byteIndex);
                sample = sampleValue / 32768f;
            }
            else if (_waveFormat.BitsPerSample == 32)
            {
                int byteIndex = i * samplesPerFrame * bytesPerSample;
                int sampleValue = BitConverter.ToInt32(buffer, byteIndex);
                sample = sampleValue / 2147483648f;
            }

            _audioBuffer[_bufferIndex] = sample;
            _bufferIndex++;

            // Process buffer when full
            if (_bufferIndex >= _audioBuffer.Length)
            {
                ProcessAudioBuffer();
                _bufferIndex = 0;
            }
        }
    }

    private void ProcessAudioBuffer()
    {
        try
        {
            // Create a copy of the buffer for analysis
            var samples = new float[_audioBuffer.Length];
            Array.Copy(_audioBuffer, samples, _audioBuffer.Length);

            // Perform advanced analysis
            var analysis = _advancedAnalysisService.AnalyzeAudio(samples);

            // Update current analysis
            lock (_lockObject)
            {
                _currentAnalysis = analysis;
            }

            // Queue for event notification
            _analysisQueue.Enqueue(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio buffer");
        }
    }

    private async Task ProcessAudioAnalysis()
    {
        while (_isCapturing)
        {
            try
            {
                // Process queued analyses
                while (_analysisQueue.TryDequeue(out var analysis))
                {
                    // Throttle analysis updates to avoid overwhelming the UI
                    var timeSinceLastUpdate = DateTime.UtcNow - _lastAnalysisTime;
                    if (timeSinceLastUpdate.TotalMilliseconds >= 50) // 20 FPS max
                    {
                        AnalysisUpdated?.Invoke(this, new AudioAnalysisEventArgs(analysis));
                        _lastAnalysisTime = DateTime.UtcNow;
                    }
                }

                await Task.Delay(10); // Small delay to prevent excessive CPU usage
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in audio analysis processing loop");
                await Task.Delay(100);
            }
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _logger.LogInformation("Audio recording stopped");
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Audio recording stopped with exception");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        try
        {
            StopCaptureAsync().Wait(5000);

            _capture?.Dispose();
            _bufferedProvider?.Dispose();

            _logger.LogInformation("Enhanced audio capture service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing enhanced audio capture service");
        }
    }

    #endregion
}
