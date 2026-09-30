using System.Windows.Controls;
using InterviewPracticeCompanion.ViewModels;
namespace InterviewPracticeCompanion.Views;
public partial class SessionControls : UserControl { public SessionControls() => InitializeComponent(); }
public partial class TranscriptPanel : UserControl { public TranscriptPanel() => InitializeComponent(); }
public partial class SuggestionPanel : UserControl { public SuggestionPanel() => InitializeComponent(); }
public partial class CodingPrompt : UserControl { public CodingPrompt() => InitializeComponent(); }
public partial class SettingsPanel : UserControl
{
    public SettingsPanel() => InitializeComponent();
    private void Key_Changed(object sender, System.Windows.RoutedEventArgs e) { if (DataContext is MainViewModel vm) vm.ApiKey = Key.Password; }
}
