using Go212.POS.Application.Interfaces;
using Go212.POS.Desktop.Views.Home;
using Go212.POS.Desktop.Views.Login;
using Microsoft.Extensions.DependencyInjection;

namespace Go212.POS.Desktop;

/// <summary>
/// Navigation service — handles window transitions.
/// Implemented in Desktop layer (knows about Windows).
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;

    public NavigationService(IServiceProvider services)
        => _services = services;

    public void NavigateToMain(UserSession session)
    {
        var mainWindow = _services.GetRequiredService<MainWindow>();
        mainWindow.SetSession(session);
        System.Windows.Application.Current.MainWindow = mainWindow;
        mainWindow.Show();
        mainWindow.Activate();

        // Close the login window
        foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
        {
            if (w is LoginWindow) { w.Close(); break; }
        }
    }

    public void NavigateToLogin()
    {
        var loginWindow = _services.GetRequiredService<LoginWindow>();
        loginWindow.Show();

        foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
        {
            if (w is MainWindow) { w.Close(); break; }
        }
    }
}
