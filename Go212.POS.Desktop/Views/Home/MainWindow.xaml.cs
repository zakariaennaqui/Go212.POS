using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Go212.POS.Application.Interfaces;
using Go212.POS.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Go212.POS.Desktop.Views.Home;

public partial class MainWindow : System.Windows.Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        Loaded += async (s, e) => await _vm.InitializeAsync();
    }

    public void SetSession(UserSession session)
        => _vm.SetSession(session);
}
