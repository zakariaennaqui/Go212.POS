using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Go212.POS.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Go212.POS.Desktop.ViewModels;

/// <summary>
/// Main shell ViewModel. Manages:
///  - Current page (navigation)
///  - Session info (logged-in user)
///  - Clock display
///  - Low stock alerts
///  - Logout
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly IAuthService     _authService;
    private readonly IStockService    _stockService;
    private readonly INavigationService _navigation;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<MainViewModel> _logger;
    private System.Threading.Timer? _clockTimer;

    public MainViewModel(
        IServiceProvider services,
        IAuthService authService,
        IStockService stockService,
        INavigationService navigation,
        ICurrentUserService currentUser,
        ILogger<MainViewModel> logger)
    {
        _services    = services;
        _authService = authService;
        _stockService= stockService;
        _navigation  = navigation;
        _currentUser = currentUser;
        _logger      = logger;
    }

    [ObservableProperty] private UserSession? _currentSession;
    [ObservableProperty] private object?      _currentPage;
    [ObservableProperty] private string       _currentPageTitle = "Accueil";
    [ObservableProperty] private string       _currentDateTime  =
        DateTime.Now.ToString("dddd dd MMMM yyyy \u2014 HH:mm",
            new System.Globalization.CultureInfo("fr-FR"));
    [ObservableProperty] private bool         _hasLowStockAlerts;
    [ObservableProperty] private int          _lowStockCount;

    public bool CanAccessManagement => _currentUser.IsManagerOrAbove;
    public bool CanAccessSettings   => _currentUser.IsAdmin;

    public void SetSession(UserSession session)
    {
        CurrentSession = session;
        _currentUser.SetSession(session);
        OnPropertyChanged(nameof(CanAccessManagement));
        OnPropertyChanged(nameof(CanAccessSettings));
    }

    public async Task InitializeAsync()
    {
        // Start clock — updates every 30s
        _clockTimer = new System.Threading.Timer(_ =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                CurrentDateTime = DateTime.Now.ToString("dddd dd MMMM yyyy \u2014 HH:mm",
                    new System.Globalization.CultureInfo("fr-FR")));
        }, null, 0, 30_000);

        Navigate("Home");
        await RefreshLowStockAlertAsync();
    }

    [RelayCommand]
    private void Navigate(string page)
    {
        // ── Role-based navigation guard ──────────────────────────
        bool forbidden = false;
        switch (page)
        {
            case "Management":
                if (!_currentUser.IsManagerOrAbove) { forbidden = true; _logger.LogWarning("Unauthorized nav attempt to Management by {Role}", _currentUser.Role); }
                break;
            case "Settings":
                if (!_currentUser.IsAdmin) { forbidden = true; _logger.LogWarning("Unauthorized nav attempt to Settings by {Role}", _currentUser.Role); }
                break;
        }
        if (forbidden)
        {
            System.Windows.MessageBox.Show(
                "Autorisation refusée : vous n'avez pas le droit d'accéder à cette page.",
                "Accès Interdit",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Stop);
            return;
        }

        CurrentPage = page switch
        {
            "Home"       => _services.GetRequiredService<HomeViewModel>(),
            "POS"        => _services.GetRequiredService<POSViewModel>(),
            "Products"   => _services.GetRequiredService<ProductsViewModel>(),
            "Stock"      => _services.GetRequiredService<StockViewModel>(),
            "Management" => _services.GetRequiredService<ManagementViewModel>(),
            "Reports"    => _services.GetRequiredService<ReportsViewModel>(),
            "Settings"   => _services.GetRequiredService<SettingsViewModel>(),
            _            => _services.GetRequiredService<HomeViewModel>()
        };

        CurrentPageTitle = page switch
        {
            "Home"       => "Accueil",
            "POS"        => "Caisse",
            "Products"   => "Catalogue Produits",
            "Stock"      => "Gestion du Stock",
            "Management" => "Gestion",
            "Reports"    => "Rapports",
            "Settings"   => "Param\u00e8tres",
            _            => "Accueil"
        };

        _logger.LogDebug("Navigated to {Page}", page);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (CurrentSession is null) return;

        var confirm = System.Windows.MessageBox.Show(
            "Voulez-vous vraiment vous d\u00e9connecter ?",
            "D\u00e9connexion", System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        await _authService.LogoutAsync(CurrentSession);
        _currentUser.Clear();
        _clockTimer?.Dispose();
        _navigation.NavigateToLogin();
    }

    private async Task RefreshLowStockAlertAsync()
    {
        try
        {
            var lowStock = await _stockService.GetLowStockProductsAsync();
            LowStockCount     = lowStock.Count();
            HasLowStockAlerts = LowStockCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check low stock alerts");
        }
    }
}
