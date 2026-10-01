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

public static class AudioSignal
{
    public static bool ContainsSpeech(ReadOnlySpan<byte> pcm)
    {
        if (pcm.Length < 2) return false;
        long squares = 0;
        var active = 0;
        var samples = pcm.Length / 2;
        for (var i = 0; i + 1 < pcm.Length; i += 2)
        {
            var sample = BitConverter.ToInt16(pcm.Slice(i, 2));
            squares += (long)sample * sample;
            if (Math.Abs((int)sample) >= 400) active++;
        }
        var rms = Math.Sqrt((double)squares / samples) / 32768d;
        return rms >= 0.006 && active >= Math.Max(1, samples / 100);
    }
}
