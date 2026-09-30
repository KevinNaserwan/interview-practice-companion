namespace InterviewPracticeCompanion.Models;

public enum Language { Id, En }
public enum SessionMode { Behavioral, Coding }
public enum SessionStatus { Idle, Listening, Transcribing, Generating, Error }
public enum CaptureSource { Microphone, System, Both }
public enum Speaker { Unknown, User, Other }

public sealed record TranscriptSegment(Guid Id, Speaker Speaker, string Text, TimeSpan StartedAt, TimeSpan EndedAt);
public sealed record AnswerSuggestion(string Summary, IReadOnlyList<string> Bullets, string? Code, string? Explanation);
public sealed record AudioChunk(Guid SessionId, ReadOnlyMemory<byte> Pcm, TimeSpan Duration, Speaker Speaker = Speaker.Unknown);
public sealed record AudioDevice(string Id, string Name, CaptureSource Source);
public sealed record GenerateAnswerRequest(Language Language, SessionMode SessionMode, string Transcript, string? CodingPrompt, string? ProgrammingLanguage);
public static class SessionReadiness
{
    public static bool CanStart(bool hasConsent, SessionStatus status) => hasConsent && status == SessionStatus.Idle;
    public static bool CanGenerate(bool hasCredential, SessionStatus status, SessionMode mode, string transcript, string codingPrompt) => hasCredential && status == SessionStatus.Idle && (mode == SessionMode.Behavioral ? !string.IsNullOrWhiteSpace(transcript) : !string.IsNullOrWhiteSpace(codingPrompt));
}

public sealed class SpeechWindowAccumulator(TimeSpan minimumDuration)
{
    private readonly MemoryStream _pcm = new();
    private TimeSpan _duration;
    public bool Add(AudioChunk chunk)
    {
        _pcm.Write(chunk.Pcm.Span); _duration += chunk.Duration;
        return _duration >= minimumDuration;
    }
    public AudioChunk Drain(Guid sessionId)
    {
        var result = new AudioChunk(sessionId, _pcm.ToArray(), _duration);
        _pcm.SetLength(0); _duration = TimeSpan.Zero;
        return result;
    }
    public void Clear() { _pcm.SetLength(0); _duration = TimeSpan.Zero; }
}

public sealed class AppSettings
{
    public Language Language { get; set; } = Language.Id;
    public string ApiBaseUrl { get; set; } = "https://ai.meetsin.id/v1";
    public string Model { get; set; } = "cx/gpt-5.6-luna";
    public CaptureSource CaptureSource { get; set; } = CaptureSource.Microphone;
    public double Opacity { get; set; } = 0.92;
    public bool AlwaysOnTop { get; set; }
    public bool AcknowledgeCustomHost { get; set; }

    public AppSettings Validated()
    {
        var defaults = new AppSettings();
        Language = Enum.IsDefined(Language) ? Language : defaults.Language;
        CaptureSource = Enum.IsDefined(CaptureSource) ? CaptureSource : defaults.CaptureSource;
        Opacity = double.IsFinite(Opacity) && Opacity is >= 0.6 and <= 1.0 ? Opacity : defaults.Opacity;
        if (Model == "luna-5.6") Model = defaults.Model;
        Model = string.IsNullOrWhiteSpace(Model) ? defaults.Model : Model.Trim();
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            ApiBaseUrl = defaults.ApiBaseUrl;
        return this;
    }
}

public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public enum ServiceErrorKind { Unauthorized, PayloadTooLarge, RateLimited, Unavailable, Timeout, Other }
public sealed class ServiceException(ServiceErrorKind kind, string message, TimeSpan? retryAfter = null) : Exception(message)
{
    public ServiceErrorKind Kind { get; } = kind;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
