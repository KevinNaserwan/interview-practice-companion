using System.IO;
using System.Text.Json;
using InterviewPracticeCompanion.Models;

namespace InterviewPracticeCompanion.Services;

public interface ISettingsService
{
    event EventHandler<string>? Warning;
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    public event EventHandler<string>? Warning;

    public SettingsService(string? localAppData = null)
    {
        var root = localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _path = Path.Combine(root, "InterviewPracticeCompanion", "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return new AppSettings();
        try
        {
            await using var stream = File.OpenRead(_path);
            return (await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken) ?? new AppSettings()).Validated();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Warning?.Invoke(this, "Settings could not be read; defaults were restored.");
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ValidateEndpoint(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            await JsonSerializer.SerializeAsync(stream, settings.Validated(), JsonOptions, cancellationToken);
        File.Move(temporary, _path, true);
    }

    public static void ValidateEndpoint(AppSettings settings)
    {
        if (!Uri.TryCreate(settings.ApiBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new DomainException("invalid_api_url", "API URL must use HTTPS.");
        if (!uri.Host.Equals("ai.meetsin.id", StringComparison.OrdinalIgnoreCase) && !settings.AcknowledgeCustomHost)
            throw new DomainException("custom_host_unacknowledged", "Acknowledge that audio and transcript leave this device.");
        if (settings.Opacity is < 0.6 or > 1.0 || !double.IsFinite(settings.Opacity))
            throw new DomainException("invalid_opacity", "Opacity must be between 0.6 and 1.0.");
        if (string.IsNullOrWhiteSpace(settings.Model)) throw new DomainException("invalid_model", "Model is required.");
    }
}
