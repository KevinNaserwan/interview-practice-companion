using InterviewPracticeCompanion.Models;

namespace InterviewPracticeCompanion.Services;

public interface IAudioCaptureService : IAsyncDisposable
{
    Task<IReadOnlyList<AudioDevice>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task StartAsync(Guid sessionId, CaptureSource source, CancellationToken cancellationToken);
    Task StopAsync();
    event EventHandler<AudioChunk>? AudioChunkAvailable;
    event EventHandler<float>? AudioLevelChanged;
    event EventHandler<string>? CaptureFaulted;
}

public static class AudioMixer
{
    public static byte[] MixPcm16(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var length = Math.Max(first.Length, second.Length) & ~1;
        var result = new byte[length];
        for (var i = 0; i < length; i += 2)
        {
            var a = i < first.Length ? BitConverter.ToInt16(first.Slice(i, 2)) : (short)0;
            var b = i < second.Length ? BitConverter.ToInt16(second.Slice(i, 2)) : (short)0;
            BitConverter.TryWriteBytes(result.AsSpan(i, 2), (short)Math.Clamp((int)a + b, short.MinValue, short.MaxValue));
        }
        return result;
    }
}
