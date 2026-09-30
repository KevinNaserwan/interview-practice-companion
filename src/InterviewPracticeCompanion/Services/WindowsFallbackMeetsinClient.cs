using System.Net.Http;
using System.IO;
using InterviewPracticeCompanion.Models;
using Whisper.net;
using Whisper.net.Ggml;

namespace InterviewPracticeCompanion.Services;

public sealed class WindowsFallbackMeetsinClient(MeetsinClient remote, IHttpClientFactory httpClientFactory) : IMeetsinClient, IAsyncDisposable
{
    private readonly SemaphoreSlim _modelLock = new(1, 1);
    private WhisperFactory? _factory;

    public async Task<string> TranscribeAsync(ReadOnlyMemory<byte> pcm, Language language, CancellationToken cancellationToken)
    {
        var factory = await GetFactoryAsync(cancellationToken);
        await using var processor = factory.CreateBuilder().WithLanguage(language == Language.Id ? "id" : "en").Build();
        using var wave = new MemoryStream(CreateWave(pcm.Span), false);
        var text = new List<string>();
        await foreach (var segment in processor.ProcessAsync(wave, cancellationToken)) text.Add(segment.Text.Trim());
        return string.Join(' ', text.Where(x => x.Length > 0));
    }

    public Task<AnswerSuggestion> GenerateAnswerAsync(GenerateAnswerRequest request, CancellationToken cancellationToken) =>
        remote.GenerateAnswerAsync(request, cancellationToken);

    private async Task<WhisperFactory> GetFactoryAsync(CancellationToken cancellationToken)
    {
        if (_factory is not null) return _factory;
        await _modelLock.WaitAsync(cancellationToken);
        try
        {
            if (_factory is not null) return _factory;
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InterviewPracticeCompanion", "models");
            var path = Path.Combine(folder, "ggml-base.bin");
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(folder);
                var temporary = path + ".tmp";
                using var response = await httpClientFactory.CreateClient().GetAsync("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var destination = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    await source.CopyToAsync(destination, cancellationToken);
                File.Move(temporary, path, true);
            }
            return _factory = WhisperFactory.FromPath(path);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw new DomainException("local_model_unavailable", "Model transkripsi lokal belum tersedia. Hubungkan internet sekali untuk mengunduh model, lalu coba lagi.");
        }
        finally { _modelLock.Release(); }
    }

    private static byte[] CreateWave(ReadOnlySpan<byte> pcm)
    {
        var wave = new byte[44 + pcm.Length];
        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wave, 0); BitConverter.GetBytes(36 + pcm.Length).CopyTo(wave, 4);
        System.Text.Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(wave, 8); BitConverter.GetBytes(16).CopyTo(wave, 16);
        BitConverter.GetBytes((short)1).CopyTo(wave, 20); BitConverter.GetBytes((short)1).CopyTo(wave, 22);
        BitConverter.GetBytes(16000).CopyTo(wave, 24); BitConverter.GetBytes(32000).CopyTo(wave, 28);
        BitConverter.GetBytes((short)2).CopyTo(wave, 32); BitConverter.GetBytes((short)16).CopyTo(wave, 34);
        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wave, 36); BitConverter.GetBytes(pcm.Length).CopyTo(wave, 40); pcm.CopyTo(wave.AsSpan(44));
        return wave;
    }

    public ValueTask DisposeAsync()
    {
        _factory?.Dispose(); _modelLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
