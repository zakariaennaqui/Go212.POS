using Go212.POS.Application.Interfaces;
using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Enums;
using Go212.POS.Domain.Exceptions;
using Go212.POS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Go212.POS.Application.Services;

/// <summary>
/// Authentication service.
/// Business rules:
///  - PIN is verified against BCrypt hash — never stored/compared in plain text
///  - Failed attempts increment counter; lock after max attempts
///  - Lock duration from settings
///  - Audit every login, logout, failed attempt
///  - NEVER log PIN, hash, or any secret value
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AuthService> _logger;

    // Defaults — overridden by Settings table values
    private int _maxAttempts  = 5;
    private int _lockMinutes  = 15;

    public AuthService(IUnitOfWork uow, ILogger<AuthService> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync()
        => await _uow.Users.GetActiveUsersAsync();

    public async Task<UserSession> AuthenticateAsync(long userId, string pin)
    {
        // Load settings
        await LoadSettingsAsync();

        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new EntityNotFoundException(nameof(User), userId);

        if (!user.IsActive)
            throw new AuthenticationException("Ce compte est désactivé.");

        // Check if currently locked
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            throw new AccountLockedException(user.LockedUntil.Value.ToLocalTime());

        // Verify PIN (BCrypt) — never log the PIN or hash
        bool valid = BCrypt.Net.BCrypt.Verify(pin, user.PinHash);

        if (!valid)
        {
            int newAttempts = user.FailedLoginAttempts + 1;
            DateTime? lockUntil = newAttempts >= _maxAttempts
                ? DateTime.UtcNow.AddMinutes(_lockMinutes)
                : null;

            await _uow.Users.UpdateFailedAttemptsAsync(userId, newAttempts, lockUntil);
            await _uow.Audit.LogAsync(new AuditEvent
            {
                UserId       = userId,
                UserName     = user.Username,
                Action       = AuditAction.Login,
                TargetEntity = nameof(User),
                TargetId     = userId,
                Details      = $"Failed PIN attempt {newAttempts}/{_maxAttempts}",
                IpOrMachine  = Environment.MachineName
            });

            _logger.LogWarning("Failed PIN attempt {Attempt}/{Max} for user {UserId}",
                newAttempts, _maxAttempts, userId); // Note: no PIN in log

            if (lockUntil.HasValue)
                throw new AccountLockedException(lockUntil.Value.ToLocalTime());

            throw new AuthenticationException("PIN incorrect.");
        }

        // Success — reset attempts, update last login
        await _uow.Users.UpdateFailedAttemptsAsync(userId, 0, null);
        await _uow.Users.UpdateLastLoginAsync(userId, DateTime.UtcNow);
        await _uow.Audit.LogAsync(new AuditEvent
        {
            UserId       = userId,
            UserName     = user.Username,
            Action       = AuditAction.Login,
            TargetEntity = nameof(User),
            TargetId     = userId,
            Details      = "Successful login",
            IpOrMachine  = Environment.MachineName
        });

        _logger.LogInformation("User {Username} authenticated successfully", user.Username);
        return new UserSession(user.Id, user.Username, user.Role, DateTime.UtcNow);
    }

    public async Task<int> GetRemainingAttemptsAsync(long userId)
    {
        await LoadSettingsAsync();
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null) return 0;
        return Math.Max(0, _maxAttempts - user.FailedLoginAttempts);
    }

    public async Task LogoutAsync(UserSession session)
    {
        await _uow.Audit.LogAsync(new AuditEvent
        {
            UserId       = session.UserId,
            UserName     = session.Username,
            Action       = AuditAction.Logout,
            TargetEntity = nameof(User),
            TargetId     = session.UserId,
            Details      = $"Session duration: {(DateTime.UtcNow - session.LoggedInAt):hh\\:mm\\:ss}",
            IpOrMachine  = Environment.MachineName
        });
        _logger.LogInformation("User {Username} logged out", session.Username);
    }

    private async Task LoadSettingsAsync()
    {
        var maxAttemptsSetting = await _uow.Settings.GetValueAsync("session.pin_max_attempts");
        var lockMinutesSetting = await _uow.Settings.GetValueAsync("session.lock_minutes");

        if (int.TryParse(maxAttemptsSetting, out int max)) _maxAttempts = max;
        if (int.TryParse(lockMinutesSetting, out int mins)) _lockMinutes = mins;
    }
}
