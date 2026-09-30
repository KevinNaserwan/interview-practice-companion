using InterviewPracticeCompanion.Models;

namespace InterviewPracticeCompanion.Services;

public interface ISessionCoordinator : IAsyncDisposable
{
    Guid? ActiveSessionId { get; }
    bool HasConsent(Guid sessionId, CaptureSource source);
    void GrantConsent(Guid sessionId, CaptureSource source);
    Task StartAsync(Guid sessionId, CaptureSource source, Language language, CancellationToken cancellationToken = default);
    Task StopAsync();
    event EventHandler<TranscriptSegment>? TranscriptReceived;
    event EventHandler<string>? Faulted;
}

public sealed class SessionCoordinator : ISessionCoordinator
{
    private readonly IAudioCaptureService _capture;
    private readonly IMeetsinClient _client;
    private CancellationTokenSource? _sessionCts;
    private readonly SemaphoreSlim _transcription = new(1, 1);
    private (Guid SessionId, CaptureSource Source)? _consent;
    private Language _language;
    public Guid? ActiveSessionId { get; private set; }
    public event EventHandler<TranscriptSegment>? TranscriptReceived;
    public event EventHandler<string>? Faulted;

    public SessionCoordinator(IAudioCaptureService capture, IMeetsinClient client)
    {
        _capture = capture; _client = client;
        _capture.AudioChunkAvailable += AudioAvailable;
        _capture.CaptureFaulted += CaptureFaulted;
    }

    public bool HasConsent(Guid sessionId, CaptureSource source) => _consent == (sessionId, source);
    public void GrantConsent(Guid sessionId, CaptureSource source) => _consent = (sessionId, source);

    public async Task StartAsync(Guid sessionId, CaptureSource source, Language language, CancellationToken cancellationToken = default)
    {
        if (ActiveSessionId is not null) throw new DomainException("session_already_active", "A practice session is already active.");
        if (!HasConsent(sessionId, source)) throw new DomainException("consent_required", "Consent is required before audio capture.");
        ActiveSessionId = sessionId; _language = language;
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try { await _capture.StartAsync(sessionId, source, _sessionCts.Token); }
        catch { ActiveSessionId = null; _sessionCts.Dispose(); _sessionCts = null; throw; }
    }

    public async Task StopAsync()
    {
        var cts = Interlocked.Exchange(ref _sessionCts, null);
        cts?.Cancel();
        await _capture.StopAsync();
        ActiveSessionId = null; _consent = null;
        cts?.Dispose();
    }

    private async void AudioAvailable(object? sender, AudioChunk chunk)
    {
        var cts = _sessionCts;
        if (cts is null || chunk.SessionId != ActiveSessionId || !await _transcription.WaitAsync(0, cts.Token)) return;
        try
        {
            var text = await _client.TranscribeAsync(chunk.Pcm, _language, cts.Token);
            if (text.Length > 0 && chunk.SessionId == ActiveSessionId)
                TranscriptReceived?.Invoke(this, new TranscriptSegment(Guid.NewGuid(), Speaker.Other, text, TimeSpan.Zero, chunk.Duration));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Faulted?.Invoke(this, SafeMessage(ex)); }
        finally { _transcription.Release(); }
    }

    private async void CaptureFaulted(object? sender, string message)
    {
        Faulted?.Invoke(this, message);
        if (message.Contains("disconnected", StringComparison.OrdinalIgnoreCase)) await StopAsync();
    }

    private static string SafeMessage(Exception exception) => exception is ServiceException or DomainException ? exception.Message : "Audio processing failed.";
    public async ValueTask DisposeAsync() { _capture.AudioChunkAvailable -= AudioAvailable; _capture.CaptureFaulted -= CaptureFaulted; await StopAsync(); _transcription.Dispose(); }
}
