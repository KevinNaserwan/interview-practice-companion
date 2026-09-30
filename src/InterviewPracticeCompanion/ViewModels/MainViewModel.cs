using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using InterviewPracticeCompanion.Models;
using InterviewPracticeCompanion.Services;

namespace InterviewPracticeCompanion.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly ISessionCoordinator _sessions;
    private readonly IMeetsinClient _client;
    private readonly ICredentialService _credentials;
    private readonly ISettingsService _settingsService;
    private AppSettings _settings = new();
    private Guid? _sessionId;
    private SessionStatus _status;
    private string _transcript = "", _suggestion = "", _codingPrompt = "", _error = "", _apiKey = "";
    private SessionMode _mode;
    private string _programmingLanguage = "Auto";

    public MainViewModel(ISessionCoordinator sessions, IMeetsinClient client, ICredentialService credentials, ISettingsService settingsService)
    {
        _sessions = sessions; _client = client; _credentials = credentials; _settingsService = settingsService;
        StartCommand = new RelayCommand(StartAsync, () => Status == SessionStatus.Idle && _credentials.HasApiKey());
        StopCommand = new RelayCommand(StopAsync, () => Status is not SessionStatus.Idle);
        GenerateCommand = new RelayCommand(GenerateAsync, () => Status == SessionStatus.Idle && (Mode == SessionMode.Behavioral ? Transcript.Length > 0 : CodingPrompt.Length > 0));
        CopyCommand = new RelayCommand(CopyAsync, () => Suggestion.Length > 0);
        ClearCommand = new RelayCommand(ClearAsync);
        SaveApiKeyCommand = new RelayCommand(SaveApiKeyAsync, () => ApiKey.Length > 0);
        _sessions.TranscriptReceived += TranscriptReceived;
        _sessions.Faulted += Faulted;
        _settingsService.Warning += Warning;
    }

    public async Task InitializeAsync() { _settings = await _settingsService.LoadAsync(); OnChanged(string.Empty); RaiseCommands(); }
    public Language Language { get => _settings.Language; set { _settings.Language = value; OnChanged(); } }
    public CaptureSource CaptureSource { get => _settings.CaptureSource; set { if (_settings.CaptureSource == value) return; _settings.CaptureSource = value; _sessionId = null; OnChanged(); } }
    public bool AlwaysOnTop { get => _settings.AlwaysOnTop; set { _settings.AlwaysOnTop = value; OnChanged(); } }
    public double ContentOpacity { get => _settings.Opacity; set { _settings.Opacity = value; OnChanged(); } }
    public SessionMode Mode { get => _mode; set { _mode = value; OnChanged(); RaiseCommands(); } }
    public SessionStatus Status { get => _status; private set { _status = value; OnChanged(); RaiseCommands(); } }
    public string Transcript { get => _transcript; set { _transcript = value; OnChanged(); RaiseCommands(); } }
    public string Suggestion { get => _suggestion; private set { _suggestion = value; OnChanged(); RaiseCommands(); } }
    public string CodingPrompt { get => _codingPrompt; set { _codingPrompt = value; OnChanged(); RaiseCommands(); } }
    public string ProgrammingLanguage { get => _programmingLanguage; set { _programmingLanguage = value; OnChanged(); } }
    public string Error { get => _error; private set { _error = value; OnChanged(); } }
    public string ApiKey { get => _apiKey; set { _apiKey = value; OnChanged(); ((RelayCommand)SaveApiKeyCommand).RaiseCanExecuteChanged(); } }
    public string ApiStatus => _credentials.HasApiKey() ? "API ready" : "API key required";
    public Array Languages => Enum.GetValues<Language>();
    public Array Modes => Enum.GetValues<SessionMode>();
    public Array CaptureSources => Enum.GetValues<CaptureSource>();
    public string[] ProgrammingLanguages { get; } = ["Auto", "JavaScript", "TypeScript", "Python", "Java", "Go", "Rust"];
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand GenerateCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand SaveApiKeyCommand { get; }

    public void GrantConsent() { _sessionId ??= Guid.NewGuid(); _sessions.GrantConsent(_sessionId.Value, CaptureSource); }
    private async Task StartAsync()
    {
        Error = ""; _sessionId ??= Guid.NewGuid();
        if (!_sessions.HasConsent(_sessionId.Value, CaptureSource)) { Error = "Consent required. Review and confirm the consent notice."; return; }
        try { await _sessions.StartAsync(_sessionId.Value, CaptureSource, Language); Status = SessionStatus.Listening; }
        catch (Exception ex) { Error = ex.Message; Status = SessionStatus.Error; }
    }
    private async Task StopAsync() { await _sessions.StopAsync(); Status = SessionStatus.Idle; }
    private async Task GenerateAsync()
    {
        Status = SessionStatus.Generating; Error = ""; var expected = _sessionId;
        try
        {
            var result = await _client.GenerateAnswerAsync(new(Language, Mode, Transcript, CodingPrompt, ProgrammingLanguage), CancellationToken.None);
            if (expected == _sessionId) Suggestion = Format(result);
        }
        catch (Exception ex) { Error = ex.Message; Status = SessionStatus.Error; return; }
        Status = SessionStatus.Idle;
    }
    private Task CopyAsync() { Clipboard.SetText(Suggestion); return Task.CompletedTask; }
    private Task ClearAsync() { Transcript = ""; Suggestion = ""; CodingPrompt = ""; return Task.CompletedTask; }
    private Task SaveApiKeyAsync() { _credentials.SetApiKey(ApiKey); ApiKey = ""; OnChanged(nameof(ApiStatus)); RaiseCommands(); return Task.CompletedTask; }
    private void TranscriptReceived(object? sender, TranscriptSegment segment) => Application.Current.Dispatcher.Invoke(() => { Transcript = string.Join(Environment.NewLine, new[] { Transcript, segment.Text }.Where(x => x.Length > 0)); Status = SessionStatus.Listening; });
    private void Faulted(object? sender, string message) => Application.Current.Dispatcher.Invoke(() => { Error = message; Status = SessionStatus.Error; });
    private void Warning(object? sender, string message) => Application.Current.Dispatcher.Invoke(() => Error = message);
    private static string Format(AnswerSuggestion answer) { var text = new StringBuilder(answer.Summary); foreach (var bullet in answer.Bullets.Take(5)) text.Append("\n• ").Append(bullet); if (!string.IsNullOrWhiteSpace(answer.Explanation)) text.Append("\n\n").Append(answer.Explanation); if (!string.IsNullOrWhiteSpace(answer.Code)) text.Append("\n\n").Append(answer.Code); return text.ToString(); }
    private void RaiseCommands() { StartCommand.RaiseCanExecuteChanged(); StopCommand.RaiseCanExecuteChanged(); GenerateCommand.RaiseCanExecuteChanged(); CopyCommand.RaiseCanExecuteChanged(); }
    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    public event PropertyChangedEventHandler? PropertyChanged;
    public async ValueTask DisposeAsync() { _sessions.TranscriptReceived -= TranscriptReceived; _sessions.Faulted -= Faulted; _settingsService.Warning -= Warning; await _sessions.DisposeAsync(); }
}
