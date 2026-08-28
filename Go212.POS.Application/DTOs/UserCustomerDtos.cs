using Go212.POS.Domain.Enums;

namespace Go212.POS.Application.DTOs;

public record UserDto(
    long Id,
    string Name,
    string Username,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt,
    bool IsLocked,
    DateTime? LockedUntil
);

public record CustomerDto(
    long Id,
    string Name,
    string? Phone,
    string? Email,
    decimal TotalPurchases,
    int VisitCount,
    decimal CurrentCredit,
    decimal CreditLimit,
    bool IsActive
);

public record ExpenseDto(
    long Id,
    long CashSessionId,
    long UserId,
    string UserName,
    string Description,
    decimal Amount,
    string? Category,
    DateTime CreatedAt
);

public record ReturnDto(
    long Id,
    long OriginalSaleId,
    string OriginalSaleNumber,
    long UserId,
    string UserName,
    string Reason,
    decimal RefundAmount,
    PaymentMethod RefundMethod,
    DateTime CreatedAt,
    List<ReturnItemDto> Items
);

public record ReturnItemDto(
    long Id,
    long ReturnId,
    long SaleItemId,
    string ProductName,
    int Quantity,
    bool RestockItem
);

public record SettingDto(
    string Key,
    string Value,
    string? Description,
    bool IsSecret
);

public record AuditLogDto(
    long Id,
    long? UserId,
    string UserName,
    AuditAction Action,
    string? TargetEntity,
    long? TargetId,
    string? Details,
    string IpOrMachine,
    DateTime CreatedAt
);
