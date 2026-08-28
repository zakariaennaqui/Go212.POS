using Go212.POS.Domain.Enums;

namespace Go212.POS.Application.DTOs;

public record ProductDto(
    long Id,
    string Name,
    string? Description,
    long CategoryId,
    string? CategoryName,
    decimal PriceHT,
    decimal TaxRate,
    decimal PriceTTC,
    string? Barcode,
    string Unit,
    string? ImagePath,
    int StockQuantity,
    int StockAlertThreshold,
    bool IsActive,
    bool IsLowStock
);

public record CategoryDto(
    long Id,
    string Name,
    string? Description,
    string Color,
    string? IconName,
    int DisplayOrder,
    bool IsActive,
    int ProductCount
);

public record StockMovementDto(
    long Id,
    long ProductId,
    string ProductName,
    StockMovementType Type,
    int QuantityBefore,
    int QuantityChange,
    int QuantityAfter,
    long? SaleId,
    long UserId,
    string UserName,
    string? Reason,
    DateTime CreatedAt
);

public record ProductSearchFilterDto(
    string? Query,
    long? CategoryId,
    bool? OnlyLowStock,
    bool? OnlyActive
);
