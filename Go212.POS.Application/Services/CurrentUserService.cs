using Go212.POS.Application.Interfaces;
using Go212.POS.Domain.Enums;
using System.Diagnostics.CodeAnalysis;

namespace Go212.POS.Application.Services;

/// <summary>Singleton ambient service that holds the current authenticated user session.</summary>
public class CurrentUserService : ICurrentUserService
{
    private UserSession? _session;

    public UserSession? Current => _session;

    [MemberNotNullWhen(true, nameof(Current))]
    public bool IsLoggedIn => _session is not null;

    public long UserId => IsLoggedIn ? _session!.UserId : throw new InvalidOperationException("No user is logged in.");
    public string Username => IsLoggedIn ? _session!.Username : string.Empty;
    public UserRole Role => IsLoggedIn ? _session!.Role : UserRole.Cashier;

    public bool IsAdmin => Role == UserRole.Administrator;
    public bool IsManagerOrAbove => Role is UserRole.Administrator or UserRole.Manager;

    public void SetSession(UserSession session) => _session = session;
    public void Clear() => _session = null;
}
