using System.Windows;
using EmTrading.Presentation.Shell;

namespace EmTrading.App;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel shell)
    {
        InitializeComponent();
        DataContext = shell;
        Loaded += async (_, _) =>
        {
            if (shell.CurrentPage is { } page)
                await page.OnNavigatedToAsync();
        };
    }
}
