using InterviewPracticeCompanion.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace InterviewPracticeCompanion;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Content.DataContext = viewModel;
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        Title = "Interview Practice Companion";
        AppWindow.Resize(new SizeInt32(1100, 740));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 820;
            presenter.PreferredMinimumHeight = 560;
        }
        Closed += async (_, _) =>
        {
            await ViewModel.DisposeAsync();
            if (Application.Current is App app) await app.ShutdownAsync();
        };
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var apiKey = new PasswordBox { Header = "Kunci API untuk saran AI (opsional)", PlaceholderText = "Disimpan aman di Windows Credential Manager" };
        var language = new ComboBox { Header = "Bahasa", ItemsSource = new[] { "Indonesia", "English" }, SelectedIndex = ViewModel.IsIndonesian ? 0 : 1 };
        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = "Transkripsi selalu lokal. API hanya digunakan ketika Anda menekan Buat saran.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(language);
        panel.Children.Add(apiKey);
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Pengaturan",
            Content = panel,
            PrimaryButtonText = "Simpan",
            CloseButtonText = "Batal",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (language.SelectedIndex == 0) ViewModel.SetIndonesianCommand.Execute(null); else ViewModel.SetEnglishCommand.Execute(null);
        if (apiKey.Password.Length > 0) { ViewModel.ApiKey = apiKey.Password; ViewModel.SaveApiKeyCommand.Execute(null); }
    }
}
