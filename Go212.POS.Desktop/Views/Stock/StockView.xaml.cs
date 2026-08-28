using Go212.POS.Desktop.ViewModels;
using System.Windows.Controls;

namespace Go212.POS.Desktop.Views.Stock;

public partial class StockView : UserControl
{
    public StockView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is StockViewModel vm)
                await vm.LoadAsync();
        };
    }
}
