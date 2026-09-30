using System.Windows;
using System.Windows.Controls;
using InterviewPracticeCompanion.ViewModels;
namespace InterviewPracticeCompanion.Views;
public partial class ConsentDialog : UserControl
{
    public ConsentDialog() => InitializeComponent();
    private void Confirm_Click(object sender, RoutedEventArgs e) { if (Consent.IsChecked == true && DataContext is MainViewModel vm) vm.GrantConsent(); }
}
