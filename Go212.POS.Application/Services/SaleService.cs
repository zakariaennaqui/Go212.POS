using Go212.POS.Application.Interfaces;
using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Enums;
using Go212.POS.Domain.Exceptions;
using Go212.POS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Go212.POS.Application.Services;

/// <summary>
/// Core POS sale use cases.
///
/// Critical business rules (from CDC):
///  1. A completed sale is NEVER deleted — cancel/refund only
///  2. Sale + payment + stock deduction are ONE atomic MySQL transaction
///  3. If ANY step fails → full rollback → no half-sale ever written
///  4. Prices are SNAPSHOTTED at time of sale (price changes don't affect past sales)
///  5. Calculations are done HERE (business layer), never only in the screen
///  6. Double-click protection: check sale status before processing payment
/// </summary>
public class SaleService : ISaleService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<SaleService> _logger;

    public SaleService(IUnitOfWork uow, ILogger<SaleService> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    /// <summary>Create a new empty sale and open it. Requires an open cash session.</summary>
    public async Task<Sale> CreateSaleAsync(long sessionId, long userId, long? customerId)
    {
        var session = await _uow.CashSessions.GetByIdAsync(sessionId)
            ?? throw new NoOpenSessionException();

        if (session.Status != SessionStatus.Open)
            throw new NoOpenSessionException();

        var saleNumber = await _uow.Sales.GenerateSaleNumberAsync();

        var sale = new Sale
        {
            SaleNumber    = saleNumber,
            UserId        = userId,
            CustomerId    = customerId,
            CashSessionId = sessionId,
            Status        = SaleStatus.Open,
        };

        sale.Id = await _uow.Sales.InsertAsync(sale);
        _logger.LogInformation("New sale {SaleNumber} created by user {UserId}", saleNumber, userId);
        return sale;
    }

    /// <summary>Add or increment a product in the sale cart.</summary>
    public async Task AddItemAsync(long saleId, long productId, int quantity, decimal discountPercent = 0)
    {
        var sale = await _uow.Sales.GetByIdAsync(saleId)
            ?? throw new EntityNotFoundException(nameof(Sale), saleId);

        if (sale.Status != SaleStatus.Open)
            throw new BusinessRuleException("Impossible d'ajouter un produit à une vente déjà clôturée.");

        var product = await _uow.Products.GetByIdAsync(productId)
            ?? throw new EntityNotFoundException(nameof(Product), productId);

        if (!product.IsActive)
            throw new BusinessRuleException($"Le produit '{product.Name}' est inactif.");

        // Snapshot prices at time of sale — not affected by future price changes
        var lineHT  = Math.Round(product.PriceHT * quantity * (1 - discountPercent / 100), 2);
        var lineTax = Math.Round(lineHT * product.TaxRate / 100, 2);

        var item = new SaleItem
        {
            SaleId          = saleId,
            ProductId       = productId,
            ProductName     = product.Name,
            ProductBarcode  = product.Barcode ?? string.Empty,
            Quantity        = quantity,
            UnitPriceHT     = product.PriceHT,   // SNAPSHOT
            TaxRate         = product.TaxRate,    // SNAPSHOT
            DiscountPercent = discountPercent,
            // LineTotalHT / LineTaxAmount / LineTotalTTC are computed properties
        };

        await _uow.Sales.InsertItemAsync(item);
        await RecalculateTotalsAsync(saleId);
    }

    /// <summary>Remove one item from the open sale cart.</summary>
    public async Task RemoveItemAsync(long saleId, long saleItemId)
    {
        var sale = await _uow.Sales.GetByIdAsync(saleId)
            ?? throw new EntityNotFoundException(nameof(Sale), saleId);

        if (sale.Status != SaleStatus.Open)
            throw new BusinessRuleException("Impossible de modifier une vente déjà clôturée.");

        await _uow.Sales.RemoveItemAsync(saleItemId);
        await RecalculateTotalsAsync(saleId);
    }

    /// <summary>Update quantity of an existing cart item.</summary>
    public async Task UpdateItemQuantityAsync(long saleId, long saleItemId, int newQuantity)
    {
        if (newQuantity <= 0)
        {
            await RemoveItemAsync(saleId, saleItemId);
            return;
        }

        var sale = await _uow.Sales.GetByIdAsync(saleId)
            ?? throw new EntityNotFoundException(nameof(Sale), saleId);

        if (sale.Status != SaleStatus.Open)
            throw new BusinessRuleException("Impossible de modifier une vente déjà clôturée.");

        var items = await _uow.Sales.GetItemsBySaleAsync(saleId);
        var item = items.FirstOrDefault(i => i.Id == saleItemId)
            ?? throw new EntityNotFoundException(nameof(SaleItem), saleItemId);

        await _uow.Sales.UpdateItemQuantityAsync(saleItemId, newQuantity, item.DiscountPercent);
        await RecalculateTotalsAsync(saleId);
    }

    /// <summary>
    /// Complete a sale: validate → calculate → deduct stock → record payment → print.
    /// This is ONE atomic MySQL transaction. If anything fails → full rollback.
    /// DOUBLE-CLICK PROTECTION: checks sale status at start.
    /// </summary>
    public async Task<Sale> CompleteSaleAsync(long saleId, PaymentRequest payment, bool printTicket = true)
    {
        // ── Pre-checks (before opening transaction) ─────────────
        var sale = await _uow.Sales.GetWithDetailsAsync(saleId)
            ?? throw new EntityNotFoundException(nameof(Sale), saleId);

        // DOUBLE-CLICK PROTECTION: if already completed, return it as-is
        if (sale.Status == SaleStatus.Completed)
        {
            _logger.LogWarning("Double-click detected on sale {SaleId} — already completed, ignoring", saleId);
            return sale;
        }

        if (sale.Status != SaleStatus.Open && sale.Status != SaleStatus.Held)
            throw new BusinessRuleException($"La vente {sale.SaleNumber} ne peut pas être validée (statut: {sale.Status}).");

        if (!sale.Items.Any())
            throw new BusinessRuleException("Impossible de valider une vente vide.");

        // ── ATOMIC TRANSACTION: sale + stock + payment ───────────
        await _uow.BeginTransactionAsync();
        try
        {
            // 1. Recalculate totals (always server-side, never trust UI)
            await RecalculateTotalsAsync(saleId);
            sale = await _uow.Sales.GetWithDetailsAsync(saleId)!;

            // 2. Validate payment amount
            decimal totalPaid = payment.CashReceived + (payment.CardAmount ?? 0);
            if (payment.Method == PaymentMethod.Cash && payment.CashReceived < sale!.TotalTTC)
                throw new BusinessRuleException($"Montant reçu ({payment.CashReceived:F2} MAD) insuffisant. Total: {sale.TotalTTC:F2} MAD.");

            // 3. Deduct stock for each item (all or nothing)
            foreach (var item in sale!.Items)
            {
                var product = await _uow.Products.GetByIdAsync(item.ProductId)!;
                int newQty  = product!.StockQuantity - item.Quantity;

                if (newQty < 0)
                    _logger.LogWarning("Stock went negative for product {ProductId} — allowed but flagged", item.ProductId);

                var movement = new StockMovement
                {
                    ProductId      = item.ProductId,
                    Type           = StockMovementType.Sale,
                    QuantityBefore = product.StockQuantity,
                    QuantityChange = -item.Quantity,
                    QuantityAfter  = newQty,
                    SaleId         = saleId,
                    UserId         = sale.UserId,
                    Reason         = $"Vente {sale.SaleNumber}",
                };
                await _uow.StockMovements.InsertAsync(movement);
                await _uow.Products.UpdateStockAsync(item.ProductId, newQty);
            }

            // 4. Record payment — for card: ONLY type + status + terminal ref. NEVER card number.
            var paymentRecord = new Payment
            {
                SaleId         = saleId,
                Method         = payment.Method,
                Amount         = sale.TotalTTC,
                IsSuccess      = true,
                CardTerminalRef= payment.CardTerminalRef,  // No card number
                CardType       = payment.CardType,
                Notes          = payment.Method == PaymentMethod.Cash
                    ? $"Reçu: {payment.CashReceived:F2} MAD — Rendu: {payment.CashReceived - sale.TotalTTC:F2} MAD"
                    : null,
            };
            await _uow.Sales.InsertPaymentAsync(paymentRecord);

            // 5. Mark sale as completed
            await _uow.Sales.UpdateStatusAsync(saleId, SaleStatus.Completed, null, null);

            // 6. Update customer stats (if linked)
            if (sale.CustomerId.HasValue)
                await _uow.Customers.UpdatePurchaseStatsAsync(sale.CustomerId.Value, sale.TotalTTC);

            // 7. Audit
            await _uow.Audit.LogAsync(new Domain.Entities.AuditEvent
            {
                UserId       = sale.UserId,
                Action       = AuditAction.SaleCreated,
                TargetEntity = nameof(Sale),
                TargetId     = saleId,
                Details      = $"Sale {sale.SaleNumber} — Total: {sale.TotalTTC:F2} MAD — Method: {payment.Method}",
                IpOrMachine  = Environment.MachineName,
            });

            // ── Commit everything ──────────────────────────────
            await _uow.CommitAsync();

            _logger.LogInformation("Sale {SaleNumber} completed. Total: {Total:F2} MAD", sale.SaleNumber, sale.TotalTTC);
            return sale;
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            _logger.LogError(ex, "Failed to complete sale {SaleId} — rolled back", saleId);
            throw; // Re-throw so ViewModel can show user-friendly message
        }
    }

    /// <summary>Cancel an open or completed sale with mandatory reason and role check.</summary>
    public async Task CancelSaleAsync(long saleId, string reason, long cancelledByUserId)
    {
        var sale = await _uow.Sales.GetWithDetailsAsync(saleId)
            ?? throw new EntityNotFoundException(nameof(Sale), saleId);

        if (sale.Status == SaleStatus.Cancelled)
            throw new BusinessRuleException("Cette vente est déjà annulée.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("Un motif d'annulation est obligatoire.");

        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.Sales.UpdateStatusAsync(saleId, SaleStatus.Cancelled, reason, cancelledByUserId);

            // Restock items if sale was completed
            if (sale.Status == SaleStatus.Completed)
            {
                foreach (var item in sale.Items)
                {
                    var product = await _uow.Products.GetByIdAsync(item.ProductId);
                    if (product is null) continue;
                    int newQty = product.StockQuantity + item.Quantity;
                    await _uow.Products.UpdateStockAsync(item.ProductId, newQty);
                    await _uow.StockMovements.InsertAsync(new StockMovement
                    {
                        ProductId      = item.ProductId,
                        Type           = StockMovementType.Return,
                        QuantityBefore = product.StockQuantity,
                        QuantityChange = item.Quantity,
                        QuantityAfter  = newQty,
                        SaleId         = saleId,
                        UserId         = cancelledByUserId,
                        Reason         = $"Annulation vente {sale.SaleNumber}: {reason}",
                    });
                }
            }

            await _uow.Audit.LogAsync(new Domain.Entities.AuditEvent
            {
                UserId       = cancelledByUserId,
                Action       = AuditAction.SaleCancelled,
                TargetEntity = nameof(Sale),
                TargetId     = saleId,
                Details      = $"Sale {sale.SaleNumber} cancelled — Reason: {reason}",
                IpOrMachine  = Environment.MachineName,
            });

            await _uow.CommitAsync();
            _logger.LogInformation("Sale {SaleNumber} cancelled by user {UserId}", sale.SaleNumber, cancelledByUserId);
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    public async Task HoldSaleAsync(long saleId)
    {
        var sale = await _uow.Sales.GetByIdAsync(saleId)
            ?? throw new EntityNotFoundException(nameof(Sale), saleId);

        if (sale.Status != SaleStatus.Open)
            throw new BusinessRuleException($"Seule une vente ouverte peut être mise en attente (statut actuel: {sale.Status}).");

        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.Sales.UpdateStatusAsync(saleId, SaleStatus.Held,
                $"Mise en attente par utilisateur {sale.UserId}", sale.UserId);

            await _uow.Audit.LogAsync(new AuditEvent
            {
                UserId       = sale.UserId,
                Action       = AuditAction.SaleCreated,
                TargetEntity = nameof(Sale),
                TargetId     = saleId,
                Details      = $"Sale {sale.SaleNumber} put on HOLD by user {sale.UserId}",
                IpOrMachine  = Environment.MachineName,
            });

            await _uow.CommitAsync();
            _logger.LogInformation("Sale {SaleId} ({SaleNumber}) put on HOLD by user {UserId}",
                saleId, sale.SaleNumber, sale.UserId);
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    public async Task<Sale?> ResumeHeldSaleAsync(long sessionId)
    {
        var openSession = await _uow.CashSessions.GetByIdAsync(sessionId);
        if (openSession is null || openSession.Status != SessionStatus.Open)
            return null;

        var sales = await _uow.Sales.GetBySessionAsync(sessionId);
        var heldSale = sales.FirstOrDefault(s => s.Status == SaleStatus.Held);
        if (heldSale is null) return null;

        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.Sales.UpdateStatusAsync(heldSale.Id, SaleStatus.Open,
                $"Reprise de la vente en attente", heldSale.UserId);

            var saleWithDetails = await _uow.Sales.GetWithDetailsAsync(heldSale.Id);

            await _uow.Audit.LogAsync(new AuditEvent
            {
                UserId       = heldSale.UserId,
                Action       = AuditAction.SaleCreated,
                TargetEntity = nameof(Sale),
                TargetId     = heldSale.Id,
                Details      = $"Sale {heldSale.SaleNumber} RESUMED from HOLD",
                IpOrMachine  = Environment.MachineName,
            });

            await _uow.CommitAsync();
            _logger.LogInformation("Sale {SaleId} ({SaleNumber}) resumed from HOLD",
                heldSale.Id, heldSale.SaleNumber);

            return saleWithDetails;
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }
    }

    /// <summary>Recalculate sale totals from items. Always called server-side.</summary>
    private async Task RecalculateTotalsAsync(long saleId)
    {
        var sale = await _uow.Sales.GetWithDetailsAsync(saleId);
        if (sale is null) return;

        sale.SubtotalHT    = sale.Items.Sum(i => i.LineTotalHT);
        sale.TaxAmount     = sale.Items.Sum(i => i.LineTaxAmount);
        sale.DiscountAmount= sale.Items.Sum(i => i.UnitPriceHT * i.Quantity * i.DiscountPercent / 100);
        sale.TotalTTC      = sale.SubtotalHT + sale.TaxAmount;

        await _uow.Sales.UpdateAsync(sale);
    }
}
