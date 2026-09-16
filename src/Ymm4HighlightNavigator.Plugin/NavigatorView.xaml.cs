using System.Windows.Controls;
using System.Windows.Input;
namespace Ymm4HighlightNavigator.Plugin;
public partial class NavigatorView : UserControl
{
    public NavigatorView() => InitializeComponent();
    private void CandidateDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is NavigatorModel model && model.JumpCommand.CanExecute(null)) model.JumpCommand.Execute(null);
    }
}
