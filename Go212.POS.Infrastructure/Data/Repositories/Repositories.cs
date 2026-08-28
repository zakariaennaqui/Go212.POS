using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Interfaces;
using Dapper;
using MySqlConnector;

namespace Go212.POS.Infrastructure.Data.Repositories;

/// <summary>Base Dapper repository — all queries use parameters (no string concatenation).</summary>
public abstract class BaseRepository<T> : IRepository<T> where T : BaseEntity
{
    protected MySqlConnection Conn;
    protected MySqlTransaction? Tx;

    protected BaseRepository(MySqlConnection connection)
        => Conn = connection;

    public void SetTransaction(MySqlTransaction? tx) => Tx = tx;

    protected async Task EnsureOpenAsync()
    {
        if (Conn.State != System.Data.ConnectionState.Open)
            await Conn.OpenAsync();
    }

    public virtual async Task<T?> GetByIdAsync(long id)
    {
        await EnsureOpenAsync();
        var sql = $"SELECT * FROM {TableName} WHERE Id = @Id LIMIT 1";
        return await Conn.QueryFirstOrDefaultAsync<T>(sql, new { Id = id }, Tx);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<T>($"SELECT * FROM {TableName}", transaction: Tx);
    }

    public virtual async Task<bool> ExistsAsync(long id)
    {
        await EnsureOpenAsync();
        var count = await Conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM {TableName} WHERE Id = @Id", new { Id = id }, Tx);
        return count > 0;
    }

    public abstract Task<long> InsertAsync(T entity);
    public abstract Task UpdateAsync(T entity);

    protected abstract string TableName { get; }
}

// ── User Repository ─────────────────────────────────────────────────────────

public class UserRepository : BaseRepository<User>, IUserRepository
{
    protected override string TableName => "User";

    public UserRepository(MySqlConnection c) : base(c) { }

    public override async Task<long> InsertAsync(User u)
    {
        await EnsureOpenAsync();
        const string sql = """
            INSERT INTO User (Name, Username, PinHash, Role, IsActive, CreatedAt, UpdatedAt)
            VALUES (@Name, @Username, @PinHash, @Role, @IsActive, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;
        return await Conn.ExecuteScalarAsync<long>(sql, u, Tx);
    }

    public override async Task UpdateAsync(User u)
    {
        await EnsureOpenAsync();
        const string sql = """
            UPDATE User SET Name=@Name, Username=@Username, Role=@Role,
                IsActive=@IsActive, UpdatedAt=UTC_TIMESTAMP()
            WHERE Id=@Id
            """;
        await Conn.ExecuteAsync(sql, u, Tx);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        await EnsureOpenAsync();
        return await Conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM User WHERE Username = @Username AND IsActive = 1 LIMIT 1",
            new { Username = username }, Tx);
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync()
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<User>(
            "SELECT * FROM User WHERE IsActive = 1 ORDER BY Name", transaction: Tx);
    }

    public async Task UpdateLastLoginAsync(long userId, DateTime loginAt)
    {
        await EnsureOpenAsync();
        await Conn.ExecuteAsync(
            "UPDATE User SET LastLoginAt=@LoginAt, UpdatedAt=UTC_TIMESTAMP() WHERE Id=@Id",
            new { Id = userId, LoginAt = loginAt }, Tx);
    }

    public async Task UpdateFailedAttemptsAsync(long userId, int attempts, DateTime? lockedUntil)
    {
        await EnsureOpenAsync();
        await Conn.ExecuteAsync(
            "UPDATE User SET FailedLoginAttempts=@Attempts, LockedUntil=@LockedUntil, UpdatedAt=UTC_TIMESTAMP() WHERE Id=@Id",
            new { Id = userId, Attempts = attempts, LockedUntil = lockedUntil }, Tx);
    }
}

// ── Category Repository ─────────────────────────────────────────────────────

public class CategoryRepository : BaseRepository<Category>, ICategoryRepository
{
    protected override string TableName => "Category";
    public CategoryRepository(MySqlConnection c) : base(c) { }

    public override async Task<long> InsertAsync(Category cat)
    {
        await EnsureOpenAsync();
        const string sql = """
            INSERT INTO Category (Name, Description, Color, IconName, DisplayOrder, IsActive, CreatedAt, UpdatedAt)
            VALUES (@Name, @Description, @Color, @IconName, @DisplayOrder, @IsActive, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;
        return await Conn.ExecuteScalarAsync<long>(sql, cat, Tx);
    }

    public override async Task UpdateAsync(Category cat)
    {
        await EnsureOpenAsync();
        await Conn.ExecuteAsync(
            "UPDATE Category SET Name=@Name, Description=@Description, Color=@Color, IconName=@IconName, DisplayOrder=@DisplayOrder, IsActive=@IsActive, UpdatedAt=UTC_TIMESTAMP() WHERE Id=@Id",
            cat, Tx);
    }

    public async Task<IEnumerable<Category>> GetActiveAsync()
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<Category>(
            "SELECT * FROM Category WHERE IsActive=1 ORDER BY DisplayOrder, Name",
            transaction: Tx);
    }
}

// ── Product Repository ──────────────────────────────────────────────────────

public class ProductRepository : BaseRepository<Product>, IProductRepository
{
    protected override string TableName => "Product";
    public ProductRepository(MySqlConnection c) : base(c) { }

    public override async Task<long> InsertAsync(Product p)
    {
        await EnsureOpenAsync();
        const string sql = """
            INSERT INTO Product (Name, Description, CategoryId, PriceHT, TaxRate, Barcode, Unit, ImagePath, StockQuantity, StockAlertThreshold, IsActive, HasVariants, CreatedAt, UpdatedAt)
            VALUES (@Name, @Description, @CategoryId, @PriceHT, @TaxRate, @Barcode, @Unit, @ImagePath, @StockQuantity, @StockAlertThreshold, @IsActive, @HasVariants, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;
        return await Conn.ExecuteScalarAsync<long>(sql, p, Tx);
    }

    public override async Task UpdateAsync(Product p)
    {
        await EnsureOpenAsync();
        await Conn.ExecuteAsync(
            "UPDATE Product SET Name=@Name, Description=@Description, CategoryId=@CategoryId, PriceHT=@PriceHT, TaxRate=@TaxRate, Barcode=@Barcode, Unit=@Unit, ImagePath=@ImagePath, StockAlertThreshold=@StockAlertThreshold, IsActive=@IsActive, UpdatedAt=UTC_TIMESTAMP() WHERE Id=@Id",
            p, Tx);
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode)
    {
        await EnsureOpenAsync();
        return await Conn.QueryFirstOrDefaultAsync<Product>(
            "SELECT * FROM Product WHERE Barcode=@Barcode AND IsActive=1 LIMIT 1",
            new { Barcode = barcode }, Tx);
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(long categoryId)
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<Product>(
            "SELECT * FROM Product WHERE CategoryId=@CategoryId AND IsActive=1 ORDER BY Name",
            new { CategoryId = categoryId }, Tx);
    }

    public async Task<IEnumerable<Product>> SearchAsync(string query)
    {
        await EnsureOpenAsync();
        var like = $"%{query}%";
        return await Conn.QueryAsync<Product>(
            "SELECT * FROM Product WHERE IsActive=1 AND (Name LIKE @Like OR Barcode LIKE @Like) ORDER BY Name LIMIT 50",
            new { Like = like }, Tx);
    }

    public async Task<IEnumerable<Product>> GetLowStockAsync()
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<Product>(
            "SELECT * FROM Product WHERE IsActive=1 AND StockQuantity <= StockAlertThreshold ORDER BY StockQuantity",
            transaction: Tx);
    }

    public async Task<IEnumerable<Product>> GetActiveAsync()
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<Product>(
            "SELECT * FROM Product WHERE IsActive=1 ORDER BY Name",
            transaction: Tx);
    }

    public async Task UpdateStockAsync(long productId, int newQuantity)
    {
        await EnsureOpenAsync();
        await Conn.ExecuteAsync(
            "UPDATE Product SET StockQuantity=@Qty, UpdatedAt=UTC_TIMESTAMP() WHERE Id=@Id",
            new { Id = productId, Qty = newQuantity }, Tx);
    }
}

// ── Sale Repository ─────────────────────────────────────────────────────────

public class SaleRepository : BaseRepository<Sale>, ISaleRepository
{
    protected override string TableName => "Sale";
    public SaleRepository(MySqlConnection c) : base(c) { }

    public override async Task<long> InsertAsync(Sale s)
    {
        await EnsureOpenAsync();
        const string sql = """
            INSERT INTO Sale (SaleNumber, UserId, CustomerId, CashSessionId, Status, SubtotalHT, TaxAmount, DiscountAmount, TotalTTC, CreatedAt, UpdatedAt)
            VALUES (@SaleNumber, @UserId, @CustomerId, @CashSessionId, @Status, @SubtotalHT, @TaxAmount, @DiscountAmount, @TotalTTC, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;
        return await Conn.ExecuteScalarAsync<long>(sql, s, Tx);
    }

    public override async Task UpdateAsync(Sale s)
    {
        await EnsureOpenAsync();
        await Conn.ExecuteAsync(
            "UPDATE Sale SET Status=@Status, SubtotalHT=@SubtotalHT, TaxAmount=@TaxAmount, DiscountAmount=@DiscountAmount, TotalTTC=@TotalTTC, UpdatedAt=UTC_TIMESTAMP() WHERE Id=@Id",
            s, Tx);
    }

    public async Task<Sale?> GetByNumberAsync(string number)
    {
        await EnsureOpenAsync();
        return await Conn.QueryFirstOrDefaultAsync<Sale>(
            "SELECT * FROM Sale WHERE SaleNumber=@Number LIMIT 1", new { Number = number }, Tx);
    }

    public async Task<Sale?> GetWithDetailsAsync(long id)
    {
        await EnsureOpenAsync();
        // Multi-result query: sale + items + payments
        const string sql = """
            SELECT * FROM Sale WHERE Id=@Id;
            SELECT * FROM SaleItem WHERE SaleId=@Id;
            SELECT * FROM Payment WHERE SaleId=@Id;
            """;
        using var multi = await Conn.QueryMultipleAsync(sql, new { Id = id }, Tx);
        var sale = await multi.ReadFirstOrDefaultAsync<Sale>();
        if (sale is null) return null;
        sale.Items    = (await multi.ReadAsync<SaleItem>()).ToList();
        sale.Payments = (await multi.ReadAsync<Payment>()).ToList();
        return sale;
    }

    public async Task<IEnumerable<Sale>> GetBySessionAsync(long sessionId)
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<Sale>(
            "SELECT * FROM Sale WHERE CashSessionId=@SessionId ORDER BY CreatedAt DESC",
            new { SessionId = sessionId }, Tx);
    }

    public async Task<IEnumerable<Sale>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<Sale>(
            "SELECT * FROM Sale WHERE CreatedAt BETWEEN @From AND @To ORDER BY CreatedAt DESC",
            new { From = from, To = to }, Tx);
    }

    public async Task<decimal> GetDailyRevenueAsync(DateTime date)
    {
        await EnsureOpenAsync();
        var from = date.Date;
        var to   = date.Date.AddDays(1);
        return await Conn.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(TotalTTC),0) FROM Sale WHERE Status=2 AND CreatedAt BETWEEN @From AND @To",
            new { From = from, To = to }, Tx);
    }

    public async Task UpdateStatusAsync(long saleId, Domain.Enums.SaleStatus status, string? reason, long? cancelledBy)
    {
        await EnsureOpenAsync();
        await Conn.ExecuteAsync(
            "UPDATE Sale SET Status=@Status, CancellationReason=@Reason, CancelledByUserId=@CancelledBy, CancelledAt=UTC_TIMESTAMP(), UpdatedAt=UTC_TIMESTAMP() WHERE Id=@Id",
            new { Id = saleId, Status = (int)status, Reason = reason, CancelledBy = cancelledBy }, Tx);
    }

    public async Task<string> GenerateSaleNumberAsync()
    {
        await EnsureOpenAsync();
        // Atomic increment using MySQL
        await Conn.ExecuteAsync(
            "UPDATE Vendor SET SaleNumberNext = SaleNumberNext + 1", transaction: Tx);
        var row = await Conn.QueryFirstAsync<(string prefix, int next)>(
            "SELECT SaleNumberPrefix, SaleNumberNext - 1 FROM Vendor LIMIT 1", transaction: Tx);
        return $"{row.prefix}-{DateTime.Now:yyyyMMdd}-{row.next:D4}";
    }

    public async Task<long> InsertItemAsync(SaleItem item)
    {
        await EnsureOpenAsync();
        const string sql = """
            INSERT INTO SaleItem (SaleId, ProductId, ProductName, ProductBarcode, Quantity, UnitPriceHT, TaxRate, DiscountPercent, CreatedAt, UpdatedAt)
            VALUES (@SaleId, @ProductId, @ProductName, @ProductBarcode, @Quantity, @UnitPriceHT, @TaxRate, @DiscountPercent, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;
        return await Conn.ExecuteScalarAsync<long>(sql, item, Tx);
    }

    public async Task RemoveItemAsync(long saleItemId)
    {
        await EnsureOpenAsync();
        await Conn.ExecuteAsync("DELETE FROM SaleItem WHERE Id=@Id", new { Id = saleItemId }, Tx);
    }

    public async Task UpdateItemQuantityAsync(long saleItemId, int newQuantity, decimal newDiscountPercent)
    {
        await EnsureOpenAsync();
        const string sql = """
            UPDATE SaleItem
            SET Quantity = @Qty,
                DiscountPercent = @Disc,
                LineTotalHT   = ROUND(UnitPriceHT * @Qty * (1 - @Disc / 100), 2),
                LineTaxAmount = ROUND(ROUND(UnitPriceHT * @Qty * (1 - @Disc / 100), 2) * TaxRate / 100, 2),
                LineTotalTTC  = ROUND(UnitPriceHT * @Qty * (1 - @Disc / 100), 2)
                              + ROUND(ROUND(UnitPriceHT * @Qty * (1 - @Disc / 100), 2) * TaxRate / 100, 2),
                UpdatedAt     = UTC_TIMESTAMP()
            WHERE Id = @Id
            """;
        await Conn.ExecuteAsync(sql, new { Id = saleItemId, Qty = newQuantity, Disc = newDiscountPercent }, Tx);
    }

    public async Task<long> InsertPaymentAsync(Payment payment)
    {
        await EnsureOpenAsync();
        const string sql = """
            INSERT INTO Payment (SaleId, Method, Amount, IsSuccess, CardTerminalRef, CardType, Notes, CreatedAt, UpdatedAt)
            VALUES (@SaleId, @Method, @Amount, @IsSuccess, @CardTerminalRef, @CardType, @Notes, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;
        return await Conn.ExecuteScalarAsync<long>(sql, payment, Tx);
    }

    public async Task<IEnumerable<SaleItem>> GetItemsBySaleAsync(long saleId)
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<SaleItem>("SELECT * FROM SaleItem WHERE SaleId=@SaleId", new { SaleId = saleId }, Tx);
    }

    public async Task<(decimal CashTotal, decimal CardTotal)> GetPaymentTotalsByDateRangeAsync(DateTime from, DateTime to)
    {
        await EnsureOpenAsync();
        const string sql = """
            SELECT
                COALESCE(SUM(CASE WHEN p.Method=1 AND p.IsSuccess=1 THEN p.Amount ELSE 0 END),0) AS CashTotal,
                COALESCE(SUM(CASE WHEN p.Method=2 AND p.IsSuccess=1 THEN p.Amount ELSE 0 END),0) AS CardTotal
            FROM Payment p
            INNER JOIN Sale s ON s.Id = p.SaleId
            WHERE s.Status=2 AND s.CreatedAt BETWEEN @From AND @To
            """;
        var row = await Conn.QueryFirstAsync<dynamic>(sql, new { From = from, To = to }, Tx);
        return ((decimal)row.CashTotal, (decimal)row.CardTotal);
    }

    public async Task<IEnumerable<(string Name, int QuantitySold, decimal Revenue)>> GetTopProductsByDateRangeAsync(DateTime from, DateTime to, int topN = 10)
    {
        await EnsureOpenAsync();
        const string sql = """
            SELECT si.ProductName AS Name,
                   CAST(SUM(si.Quantity) AS SIGNED) AS QuantitySold,
                   COALESCE(SUM(si.LineTotalTTC),0)  AS Revenue
            FROM SaleItem si
            INNER JOIN Sale s ON s.Id = si.SaleId
            WHERE s.Status=2 AND s.CreatedAt BETWEEN @From AND @To
            GROUP BY si.ProductName
            ORDER BY Revenue DESC
            LIMIT @TopN
            """;
        var rows = await Conn.QueryAsync<dynamic>(sql, new { From = from, To = to, TopN = topN }, Tx);
        var list = new List<(string Name, int QuantitySold, decimal Revenue)>();
        foreach (var r in rows)
        {
            list.Add(((string)r.Name, (int)(long)r.QuantitySold, (decimal)r.Revenue));
        }
        return list;
    }

    public async Task<IEnumerable<(int Hour, int Count, decimal Amount)>> GetHourlySalesByDateRangeAsync(DateTime from, DateTime to)
    {
        await EnsureOpenAsync();
        const string sql = """
            SELECT
                CAST(HOUR(CreatedAt) AS SIGNED) AS Hour,
                COUNT(*)                        AS Count,
                COALESCE(SUM(TotalTTC),0)       AS Amount
            FROM Sale
            WHERE Status=2 AND CreatedAt BETWEEN @From AND @To
            GROUP BY HOUR(CreatedAt)
            ORDER BY Hour
            """;
        var rows = await Conn.QueryAsync<dynamic>(sql, new { From = from, To = to }, Tx);
        var list = new List<(int Hour, int Count, decimal Amount)>();
        foreach (var r in rows)
        {
            list.Add(((int)(long)r.Hour, (int)(long)r.Count, (decimal)r.Amount));
        }
        return list;
    }
}

// ── Cash Session Repository ─────────────────────────────────────────────────

public class CashSessionRepository : BaseRepository<CashSession>, ICashSessionRepository
{
    protected override string TableName => "CashSession";
    public CashSessionRepository(MySqlConnection c) : base(c) { }

    public override async Task<long> InsertAsync(CashSession s)
    {
        await EnsureOpenAsync();
        const string sql = """
            INSERT INTO CashSession (UserId, OpeningFloat, OpenedAt, Status, CreatedAt, UpdatedAt)
            VALUES (@UserId, @OpeningFloat, @OpenedAt, @Status, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;
        return await Conn.ExecuteScalarAsync<long>(sql, s, Tx);
    }

    public override async Task UpdateAsync(CashSession s)
    {
        await EnsureOpenAsync();
        await Conn.ExecuteAsync(
            "UPDATE CashSession SET Status=@Status, ClosingExpected=@ClosingExpected, ClosingCounted=@ClosingCounted, ClosingDiscrepancy=@ClosingDiscrepancy, ClosedAt=@ClosedAt, Notes=@Notes, UpdatedAt=UTC_TIMESTAMP() WHERE Id=@Id",
            s, Tx);
    }

    public async Task<CashSession?> GetOpenSessionAsync()
    {
        await EnsureOpenAsync();
        return await Conn.QueryFirstOrDefaultAsync<CashSession>(
            "SELECT * FROM CashSession WHERE Status=1 LIMIT 1", transaction: Tx);
    }

    public async Task<IEnumerable<CashSession>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<CashSession>(
            "SELECT * FROM CashSession WHERE OpenedAt BETWEEN @From AND @To ORDER BY OpenedAt DESC",
            new { From = from, To = to }, Tx);
    }

    public async Task CloseSessionAsync(long sessionId, decimal counted, DateTime closedAt, string? notes)
    {
        await EnsureOpenAsync();
        var openingFloat = await Conn.ExecuteScalarAsync<decimal>(
            "SELECT OpeningFloat FROM CashSession WHERE Id=@Id",
            new { Id = sessionId }, Tx);
        var salesTotal = await Conn.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(TotalTTC),0) FROM Sale WHERE CashSessionId=@Id AND Status=2",
            new { Id = sessionId }, Tx);
        var expensesTotal = await Conn.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(Amount),0) FROM Expense WHERE CashSessionId=@Id",
            new { Id = sessionId }, Tx);
        decimal expected = Math.Round(openingFloat + salesTotal - expensesTotal, 2);

        await Conn.ExecuteAsync(
            "UPDATE CashSession SET Status=2, ClosingExpected=@Expected, ClosingCounted=@Counted, ClosingDiscrepancy=@Disc, ClosedAt=@ClosedAt, Notes=@Notes, UpdatedAt=UTC_TIMESTAMP() WHERE Id=@Id",
            new { Id = sessionId, Expected = expected, Counted = counted, Disc = Math.Round(counted - expected, 2), ClosedAt = closedAt, Notes = notes }, Tx);
    }
}

// ── Stock Movement Repository ───────────────────────────────────────────────

public class StockMovementRepository : BaseRepository<StockMovement>, IStockMovementRepository
{
    protected override string TableName => "StockMovement";
    public StockMovementRepository(MySqlConnection c) : base(c) { }

    public override async Task<long> InsertAsync(StockMovement m)
    {
        await EnsureOpenAsync();
        const string sql = """
            INSERT INTO StockMovement (ProductId, Type, QuantityBefore, QuantityChange, QuantityAfter, SaleId, UserId, Reason, CreatedAt, UpdatedAt)
            VALUES (@ProductId, @Type, @QuantityBefore, @QuantityChange, @QuantityAfter, @SaleId, @UserId, @Reason, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;
        return await Conn.ExecuteScalarAsync<long>(sql, m, Tx);
    }

    public override Task UpdateAsync(StockMovement m) => Task.CompletedTask; // Immutable

    public async Task<IEnumerable<StockMovement>> GetByProductAsync(long productId)
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<StockMovement>(
            "SELECT * FROM StockMovement WHERE ProductId=@ProductId ORDER BY CreatedAt DESC",
            new { ProductId = productId }, Tx);
    }

    public async Task<IEnumerable<StockMovement>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<StockMovement>(
            "SELECT * FROM StockMovement WHERE CreatedAt BETWEEN @From AND @To ORDER BY CreatedAt DESC",
            new { From = from, To = to }, Tx);
    }
}

// ── Customer Repository ────────────────────────────────────────────────────

public class CustomerRepository : BaseRepository<Customer>, ICustomerRepository
{
    protected override string TableName => "Customer";
    public CustomerRepository(MySqlConnection c) : base(c) { }

    public override async Task<long> InsertAsync(Customer c)
    {
        await EnsureOpenAsync();
        const string sql = """
            INSERT INTO Customer (Name, Phone, Email, IsActive, CreatedAt, UpdatedAt)
            VALUES (@Name, @Phone, @Email, @IsActive, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;
        return await Conn.ExecuteScalarAsync<long>(sql, c, Tx);
    }

    public override async Task UpdateAsync(Customer cust)
    {
        await EnsureOpenAsync();
        await Conn.ExecuteAsync(
            "UPDATE Customer SET Name=@Name, Phone=@Phone, Email=@Email, IsActive=@IsActive, UpdatedAt=UTC_TIMESTAMP() WHERE Id=@Id",
            cust, Tx);
    }

    public async Task<Customer?> GetByPhoneAsync(string phone)
    {
        await EnsureOpenAsync();
        return await Conn.QueryFirstOrDefaultAsync<Customer>(
            "SELECT * FROM Customer WHERE Phone=@Phone AND IsActive=1 LIMIT 1",
            new { Phone = phone }, Tx);
    }

    public async Task<IEnumerable<Customer>> SearchAsync(string query)
    {
        await EnsureOpenAsync();
        var like = $"%{query}%";
        return await Conn.QueryAsync<Customer>(
            "SELECT * FROM Customer WHERE IsActive=1 AND (Name LIKE @Like OR Phone LIKE @Like) LIMIT 30",
            new { Like = like }, Tx);
    }

    public async Task UpdatePurchaseStatsAsync(long customerId, decimal amount)
    {
        await EnsureOpenAsync();
        await Conn.ExecuteAsync(
            "UPDATE Customer SET TotalPurchases=TotalPurchases+@Amount, VisitCount=VisitCount+1, UpdatedAt=UTC_TIMESTAMP() WHERE Id=@Id",
            new { Id = customerId, Amount = amount }, Tx);
    }
}

// ── Expense Repository ─────────────────────────────────────────────────────

public class ExpenseRepository : BaseRepository<Expense>, IExpenseRepository
{
    protected override string TableName => "Expense";
    public ExpenseRepository(MySqlConnection c) : base(c) { }

    public override async Task<long> InsertAsync(Expense e)
    {
        await EnsureOpenAsync();
        const string sql = """
            INSERT INTO Expense (CashSessionId, UserId, Description, Amount, Category, CreatedAt, UpdatedAt)
            VALUES (@CashSessionId, @UserId, @Description, @Amount, @Category, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;
        return await Conn.ExecuteScalarAsync<long>(sql, e, Tx);
    }

    public override Task UpdateAsync(Expense e) => Task.CompletedTask;

    public async Task<IEnumerable<Expense>> GetBySessionAsync(long sessionId)
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<Expense>(
            "SELECT * FROM Expense WHERE CashSessionId=@Id ORDER BY CreatedAt",
            new { Id = sessionId }, Tx);
    }

    public async Task<decimal> GetTotalBySessionAsync(long sessionId)
    {
        await EnsureOpenAsync();
        return await Conn.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(Amount),0) FROM Expense WHERE CashSessionId=@Id",
            new { Id = sessionId }, Tx);
    }
}

// ── Return Repository ──────────────────────────────────────────────────────

public class ReturnRepository : BaseRepository<Return>, IReturnRepository
{
    protected override string TableName => "Return";
    public ReturnRepository(MySqlConnection c) : base(c) { }

    public override async Task<long> InsertAsync(Return r)
    {
        await EnsureOpenAsync();
        const string sql = """
            INSERT INTO `Return` (OriginalSaleId, UserId, Reason, RefundAmount, RefundMethod, CreatedAt, UpdatedAt)
            VALUES (@OriginalSaleId, @UserId, @Reason, @RefundAmount, @RefundMethod, UTC_TIMESTAMP(), UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;
        return await Conn.ExecuteScalarAsync<long>(sql, r, Tx);
    }

    public override Task UpdateAsync(Return r) => Task.CompletedTask;

    public async Task<IEnumerable<Return>> GetBySaleAsync(long saleId)
    {
        await EnsureOpenAsync();
        return await Conn.QueryAsync<Return>(
            "SELECT * FROM `Return` WHERE OriginalSaleId=@Id ORDER BY CreatedAt",
            new { Id = saleId }, Tx);
    }
}

// ── Setting Repository ─────────────────────────────────────────────────────

public class SettingRepository : ISettingRepository
{
    private readonly MySqlConnection _conn;
    private MySqlTransaction? _tx;

    public SettingRepository(MySqlConnection c) => _conn = c;
    public void SetTransaction(MySqlTransaction? tx) => _tx = tx;

    private async Task EnsureOpenAsync()
    {
        if (_conn.State != System.Data.ConnectionState.Open) await _conn.OpenAsync();
    }

    public async Task<string?> GetValueAsync(string key)
    {
        await EnsureOpenAsync();
        return await _conn.ExecuteScalarAsync<string?>(
            "SELECT Value FROM Setting WHERE `Key`=@Key LIMIT 1", new { Key = key }, _tx);
    }

    public async Task SetValueAsync(string key, string value)
    {
        await EnsureOpenAsync();
        await _conn.ExecuteAsync(
            "INSERT INTO Setting (`Key`, Value, UpdatedAt) VALUES (@Key, @Value, UTC_TIMESTAMP()) ON DUPLICATE KEY UPDATE Value=@Value, UpdatedAt=UTC_TIMESTAMP()",
            new { Key = key, Value = value }, _tx);
    }

    public async Task<IEnumerable<Setting>> GetAllAsync()
    {
        await EnsureOpenAsync();
        return await _conn.QueryAsync<Setting>("SELECT * FROM Setting WHERE IsSecret=0 ORDER BY `Key`", transaction: _tx);
    }
}

// ── Audit Repository ───────────────────────────────────────────────────────

public class AuditRepository : IAuditRepository
{
    private MySqlConnection _conn;
    private MySqlTransaction? _tx;

    public AuditRepository(MySqlConnection c) => _conn = c;
    public void SetTransaction(MySqlTransaction? tx) => _tx = tx;

    private async Task EnsureOpenAsync()
    {
        if (_conn.State != System.Data.ConnectionState.Open) await _conn.OpenAsync();
    }

    public async Task LogAsync(AuditEvent ev)
    {
        await EnsureOpenAsync();
        // Audit uses its OWN connection to ensure it's always written, even if main tx fails
        const string sql = """
            INSERT INTO AuditEvent (UserId, UserName, Action, TargetEntity, TargetId, Details, IpOrMachine, CreatedAt, UpdatedAt)
            VALUES (@UserId, @UserName, @Action, @TargetEntity, @TargetId, @Details, @IpOrMachine, UTC_TIMESTAMP(), UTC_TIMESTAMP())
            """;
        await _conn.ExecuteAsync(sql, ev, _tx);
    }

    public async Task<IEnumerable<AuditEvent>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        await EnsureOpenAsync();
        return await _conn.QueryAsync<AuditEvent>(
            "SELECT * FROM AuditEvent WHERE CreatedAt BETWEEN @From AND @To ORDER BY CreatedAt DESC",
            new { From = from, To = to });
    }

    public async Task<IEnumerable<AuditEvent>> GetByUserAsync(long userId)
    {
        await EnsureOpenAsync();
        return await _conn.QueryAsync<AuditEvent>(
            "SELECT * FROM AuditEvent WHERE UserId=@UserId ORDER BY CreatedAt DESC",
            new { UserId = userId });
    }
}
