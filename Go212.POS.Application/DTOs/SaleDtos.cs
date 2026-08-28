using Go212.POS.Domain.Enums;

namespace Go212.POS.Application.DTOs;

public record SaleDto(
    long Id,
    string SaleNumber,
    long UserId,
    string UserName,
    long? CustomerId,
    string? CustomerName,
    long CashSessionId,
    SaleStatus Status,
    decimal SubtotalHT,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal TotalTTC,
    DateTime CreatedAt,
    List<SaleItemDto> Items,
    List<PaymentDto> Payments
);

public record SaleItemDto(
    long Id,
    long SaleId,
    long ProductId,
    string ProductName,
    string ProductBarcode,
    int Quantity,
    decimal UnitPriceHT,
    decimal TaxRate,
    decimal DiscountPercent,
    decimal LineTotalHT,
    decimal LineTaxAmount,
    decimal LineTotalTTC,
    string? Note
);

public record PaymentDto(
    long Id,
    long SaleId,
    PaymentMethod Method,
    decimal Amount,
    bool IsSuccess,
    string? CardTerminalRef,
    string? CardType,
    string? Notes,
    DateTime CreatedAt
);

public record HoldSaleDto(
    long SaleId,
    string SaleNumber,
    int ItemCount,
    decimal TotalTTC,
    DateTime HeldAt,
    string CashierName
);

public record CartItemDto(
    long ProductId,
    string Name,
    string? Barcode,
    decimal PriceHT,
    decimal TaxRate,
    int Quantity,
    decimal DiscountPercent = 0m,
    string? ImagePath = null
)
{
    public decimal PriceTTC => Math.Round(PriceHT * (1 + TaxRate / 100m), 2);
    public decimal LineTotalHT => Math.Round(PriceHT * Quantity * (1 - DiscountPercent / 100m), 2);
    public decimal LineTaxAmount => Math.Round(LineTotalHT * TaxRate / 100m, 2);
    public decimal LineTotalTTC => LineTotalHT + LineTaxAmount;
}
