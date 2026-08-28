using Go212.POS.Domain.Entities;

namespace Go212.POS.Application.Interfaces;

/// <summary>Represents an authenticated user session (passed after login).</summary>
public record UserSession(long UserId, string Username, Domain.Enums.UserRole Role, DateTime LoggedInAt);

/// <summary>Ambient state for the currently authenticated user (set once after login).</summary>
public interface ICurrentUserService
{
    UserSession? Current { get; }
    long UserId { get; }
    string Username { get; }
    Domain.Enums.UserRole Role { get; }
    bool IsLoggedIn { get; }
    bool IsAdmin { get; }
    bool IsManagerOrAbove { get; }
    void SetSession(UserSession session);
    void Clear();
}

/// <summary>Authentication use cases.</summary>
public interface IAuthService
{
    /// <summary>Returns active users for the login screen user list.</summary>
    Task<IEnumerable<User>> GetActiveUsersAsync();

    /// <summary>
    /// Authenticates user by PIN.
    /// Throws <see cref="Domain.Exceptions.AuthenticationException"/> on wrong PIN.
    /// Throws <see cref="Domain.Exceptions.AccountLockedException"/> if account is locked.
    /// </summary>
    Task<UserSession> AuthenticateAsync(long userId, string pin);

    /// <summary>Returns remaining PIN attempts before lockout.</summary>
    Task<int> GetRemainingAttemptsAsync(long userId);

    /// <summary>Logs out current session and writes audit.</summary>
    Task LogoutAsync(UserSession session);
}

/// <summary>Sale / POS use cases.</summary>
public interface ISaleService
{
    Task<Domain.Entities.Sale> CreateSaleAsync(long sessionId, long userId, long? customerId);
    Task AddItemAsync(long saleId, long productId, int quantity, decimal discountPercent = 0);
    Task RemoveItemAsync(long saleId, long saleItemId);
    Task UpdateItemQuantityAsync(long saleId, long saleItemId, int newQuantity);
    Task<Domain.Entities.Sale> CompleteSaleAsync(long saleId, PaymentRequest payment, bool printTicket = true);
    Task CancelSaleAsync(long saleId, string reason, long cancelledByUserId);
    Task HoldSaleAsync(long saleId);
    Task<Domain.Entities.Sale?> ResumeHeldSaleAsync(long sessionId);
}

/// <summary>Payment details for completing a sale.</summary>
public record PaymentRequest(
    Domain.Enums.PaymentMethod Method,
    decimal CashReceived,           // For cash: amount handed by customer
    decimal? CardAmount = null,     // For mixed: card portion
    string? CardTerminalRef = null, // Card only — NO card number
    string? CardType = null         // "Visa", "Mastercard" — NO card number
);

/// <summary>Product catalog management.</summary>
public interface IProductService
{
    Task<IEnumerable<Domain.Entities.Product>> GetAllActiveAsync();
    Task<IEnumerable<Domain.Entities.Product>> GetByCategoryAsync(long categoryId);
    Task<Domain.Entities.Product?> GetByBarcodeAsync(string barcode);
    Task<IEnumerable<Domain.Entities.Product>> SearchAsync(string query);
    Task<long> CreateAsync(Domain.Entities.Product product);
    Task UpdateAsync(Domain.Entities.Product product);
    Task DeactivateAsync(long productId); // Never delete — deactivate
}

/// <summary>Stock management.</summary>
public interface IStockService
{
    Task AdjustStockAsync(long productId, int quantityChange, string reason, long userId);
    Task<IEnumerable<Domain.Entities.StockMovement>> GetHistoryAsync(long productId);
    Task<IEnumerable<Domain.Entities.Product>> GetLowStockProductsAsync();
}

/// <summary>Cash session management.</summary>
public interface ISessionService
{
    Task<Domain.Entities.CashSession> OpenSessionAsync(long userId, decimal openingFloat);
    Task<Domain.Entities.CashSession?> GetCurrentOpenSessionAsync();
    Task<Domain.Entities.CashSession> CloseSessionAsync(long sessionId, decimal countedCash, string? notes);
}

/// <summary>Reporting service.</summary>
public interface IReportService
{
    Task<DailySalesReport> GetDailyReportAsync(DateTime date);
    Task<IEnumerable<Domain.Entities.Sale>> GetSalesByDateRangeAsync(DateTime from, DateTime to);
    Task<byte[]> ExportDailyReportPdfAsync(DateTime date);
    Task<byte[]> ExportDailyReportCsvAsync(DateTime date);
}

/// <summary>Daily sales summary for dashboard.</summary>
public record DailySalesReport(
    DateTime Date,
    int SaleCount,
    decimal TotalHT,
    decimal TotalTax,
    decimal TotalTTC,
    decimal CashTotal,
    decimal CardTotal,
    IEnumerable<TopProductItem> TopProducts,
    IEnumerable<HourlySaleItem> HourlySales,
    IEnumerable<PaymentBreakdownItem> PaymentBreakdown
);

public record TopProductItem(string Name, int QuantitySold, decimal Revenue);
public record HourlySaleItem(int Hour, int Count, decimal Amount);
public record PaymentBreakdownItem(string Method, decimal TotalAmount);

/// <summary>Return / refund processing.</summary>
public interface IReturnService
{
    Task<Domain.Entities.Return> ProcessReturnAsync(long originalSaleId, ReturnRequest request, long userId);
}

public record ReturnRequest(
    string Reason,
    Domain.Enums.PaymentMethod RefundMethod,
    IEnumerable<ReturnItemRequest> Items
);

public record ReturnItemRequest(long SaleItemId, int Quantity, bool Restock = true);

/// <summary>Navigation between windows/pages (implemented in Desktop layer).</summary>
public interface INavigationService
{
    void NavigateToMain(UserSession session);
    void NavigateToLogin();
}

/// <summary>Backup and restore operations.</summary>
public interface IBackupService
{
    Task<string> CreateBackupAsync();   // Returns backup file path
    Task RestoreBackupAsync(string backupFilePath, long adminUserId);
    Task<bool> ValidateBackupAsync(string backupFilePath);
}

/// <summary>ESC/POS Receipt and Z-Report generation and printing service.</summary>
public interface IReceiptService
{
    Task<string> GenerateReceiptTextAsync(long saleId);
    Task<string> GenerateZReportTextAsync(long sessionId);
    Task<byte[]> GenerateEscPosReceiptBytesAsync(long saleId, bool cutPaper = true);
    Task<bool> PrintReceiptAsync(long saleId);
}
