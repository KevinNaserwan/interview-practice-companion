namespace InterviewPracticeCompanion.Services;

public interface IPrivacyLogger { void Log(string operation, int? httpStatus, TimeSpan latency, Guid sessionId); }
public sealed class PrivacyLogger : IPrivacyLogger
{
    public void Log(string operation, int? httpStatus, TimeSpan latency, Guid sessionId) =>
        System.Diagnostics.Trace.WriteLine($"{DateTimeOffset.UtcNow:O} operation={operation} status={httpStatus?.ToString() ?? "none"} latency_ms={latency.TotalMilliseconds:F0} session={sessionId:N}");
}
