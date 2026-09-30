using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using InterviewPracticeCompanion.Models;
using InterviewPracticeCompanion.Services;

namespace InterviewPracticeCompanion.ViewModels;

public sealed record SelectionOption<T>(T Value, string Display);

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly ISessionCoordinator _sessions;
    private readonly IMeetsinClient _client;
    private readonly ICredentialService _credentials;
    private readonly ISettingsService _settingsService;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private CancellationTokenSource? _generation;
    private AppSettings _settings = new();
    private Guid? _sessionId;
    private SessionStatus _status = SessionStatus.Idle;
    private SessionMode _mode;
    private string _transcript = "", _suggestion = "", _codingPrompt = "", _error = "", _apiKey = "";
    private string _programmingLanguage = "Auto";
    private bool _hasApiKey, _consentChecked, _consentGranted, _initialized;
    private float _audioLevel;
    private bool _disposed;

    public MainViewModel(ISessionCoordinator sessions, IMeetsinClient client, ICredentialService credentials, ISettingsService settingsService)
    {
        _sessions = sessions; _client = client; _credentials = credentials; _settingsService = settingsService;
        StartCommand = new RelayCommand(StartAsync, () => CanStart);
        StopCommand = new RelayCommand(StopAsync, () => CanStop);
        GenerateCommand = new RelayCommand(GenerateAsync, () => CanGenerate);
        CopyCommand = new RelayCommand(CopyAsync, () => Suggestion.Length > 0);
        ClearCommand = new RelayCommand(ClearAsync);
        SaveApiKeyCommand = new RelayCommand(SaveApiKeyAsync, () => ApiKey.Length > 0);
        ConfirmConsentCommand = new RelayCommand(ConfirmConsentAsync, () => ConsentChecked && !IsSessionActive);
        OpenApiPortalCommand = new RelayCommand(OpenApiPortalAsync);
        SetIndonesianCommand = new RelayCommand(() => SetLanguageAsync(Language.Id));
        SetEnglishCommand = new RelayCommand(() => SetLanguageAsync(Language.En));
        RetryCommand = new RelayCommand(RetryAsync, () => Status == SessionStatus.Error && HasApiKey);
        _sessions.TranscriptReceived += TranscriptReceived;
        _sessions.AudioLevelChanged += AudioLevelChanged;
        _sessions.TranscriptionStateChanged += TranscriptionStateChanged;
        _sessions.Faulted += Faulted;
        _settingsService.Warning += Warning;
        RefreshOptions();
    }

    public async Task InitializeAsync()
    {
        _settings = await _settingsService.LoadAsync(_lifetime.Token);
        _hasApiKey = _credentials.HasApiKey();
        ApplyLanguageResources(_settings.Language);
        RefreshOptions();
        _initialized = true;
        NotifyAll();
    }

    public Language Language => _settings.Language;
    public bool IsIndonesian => Language == Language.Id;
    public bool IsEnglish => Language == Language.En;
    public CaptureSource CaptureSource
    {
        get => _settings.CaptureSource;
        set
        {
            if (_settings.CaptureSource == value || IsSessionActive) return;
            _settings.CaptureSource = value;
            InvalidateConsent();
            OnChanged();
            _ = SaveSettingsAsync();
        }
    }
    public bool AlwaysOnTop { get => _settings.AlwaysOnTop; set { if (_settings.AlwaysOnTop == value) return; _settings.AlwaysOnTop = value; OnChanged(); _ = SaveSettingsAsync(); } }
    public double ContentOpacity { get => _settings.Opacity; set { var next = Math.Clamp(value, .6, 1); if (Math.Abs(_settings.Opacity - next) < .001) return; _settings.Opacity = next; OnChanged(); _ = SaveSettingsAsync(); } }
    public SessionMode Mode { get => _mode; set { if (_mode == value) return; _mode = value; OnChanged(); OnChanged(nameof(IsCodingMode)); RaiseCommands(); } }
    public bool IsCodingMode => Mode == SessionMode.Coding;
    public bool IsSessionActive => Status is SessionStatus.Listening or SessionStatus.Transcribing or SessionStatus.Generating;
    public bool CanEditSetup => !IsSessionActive;
    public SessionStatus Status { get => _status; private set { if (_status == value) return; _status = value; OnChanged(); OnChanged(nameof(StatusText)); OnChanged(nameof(IsSessionActive)); OnChanged(nameof(CanEditSetup)); RaiseCommands(); } }
    public string StatusText => Resource($"Status{Status}");
    public string Transcript { get => _transcript; set { _transcript = value; OnChanged(); RaiseCommands(); } }
    public string Suggestion { get => _suggestion; private set { _suggestion = value; OnChanged(); RaiseCommands(); } }
    public string CodingPrompt { get => _codingPrompt; set { _codingPrompt = value; OnChanged(); RaiseCommands(); } }
    public string ProgrammingLanguage { get => _programmingLanguage; set { _programmingLanguage = value; OnChanged(); } }
    public string Error { get => _error; private set { _error = value; OnChanged(); OnChanged(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public string ApiKey { get => _apiKey; set { _apiKey = value; OnChanged(); SaveApiKeyCommand.RaiseCanExecuteChanged(); } }
    public bool HasApiKey => _hasApiKey;
    public string ApiStatus => Resource(_hasApiKey ? "ApiReady" : "ApiKeyRequired");
    public bool ConsentChecked { get => _consentChecked; set { _consentChecked = value; OnChanged(); ConfirmConsentCommand.RaiseCanExecuteChanged(); } }
    public bool ConsentGranted { get => _consentGranted; private set { _consentGranted = value; OnChanged(); OnChanged(nameof(ConsentStatus)); RaiseCommands(); } }
    public string ConsentStatus => Resource(ConsentGranted ? "ConsentConfirmed" : "ConsentPending");
    public float AudioLevel { get => _audioLevel; private set { _audioLevel = value; OnChanged(); } }
    public bool CanStart => _initialized && SessionReadiness.CanStart(ConsentChecked || ConsentGranted, Status);
    public bool CanStop => IsSessionActive || Status == SessionStatus.Error;
    public bool CanGenerate => SessionReadiness.CanGenerate(HasApiKey, Status, Mode, Transcript, CodingPrompt);
    public IReadOnlyList<SelectionOption<SessionMode>> ModeOptions { get; private set; } = [];
    public IReadOnlyList<SelectionOption<CaptureSource>> CaptureSourceOptions { get; private set; } = [];
    public string[] ProgrammingLanguages { get; } = ["Auto", "JavaScript", "TypeScript", "Python", "Java", "Go", "Rust"];

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand GenerateCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand SaveApiKeyCommand { get; }
    public RelayCommand ConfirmConsentCommand { get; }
    public RelayCommand OpenApiPortalCommand { get; }
    public RelayCommand SetIndonesianCommand { get; }
    public RelayCommand SetEnglishCommand { get; }
    public RelayCommand RetryCommand { get; }

    private Task SetLanguageAsync(Language language)
    {
        if (_settings.Language == language) return Task.CompletedTask;
        _settings.Language = language;
        ApplyLanguageResources(language);
        RefreshOptions();
        NotifyAll();
        return SaveSettingsAsync();
    }

    private Task ConfirmConsentAsync()
    {
        _sessionId = Guid.NewGuid();
        _sessions.GrantConsent(_sessionId.Value, CaptureSource);
        ConsentGranted = true;
        Error = "";
        return Task.CompletedTask;
    }

    private async Task StartAsync()
    {
        if (!CanStart) return;
        if (_sessionId is null) { _sessionId = Guid.NewGuid(); _sessions.GrantConsent(_sessionId.Value, CaptureSource); ConsentGranted = true; }
        Error = "";
        try { await _sessions.StartAsync(_sessionId.Value, CaptureSource, Language, _lifetime.Token); Status = SessionStatus.Listening; }
        catch (Exception ex) { Error = UserMessage(ex); Status = SessionStatus.Error; ConsentGranted = false; }
    }

    private async Task StopAsync()
    {
        _generation?.Cancel();
        _sessionId = null;
        await _sessions.StopAsync();
        AudioLevel = 0;
        InvalidateConsent();
        Status = SessionStatus.Idle;
    }

    private async Task RetryAsync()
    {
        await _sessions.StopAsync();
        _sessionId = null;
        InvalidateConsent();
        Error = "";
        Status = SessionStatus.Idle;
    }

    private async Task GenerateAsync()
    {
        if (!CanGenerate) return;
        Status = SessionStatus.Generating; Error = "";
        _generation?.Cancel(); _generation?.Dispose();
        _generation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var expected = _sessionId;
        try
        {
            var result = await _client.GenerateAnswerAsync(new(Language, Mode, Transcript, CodingPrompt, ProgrammingLanguage), _generation.Token);
            if (expected == _sessionId && !_generation.IsCancellationRequested) Suggestion = Format(result);
            Status = SessionStatus.Idle;
        }
        catch (OperationCanceledException) { Status = SessionStatus.Idle; }
        catch (ServiceException ex) when (ex.Kind == ServiceErrorKind.Unauthorized)
        {
            _hasApiKey = false; OnChanged(nameof(HasApiKey)); OnChanged(nameof(ApiStatus)); Error = Resource("ErrorCredential"); Status = SessionStatus.Error;
        }
        catch (Exception ex) { Error = UserMessage(ex); Status = SessionStatus.Error; }
    }

    private Task CopyAsync() { var package = new DataPackage(); package.SetText(Suggestion); Clipboard.SetContent(package); return Task.CompletedTask; }
    private Task ClearAsync() { Transcript = ""; Suggestion = ""; CodingPrompt = ""; Error = ""; return Task.CompletedTask; }
    private Task OpenApiPortalAsync() { Process.Start(new ProcessStartInfo("https://ai.meetsin.id/login") { UseShellExecute = true }); return Task.CompletedTask; }
    private Task SaveApiKeyAsync()
    {
        try
        {
            _credentials.SetApiKey(ApiKey.Trim());
            ApiKey = ""; _hasApiKey = true;
            OnChanged(nameof(HasApiKey)); OnChanged(nameof(ApiStatus)); Error = ""; RaiseCommands();
        }
        catch { Error = Resource("ErrorCredentialSave"); }
        return Task.CompletedTask;
    }

    private void InvalidateConsent() { ConsentChecked = false; ConsentGranted = false; _sessionId = null; }
    private async Task SaveSettingsAsync() { if (!_initialized) return; try { await _settingsService.SaveAsync(_settings, _lifetime.Token); } catch { Error = Resource("ErrorSettingsSave"); } }
    private void TranscriptReceived(object? sender, TranscriptSegment segment) => Dispatch(() => { Transcript = string.Join(Environment.NewLine, new[] { Transcript, segment.Text }.Where(x => x.Length > 0)); Status = SessionStatus.Listening; });
    private void Faulted(object? sender, string message) => Dispatch(() => { Error = string.IsNullOrWhiteSpace(message) ? Resource("ErrorGeneric") : message; Status = SessionStatus.Error; });
    private void AudioLevelChanged(object? sender, float level) => Dispatch(() => AudioLevel = level);
    private void TranscriptionStateChanged(object? sender, bool active) => Dispatch(() => { if (Status != SessionStatus.Error) Status = active ? SessionStatus.Transcribing : SessionStatus.Listening; });
    private void Warning(object? sender, string message) => Dispatch(() => Error = message);
    private void Dispatch(Action action) { if (_dispatcher.HasThreadAccess) action(); else _dispatcher.TryEnqueue(() => action()); }
    private string UserMessage(Exception exception) => exception is DomainException { Code: "consent_required" } ? Resource("ErrorConsentRequired") : exception is ServiceException { Kind: ServiceErrorKind.Unauthorized } ? Resource("ErrorCredential") : exception is ServiceException { Kind: ServiceErrorKind.Timeout } ? Resource("ErrorTimeout") : exception is ServiceException { Kind: ServiceErrorKind.Other } ? Resource("ErrorProviderConfig") : Resource("ErrorGeneric");
    private static string Format(AnswerSuggestion answer) { var text = new StringBuilder(answer.Summary); foreach (var bullet in answer.Bullets.Take(5)) text.Append("\n• ").Append(bullet); if (!string.IsNullOrWhiteSpace(answer.Explanation)) text.Append("\n\n").Append(answer.Explanation); if (!string.IsNullOrWhiteSpace(answer.Code)) text.Append("\n\n").Append(answer.Code); return text.ToString(); }
    private string Resource(string key) => (Language, key) switch
    {
        (_, "ModeBehavioral") => Language == Language.Id ? "Perilaku" : "Behavioral",
        (_, "ModeCoding") => "Coding",
        (_, "SourceMicrophone") => Language == Language.Id ? "Mikrofon" : "Microphone",
        (_, "SourceSystem") => Language == Language.Id ? "Audio sistem" : "System audio",
        (_, "SourceBoth") => Language == Language.Id ? "Mikrofon + sistem" : "Microphone + system",
        (_, "ApiReady") => Language == Language.Id ? "API siap" : "API connected",
        (_, "ApiKeyRequired") => Language == Language.Id ? "API opsional" : "Optional API",
        (_, "ConsentConfirmed") => Language == Language.Id ? "Diizinkan" : "Authorized",
        (_, "ConsentPending") => Language == Language.Id ? "Belum diizinkan" : "Not authorized",
        (_, "ErrorConsentRequired") => Language == Language.Id ? "Izinkan perekaman sesi sebelum mulai." : "Authorize this recording session before starting.",
        (_, "ErrorCredential") => Language == Language.Id ? "Kunci API tidak valid atau kedaluwarsa." : "The API key is invalid or expired.",
        (_, "ErrorCredentialSave") => Language == Language.Id ? "Kunci API tidak dapat disimpan." : "The API key could not be saved.",
        (_, "ErrorTimeout") => Language == Language.Id ? "Layanan terlalu lama merespons." : "The service timed out.",
        (_, "ErrorProviderConfig") => Language == Language.Id ? "Provider AI belum tersedia." : "The AI provider is unavailable.",
        (_, "ErrorSettingsSave") => Language == Language.Id ? "Pengaturan tidak dapat disimpan." : "Settings could not be saved.",
        (_, "ErrorGeneric") => Language == Language.Id ? "Operasi gagal. Coba lagi." : "The operation failed. Try again.",
        (_, var status) when status.StartsWith("Status") => (Language, status) switch { (Language.Id, "StatusIdle") => "Siap", (Language.Id, "StatusListening") => "Mendengarkan", (Language.Id, "StatusTranscribing") => "Mentranskripsi lokal", (Language.Id, "StatusGenerating") => "Menyusun saran", (Language.Id, _) => "Perlu perhatian", (_, "StatusIdle") => "Ready", (_, "StatusListening") => "Listening", (_, "StatusTranscribing") => "Transcribing locally", (_, "StatusGenerating") => "Building suggestion", _ => "Needs attention" },
        _ => key
    };
    private void ApplyLanguageResources(Language language) { }
    private void RefreshOptions()
    {
        ModeOptions = [new(SessionMode.Behavioral, Resource("ModeBehavioral")), new(SessionMode.Coding, Resource("ModeCoding"))];
        CaptureSourceOptions = [new(CaptureSource.Microphone, Resource("SourceMicrophone")), new(CaptureSource.System, Resource("SourceSystem")), new(CaptureSource.Both, Resource("SourceBoth"))];
        OnChanged(nameof(ModeOptions)); OnChanged(nameof(CaptureSourceOptions));
    }
    private void NotifyAll() { OnChanged(nameof(Language)); OnChanged(nameof(IsIndonesian)); OnChanged(nameof(IsEnglish)); OnChanged(nameof(ApiStatus)); OnChanged(nameof(StatusText)); OnChanged(nameof(ConsentStatus)); OnChanged(nameof(CaptureSource)); OnChanged(nameof(AlwaysOnTop)); OnChanged(nameof(ContentOpacity)); OnChanged(nameof(Mode)); OnChanged(nameof(IsCodingMode)); OnChanged(nameof(HasApiKey)); RaiseCommands(); }
    private void RaiseCommands() { StartCommand.RaiseCanExecuteChanged(); StopCommand.RaiseCanExecuteChanged(); GenerateCommand.RaiseCanExecuteChanged(); CopyCommand.RaiseCanExecuteChanged(); ConfirmConsentCommand.RaiseCanExecuteChanged(); RetryCommand.RaiseCanExecuteChanged(); }
    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public event PropertyChangedEventHandler? PropertyChanged;
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel(); _generation?.Cancel();
        _sessions.TranscriptReceived -= TranscriptReceived; _sessions.AudioLevelChanged -= AudioLevelChanged; _sessions.TranscriptionStateChanged -= TranscriptionStateChanged; _sessions.Faulted -= Faulted; _settingsService.Warning -= Warning;
        await _sessions.DisposeAsync(); _generation?.Dispose(); _lifetime.Dispose();
    }
}
