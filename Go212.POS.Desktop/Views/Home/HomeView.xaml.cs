using Go212.POS.Desktop.ViewModels;
using System.Windows.Controls;

namespace Go212.POS.Desktop.Views.Home;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is HomeViewModel vm)
                await vm.LoadAsync();
        };
    }
}
