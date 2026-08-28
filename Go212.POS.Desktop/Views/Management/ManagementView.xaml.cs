using System.Windows;
using System.Windows.Controls;

namespace Go212.POS.Desktop.Views.Management;

public partial class ManagementView : UserControl
{
    public ManagementView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is ViewModels.ManagementViewModel vm)
                await vm.LoadAsync();
        };
    }

    // PasswordBox cannot be databound directly — push value into ViewModel via event.
    private void PinBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ManagementViewModel vm && sender is PasswordBox pb)
            vm.NewUserPin = pb.Password;
    }
}

