using Go212.POS.Application.Interfaces;
using Go212.POS.Application.Services;
using Go212.POS.Desktop.ViewModels;
using Go212.POS.Desktop.Views.Login;
using Go212.POS.Desktop.Views.Home;
using Microsoft.Extensions.DependencyInjection;

namespace Go212.POS.Desktop;

/// <summary>Registers all ViewModels, Windows and Desktop-layer services into the DI container.</summary>
public static class DesktopExtensions
{
    public static IServiceCollection AddDesktop(this IServiceCollection services)
    {
        // ── Windows (Transient — new instance each time shown) ──
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();

        // ── ViewModels ──────────────────────────────────────────
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<POSViewModel>();
        services.AddTransient<ProductsViewModel>();
        services.AddTransient<StockViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ManagementViewModel>();

        // ── Navigation (Singleton — one navigator for the app lifetime) ──
        services.AddSingleton<INavigationService, NavigationService>();

        return services;
    }
}
