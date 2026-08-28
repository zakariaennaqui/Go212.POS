namespace Go212.POS.Domain.Exceptions;

/// <summary>Base for all GO212 domain exceptions.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

/// <summary>Thrown when a business rule is violated.</summary>
public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string rule) : base(rule) { }
}

/// <summary>Thrown when a required entity is not found.</summary>
public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entity, object id)
        : base($"{entity} with id '{id}' was not found.") { }
}

/// <summary>Thrown when a sale is attempted while session is closed.</summary>
public class NoOpenSessionException : DomainException
{
    public NoOpenSessionException() : base("No open cash session. Open a session before making sales.") { }
}

/// <summary>Thrown when a completed sale is attempted to be deleted (not allowed).</summary>
public class SaleDeletionNotAllowedException : DomainException
{
    public SaleDeletionNotAllowedException(string saleNumber)
        : base($"Sale '{saleNumber}' cannot be deleted. Cancel or refund it instead.") { }
}

/// <summary>Thrown when stock would go below zero.</summary>
public class InsufficientStockException : DomainException
{
    public InsufficientStockException(string productName, int available, int requested)
        : base($"Insufficient stock for '{productName}': {available} available, {requested} requested.") { }
}

/// <summary>Thrown when a user tries to perform an action without required role.</summary>
public class UnauthorizedActionException : DomainException
{
    public UnauthorizedActionException(string action)
        : base($"You do not have permission to perform: {action}") { }
}

/// <summary>Thrown when PIN authentication fails.</summary>
public class AuthenticationException : DomainException
{
    public AuthenticationException(string message) : base(message) { }
}

/// <summary>Thrown when account is temporarily locked.</summary>
public class AccountLockedException : DomainException
{
    public DateTime LockedUntil { get; }
    public AccountLockedException(DateTime lockedUntil)
        : base($"Account is locked until {lockedUntil:HH:mm:ss}. Too many failed attempts.")
    {
        LockedUntil = lockedUntil;
    }
}
