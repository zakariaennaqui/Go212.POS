using Go212.POS.Desktop.ViewModels;
using System.Windows.Controls;

namespace Go212.POS.Desktop.Views.Products;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is ProductsViewModel vm)
                await vm.LoadAsync();
        };
    }
}
