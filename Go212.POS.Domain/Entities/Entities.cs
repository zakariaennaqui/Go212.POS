using Go212.POS.Domain.Enums;

namespace Go212.POS.Domain.Entities;

/// <summary>
/// Vendor configuration — one per installation (one store = one DB).
/// </summary>
public class Vendor : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxId { get; set; }           // ICE / IF number
    public string Currency { get; set; } = "MAD";
    public string LogoPath { get; set; } = string.Empty;
    public string ReceiptHeader { get; set; } = string.Empty;
    public string ReceiptFooter { get; set; } = string.Empty;
    public string SaleNumberPrefix { get; set; } = "VTE";
    public int SaleNumberNext { get; set; } = 1;
}

/// <summary>
/// Application user (Admin / Manager / Cashier).
/// PIN is hashed with BCrypt, never stored in plain text.
/// </summary>
public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PinHash { get; set; } = string.Empty;       // BCrypt hash
    public UserRole Role { get; set; } = UserRole.Cashier;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }               // Temporary lock
}

/// <summary>
/// Product category (can have subcategories).
/// </summary>
public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#00BF63";            // GO212 green default
    public string? IconName { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    // Populated after JOIN queries (not stored in DB)
    public int ProductCount { get; set; }
}

/// <summary>
/// Product with pricing, TVA, barcode and stock tracking.
/// </summary>
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long CategoryId { get; set; }
    public Category? Category { get; set; }
    public decimal PriceHT { get; set; }                     // Price before tax
    public decimal TaxRate { get; set; } = 20m;              // TVA %
    public decimal PriceTTC => Math.Round(PriceHT * (1 + TaxRate / 100), 2);
    public string? Barcode { get; set; }
    public string Unit { get; set; } = "pcs";
    public string? ImagePath { get; set; }
    public int StockQuantity { get; set; } = 0;
    public int StockAlertThreshold { get; set; } = 5;
    public bool IsActive { get; set; } = true;
    public bool HasVariants { get; set; } = false;
    
    // Computed
    public bool IsLowStock => StockQuantity <= StockAlertThreshold;

    // Populated after JOIN queries (not stored in DB)
    public string? CategoryName { get; set; }
}

/// <summary>
/// Simple customer for loyalty / history tracking.
/// </summary>
public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal TotalPurchases { get; set; } = 0m;
    public int VisitCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    // Credit / loyalty fields
    public decimal CurrentCredit  { get; set; } = 0m;
    public decimal CreditLimit    { get; set; } = 0m;

    // Alias — ManagementView binds to FullName
    public string FullName => Name;
}

/// <summary>
/// A sale transaction. Once Completed, it cannot be deleted — only Cancelled/Refunded.
/// </summary>
public class Sale : BaseEntity
{
    public string SaleNumber { get; set; } = string.Empty;    // VTE-20260801-001
    public long UserId { get; set; }
    public User? User { get; set; }
    public long? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public long CashSessionId { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Open;
    public decimal SubtotalHT { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal TotalTTC { get; set; }
    public string? CancellationReason { get; set; }
    public long? CancelledByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    
    public List<SaleItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    
    // Business rule: a completed sale must balance (sum of payments = TotalTTC)
    public decimal TotalPaid => Payments.Where(p => p.IsSuccess).Sum(p => p.Amount);
    public decimal Change => TotalPaid - TotalTTC;
}

/// <summary>
/// One line in a sale. Prices captured at time of sale (immutable after completion).
/// </summary>
public class SaleItem : BaseEntity
{
    public long SaleId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;   // Snapshot at sale time
    public string ProductBarcode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPriceHT { get; set; }                  // Snapshot at sale time
    public decimal TaxRate { get; set; }                      // Snapshot
    public decimal DiscountPercent { get; set; } = 0m;
    public decimal LineTotalHT => Math.Round(UnitPriceHT * Quantity * (1 - DiscountPercent / 100), 2);
    public decimal LineTaxAmount => Math.Round(LineTotalHT * TaxRate / 100, 2);
    public decimal LineTotalTTC => LineTotalHT + LineTaxAmount;
    public string? Note { get; set; }
}

/// <summary>
/// Payment record. For card: only type + status + terminal reference — NEVER card number.
/// </summary>
public class Payment : BaseEntity
{
    public long SaleId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public bool IsSuccess { get; set; } = true;
    public string? CardTerminalRef { get; set; }             // Card only — no card number
    public string? CardType { get; set; }                    // "Visa", "Mastercard", etc.
    public string? Notes { get; set; }
}

/// <summary>
/// Cash register session (opening → closing with expected vs. actual cash count).
/// </summary>
public class CashSession : BaseEntity
{
    public long UserId { get; set; }
    public User? User { get; set; }
    public decimal OpeningFloat { get; set; }                // Fond de caisse à l'ouverture
    public DateTime OpenedAt { get; set; }
    public decimal? ClosingExpected { get; set; }
    public decimal? ClosingCounted { get; set; }
    public decimal? ClosingDiscrepancy => ClosingCounted.HasValue && ClosingExpected.HasValue
        ? ClosingCounted - ClosingExpected : null;
    public DateTime? ClosedAt { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Open;
    public string? Notes { get; set; }
}

/// <summary>
/// Stock movement log (every change to stock quantity).
/// </summary>
public class StockMovement : BaseEntity
{
    public long ProductId { get; set; }
    public Product? Product { get; set; }
    public StockMovementType Type { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityChange { get; set; }                  // Positive = in, Negative = out
    public int QuantityAfter { get; set; }
    public long? SaleId { get; set; }                        // Linked sale (if applicable)
    public long UserId { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Store expense (cash out for supplies, rent, etc.).
/// </summary>
public class Expense : BaseEntity
{
    public long CashSessionId { get; set; }
    public long UserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Category { get; set; }
}

/// <summary>
/// A return/refund on a completed sale.
/// </summary>
public class Return : BaseEntity
{
    public long OriginalSaleId { get; set; }
    public long UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public PaymentMethod RefundMethod { get; set; }
    public List<ReturnItem> Items { get; set; } = [];
}

public class ReturnItem : BaseEntity
{
    public long ReturnId { get; set; }
    public long SaleItemId { get; set; }
    public int Quantity { get; set; }
    public bool RestockItem { get; set; } = true;
}

/// <summary>
/// Application setting (key-value store, versioned changes tracked in audit).
/// </summary>
public class Setting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSecret { get; set; } = false;              // Never log secret settings
}

/// <summary>
/// Immutable audit log. Every sensitive action is recorded here.
/// Sensitive data (PIN, card numbers) must NEVER appear in audit logs.
/// </summary>
public class AuditEvent : BaseEntity
{
    public long? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;    // Snapshot
    public AuditAction Action { get; set; }
    public string? TargetEntity { get; set; }               // "Sale", "Product", etc.
    public long? TargetId { get; set; }
    public string? Details { get; set; }                    // Human-readable — NO secrets
    public string IpOrMachine { get; set; } = string.Empty;
}
