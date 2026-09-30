using System.IO;
using InterviewPracticeCompanion.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace InterviewPracticeCompanion.Services;


public sealed class AudioCaptureService : IAudioCaptureService
{
    private readonly object _gate = new();
    private IWaveIn? _microphone;
    private WasapiLoopbackCapture? _system;
    private Guid _sessionId;
    private CaptureSource _source;
    private MemoryStream _microphonePcm = new(), _systemPcm = new();
    private DateTimeOffset _lastSignal;
    private Timer? _batchTimer, _silenceTimer;

    public event EventHandler<AudioChunk>? AudioChunkAvailable;
    public event EventHandler<float>? AudioLevelChanged;
    public event EventHandler<string>? CaptureFaulted;

    public Task<IReadOnlyList<AudioDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var devices = new List<AudioDevice>();
        for (var i = 0; i < WaveIn.DeviceCount; i++) devices.Add(new AudioDevice($"mic:{i}", WaveIn.GetCapabilities(i).ProductName, CaptureSource.Microphone));
        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)) devices.Add(new AudioDevice(device.ID, device.FriendlyName, CaptureSource.System));
        return Task.FromResult<IReadOnlyList<AudioDevice>>(devices);
    }

    public Task StartAsync(Guid sessionId, CaptureSource source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_microphone is not null || _system is not null) throw new DomainException("capture_already_active", "Audio capture is already active.");
            _sessionId = sessionId;
            _source = source;
            _lastSignal = DateTimeOffset.UtcNow;
            try
            {
                if (source is CaptureSource.Microphone or CaptureSource.Both)
                {
                    if (WaveIn.DeviceCount == 0) throw new DomainException("no_default_device", "No microphone is available.");
                    var microphone = new WaveInEvent { WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
                    microphone.DataAvailable += MicrophoneData;
                    microphone.RecordingStopped += CaptureStopped;
                    _microphone = microphone;
                }
                if (source is CaptureSource.System or CaptureSource.Both)
                {
                    var system = new WasapiLoopbackCapture();
                    system.DataAvailable += SystemData;
                    system.RecordingStopped += CaptureStopped;
                    _system = system;
                }
                _microphone?.StartRecording();
                _system?.StartRecording();
                _batchTimer = new Timer(_ => FlushBatch(), null, 500, 500);
                _silenceTimer = new Timer(_ => CheckSilence(), null, 1000, 1000);
            }
            catch (UnauthorizedAccessException) { ResetCapture(); throw new DomainException("access_denied", "Microphone access was denied."); }
            catch (DomainException) { ResetCapture(); throw; }
            catch (Exception) { ResetCapture(); throw new DomainException("unsupported_format", "The selected audio format is unsupported."); }
        }
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            _batchTimer?.Dispose(); _batchTimer = null;
            _silenceTimer?.Dispose(); _silenceTimer = null;
            try { _microphone?.StopRecording(); } catch { }
            try { _system?.StopRecording(); } catch { }
            ResetCapture();
            while (_chunks.Reader.TryRead(out _)) { }
        }
        return Task.CompletedTask;
    }

    private void MicrophoneData(object? sender, WaveInEventArgs e) => Append(_microphonePcm, e.Buffer.AsSpan(0, e.BytesRecorded), ((IWaveIn)sender!).WaveFormat);
    private void SystemData(object? sender, WaveInEventArgs e) => Append(_systemPcm, e.Buffer.AsSpan(0, e.BytesRecorded), ((WasapiLoopbackCapture)sender!).WaveFormat);

    private void Append(MemoryStream destination, ReadOnlySpan<byte> input, WaveFormat sourceFormat)
    {
        byte[] pcm;
        if (sourceFormat.SampleRate == 16000 && sourceFormat.BitsPerSample == 16 && sourceFormat.Channels == 1) pcm = input.ToArray();
        else
        {
            using var source = new RawSourceWaveStream(new MemoryStream(input.ToArray(), false), sourceFormat);
            using var resampler = new MediaFoundationResampler(source, new WaveFormat(16000, 16, 1)) { ResamplerQuality = 60 };
            using var output = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0) output.Write(buffer, 0, read);
            pcm = output.ToArray();
        }
        var level = Peak(pcm);
        if (level > 0.01f) _lastSignal = DateTimeOffset.UtcNow;
        AudioLevelChanged?.Invoke(this, level);
        lock (_gate) destination.Write(pcm);
    }

    private void FlushBatch()
    {
        AudioChunk? chunk = null;
        lock (_gate)
        {
            var mic = Drain(_microphonePcm); var system = Drain(_systemPcm);
            var pcm = _source == CaptureSource.Both ? AudioMixer.MixPcm16(mic, system) : _source == CaptureSource.Microphone ? mic : system;
            if (pcm.Length > 0) chunk = new AudioChunk(_sessionId, pcm, TimeSpan.FromSeconds(pcm.Length / 32000d));
        }
        if (chunk is not null) AudioChunkAvailable?.Invoke(this, chunk);
    }


    private void CheckSilence() { if (DateTimeOffset.UtcNow - _lastSignal >= TimeSpan.FromSeconds(10)) CaptureFaulted?.Invoke(this, "No audio signal detected for 10 seconds."); }
    private void CaptureStopped(object? sender, StoppedEventArgs e) { if (e.Exception is not null) CaptureFaulted?.Invoke(this, "Audio device disconnected."); }
    private static byte[] Drain(MemoryStream stream) { var bytes = stream.ToArray(); stream.SetLength(0); return bytes; }
    private static float Peak(byte[] pcm) { var peak = 0; for (var i = 0; i + 1 < pcm.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(pcm, i))); return peak / 32768f; }
    private void ResetCapture() { _microphone?.Dispose(); _system?.Dispose(); _microphone = null; _system = null; _microphonePcm.SetLength(0); _systemPcm.SetLength(0); }
    public async ValueTask DisposeAsync() { await StopAsync(); _batchTimer?.Dispose(); _silenceTimer?.Dispose(); }
}
