using System.Windows;
using EmTrading.Presentation.Common;

namespace EmTrading.App.Hosting;

/// <summary>A Yes/No dialog owned by the main window; "No" is the default so Enter does not confirm by accident.</summary>
internal sealed class MessageBoxConfirmation : IUserConfirmation
{
    public bool Confirm(string title, string message)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var result = owner is null
            ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            : MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }
}
