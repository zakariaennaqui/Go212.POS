namespace Go212.POS.Domain.Enums;

public enum UserRole
{
    Administrator = 1,
    Manager = 2,
    Cashier = 3
}

public enum SaleStatus
{
    Open = 1,
    Completed = 2,
    Cancelled = 3,
    Refunded = 4,
    Held = 5
}

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    Mixed = 3
}

public enum StockMovementType
{
    Entry = 1,        // Entrée stock
    Exit = 2,         // Sortie manuelle
    Sale = 3,         // Sortie par vente
    Return = 4,       // Retour client
    Adjustment = 5    // Ajustement inventaire
}

public enum SessionStatus
{
    Open = 1,
    Closed = 2
}

public enum AuditAction
{
    Login = 1,
    Logout = 2,
    SaleCreated = 3,
    SaleCancelled = 4,
    SaleRefunded = 5,
    DiscountApplied = 6,
    StockAdjusted = 7,
    SessionOpened = 8,
    SessionClosed = 9,
    BackupCreated = 10,
    BackupRestored = 11,
    UserCreated = 12,
    UserModified = 13,
    ProductModified = 14,
    SettingChanged = 15
}
