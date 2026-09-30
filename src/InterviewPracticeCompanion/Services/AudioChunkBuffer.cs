using InterviewPracticeCompanion.Models;

namespace InterviewPracticeCompanion.Services;

public sealed class AudioChunkBuffer(TimeSpan capacity)
{
    private readonly Queue<AudioChunk> _queue = new();
    private TimeSpan _duration;
    public bool Add(AudioChunk chunk)
    {
        var dropped = false;
        _queue.Enqueue(chunk); _duration += chunk.Duration;
        while (_duration > capacity && _queue.TryDequeue(out var removed)) { _duration -= removed.Duration; dropped = true; }
        return dropped;
    }
    public bool TryTake(out AudioChunk? chunk)
    {
        if (!_queue.TryDequeue(out chunk)) return false;
        _duration -= chunk.Duration;
        return true;
    }
    public void Clear() { _queue.Clear(); _duration = TimeSpan.Zero; }
    public int Count => _queue.Count;
}
