using System.Windows;
using EmTrading.Presentation;

namespace EmTrading.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) =>
        {
            await viewModel.LoadStrategiesCommand.ExecuteAsync(null);
            await viewModel.RefreshHistoryCommand.ExecuteAsync(null);
            await viewModel.RefreshCoverageCommand.ExecuteAsync(null);
        };
    }
}
