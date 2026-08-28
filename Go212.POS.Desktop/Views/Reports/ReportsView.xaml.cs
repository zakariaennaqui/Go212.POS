using Go212.POS.Desktop.ViewModels;
using System.Windows.Controls;

namespace Go212.POS.Desktop.Views.Reports;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is ReportsViewModel vm)
                await vm.LoadDailyReportCommand.ExecuteAsync(null);
        };
    }
}
