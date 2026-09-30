using InterviewPracticeCompanion.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using System.ComponentModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace InterviewPracticeCompanion;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    private Grid? _root;
    private Grid? _commandBar;
    private readonly List<Border> _cards = [];
    private StackPanel? _transcriptBubbles;
    private StackPanel? _answerBubbles;
    private ScrollViewer? _transcriptScroll;
    private ScrollViewer? _answerScroll;

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        Content = _root = BuildShell();
        _root.ActualThemeChanged += (_, _) => ApplyTheme();
        ApplyTheme();
        ViewModel.PropertyChanged += ViewModelChanged;
        RenderConversation();
        SystemBackdrop = new MicaBackdrop();
        SetTitleBar((UIElement)((Grid)Content).Children[0]);
        Title = "Interview Practice Companion";
        AppWindow.Resize(new SizeInt32(1100, 740));
        if (AppWindow.Presenter is OverlappedPresenter presenter) { presenter.PreferredMinimumWidth = 820; presenter.PreferredMinimumHeight = 560; }
        Closed += async (_, _) => { ViewModel.PropertyChanged -= ViewModelChanged; await ViewModel.DisposeAsync(); if (Application.Current is App app) await app.ShutdownAsync(); };
    }

    private Grid BuildShell()
    {
        var root = new Grid { DataContext = ViewModel, RequestedTheme = ElementTheme.Default };
        root.RowDefinitions.Add(new() { Height = new GridLength(48) }); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new());

        var title = new Grid { Padding = new Thickness(16, 0, 12, 0) }; title.ColumnDefinitions.Add(new()); title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///Resources/AppLogo.png")), Width = 24, Height = 24 });
        brand.Children.Add(new TextBlock { Text = "Interview Practice", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        brand.Children.Add(new Border { Background = ThemeBrush("AccentFillColorSecondaryBrush"), CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 3, 9, 3), Child = new TextBlock { Text = "LOCAL", FontSize = 11, Foreground = ThemeBrush("TextOnAccentFillColorPrimaryBrush"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold } });
        title.Children.Add(brand);
        var settings = new Button { Content = new FontIcon { Glyph = "\uE713" }, VerticalAlignment = VerticalAlignment.Center };
        settings.Click += Settings_Click; Grid.SetColumn(settings, 1); title.Children.Add(settings); root.Children.Add(title);

        var bar = _commandBar = new Grid { Padding = new Thickness(20, 12, 20, 12), ColumnSpacing = 12 };
        bar.ColumnDefinitions.Add(new() { Width = new GridLength(150) }); bar.ColumnDefinitions.Add(new() { Width = new GridLength(165) }); bar.ColumnDefinitions.Add(new()); bar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var mode = new ComboBox { Header = "Mode", DisplayMemberPath = "Display", SelectedValuePath = "Value" }; Bind(mode, ItemsControl.ItemsSourceProperty, "ModeOptions"); Bind(mode, ComboBox.SelectedValueProperty, "Mode", BindingMode.TwoWay); bar.Children.Add(mode);
        var audio = new ComboBox { Header = "Audio", DisplayMemberPath = "Display", SelectedValuePath = "Value" }; Bind(audio, ItemsControl.ItemsSourceProperty, "CaptureSourceOptions"); Bind(audio, ComboBox.SelectedValueProperty, "CaptureSource", BindingMode.TwoWay); Bind(audio, Control.IsEnabledProperty, "CanEditSetup"); Grid.SetColumn(audio, 1); bar.Children.Add(audio);
        var state = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center }; var status = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }; Bind(status, TextBlock.TextProperty, "StatusText"); var level = new ProgressBar { Minimum = 0, Maximum = 1, Height = 3 }; Bind(level, RangeBase.ValueProperty, "AudioLevel"); state.Children.Add(status); state.Children.Add(level); Grid.SetColumn(state, 2); bar.Children.Add(state);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Bottom }; var consent = new CheckBox { Content = "Saya berizin merekam sesi ini", VerticalAlignment = VerticalAlignment.Center }; Bind(consent, ToggleButton.IsCheckedProperty, "ConsentChecked", BindingMode.TwoWay); actions.Children.Add(consent); var start = CommandButton("Mulai sesi", "StartCommand"); start.Style = Application.Current.Resources["AccentButtonStyle"] as Style; actions.Children.Add(start); actions.Children.Add(CommandButton("Berhenti", "StopCommand")); Grid.SetColumn(actions, 3); bar.Children.Add(actions); Grid.SetRow(bar, 1); root.Children.Add(bar);

        var body = new Grid { Padding = new Thickness(20), RowSpacing = 12 }; body.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.RowDefinitions.Add(new());
        var error = new InfoBar { Severity = InfoBarSeverity.Error, Title = "Perlu perhatian", IsClosable = false, ActionButton = CommandButton("Atur ulang", "RetryCommand") }; Bind(error, InfoBar.IsOpenProperty, "HasError"); Bind(error, InfoBar.MessageProperty, "Error"); body.Children.Add(error);
        var columns = new Grid { ColumnSpacing = 14 }; columns.ColumnDefinitions.Add(new()); columns.ColumnDefinitions.Add(new()); columns.Children.Add(Card("1. Transkrip", "Audio diproses lokal dan dapat diedit", "Transcript", false)); var answer = Card("2. Saran jawaban", "Periksa transkrip, lalu tekan Buat saran", "Suggestion", true); Grid.SetColumn(answer, 1); columns.Children.Add(answer); Grid.SetRow(columns, 1); body.Children.Add(columns); Grid.SetRow(body, 2); root.Children.Add(body);
        return root;
    }

    private Border Card(string title, string subtitle, string property, bool answer)
    {
        var panel = new Grid { RowSpacing = 12 };
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        if (answer) panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new());
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = subtitle, Foreground = ThemeBrush("TextFillColorSecondaryBrush") });
        panel.Children.Add(heading);

        var contentRow = 1;
        if (answer)
        {
            var prompt = new TextBox { Header = "Soal coding (opsional)", PlaceholderText = "Tempel teks atau ambil screenshot dengan Win+Shift+S" };
            Bind(prompt, TextBox.TextProperty, "CodingPrompt", BindingMode.TwoWay);
            var readScreenshot = new Button { Content = "Baca screenshot", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
            readScreenshot.Click += ReadScreenshot_Click;
            var promptPanel = new StackPanel(); promptPanel.Children.Add(prompt); promptPanel.Children.Add(readScreenshot);
            Grid.SetRow(promptPanel, 1); panel.Children.Add(promptPanel); contentRow = 2;
        }

        var bubbles = new StackPanel { Spacing = 10, Padding = new Thickness(4, 8, 4, 8) };
        var scroll = new ScrollViewer { Content = bubbles, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        if (answer) { _answerBubbles = bubbles; _answerScroll = scroll; } else { _transcriptBubbles = bubbles; _transcriptScroll = scroll; }
        Grid.SetRow(scroll, contentRow); panel.Children.Add(scroll);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = answer ? HorizontalAlignment.Right : HorizontalAlignment.Left };
        if (answer)
        {
            buttons.Children.Add(CommandButton("Salin", "CopyCommand"));
            var generate = CommandButton("Buat saran", "GenerateCommand"); generate.Style = Application.Current.Resources["AccentButtonStyle"] as Style; buttons.Children.Add(generate);
        }
        else buttons.Children.Add(CommandButton("Bersihkan transkrip", "ClearCommand"));
        Grid.SetRow(buttons, contentRow + 1); panel.Children.Add(buttons);
        var card = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = panel };
        _cards.Add(card); return card;
    }

    private void ViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.Transcript) or nameof(MainViewModel.Suggestion)) RenderConversation();
    }

    private void RenderConversation()
    {
        RenderBubbles(_transcriptBubbles, ViewModel.Transcript, false, "Percakapan akan muncul di sini…");
        RenderBubbles(_answerBubbles, ViewModel.Suggestion, true, "Saran terstruktur akan tampil di sini…");
        ScrollToEnd(_transcriptScroll);
        ScrollToEnd(_answerScroll);
    }

    private static void RenderBubbles(StackPanel? host, string text, bool assistant, string empty)
    {
        if (host is null) return;
        host.Children.Clear();
        string[] messages = string.IsNullOrWhiteSpace(text) ? [empty] : assistant ? [text.Trim()] : text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var message in messages)
        {
            var bubble = new Border
            {
                Background = ThemeBrush(assistant ? "AccentFillColorSecondaryBrush" : "SubtleFillColorSecondaryBrush"),
                CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10, 14, 10),
                MaxWidth = 460, HorizontalAlignment = assistant ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                Child = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true }
            };
            host.Children.Add(bubble);
        }
    }
    private void ScrollToEnd(ScrollViewer? scroll)
    {
        if (scroll is null) return;
        DispatcherQueue.TryEnqueue(() => { scroll.UpdateLayout(); scroll.ChangeView(null, scroll.ScrollableHeight, null, false); });
    }


    private async void ReadScreenshot_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var clipboard = Clipboard.GetContent();
            if (!clipboard.Contains(StandardDataFormats.Bitmap)) throw new InvalidOperationException("Clipboard belum berisi screenshot. Tekan Win+Shift+S terlebih dahulu.");
            var bitmapReference = await clipboard.GetBitmapAsync();
            using var stream = await bitmapReference.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            var engine = OcrEngine.TryCreateFromUserProfileLanguages() ?? throw new InvalidOperationException("Bahasa OCR lokal tidak tersedia di Windows.");
            var result = await engine.RecognizeAsync(bitmap);
            if (string.IsNullOrWhiteSpace(result.Text)) throw new InvalidOperationException("Tidak ada teks yang terbaca dari screenshot.");
            ViewModel.CodingPrompt = result.Text;
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = "Screenshot tidak dapat dibaca", Content = ex.Message, CloseButtonText = "Tutup" };
            await dialog.ShowAsync();
        }
    }

    private Button CommandButton(string content, string command) { var button = new Button { Content = content, Padding = new Thickness(16, 8, 16, 8) }; Bind(button, Button.CommandProperty, command); return button; }
    private static SolidColorBrush ThemeBrush(string key) => (SolidColorBrush)Application.Current.Resources[key];
    private void ApplyTheme()
    {
        if (_root is null) return;
        _root.Background = ThemeBrush("ApplicationPageBackgroundThemeBrush");
        if (_commandBar is not null) _commandBar.Background = ThemeBrush("CardBackgroundFillColorDefaultBrush");
        foreach (var card in _cards) { card.Background = ThemeBrush("CardBackgroundFillColorDefaultBrush"); card.BorderBrush = ThemeBrush("CardStrokeColorDefaultBrush"); }
    }
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
