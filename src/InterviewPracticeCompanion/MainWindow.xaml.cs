using System.ComponentModel;
using System.Windows;
using InterviewPracticeCompanion.ViewModels;

namespace InterviewPracticeCompanion;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _closing;
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_closing) { base.OnClosing(e); return; }
        e.Cancel = true; _closing = true;
        try { await _viewModel.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (TimeoutException) { }
        Close();
    }
}
