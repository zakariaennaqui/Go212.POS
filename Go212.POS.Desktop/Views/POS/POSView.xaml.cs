using Go212.POS.Desktop.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace Go212.POS.Desktop.Views.POS;

public partial class POSView : UserControl
{
    public POSView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is POSViewModel vm)
                await vm.LoadAsync();
        };
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is POSViewModel vm)
                _ = vm.SearchCommand.ExecuteAsync(null);
        }
    }
}
