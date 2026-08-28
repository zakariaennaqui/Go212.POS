using Go212.POS.Desktop.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace Go212.POS.Desktop.Views.Login;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        // Allow window dragging ONLY on the left branding panel
        if (FindName("BrandPanel") is FrameworkElement brandPanel)
        {
            brandPanel.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        }

        Loaded += async (s, e) => await _vm.LoadUsersAsync();
    }

    // Bridge PasswordBox to ViewModel (PasswordBox can't be bound directly for security)
    private void PinBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.Pin = PinBox.Password;

    // Allow Enter key to trigger login
    private void PinBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _vm.LoginCommand.CanExecute(null))
            _vm.LoginCommand.Execute(null);
    }
}
