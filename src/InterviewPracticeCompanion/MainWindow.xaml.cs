using System.ComponentModel;
using System.Windows;
using InterviewPracticeCompanion.ViewModels;

namespace InterviewPracticeCompanion;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _canClose;
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_canClose) { base.OnClosing(e); return; }
        e.Cancel = true;
        IsEnabled = false;
        try { await _viewModel.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) { }
        _canClose = true;
        await Dispatcher.InvokeAsync(Close);
    }
}
