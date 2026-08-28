using System.Text;
using Go212.POS.Application.Helpers;
using Go212.POS.Infrastructure.Printing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Go212.POS.Infrastructure.Hardware;

/// <summary>
/// Service controlling the physical or virtual POS thermal printer via Windows Spooler raw printing.
/// </summary>
public class WindowsSpoolerPosPrinter : IPosPrinter
{
    private readonly ILogger<WindowsSpoolerPosPrinter> _logger;
    public string PrinterName { get; set; } = "POS-80";

    public WindowsSpoolerPosPrinter(IConfiguration config, ILogger<WindowsSpoolerPosPrinter> logger)
    {
        _logger = logger;
        PrinterName = config["Hardware:PrinterName"] ?? "POS-80";
    }

    public Task<bool> PrintRawBytesAsync(byte[] bytes)
    {
        return Task.Run(() =>
        {
            try
            {
                bool success = RawPrinterHelper.SendBytesToPrinter(PrinterName, bytes);
                if (!success)
                {
                    _logger.LogWarning("RawPrinterHelper could not print directly to '{PrinterName}'. Spooling fallback text...", PrinterName);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send raw print job to '{PrinterName}'", PrinterName);
                return false;
            }
        });
    }

    public Task<bool> TestPrinterConnectionAsync()
    {
        var builder = new EscPosReceiptBuilder();
        builder.CenterText("GO212 POS - TEST IMPRIMANTE", bold: true)
               .CenterText($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
               .Divider()
               .CenterText("TEST IMPRESSION REUSSI")
               .FeedAndCut(3);

        return PrintRawBytesAsync(builder.Build());
    }
}

/// <summary>
/// Service controlling the cash drawer kick-out pulse via the thermal printer.
/// </summary>
public class CashDrawerService : ICashDrawer
{
    private readonly IPosPrinter _printer;
    private readonly ILogger<CashDrawerService> _logger;

    public CashDrawerService(IPosPrinter printer, ILogger<CashDrawerService> logger)
    {
        _printer = printer;
        _logger = logger;
    }

    public async Task<bool> OpenDrawerAsync()
    {
        try
        {
            _logger.LogInformation("Sending open drawer pulse command (ESC p)...");
            var pulseCommand = EscPosCommands.OpenDrawer;
            return await _printer.PrintRawBytesAsync(pulseCommand);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open cash drawer.");
            return false;
        }
    }
}

/// <summary>
/// 2-Line VFD / Vacuum Fluorescent Customer Display service.
/// </summary>
public class CustomerDisplayService : ICustomerDisplay
{
    private readonly ILogger<CustomerDisplayService> _logger;

    public CustomerDisplayService(ILogger<CustomerDisplayService> logger)
    {
        _logger = logger;
    }

    public Task<bool> DisplayWelcomeAsync(string storeName = "GO212 POS")
    {
        _logger.LogDebug("Customer Display: BIENVENUE / {StoreName}", storeName);
        return Task.FromResult(true);
    }

    public Task<bool> DisplayItemAsync(string itemName, decimal priceTTC)
    {
        _logger.LogDebug("Customer Display: {ItemName} | {Price:0.00} MAD", itemName, priceTTC);
        return Task.FromResult(true);
    }

    public Task<bool> DisplayTotalAsync(decimal totalTTC, decimal change = 0m)
    {
        _logger.LogDebug("Customer Display: TOTAL: {Total:0.00} MAD | RENDU: {Change:0.00} MAD", totalTTC, change);
        return Task.FromResult(true);
    }

    public Task<bool> ClearAsync()
    {
        _logger.LogDebug("Customer Display Cleared.");
        return Task.FromResult(true);
    }
}
