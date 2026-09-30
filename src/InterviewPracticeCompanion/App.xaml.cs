using System.Windows;
using InterviewPracticeCompanion.Services;
using InterviewPracticeCompanion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewPracticeCompanion;

public partial class App : Application
{
    private ServiceProvider? _services;
    private bool _shuttingDown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            if (!_shuttingDown) ShowStartupError(args.Exception);
            args.Handled = true;
        };
        base.OnStartup(e);
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
            var window = _services.GetRequiredService<MainWindow>();
            await ((MainViewModel)window.DataContext).InitializeAsync();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
            Shutdown(1);
        }
    }
    private static void ShowStartupError(Exception exception)
    {
        var details = $"{DateTimeOffset.UtcNow:O}{Environment.NewLine}{exception}";
        string? path = null;
        try
        {
            path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InterviewPracticeCompanion-startup.log");
            System.IO.File.WriteAllText(path, details);
        }
        catch { }
        var message = $"Application could not start.{Environment.NewLine}{exception.GetType().Name}: {exception.Message}";
        if (path is not null) message += $"{Environment.NewLine}{Environment.NewLine}Diagnostic: {path}";
        MessageBox.Show(message, "Interview Practice Companion", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _shuttingDown = true;
        try { if (_services is not null) await _services.DisposeAsync(); }
        catch { }
        base.OnExit(e);
    }
}
