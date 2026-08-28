namespace Go212.POS.Infrastructure.Hardware;

public interface IPosPrinter
{
    string PrinterName { get; set; }
    Task<bool> PrintRawBytesAsync(byte[] bytes);
    Task<bool> TestPrinterConnectionAsync();
}

public interface ICashDrawer
{
    Task<bool> OpenDrawerAsync();
}

public interface IBarcodeScanner
{
    event EventHandler<string>? BarcodeScanned;
    void StartListening();
    void StopListening();
}

public interface ICustomerDisplay
{
    Task<bool> DisplayWelcomeAsync(string storeName = "GO212 POS");
    Task<bool> DisplayItemAsync(string itemName, decimal priceTTC);
    Task<bool> DisplayTotalAsync(decimal totalTTC, decimal change = 0m);
    Task<bool> ClearAsync();
}
