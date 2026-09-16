using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Ymm4HighlightNavigator.Plugin;
public partial class NavigatorView : UserControl
{
    public NavigatorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { if (DataContext is NavigatorModel model) await model.EnsureLearningLoadedAsync(); }
        catch (Exception) { /* Optional stored learning data must not escape into the host Loaded callback. */ }
    }
    private void CandidateDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is NavigatorModel model && model.JumpCommand.CanExecute(null)) model.JumpCommand.Execute(null);
    }
}
