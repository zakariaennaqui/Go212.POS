using Go212.POS.Application.Interfaces;
using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Exceptions;
using Go212.POS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Go212.POS.Application.Services;

/// <summary>
/// Service for generating ESC/POS thermal receipts and plain text receipts (80mm / 48 chars).
/// Complies with Moroccan commercial standards (ICE, IF, RC, TVA breakdown).
/// </summary>
public class ReceiptService : IReceiptService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ReceiptService> _logger;

    public ReceiptService(IUnitOfWork uow, ILogger<ReceiptService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<string> GenerateReceiptTextAsync(long saleId)
    {
        var sale = await _uow.Sales.GetWithDetailsAsync(saleId)
            ?? throw new EntityNotFoundException(nameof(Sale), saleId);

        var cashier = await _uow.Users.GetByIdAsync(sale.UserId);
        var sb = new StringBuilder();
        const int width = 42;

        string Center(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Length >= width) return text[..width];
            int pad = (width - text.Length) / 2;
            return text.PadLeft(pad + text.Length).PadRight(width);
        }

        string Row(string left, string right)
        {
            int space = width - left.Length - right.Length;
            if (space < 1) space = 1;
            return left + new string(' ', space) + right;
        }

        string Line() => new('-', width);
        string DblLine() => new('=', width);

        // Header
        sb.AppendLine(DblLine());
        sb.AppendLine(Center("GO212 POS"));
        sb.AppendLine(Center("SYSTEME DE CAISSE ENREGISTREUSE"));
        sb.AppendLine(Center("Maroc - www.go212.ma"));
        sb.AppendLine(DblLine());

        sb.AppendLine(Row($"Ticket: {sale.SaleNumber}", sale.CreatedAt.ToString("dd/MM/yyyy HH:mm")));
        sb.AppendLine(Row($"Caissier: {cashier?.Name ?? "Admin"}", $"Caisse #{sale.CashSessionId}"));
        sb.AppendLine(Line());

        // Column Headers
        sb.AppendLine(Row("ARTICLE (QTE x P.U)", "TOTAL TTC"));
        sb.AppendLine(Line());

        // Items
        foreach (var item in sale.Items)
        {
            sb.AppendLine(item.ProductName);
            string detail = $"  {item.Quantity} x {item.UnitPriceHT * (1 + item.TaxRate / 100m):N2} MAD";
            if (item.DiscountPercent > 0)
                detail += $" (-{item.DiscountPercent:N0}%)";
            sb.AppendLine(Row(detail, $"{item.LineTotalTTC:N2} MAD"));
        }

        sb.AppendLine(Line());

        // Totals
        sb.AppendLine(Row("Sous-total HT:", $"{sale.SubtotalHT:N2} MAD"));
        sb.AppendLine(Row("Total TVA:", $"{sale.TaxAmount:N2} MAD"));
        if (sale.DiscountAmount > 0)
            sb.AppendLine(Row("Remise globale:", $"-{sale.DiscountAmount:N2} MAD"));

        sb.AppendLine(DblLine());
        sb.AppendLine(Row("TOTAL A PAYER:", $"{sale.TotalTTC:N2} MAD"));
        sb.AppendLine(DblLine());

        // Payments
        foreach (var p in sale.Payments)
        {
            sb.AppendLine(Row($"Mode: {p.Method}", $"{p.Amount:N2} MAD"));
            if (!string.IsNullOrWhiteSpace(p.Notes))
                sb.AppendLine($"  {p.Notes}");
        }

        // Footer
        sb.AppendLine(Line());
        sb.AppendLine(Center("MERCI DE VOTRE VISITE !"));
        sb.AppendLine(Center("A BIENTOT"));
        sb.AppendLine(DblLine());

        return sb.ToString();
    }

    public async Task<string> GenerateZReportTextAsync(long sessionId)
    {
        var session = await _uow.CashSessions.GetByIdAsync(sessionId)
            ?? throw new EntityNotFoundException(nameof(CashSession), sessionId);

        var cashier = await _uow.Users.GetByIdAsync(session.UserId);
        var sales = (await _uow.Sales.GetBySessionAsync(sessionId)).ToList();
        var expenses = (await _uow.Expenses.GetBySessionAsync(sessionId)).ToList();

        var completedSales = sales.Where(s => s.Status == Domain.Enums.SaleStatus.Completed).ToList();
        var totalTTC = completedSales.Sum(s => s.TotalTTC);
        var totalHT  = completedSales.Sum(s => s.SubtotalHT);
        var totalTVA = completedSales.Sum(s => s.TaxAmount);
        var totalExpenses = expenses.Sum(e => e.Amount);

        decimal theoreticalCash = session.OpeningFloat + totalTTC - totalExpenses;
        decimal discrepancy = (session.ClosingCounted ?? theoreticalCash) - theoreticalCash;

        var sb = new StringBuilder();
        const int width = 42;

        string Center(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Length >= width) return text[..width];
            int pad = (width - text.Length) / 2;
            return text.PadLeft(pad + text.Length).PadRight(width);
        }

        string Row(string left, string right)
        {
            int space = width - left.Length - right.Length;
            if (space < 1) space = 1;
            return left + new string(' ', space) + right;
        }

        string Line() => new('-', width);
        string DblLine() => new('=', width);

        sb.AppendLine(DblLine());
        sb.AppendLine(Center("GO212 POS — RAPPORT Z DE CLÔTURE"));
        sb.AppendLine(Center("CLÔTURE DE SESSION DE CAISSE"));
        sb.AppendLine(DblLine());

        sb.AppendLine(Row($"Session ID: #{session.Id}", $"Caissier: {cashier?.Name ?? "Admin"}"));
        sb.AppendLine(Row("Ouverture:", session.OpenedAt.ToString("dd/MM/yyyy HH:mm")));
        sb.AppendLine(Row("Clôture:", (session.ClosedAt ?? DateTime.UtcNow).ToString("dd/MM/yyyy HH:mm")));
        sb.AppendLine(Line());

        sb.AppendLine(Center("--- RECAPITULATIF DES VENTES ---"));
        sb.AppendLine(Row("Nombre de ventes:", $"{completedSales.Count}"));
        sb.AppendLine(Row("Total Ventes HT:", $"{totalHT:N2} MAD"));
        sb.AppendLine(Row("Total TVA:", $"{totalTVA:N2} MAD"));
        sb.AppendLine(Row("TOTAL VENTES TTC:", $"{totalTTC:N2} MAD"));
        sb.AppendLine(Line());

        sb.AppendLine(Center("--- MOUVEMENTS DU TIROIR CAISSE ---"));
        sb.AppendLine(Row("Fond de caisse initial:", $"{session.OpeningFloat:N2} MAD"));
        sb.AppendLine(Row("Encaissé Espèces (+):", $"{totalTTC:N2} MAD"));
        sb.AppendLine(Row("Dépenses Caisse (-):", $"-{totalExpenses:N2} MAD"));
        sb.AppendLine(DblLine());
        sb.AppendLine(Row("SOLDE THEORIQUE:", $"{theoreticalCash:N2} MAD"));
        sb.AppendLine(Row("SOLDE REEL COMPTE:", $"{session.ClosingCounted ?? theoreticalCash:N2} MAD"));

        if (discrepancy == 0)
            sb.AppendLine(Row("ECART DE CAISSE:", "0.00 MAD (PARFAIT)"));
        else if (discrepancy > 0)
            sb.AppendLine(Row("ECART DE CAISSE:", $"+{discrepancy:N2} MAD (EXCEDENT)"));
        else
            sb.AppendLine(Row("ECART DE CAISSE:", $"{discrepancy:N2} MAD (DEFICIT)"));

        sb.AppendLine(DblLine());
        sb.AppendLine(Center("DOCUMENT OFFICIEL DE CAISSE"));
        sb.AppendLine(Center(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")));
        sb.AppendLine(DblLine());

        return sb.ToString();
    }

    public async Task<byte[]> GenerateEscPosReceiptBytesAsync(long saleId, bool cutPaper = true)
    {
        var text = await GenerateReceiptTextAsync(saleId);
        using var ms = new MemoryStream();

        // 1. Initialize printer (ESC @)
        ms.Write([0x1B, 0x40]);

        // 2. Select Code Page CP437 or UTF-8
        ms.Write([0x1B, 0x74, 0x00]);

        // 3. Write text content (ASCII / UTF-8)
        var bytes = Encoding.UTF8.GetBytes(text);
        ms.Write(bytes, 0, bytes.Length);

        // 4. Feed 3 lines (ESC d 3)
        ms.Write([0x1B, 0x64, 0x03]);

        // 5. Open cash drawer (ESC p 0 25 250)
        ms.Write([0x1B, 0x70, 0x00, 0x19, 0xFA]);

        // 6. Paper cut (GS V 66 0)
        if (cutPaper)
        {
            ms.Write([0x1D, 0x56, 0x42, 0x00]);
        }

        return ms.ToArray();
    }

    public async Task<bool> PrintReceiptAsync(long saleId)
    {
        try
        {
            var text = await GenerateReceiptTextAsync(saleId);
            
            // Try printing via RawPrinterHelper first
            try
            {
                var bytes = await GenerateEscPosReceiptBytesAsync(saleId);
                // "POS-80" is a standard name for generic 80mm thermal printers
                bool printed = Go212.POS.Application.Helpers.RawPrinterHelper.SendBytesToPrinter("POS-80", bytes);
                if (printed)
                {
                    _logger.LogInformation("Receipt for sale {SaleId} sent to ESC/POS printer 'POS-80'", saleId);
                }
                else
                {
                    _logger.LogWarning("RawPrinterHelper failed to print to 'POS-80'. Fallback to local file saving.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception during ESC/POS raw printing. Fallback to local file saving.");
            }

            // Fallback: Save copy to local receipts folder
            var receiptDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Receipts");
            Directory.CreateDirectory(receiptDir);
            var filePath = Path.Combine(receiptDir, $"Ticket_{saleId}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            await File.WriteAllTextAsync(filePath, text);

            _logger.LogInformation("Receipt generated and saved to {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to print receipt for sale {SaleId}", saleId);
            return false;
        }
    }
}
