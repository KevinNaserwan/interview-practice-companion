using System.Windows;
using InterviewPracticeCompanion.Services;
using InterviewPracticeCompanion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewPracticeCompanion;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show("Unexpected application error. No private session data was logged.", "Interview Practice Companion", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ICredentialService, CredentialService>();
            services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
            services.AddHttpClient<IMeetsinClient, MeetsinClient>();
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InterviewPracticeCompanion-startup.log");
            await System.IO.File.WriteAllTextAsync(path, $"{DateTimeOffset.UtcNow:O} {ex.GetType().Name}: {ex.Message}");
            MessageBox.Show($"Application could not start. Diagnostic saved to:\n{path}", "Interview Practice Companion", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_services is not null) await _services.DisposeAsync();
        base.OnExit(e);
    }
}
