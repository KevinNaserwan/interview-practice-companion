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

    [Fact] public void TranscriptLimitPreservesNewestQuestion()
    {
        var latest = "What is your greatest achievement?";
        var limited = MeetsinClient.LimitTranscript(new string('x', 13000) + latest, 12000);
        Assert.Equal(12000, limited.Length);
        Assert.EndsWith(latest, limited);
    }
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void StartRequiresCredentialAndConsent(bool credential, bool consent, bool expected) =>
        Assert.Equal(expected, SessionReadiness.CanStart(credential, consent, SessionStatus.Idle));

    [Fact] public void GenerationRequiresCredentialAndModeInput()
    {
        Assert.False(SessionReadiness.CanGenerate(false, SessionStatus.Idle, SessionMode.Coding, "", "prompt"));
        Assert.False(SessionReadiness.CanGenerate(true, SessionStatus.Idle, SessionMode.Coding, "question", ""));
        Assert.True(SessionReadiness.CanGenerate(true, SessionStatus.Idle, SessionMode.Coding, "", "prompt"));
    }

    private sealed class FakeCapture : IAudioCaptureService
    {
        public event EventHandler<AudioChunk>? AudioChunkAvailable { add { } remove { } }
        public event EventHandler<float>? AudioLevelChanged { add { } remove { } }
        public event EventHandler<string>? CaptureFaulted { add { } remove { } }
        public Task<IReadOnlyList<AudioDevice>> GetDevicesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AudioDevice>>([]);
        public Task StartAsync(Guid sessionId, CaptureSource source, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class FakeClient : IMeetsinClient
    {
        public Task<string> TranscribeAsync(ReadOnlyMemory<byte> pcm, Language language, CancellationToken cancellationToken) => Task.FromResult("");
        public Task<AnswerSuggestion> GenerateAnswerAsync(GenerateAnswerRequest request, CancellationToken cancellationToken) => Task.FromResult(new AnswerSuggestion("", [], null, null));
    }
}
