using System.Net;
using InterviewPracticeCompanion.Models;
using InterviewPracticeCompanion.Services;
using Xunit;

namespace InterviewPracticeCompanion.Tests;

public sealed class BehaviorTests
{
    [Fact] public void MixerSaturatesWithoutOverflow()
    {
        var high = BitConverter.GetBytes(short.MaxValue);
        Assert.Equal(short.MaxValue, BitConverter.ToInt16(AudioMixer.MixPcm16(high, high)));
        var low = BitConverter.GetBytes(short.MinValue);
        Assert.Equal(short.MinValue, BitConverter.ToInt16(AudioMixer.MixPcm16(low, low)));
    }

    [Fact] public void BufferDropsOldestBeyondTenSeconds()
    {
        var buffer = new AudioChunkBuffer(TimeSpan.FromSeconds(10));
        var first = new AudioChunk(Guid.NewGuid(), new byte[2], TimeSpan.FromMilliseconds(500));
        buffer.Add(first);
        for (var i = 0; i < 20; i++) buffer.Add(new AudioChunk(Guid.NewGuid(), new byte[2], TimeSpan.FromMilliseconds(500)));
        Assert.Equal(20, buffer.Count);
        Assert.True(buffer.TryTake(out var oldest));
        Assert.NotEqual(first.SessionId, oldest!.SessionId);
    }
    [Fact] public void SpeechWindowWaitsForUsableAudioAndDrainsCleanly()
    {
        var id = Guid.NewGuid();
        var window = new SpeechWindowAccumulator(TimeSpan.FromSeconds(3));
        Assert.False(window.Add(new AudioChunk(id, new byte[] { 1, 2 }, TimeSpan.FromSeconds(1))));
        Assert.False(window.Add(new AudioChunk(id, new byte[] { 3, 4 }, TimeSpan.FromSeconds(1))));
        Assert.True(window.Add(new AudioChunk(id, new byte[] { 5, 6 }, TimeSpan.FromSeconds(1))));
        var chunk = window.Drain(id);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, chunk.Pcm.ToArray());
        Assert.Equal(TimeSpan.FromSeconds(3), chunk.Duration);
        Assert.False(window.Add(new AudioChunk(id, new byte[] { 7, 8 }, TimeSpan.FromSeconds(1))));
    }

    [Fact] public void SpeechGateRejectsSilenceAndAcceptsVoiceLevelSignal()
    {
        Assert.False(AudioSignal.ContainsSpeech(new byte[32000]));
        var speech = new byte[32000];
        for (var i = 0; i < speech.Length; i += 2) BitConverter.TryWriteBytes(speech.AsSpan(i, 2), (short)(i % 4 == 0 ? 1800 : -1800));
        Assert.True(AudioSignal.ContainsSpeech(speech));
    }

    [Theory]
    [InlineData("http://ai.meetsin.id/v1", false, "invalid_api_url")]
    [InlineData("https://example.com/v1", false, "custom_host_unacknowledged")]
    public void EndpointValidationRejectsUnsafeTargets(string url, bool acknowledged, string code)
    {
        var settings = new AppSettings { ApiBaseUrl = url, AcknowledgeCustomHost = acknowledged };
        Assert.Equal(code, Assert.Throws<DomainException>(() => SettingsService.ValidateEndpoint(settings)).Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ServiceErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, ServiceErrorKind.PayloadTooLarge)]
    [InlineData(HttpStatusCode.TooManyRequests, ServiceErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ServiceErrorKind.Unavailable)]
    [InlineData(HttpStatusCode.InternalServerError, ServiceErrorKind.Other)]
    public void MapsHttpFailures(HttpStatusCode status, ServiceErrorKind expected) => Assert.Equal(expected, MeetsinClient.MapError(status).Kind);

    [Fact] public async Task CoordinatorRequiresConsentAndStopIsIdempotent()
    {
        var capture = new FakeCapture(); var coordinator = new SessionCoordinator(capture, new FakeClient()); var id = Guid.NewGuid();
        Assert.Equal("consent_required", (await Assert.ThrowsAsync<DomainException>(() => coordinator.StartAsync(id, CaptureSource.Microphone, Language.Id))).Code);
        coordinator.GrantConsent(id, CaptureSource.Microphone);
        await coordinator.StartAsync(id, CaptureSource.Microphone, Language.Id);
        Assert.Equal("session_already_active", (await Assert.ThrowsAsync<DomainException>(() => coordinator.StartAsync(Guid.NewGuid(), CaptureSource.Microphone, Language.Id))).Code);
        await coordinator.StopAsync(); await coordinator.StopAsync();
        Assert.Null(coordinator.ActiveSessionId);
    }
    [Fact] public async Task CoordinatorPreservesAudioSpeakerIdentity()
    {
        var capture = new FakeCapture();
        var coordinator = new SessionCoordinator(capture, new FakeClient("pertanyaan"));
        var id = Guid.NewGuid();
        var received = new TaskCompletionSource<TranscriptSegment>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.TranscriptReceived += (_, segment) => received.TrySetResult(segment);
        coordinator.GrantConsent(id, CaptureSource.Both);
        await coordinator.StartAsync(id, CaptureSource.Both, Language.Id);
        capture.Emit(new AudioChunk(id, VoicePcm(96000), TimeSpan.FromSeconds(3), Speaker.Other));
        var segment = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(Speaker.Other, segment.Speaker);
        Assert.Equal("pertanyaan", segment.Text);
        await coordinator.DisposeAsync();
    }


    [Fact] public void TranscriptLimitPreservesNewestQuestion()
    {
        var latest = "What is your greatest achievement?";
        var limited = MeetsinClient.LimitTranscript(new string('x', 13000) + latest, 12000);
        Assert.Equal(12000, limited.Length);
        Assert.EndsWith(latest, limited);
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void StartRequiresSessionConsent(bool consent, bool expected) =>
        Assert.Equal(expected, SessionReadiness.CanStart(consent, SessionStatus.Idle));

    [Fact] public void GenerationRequiresCredentialAndModeInput()
    {
        Assert.False(SessionReadiness.CanGenerate(false, SessionStatus.Idle, SessionMode.Coding, "", "prompt"));
        Assert.False(SessionReadiness.CanGenerate(true, SessionStatus.Idle, SessionMode.Coding, "question", ""));
        Assert.True(SessionReadiness.CanGenerate(true, SessionStatus.Idle, SessionMode.Coding, "", "prompt"));
    }

    [Theory]
    [InlineData("Ceritakan pengalaman memimpin tim", true)]
    [InlineData("How do you handle conflict?", true)]
    [InlineData("Selamat pagi", false)]
    [InlineData("Thank you", false)]
    public void DetectsInterviewQuestionsWithoutGreetingHallucinations(string text, bool expected) =>
        Assert.Equal(expected, SessionReadiness.IsLikelyInterviewQuestion(text));

    private static byte[] VoicePcm(int length)
    {
        var pcm = new byte[length];
        for (var i = 0; i < pcm.Length; i += 2) BitConverter.TryWriteBytes(pcm.AsSpan(i, 2), (short)(i % 4 == 0 ? 1800 : -1800));
        return pcm;
    }

    private sealed class FakeCapture : IAudioCaptureService
    {
        public event EventHandler<AudioChunk>? AudioChunkAvailable;
        public event EventHandler<float>? AudioLevelChanged;
        public event EventHandler<string>? CaptureFaulted;
        public Task<IReadOnlyList<AudioDevice>> GetDevicesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AudioDevice>>([]);
        public Task StartAsync(Guid sessionId, CaptureSource source, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void Emit(AudioChunk chunk) => AudioChunkAvailable?.Invoke(this, chunk);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class FakeClient(string transcript = "") : IMeetsinClient
    {
        public Task<string> TranscribeAsync(ReadOnlyMemory<byte> pcm, Language language, CancellationToken cancellationToken) => Task.FromResult(transcript);
        public Task<AnswerSuggestion> GenerateAnswerAsync(GenerateAnswerRequest request, CancellationToken cancellationToken) => Task.FromResult(new AnswerSuggestion("", [], null, null));
    }
}
