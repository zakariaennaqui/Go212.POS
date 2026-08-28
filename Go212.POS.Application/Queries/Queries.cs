using Go212.POS.Domain.Enums;
using Go212.POS.Domain.ValueObjects;

namespace Go212.POS.Application.Queries;

// ── Catalog Queries ─────────────────────────────────────────────────────────

public record GetProductsQuery(
    long? CategoryId = null,
    bool OnlyActive = true,
    bool OnlyLowStock = false
);

public record SearchProductsQuery(
    string SearchTerm,
    int MaxResults = 50
);

public record GetProductByBarcodeQuery(string Barcode);

public record GetCategoriesQuery(bool OnlyActive = true);

public record GetLowStockAlertsQuery();

// ── Sales Queries ───────────────────────────────────────────────────────────

public record GetSaleByIdQuery(long SaleId);

public record GetSaleByNumberQuery(string SaleNumber);

public record GetSalesByDateRangeQuery(DateRange Range);

public record GetSalesBySessionQuery(long SessionId);

public record GetHeldSalesQuery(long SessionId);

// ── Session & Cash Queries ──────────────────────────────────────────────────

public record GetCurrentOpenSessionQuery();

public record GetSessionsByDateRangeQuery(DateRange Range);

public record GetSessionExpensesQuery(long SessionId);

// ── Report Queries ──────────────────────────────────────────────────────────

public record GetDailyZReportQuery(DateTime Date);

public record GetSessionZReportQuery(long SessionId);

public record GetTopSellingProductsQuery(DateRange Range, int TopN = 10);

public record GetHourlySalesDistributionQuery(DateRange Range);

// ── User, Customer & Audit Queries ──────────────────────────────────────────

public record GetActiveUsersQuery();

public record GetAllUsersQuery();

public record SearchCustomersQuery(string SearchTerm);

public record GetCustomerByIdQuery(long CustomerId);

public record GetAuditLogsQuery(DateRange Range, long? UserId = null);
