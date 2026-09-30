using InterviewPracticeCompanion.Services;
using InterviewPracticeCompanion.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace InterviewPracticeCompanion;

public partial class App : Application
{
    private ServiceProvider? _services;
    public static MainWindow? Window { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            WriteDiagnostic(args.Exception);
            args.Handled = true;
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ICredentialService, CredentialService>();
            services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
            services.AddHttpClient<MeetsinClient>();
            services.AddSingleton<IMeetsinClient, WindowsFallbackMeetsinClient>();
            services.AddSingleton<ISessionCoordinator, SessionCoordinator>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();
            _services = services.BuildServiceProvider();
            Window = _services.GetRequiredService<MainWindow>();
            await Window.ViewModel.InitializeAsync();
            Window.Activate();
        }
        catch (Exception ex) { WriteDiagnostic(ex); }
    }

    public async Task ShutdownAsync()
    {
        try { if (_services is not null) await _services.DisposeAsync(); }
        catch { }
    }

    private static void WriteDiagnostic(Exception exception)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "InterviewPracticeCompanion-startup.log");
            File.WriteAllText(path, $"{DateTimeOffset.UtcNow:O}{Environment.NewLine}{exception}");
        }
        catch { }
    }
}
