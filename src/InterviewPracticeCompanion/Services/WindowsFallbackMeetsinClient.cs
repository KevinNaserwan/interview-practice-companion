using System.IO;
using System.Globalization;
using System.Speech.Recognition;
using InterviewPracticeCompanion.Models;

namespace InterviewPracticeCompanion.Services;

public sealed class WindowsFallbackMeetsinClient(MeetsinClient remote) : IMeetsinClient
{
    public async Task<string> TranscribeAsync(ReadOnlyMemory<byte> pcm, Language language, CancellationToken cancellationToken)
    {
        try { return await remote.TranscribeAsync(pcm, language, cancellationToken); }
        catch (ServiceException ex) when (ex.Kind == ServiceErrorKind.Other)
        {
            return await Task.Run(() => Recognize(pcm, language, cancellationToken), cancellationToken);
        }
    }

    public Task<AnswerSuggestion> GenerateAnswerAsync(GenerateAnswerRequest request, CancellationToken cancellationToken) =>
        remote.GenerateAnswerAsync(request, cancellationToken);

    private static string Recognize(ReadOnlyMemory<byte> pcm, Language language, CancellationToken cancellationToken)
    {
        var prefix = language == Language.Id ? "id" : "en";
        var recognizer = SpeechRecognitionEngine.InstalledRecognizers().FirstOrDefault(x => x.Culture.TwoLetterISOLanguageName.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException("speech_language_missing", language == Language.Id ? "Paket bahasa Indonesia Windows Speech belum terpasang." : "The English Windows Speech language pack is not installed.");
        using var engine = new SpeechRecognitionEngine(recognizer);
        engine.LoadGrammar(new DictationGrammar());
        using var wave = new MemoryStream(CreateWave(pcm.Span), false);
        engine.SetInputToWaveStream(wave);
        cancellationToken.ThrowIfCancellationRequested();
        return engine.Recognize(TimeSpan.FromSeconds(10))?.Text?.Trim() ?? string.Empty;
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
}
