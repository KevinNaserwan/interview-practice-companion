using InterviewPracticeCompanion.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;

namespace InterviewPracticeCompanion;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Content = BuildShell();
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        ExtendsContentIntoTitleBar = true;
        SetTitleBar((UIElement)((Grid)Content).Children[0]);
        Title = "Interview Practice Companion";
        AppWindow.Resize(new SizeInt32(1100, 740));
        if (AppWindow.Presenter is OverlappedPresenter presenter) { presenter.PreferredMinimumWidth = 820; presenter.PreferredMinimumHeight = 560; }
        Closed += async (_, _) => { await ViewModel.DisposeAsync(); if (Application.Current is App app) await app.ShutdownAsync(); };
    }

    private Grid BuildShell()
    {
        var root = new Grid { Background = Brush("#FFF3F6FA"), DataContext = ViewModel };
        root.RowDefinitions.Add(new() { Height = new GridLength(48) }); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new());

        var title = new Grid { Padding = new Thickness(16, 0, 12, 0) }; title.ColumnDefinitions.Add(new()); title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///Resources/AppLogo.png")), Width = 24, Height = 24 });
        brand.Children.Add(new TextBlock { Text = "Interview Practice", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        brand.Children.Add(new Border { Background = Brush("#182563EB"), CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 3, 9, 3), Child = new TextBlock { Text = "LOCAL", FontSize = 11, Foreground = Brush("#FF2563EB"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold } });
        title.Children.Add(brand);
        var settings = new Button { Content = new FontIcon { Glyph = "\uE713" }, VerticalAlignment = VerticalAlignment.Center };
        settings.Click += Settings_Click; Grid.SetColumn(settings, 1); title.Children.Add(settings); root.Children.Add(title);

        var bar = new Grid { Background = Brush("#FFFFFFFF"), Padding = new Thickness(20, 12, 20, 12), ColumnSpacing = 12 };
        bar.ColumnDefinitions.Add(new() { Width = new GridLength(150) }); bar.ColumnDefinitions.Add(new() { Width = new GridLength(165) }); bar.ColumnDefinitions.Add(new()); bar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var mode = new ComboBox { Header = "Mode", DisplayMemberPath = "Display", SelectedValuePath = "Value" }; Bind(mode, ItemsControl.ItemsSourceProperty, "ModeOptions"); Bind(mode, ComboBox.SelectedValueProperty, "Mode", BindingMode.TwoWay); bar.Children.Add(mode);
        var audio = new ComboBox { Header = "Audio", DisplayMemberPath = "Display", SelectedValuePath = "Value" }; Bind(audio, ItemsControl.ItemsSourceProperty, "CaptureSourceOptions"); Bind(audio, ComboBox.SelectedValueProperty, "CaptureSource", BindingMode.TwoWay); Bind(audio, Control.IsEnabledProperty, "CanEditSetup"); Grid.SetColumn(audio, 1); bar.Children.Add(audio);
        var state = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center }; var status = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }; Bind(status, TextBlock.TextProperty, "StatusText"); var level = new ProgressBar { Minimum = 0, Maximum = 1, Height = 3 }; Bind(level, RangeBase.ValueProperty, "AudioLevel"); state.Children.Add(status); state.Children.Add(level); Grid.SetColumn(state, 2); bar.Children.Add(state);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Bottom }; var consent = new CheckBox { Content = "Sesi ini berizin", VerticalAlignment = VerticalAlignment.Center }; Bind(consent, ToggleButton.IsCheckedProperty, "ConsentChecked", BindingMode.TwoWay); actions.Children.Add(consent); actions.Children.Add(CommandButton("Mulai", "StartCommand")); actions.Children.Add(CommandButton("Berhenti", "StopCommand")); Grid.SetColumn(actions, 3); bar.Children.Add(actions); Grid.SetRow(bar, 1); root.Children.Add(bar);

        var body = new Grid { Padding = new Thickness(20), RowSpacing = 12 }; body.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.RowDefinitions.Add(new());
        var error = new InfoBar { Severity = InfoBarSeverity.Error, Title = "Perlu perhatian", IsClosable = false, ActionButton = CommandButton("Atur ulang", "RetryCommand") }; Bind(error, InfoBar.IsOpenProperty, "HasError"); Bind(error, InfoBar.MessageProperty, "Error"); body.Children.Add(error);
        var columns = new Grid { ColumnSpacing = 14 }; columns.ColumnDefinitions.Add(new()); columns.ColumnDefinitions.Add(new()); columns.Children.Add(Card("Transkrip", "Diproses lokal di perangkat", "Transcript", false)); var answer = Card("Saran jawaban", "API opsional, hanya saat diminta", "Suggestion", true); Grid.SetColumn(answer, 1); columns.Children.Add(answer); Grid.SetRow(columns, 1); body.Children.Add(columns); Grid.SetRow(body, 2); root.Children.Add(body);
        return root;
    }

    private Border Card(string title, string subtitle, string property, bool answer)
    {
        var panel = new Grid { RowSpacing = 12 }; panel.RowDefinitions.Add(new() { Height = GridLength.Auto }); panel.RowDefinitions.Add(new()); panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = new StackPanel(); heading.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }); heading.Children.Add(new TextBlock { Text = subtitle, Foreground = Brush("#FF64748B") }); panel.Children.Add(heading);
        var text = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsReadOnly = answer, PlaceholderText = answer ? "Saran terstruktur akan tampil di sini…" : "Percakapan akan muncul di sini…", Padding = new Thickness(14) }; Bind(text, TextBox.TextProperty, property, answer ? BindingMode.OneWay : BindingMode.TwoWay); Grid.SetRow(text, 1); panel.Children.Add(text);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = answer ? HorizontalAlignment.Right : HorizontalAlignment.Left }; if (answer) { buttons.Children.Add(CommandButton("Salin", "CopyCommand")); buttons.Children.Add(CommandButton("Buat saran", "GenerateCommand")); } else buttons.Children.Add(CommandButton("Bersihkan", "ClearCommand")); Grid.SetRow(buttons, 2); panel.Children.Add(buttons);
        return new Border { Background = Brush("#FAFFFFFF"), BorderBrush = Brush("#18000000"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = panel };
    }

    private Button CommandButton(string content, string command) { var button = new Button { Content = content, Padding = new Thickness(16, 8, 16, 8) }; Bind(button, Button.CommandProperty, command); return button; }
    private static SolidColorBrush Brush(string value) => new(Windows.UI.ColorHelper.FromArgb(Convert.ToByte(value[1..3], 16), Convert.ToByte(value[3..5], 16), Convert.ToByte(value[5..7], 16), Convert.ToByte(value[7..9], 16)));
    private static void Bind(DependencyObject target, DependencyProperty property, string path, BindingMode mode = BindingMode.OneWay) => BindingOperations.SetBinding(target, property, new Binding { Path = new PropertyPath(path), Mode = mode });

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var apiKey = new PasswordBox { Header = "Kunci API untuk saran AI (opsional)", PlaceholderText = "Disimpan aman di Windows Credential Manager" };
        var language = new ComboBox { Header = "Bahasa", ItemsSource = new[] { "Indonesia", "English" }, SelectedIndex = ViewModel.IsIndonesian ? 0 : 1 };
        var panel = new StackPanel { Spacing = 16 }; panel.Children.Add(new TextBlock { Text = "Transkripsi selalu lokal. API hanya digunakan ketika Anda menekan Buat saran.", TextWrapping = TextWrapping.Wrap }); panel.Children.Add(language); panel.Children.Add(apiKey);
        var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = "Pengaturan", Content = panel, PrimaryButtonText = "Simpan", CloseButtonText = "Batal", DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        (language.SelectedIndex == 0 ? ViewModel.SetIndonesianCommand : ViewModel.SetEnglishCommand).Execute(null);
        if (apiKey.Password.Length > 0) { ViewModel.ApiKey = apiKey.Password; ViewModel.SaveApiKeyCommand.Execute(null); }
    }
}
