using Go212.POS.Application.Interfaces;
using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Exceptions;
using Go212.POS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Go212.POS.Application.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IUnitOfWork uow, ILogger<ProductService> logger)
    { _uow = uow; _logger = logger; }

    public Task<IEnumerable<Product>> GetAllActiveAsync()          => _uow.Products.GetActiveAsync();
    public Task<IEnumerable<Product>> GetByCategoryAsync(long cid) => _uow.Products.GetByCategoryAsync(cid);
    public Task<Product?> GetByBarcodeAsync(string barcode)        => _uow.Products.GetByBarcodeAsync(barcode);
    public Task<IEnumerable<Product>> SearchAsync(string query)    => _uow.Products.SearchAsync(query);

    public async Task<long> CreateAsync(Product product)
    {
        // Validate barcode uniqueness
        if (!string.IsNullOrEmpty(product.Barcode))
        {
            var existing = await _uow.Products.GetByBarcodeAsync(product.Barcode);
            if (existing is not null)
                throw new BusinessRuleException($"Le code-barres '{product.Barcode}' est déjà utilisé par '{existing.Name}'.");
        }

        var id = await _uow.Products.InsertAsync(product);
        _logger.LogInformation("Product '{Name}' created (Id={Id})", product.Name, id);
        return id;
    }

    public async Task UpdateAsync(Product product)
    {
        var existing = await _uow.Products.GetByIdAsync(product.Id)
            ?? throw new EntityNotFoundException(nameof(Product), product.Id);

        await _uow.Products.UpdateAsync(product);
        _logger.LogInformation("Product '{Name}' (Id={Id}) updated", product.Name, product.Id);
    }

    public async Task DeactivateAsync(long productId)
    {
        var product = await _uow.Products.GetByIdAsync(productId)
            ?? throw new EntityNotFoundException(nameof(Product), productId);

        product.IsActive = false;
        await _uow.Products.UpdateAsync(product);
        _logger.LogInformation("Product '{Name}' (Id={Id}) deactivated", product.Name, productId);
    }
}

public class StockService : IStockService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<StockService> _logger;

    public StockService(IUnitOfWork uow, ILogger<StockService> logger)
    { _uow = uow; _logger = logger; }

    public async Task AdjustStockAsync(long productId, int quantityChange, string reason, long userId)
    {
        var product = await _uow.Products.GetByIdAsync(productId)
            ?? throw new EntityNotFoundException(nameof(Product), productId);

        int newQty = product.StockQuantity + quantityChange;
        var movement = new StockMovement
        {
            ProductId      = productId,
            Type           = quantityChange > 0 ? Domain.Enums.StockMovementType.Entry : Domain.Enums.StockMovementType.Adjustment,
            QuantityBefore = product.StockQuantity,
            QuantityChange = quantityChange,
            QuantityAfter  = newQty,
            UserId         = userId,
            Reason         = reason,
        };

        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.Products.UpdateStockAsync(productId, newQty);
            await _uow.StockMovements.InsertAsync(movement);
            await _uow.Audit.LogAsync(new AuditEvent
            {
                UserId = userId, Action = Domain.Enums.AuditAction.StockAdjusted,
                TargetEntity = nameof(Product), TargetId = productId,
                Details = $"Stock {(quantityChange > 0 ? "+" : "")}{quantityChange} — {reason}",
                IpOrMachine = Environment.MachineName
            });
            await _uow.CommitAsync();
            _logger.LogInformation("Stock adjusted for product {Id}: {Change} ({Reason})", productId, quantityChange, reason);
        }
        catch { await _uow.RollbackAsync(); throw; }
    }

    public Task<IEnumerable<StockMovement>> GetHistoryAsync(long productId)
        => _uow.StockMovements.GetByProductAsync(productId);

    public Task<IEnumerable<Product>> GetLowStockProductsAsync()
        => _uow.Products.GetLowStockAsync();
}

public class SessionService : ISessionService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SessionService> _logger;

    public SessionService(IUnitOfWork uow, ILogger<SessionService> logger)
    { _uow = uow; _logger = logger; }

    public async Task<CashSession> OpenSessionAsync(long userId, decimal openingFloat)
    {
        var existing = await _uow.CashSessions.GetOpenSessionAsync();
        if (existing is not null)
            throw new BusinessRuleException("Une session de caisse est déjà ouverte. Clôturez-la avant d'en ouvrir une nouvelle.");

        var session = new CashSession
        {
            UserId = userId,
            OpeningFloat = openingFloat,
            OpenedAt = DateTime.UtcNow,
            Status = Domain.Enums.SessionStatus.Open,
        };
        session.Id = await _uow.CashSessions.InsertAsync(session);

        await _uow.Audit.LogAsync(new AuditEvent
        {
            UserId = userId, Action = Domain.Enums.AuditAction.SessionOpened,
            TargetEntity = nameof(CashSession), TargetId = session.Id,
            Details = $"Session opened with float: {openingFloat:F2} MAD",
            IpOrMachine = Environment.MachineName
        });

        _logger.LogInformation("Cash session {Id} opened by user {UserId}, float: {Float}", session.Id, userId, openingFloat);
        return session;
    }

    public Task<CashSession?> GetCurrentOpenSessionAsync()
        => _uow.CashSessions.GetOpenSessionAsync();

    public async Task<CashSession> CloseSessionAsync(long sessionId, decimal countedCash, string? notes)
    {
        var session = await _uow.CashSessions.GetByIdAsync(sessionId)
            ?? throw new EntityNotFoundException(nameof(CashSession), sessionId);

        if (session.Status != Domain.Enums.SessionStatus.Open)
            throw new BusinessRuleException("Cette session de caisse est déjà clôturée.");

        await _uow.CashSessions.CloseSessionAsync(sessionId, countedCash, DateTime.UtcNow, notes);

        await _uow.Audit.LogAsync(new AuditEvent
        {
            UserId = session.UserId, Action = Domain.Enums.AuditAction.SessionClosed,
            TargetEntity = nameof(CashSession), TargetId = sessionId,
            Details = $"Session closed. Counted: {countedCash:F2} MAD",
            IpOrMachine = Environment.MachineName
        });

        return (await _uow.CashSessions.GetByIdAsync(sessionId))!;
    }
}

public class ReturnService : IReturnService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ReturnService> _logger;

    public ReturnService(IUnitOfWork uow, ILogger<ReturnService> logger)
    { _uow = uow; _logger = logger; }

    public async Task<Return> ProcessReturnAsync(long originalSaleId, ReturnRequest request, long userId)
    {
        var sale = await _uow.Sales.GetWithDetailsAsync(originalSaleId)
            ?? throw new EntityNotFoundException(nameof(Sale), originalSaleId);

        if (sale.Status != Domain.Enums.SaleStatus.Completed)
            throw new BusinessRuleException("Seules les ventes complétées peuvent faire l'objet d'un retour.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BusinessRuleException("Un motif de retour est obligatoire.");

        decimal refundAmount = 0;
        var returnItems = new List<ReturnItem>();

        foreach (var ri in request.Items)
        {
            var saleItem = sale.Items.FirstOrDefault(i => i.Id == ri.SaleItemId)
                ?? throw new BusinessRuleException($"Article {ri.SaleItemId} non trouvé dans la vente.");

            if (ri.Quantity > saleItem.Quantity)
                throw new BusinessRuleException($"Impossible de retourner {ri.Quantity} unités — seulement {saleItem.Quantity} vendues.");

            refundAmount += saleItem.LineTotalTTC * ri.Quantity / saleItem.Quantity;
            returnItems.Add(new ReturnItem { SaleItemId = ri.SaleItemId, Quantity = ri.Quantity, RestockItem = ri.Restock });
        }

        var returnEntity = new Return
        {
            OriginalSaleId = originalSaleId,
            UserId         = userId,
            Reason         = request.Reason,
            RefundAmount   = Math.Round(refundAmount, 2),
            RefundMethod   = request.RefundMethod,
            Items          = returnItems,
        };

        await _uow.BeginTransactionAsync();
        try
        {
            returnEntity.Id = await _uow.Returns.InsertAsync(returnEntity);

            // Restock if requested
            foreach (var ri in request.Items.Where(r => r.Restock))
            {
                var saleItem = sale.Items.First(i => i.Id == ri.SaleItemId);
                var product  = await _uow.Products.GetByIdAsync(saleItem.ProductId);
                if (product is null) continue;
                int newQty = product.StockQuantity + ri.Quantity;
                await _uow.Products.UpdateStockAsync(product.Id, newQty);
                await _uow.StockMovements.InsertAsync(new StockMovement
                {
                    ProductId = product.Id, Type = Domain.Enums.StockMovementType.Return,
                    QuantityBefore = product.StockQuantity, QuantityChange = ri.Quantity, QuantityAfter = newQty,
                    UserId = userId, Reason = $"Retour vente #{originalSaleId}: {request.Reason}",
                });
            }

            await _uow.Sales.UpdateStatusAsync(originalSaleId, Domain.Enums.SaleStatus.Refunded, request.Reason, userId);

            await _uow.Audit.LogAsync(new AuditEvent
            {
                UserId = userId, Action = Domain.Enums.AuditAction.SaleRefunded,
                TargetEntity = nameof(Sale), TargetId = originalSaleId,
                Details = $"Return processed. Refund: {refundAmount:F2} MAD — {request.Reason}",
                IpOrMachine = Environment.MachineName
            });

            await _uow.CommitAsync();
            _logger.LogInformation("Return processed for sale {SaleId}, refund: {Amount} MAD", originalSaleId, refundAmount);
            return returnEntity;
        }
        catch { await _uow.RollbackAsync(); throw; }
    }
}

public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;
    public ReportService(IUnitOfWork uow) => _uow = uow;

    public async Task<DailySalesReport> GetDailyReportAsync(DateTime date)
    {
        var from  = date.Date;
        var to    = date.Date.AddDays(1);
        var sales = (await _uow.Sales.GetByDateRangeAsync(from, to))
                    .Where(s => s.Status == Domain.Enums.SaleStatus.Completed).ToList();

        var (cashTotal, cardTotal) = await _uow.Sales.GetPaymentTotalsByDateRangeAsync(from, to);

        var topProductsRaw = await _uow.Sales.GetTopProductsByDateRangeAsync(from, to);
        var topProducts = topProductsRaw
            .Select(t => new TopProductItem(t.Name, t.QuantitySold, t.Revenue))
            .ToList();

        var hourlyRaw = await _uow.Sales.GetHourlySalesByDateRangeAsync(from, to);
        var hourlySales = hourlyRaw
            .Select(h => new HourlySaleItem(h.Hour, h.Count, h.Amount))
            .ToList();

        return new DailySalesReport(
            Date: date,
            SaleCount: sales.Count,
            TotalHT: Math.Round(sales.Sum(s => s.SubtotalHT), 2),
            TotalTax: Math.Round(sales.Sum(s => s.TaxAmount), 2),
            TotalTTC: Math.Round(sales.Sum(s => s.TotalTTC), 2),
            CashTotal: Math.Round(cashTotal, 2),
            CardTotal: Math.Round(cardTotal, 2),
            TopProducts: topProducts,
            HourlySales: hourlySales
        );
    }

    public Task<IEnumerable<Sale>> GetSalesByDateRangeAsync(DateTime from, DateTime to)
        => _uow.Sales.GetByDateRangeAsync(from, to);

    public async Task<byte[]> ExportDailyReportPdfAsync(DateTime date)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var report = await GetDailyReportAsync(date);

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(QuestPDF.Helpers.PageSizes.A4);
                page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(QuestPDF.Helpers.Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(QuestPDF.Helpers.Fonts.Arial));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);

                void ComposeHeader(QuestPDF.Infrastructure.IContainer container)
                {
                    container.Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Text($"Rapport Journalier GO212 POS").FontSize(20).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                            column.Item().Text($"Date : {report.Date:dd/MM/yyyy}");
                        });
                        row.ConstantItem(100).AlignRight().Text($"Ventes : {report.SaleCount}").FontSize(14).SemiBold();
                    });
                }

                void ComposeContent(QuestPDF.Infrastructure.IContainer container)
                {
                    container.PaddingVertical(1, QuestPDF.Infrastructure.Unit.Centimetre).Column(column =>
                    {
                        column.Spacing(20);

                        // Summary Table
                        column.Item().Text("Résumé des Ventes").FontSize(14).SemiBold();
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Total HT");
                                header.Cell().Element(CellStyle).Text("Total TVA");
                                header.Cell().Element(CellStyle).Text("Espèces");
                                header.Cell().Element(CellStyle).Text("Total TTC").SemiBold();
                                
                                QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                {
                                    return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Black);
                                }
                            });

                            table.Cell().Element(CellStyle).Text($"{report.TotalHT:F2} MAD");
                            table.Cell().Element(CellStyle).Text($"{report.TotalTax:F2} MAD");
                            table.Cell().Element(CellStyle).Text($"{report.CashTotal:F2} MAD");
                            table.Cell().Element(CellStyle).Text($"{report.TotalTTC:F2} MAD").SemiBold();
                            
                            QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                            {
                                return container.BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).PaddingVertical(5);
                            }
                        });

                        // Top Products
                        if (report.TopProducts.Any())
                        {
                            column.Item().Text("Top 10 Produits").FontSize(14).SemiBold();
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Produit");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Qté");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Revenu");
                                    
                                    QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                    {
                                        return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Black);
                                    }
                                });

                                foreach (var item in report.TopProducts)
                                {
                                    table.Cell().Element(CellStyle).Text(item.Name);
                                    table.Cell().Element(CellStyle).AlignRight().Text(item.QuantitySold.ToString());
                                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.Revenue:F2} MAD");
                                    
                                    QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                    {
                                        return container.BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).PaddingVertical(5);
                                    }
                                }
                            });
                        }
                        
                        // Hourly Sales
                        if (report.HourlySales.Any())
                        {
                            column.Item().Text("Ventes par Heure").FontSize(14).SemiBold();
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Heure");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Tickets");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Montant");
                                    
                                    QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                    {
                                        return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Black);
                                    }
                                });

                                foreach (var item in report.HourlySales.OrderBy(h => h.Hour))
                                {
                                    table.Cell().Element(CellStyle).Text($"{item.Hour:D2}:00 - {item.Hour:D2}:59");
                                    table.Cell().Element(CellStyle).AlignRight().Text(item.Count.ToString());
                                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.Amount:F2} MAD");
                                    
                                    QuestPDF.Infrastructure.IContainer CellStyle(QuestPDF.Infrastructure.IContainer container)
                                    {
                                        return container.BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten2).PaddingVertical(5);
                                    }
                                }
                            });
                        }
                    });
                }

                void ComposeFooter(QuestPDF.Infrastructure.IContainer container)
                {
                    container.AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                }
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> ExportDailyReportCsvAsync(DateTime date)
    {
        var from  = date.Date;
        var to    = date.Date.AddDays(1);
        var sales = (await _uow.Sales.GetByDateRangeAsync(from, to))
                    .Where(s => s.Status == Domain.Enums.SaleStatus.Completed).ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Numero_Vente;Date_Heure;SousTotal_HT;Montant_TVA;Remise;Total_TTC;Statut");

        foreach (var s in sales)
        {
            sb.AppendLine($"{s.SaleNumber};{s.CreatedAt:yyyy-MM-dd HH:mm:ss};{s.SubtotalHT:F2};{s.TaxAmount:F2};{s.DiscountAmount:F2};{s.TotalTTC:F2};{s.Status}");
        }

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }
}
