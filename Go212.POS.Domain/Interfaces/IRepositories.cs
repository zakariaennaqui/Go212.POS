using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Enums;

namespace Go212.POS.Domain.Interfaces;

// ── Generic repository ─────────────────────────────────────────────────────

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(long id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<long> InsertAsync(T entity);
    Task UpdateAsync(T entity);
    Task<bool> ExistsAsync(long id);
}

// ── Specific repositories ──────────────────────────────────────────────────

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username);
    Task<IEnumerable<User>> GetActiveUsersAsync();
    Task UpdateLastLoginAsync(long userId, DateTime loginAt);
    Task UpdateFailedAttemptsAsync(long userId, int attempts, DateTime? lockedUntil);
}

public interface ICategoryRepository : IRepository<Category>
{
    Task<IEnumerable<Category>> GetActiveAsync();
}

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetByBarcodeAsync(string barcode);
    Task<IEnumerable<Product>> GetByCategoryAsync(long categoryId);
    Task<IEnumerable<Product>> SearchAsync(string query);
    Task<IEnumerable<Product>> GetLowStockAsync();
    Task<IEnumerable<Product>> GetActiveAsync();
    Task UpdateStockAsync(long productId, int newQuantity);
}

public interface ISaleRepository : IRepository<Sale>
{
    Task<Sale?> GetByNumberAsync(string saleNumber);
    Task<Sale?> GetWithDetailsAsync(long id);
    Task<IEnumerable<Sale>> GetBySessionAsync(long sessionId);
    Task<IEnumerable<Sale>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task<decimal> GetDailyRevenueAsync(DateTime date);
    Task UpdateStatusAsync(long saleId, SaleStatus status, string? reason, long? cancelledBy);
    Task<string> GenerateSaleNumberAsync(); // VTE-YYYYMMDD-001
    Task<long> InsertItemAsync(SaleItem item);
    Task RemoveItemAsync(long saleItemId);
    Task UpdateItemQuantityAsync(long saleItemId, int newQuantity, decimal newDiscountPercent);
    Task<long> InsertPaymentAsync(Payment payment);
    Task<IEnumerable<SaleItem>> GetItemsBySaleAsync(long saleId);

    Task<(decimal CashTotal, decimal CardTotal)> GetPaymentTotalsByDateRangeAsync(DateTime from, DateTime to);
    Task<IEnumerable<(string Name, int QuantitySold, decimal Revenue)>> GetTopProductsByDateRangeAsync(DateTime from, DateTime to, int topN = 10);
    Task<IEnumerable<(int Hour, int Count, decimal Amount)>> GetHourlySalesByDateRangeAsync(DateTime from, DateTime to);
}

public interface ICashSessionRepository : IRepository<CashSession>
{
    Task<CashSession?> GetOpenSessionAsync();
    Task<IEnumerable<CashSession>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task CloseSessionAsync(long sessionId, decimal counted, DateTime closedAt, string? notes);
}

public interface IStockMovementRepository : IRepository<StockMovement>
{
    Task<IEnumerable<StockMovement>> GetByProductAsync(long productId);
    Task<IEnumerable<StockMovement>> GetByDateRangeAsync(DateTime from, DateTime to);
}

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByPhoneAsync(string phone);
    Task<IEnumerable<Customer>> SearchAsync(string query);
    Task UpdatePurchaseStatsAsync(long customerId, decimal amount);
}

public interface IExpenseRepository : IRepository<Expense>
{
    Task<IEnumerable<Expense>> GetBySessionAsync(long sessionId);
    Task<decimal> GetTotalBySessionAsync(long sessionId);
}

public interface IReturnRepository : IRepository<Return>
{
    Task<IEnumerable<Return>> GetBySaleAsync(long saleId);
}

public interface ISettingRepository
{
    Task<string?> GetValueAsync(string key);
    Task SetValueAsync(string key, string value);
    Task<IEnumerable<Setting>> GetAllAsync();
}

public interface IAuditRepository
{
    Task LogAsync(AuditEvent auditEvent);
    Task<IEnumerable<AuditEvent>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task<IEnumerable<AuditEvent>> GetByUserAsync(long userId);
}

// ── Unit of Work (atomic transactions) ────────────────────────────────────

public interface IUnitOfWork : IAsyncDisposable
{
    IUserRepository Users { get; }
    ICategoryRepository Categories { get; }
    IProductRepository Products { get; }
    ISaleRepository Sales { get; }
    ICashSessionRepository CashSessions { get; }
    IStockMovementRepository StockMovements { get; }
    ICustomerRepository Customers { get; }
    IExpenseRepository Expenses { get; }
    IReturnRepository Returns { get; }
    ISettingRepository Settings { get; }
    IAuditRepository Audit { get; }
    
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
}
