using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Go212.POS.Application.Interfaces;
using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace Go212.POS.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Login screen.
/// Rules:
///  - No business logic here — calls AuthService
///  - No sensitive data in logs (no PIN, no hashes)
///  - Shows friendly error messages to user
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigation;
    private readonly ILogger<LoginViewModel> _logger;

    public LoginViewModel(
        IAuthService authService,
        INavigationService navigation,
        ILogger<LoginViewModel> logger)
    {
        _authService = authService;
        _navigation  = navigation;
        _logger      = logger;
    }

    [ObservableProperty]
    private ObservableCollection<User> _users = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private User? _selectedUser;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _pin = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _lockMessage;

    [ObservableProperty]
    private bool _isLoading;

    public bool CanLogin => SelectedUser is not null && Pin.Length >= 4 && !IsLoading;

    public async Task LoadUsersAsync()
    {
        try
        {
            var users = await _authService.GetActiveUsersAsync();
            Users = new ObservableCollection<User>(users);
            if (Users.Count == 1) SelectedUser = Users[0]; // auto-select if one user
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load users for login screen");
            ErrorMessage = "Impossible de charger les utilisateurs. Vérifiez la connexion à la base de données.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        if (SelectedUser is null) return;

        IsLoading = true;
        ErrorMessage = null;
        LockMessage  = null;

        try
        {
            var session = await _authService.AuthenticateAsync(SelectedUser.Id, Pin);
            _logger.LogInformation("User {Username} logged in successfully", SelectedUser.Username);

            // Navigate to main window — pass authenticated session
            _navigation.NavigateToMain(session);
        }
        catch (AccountLockedException ex)
        {
            LockMessage = $"Compte bloqué jusqu'à {ex.LockedUntil:HH:mm}. Contactez votre administrateur.";
            _logger.LogWarning("Login blocked for user {UserId} — account locked", SelectedUser.Id);
        }
        catch (AuthenticationException)
        {
            // How many attempts remaining?
            var remaining = await _authService.GetRemainingAttemptsAsync(SelectedUser.Id);
            ErrorMessage = remaining > 0
                ? $"PIN incorrect. {remaining} tentative(s) restante(s) avant blocage."
                : "PIN incorrect. Compte bloqué.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for user {UserId}", SelectedUser?.Id);
            ErrorMessage = "Une erreur inattendue s'est produite. Réessayez.";
        }
        finally
        {
            IsLoading = false;
            Pin = string.Empty;   // Always clear PIN from memory after attempt
        }
    }

    [RelayCommand]
    private void Close()
    {
        System.Windows.Application.Current.Shutdown();
    }
}
