namespace Go212.POS.Application.DTOs;

public record ZReportDto(
    long SessionId,
    string CashierName,
    DateTime OpenedAt,
    DateTime ClosedAt,
    decimal OpeningFloat,
    int TotalSalesCount,
    decimal TotalHT,
    decimal TotalTax,
    decimal TotalTTC,
    decimal TotalDiscount,
    decimal CashSales,
    decimal CardSales,
    decimal TotalExpenses,
    decimal ExpectedCashInDrawer,
    decimal CountedCashInDrawer,
    decimal Discrepancy,
    List<VatBreakdownDto> VatBreakdown,
    List<TopProductSaleDto> TopProducts
);

public record VatBreakdownDto(
    decimal TaxRate,
    decimal BaseHT,
    decimal TaxAmount,
    decimal TotalTTC
);

public record TopProductSaleDto(
    string ProductName,
    int QuantitySold,
    decimal RevenueTTC
);

public record HourlySalesDto(
    int Hour,
    int TransactionCount,
    decimal TotalAmount
);

public record DailyFinancialSummaryDto(
    DateTime Date,
    int TotalTransactions,
    decimal TotalRevenueTTC,
    decimal TotalHT,
    decimal TotalTax,
    decimal CashCollected,
    decimal CardCollected,
    decimal TotalDiscounts,
    decimal TotalExpenses,
    int LowStockAlertsCount
);
