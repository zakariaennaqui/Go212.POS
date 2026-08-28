using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Go212.POS.Application.Interfaces;
using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Enums;
using Go212.POS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows;

namespace Go212.POS.Desktop.ViewModels;

/// <summary>
/// POS Screen ViewModel — cashier sales engine.
///
/// Handles:
///  - Real-time product search and EAN13 barcode scanning
///  - Category filtering
///  - Shopping cart calculations with snapshot prices and VAT
///  - Atomic sale completion + stock decrement + payment recording in MySQL
///  - ESC/POS thermal receipt printing and dialog preview
///  - Park / Hold ticket functionality (DB-persisted via SaleService)
///  - Double-click protection on payment submission
/// </summary>
public partial class POSViewModel : ObservableObject
{
    private readonly ISaleService _saleService;
    private readonly IProductService _productService;
    private readonly ISessionService _sessionService;
    private readonly IReceiptService _receiptService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<POSViewModel> _logger;

    private bool _isProcessingPayment; // Double-click protection

    /// <summary>Tracks the DB ID of the currently open sale (null = no active sale yet).</summary>
    private long? _currentSaleId;

    public POSViewModel(
        ISaleService saleService,
        IProductService productService,
        ISessionService sessionService,
        IReceiptService receiptService,
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ILogger<POSViewModel> logger)
    {
        _saleService    = saleService;
        _productService = productService;
        _sessionService = sessionService;
        _receiptService = receiptService;
        _uow            = uow;
        _currentUser    = currentUser;
        _logger         = logger;
    }

    // ── Cart ─────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<CartItemViewModel> _cartItems = [];
    [ObservableProperty] private decimal _subtotalHT;
    [ObservableProperty] private decimal _taxAmount;
    [ObservableProperty] private decimal _totalTTC;
    [ObservableProperty] private int     _itemCount;

    // ── Search & Categories ──────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private string _searchQuery = string.Empty;

    [ObservableProperty] private ObservableCollection<Product>  _searchResults = [];
    [ObservableProperty] private ObservableCollection<Category> _categories = [];
    [ObservableProperty] private Category? _selectedCategory;

    // ── Payment ──────────────────────────────────────────────
    [ObservableProperty] private decimal _cashReceived;
    [ObservableProperty] private decimal _changeAmount;
    [ObservableProperty] private bool    _isPaymentPanelVisible;
    [ObservableProperty] private string? _paymentError;
    [ObservableProperty] private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;

    // ── Held Ticket (DB-persisted) ───────────────────────────
    [ObservableProperty] private bool _hasHeldTicket;
    [ObservableProperty] private int  _heldTicketCount;

    // ── Status ───────────────────────────────────────────────
    [ObservableProperty] private bool    _isLoading;
    [ObservableProperty] private string? _statusMessage;

    public bool HasItems => CartItems.Count > 0;
    public bool CanPay   => HasItems && !_isProcessingPayment;

    // ── Initialization ────────────────────────────────────────
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            // 1. Load Categories
            var categories = await _uow.Categories.GetActiveAsync();
            var catList = new List<Category> { new Category { Id = 0, Name = "TOUT" } };
            catList.AddRange(categories);
            Categories = new ObservableCollection<Category>(catList);

            // 2. Load active products
            var products = await _productService.GetAllActiveAsync();
            SearchResults = new ObservableCollection<Product>(products);

            // 3. Ensure cash session is open
            var openSession = await _sessionService.GetCurrentOpenSessionAsync();
            if (openSession is null)
            {
                openSession = await _sessionService.OpenSessionAsync(_currentUser.UserId, 200.00m);
                _logger.LogInformation("Auto-opened cash session with default float.");
            }

            // 4. Check if there is a held sale to resume
            var heldSale = await _saleService.ResumeHeldSaleAsync(openSession.Id);
            if (heldSale is not null)
            {
                await RestoreCartFromSaleAsync(heldSale);
                StatusMessage = $"Ticket en attente repris automatiquement : {heldSale.SaleNumber} ▶";
            }

            // 5. Check if there is already a held sale (for the button state)
            await RefreshHeldTicketStateAsync(openSession.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load POS screen data");
            StatusMessage = "Erreur lors du chargement des données de caisse.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Search & Filter ───────────────────────────────────────
    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            var all = await _productService.GetAllActiveAsync();
            SearchResults = new ObservableCollection<Product>(all);
            return;
        }

        try
        {
            // Barcode scan direct add
            if (SearchQuery.All(char.IsDigit) && SearchQuery.Length >= 6)
            {
                var barcodeProduct = await _productService.GetByBarcodeAsync(SearchQuery.Trim());
                if (barcodeProduct is not null)
                {
                    await AddProductToCartAsync(barcodeProduct);
                    SearchQuery = string.Empty;
                    return;
                }
            }

            var results = await _productService.SearchAsync(SearchQuery);
            SearchResults = new ObservableCollection<Product>(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Product search failed");
        }
    }

    [RelayCommand]
    private async Task FilterByCategoryAsync(long categoryId)
    {
        try
        {
            if (categoryId == 0)
            {
                var all = await _productService.GetAllActiveAsync();
                SearchResults = new ObservableCollection<Product>(all);
            }
            else
            {
                var filtered = await _productService.GetByCategoryAsync(categoryId);
                SearchResults = new ObservableCollection<Product>(filtered);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Filter by category failed");
        }
    }

    // ── Cart Operations ───────────────────────────────────────
    [RelayCommand]
    private async Task AddProductToCartAsync(Product product)
    {
        try
        {
            var existing = CartItems.FirstOrDefault(i => i.ProductId == product.Id);
            if (existing is not null)
            {
                existing.Quantity++;
            }
            else
            {
                CartItems.Add(new CartItemViewModel
                {
                    ProductId   = product.Id,
                    ProductName = product.Name,
                    UnitPriceHT = product.PriceHT,
                    TaxRate     = product.TaxRate,
                    Quantity    = 1,
                    Barcode     = product.Barcode ?? string.Empty,
                });
            }

            await RefreshTotalsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add product {Id} to cart", product.Id);
        }
    }

    [RelayCommand]
    private async Task RemoveItemAsync(CartItemViewModel item)
    {
        CartItems.Remove(item);
        await RefreshTotalsAsync();
    }

    [RelayCommand]
    private async Task IncrementQuantityAsync(CartItemViewModel item)
    {
        item.Quantity++;
        await RefreshTotalsAsync();
    }

    [RelayCommand]
    private async Task DecrementQuantityAsync(CartItemViewModel item)
    {
        if (item.Quantity <= 1)
        {
            CartItems.Remove(item);
        }
        else
        {
            item.Quantity--;
        }
        await RefreshTotalsAsync();
    }

    [RelayCommand]
    private async Task ClearCartAsync()
    {
        if (!HasItems) return;
        var confirm = MessageBox.Show(
            "Voulez-vous vraiment vider le panier actuel ?", "Confirmation",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        // If there's a current open sale in DB, cancel it
        if (_currentSaleId.HasValue)
        {
            try
            {
                await _saleService.CancelSaleAsync(_currentSaleId.Value, "Panier vidé par le caissier", _currentUser.UserId);
                _currentSaleId = null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not cancel sale {SaleId} on cart clear — resetting locally only", _currentSaleId);
                _currentSaleId = null;
            }
        }

        await ResetCartAsync();
    }

    // ── Park / Hold Sale (DB-persisted) ───────────────────────
    [RelayCommand]
    private async Task HoldTicketAsync()
    {
        if (!HasItems) return;

        IsLoading = true;
        try
        {
            // 1. Get or create the open sale in DB
            var session = await _sessionService.GetCurrentOpenSessionAsync()
                ?? await _sessionService.OpenSessionAsync(_currentUser.UserId, 200.00m);

            if (_currentSaleId is null)
            {
                // Create a new sale and add all cart items
                var newSale = await _saleService.CreateSaleAsync(session.Id, _currentUser.UserId, null);
                _currentSaleId = newSale.Id;
                foreach (var cartItem in CartItems)
                    await _saleService.AddItemAsync(newSale.Id, cartItem.ProductId, cartItem.Quantity, cartItem.DiscountPercent);
            }

            // 2. Put the sale on HOLD in DB
            await _saleService.HoldSaleAsync(_currentSaleId.Value);

            // 3. Clear UI cart
            CartItems.Clear();
            _currentSaleId = null;
            HasHeldTicket = true;
            HeldTicketCount++;
            await RefreshTotalsAsync();
            StatusMessage = "Ticket mis en attente ⏸ (sauvegardé)";
            _logger.LogInformation("Ticket held in DB.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HoldTicket failed");
            StatusMessage = $"Erreur mise en attente: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ResumeHeldTicketAsync()
    {
        if (!HasHeldTicket) return;

        IsLoading = true;
        try
        {
            var session = await _sessionService.GetCurrentOpenSessionAsync();
            if (session is null) { StatusMessage = "Aucune session de caisse ouverte."; return; }

            var heldSale = await _saleService.ResumeHeldSaleAsync(session.Id);
            if (heldSale is null) { HasHeldTicket = false; StatusMessage = "Aucun ticket en attente trouvé."; return; }

            await RestoreCartFromSaleAsync(heldSale);
            StatusMessage = $"Ticket repris ▶ ({heldSale.SaleNumber})";
            _logger.LogInformation("Held ticket {SaleNumber} resumed from DB.", heldSale.SaleNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ResumeHeldTicket failed");
            StatusMessage = $"Erreur reprise ticket: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Restores the cart UI from a DB-fetched Sale (with Items populated).</summary>
    private async Task RestoreCartFromSaleAsync(Sale sale)
    {
        _currentSaleId = sale.Id;
        CartItems.Clear();

        foreach (var item in sale.Items)
        {
            CartItems.Add(new CartItemViewModel
            {
                ProductId       = item.ProductId,
                ProductName     = item.ProductName,
                UnitPriceHT     = item.UnitPriceHT,
                TaxRate         = item.TaxRate,
                Quantity        = item.Quantity,
                DiscountPercent = item.DiscountPercent,
                Barcode         = item.ProductBarcode ?? string.Empty,
            });
        }

        HasHeldTicket = false;
        await RefreshTotalsAsync();
    }

    /// <summary>Checks if a held sale exists for the current session and updates HasHeldTicket.</summary>
    private async Task RefreshHeldTicketStateAsync(long sessionId)
    {
        try
        {
            var sales = await _uow.Sales.GetBySessionAsync(sessionId);
            var heldSales = sales.Where(s => s.Status == SaleStatus.Held).ToList();
            HasHeldTicket = heldSales.Count > 0;
            HeldTicketCount = heldSales.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not refresh held ticket state");
        }
    }

    // ── Payment Flow ──────────────────────────────────────────
    [RelayCommand]
    private void ShowPaymentPanel()
    {
        if (!HasItems) return;
        CashReceived          = TotalTTC;
        ChangeAmount          = 0;
        PaymentError          = null;
        SelectedPaymentMethod = PaymentMethod.Cash;
        IsPaymentPanelVisible = true;
    }

    [RelayCommand]
    private void CancelPayment()
    {
        IsPaymentPanelVisible = false;
        PaymentError          = null;
    }

    [RelayCommand(CanExecute = nameof(CanPay))]
    private async Task ProcessPaymentAsync()
    {
        if (_isProcessingPayment) return;
        _isProcessingPayment = true;
        OnPropertyChanged(nameof(CanPay));
        PaymentError = null;

        try
        {
            if (SelectedPaymentMethod == PaymentMethod.Cash && CashReceived < TotalTTC)
            {
                PaymentError = $"Montant insuffisant. Il manque {(TotalTTC - CashReceived):N2} MAD.";
                return;
            }

            ChangeAmount = SelectedPaymentMethod == PaymentMethod.Cash ? Math.Max(0, CashReceived - TotalTTC) : 0;

            // 1. Get open session
            var session = await _sessionService.GetCurrentOpenSessionAsync();
            long sessionId = session?.Id ?? 1;

            // 2. Create sale in DB if not already created (e.g. resumed from hold)
            if (_currentSaleId is null)
            {
                var newSale = await _saleService.CreateSaleAsync(sessionId, _currentUser.UserId, null);
                _currentSaleId = newSale.Id;

                // Add each cart item to the DB sale
                foreach (var item in CartItems)
                    await _saleService.AddItemAsync(newSale.Id, item.ProductId, item.Quantity, item.DiscountPercent);
            }

            // 3. Complete payment & deduct stock atomically
            var paymentRequest = new PaymentRequest(
                Method:       SelectedPaymentMethod,
                CashReceived: SelectedPaymentMethod == PaymentMethod.Cash ? CashReceived : TotalTTC
            );

            var completedSale = await _saleService.CompleteSaleAsync(_currentSaleId.Value, paymentRequest);
            _currentSaleId = null; // Sale is done

            // 4. Generate & print receipt
            await _receiptService.PrintReceiptAsync(completedSale.Id);
            var receiptText = await _receiptService.GenerateReceiptTextAsync(completedSale.Id);

            IsPaymentPanelVisible = false;

            // 5. Show receipt preview to cashier
            MessageBox.Show(
                $"VENTE ENREGISTRÉE AVEC SUCCÈS !\n\n" +
                $"Ticket : {completedSale.SaleNumber}\n" +
                $"Total Payé : {completedSale.TotalTTC:N2} MAD\n" +
                $"Rendu : {ChangeAmount:N2} MAD\n\n" +
                $"--- APERÇU TICKET ---\n{receiptText}",
                "GO212 POS — Vente Terminée",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await ResetCartAsync();
            await LoadAsync(); // Refresh product stock numbers
        }
        catch (Domain.Exceptions.BusinessRuleException ex)
        {
            PaymentError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment processing failed");
            PaymentError = $"Erreur lors du paiement: {ex.Message}";
        }
        finally
        {
            _isProcessingPayment = false;
            OnPropertyChanged(nameof(CanPay));
        }
    }

    private async Task RefreshTotalsAsync()
    {
        SubtotalHT = CartItems.Sum(i => i.LineTotalHT);
        TaxAmount  = CartItems.Sum(i => i.LineTaxAmount);
        TotalTTC   = CartItems.Sum(i => i.LineTotalTTC);
        ItemCount  = CartItems.Sum(i => i.Quantity);
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CanPay));
        await Task.CompletedTask;
    }

    private async Task ResetCartAsync()
    {
        CartItems.Clear();
        SubtotalHT   = 0;
        TaxAmount    = 0;
        TotalTTC     = 0;
        ItemCount    = 0;
        ChangeAmount = 0;
        CashReceived = 0;
        _currentSaleId = null;
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CanPay));
        await Task.CompletedTask;
    }
}



/// <summary>Cart Item representation in POS view.</summary>
public partial class CartItemViewModel : ObservableObject
{
    public long   ProductId   { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Barcode     { get; set; } = string.Empty;
    public decimal UnitPriceHT { get; set; }
    public decimal TaxRate     { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotalHT))]
    [NotifyPropertyChangedFor(nameof(LineTaxAmount))]
    [NotifyPropertyChangedFor(nameof(LineTotalTTC))]
    private int _quantity = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotalHT))]
    [NotifyPropertyChangedFor(nameof(LineTaxAmount))]
    [NotifyPropertyChangedFor(nameof(LineTotalTTC))]
    private decimal _discountPercent = 0;

    public decimal LineTotalHT   => Math.Round(UnitPriceHT * Quantity * (1 - DiscountPercent / 100), 2);
    public decimal LineTaxAmount => Math.Round(LineTotalHT * TaxRate / 100, 2);
    public decimal LineTotalTTC  => LineTotalHT + LineTaxAmount;
    public decimal UnitPriceTTC  => Math.Round(UnitPriceHT * (1 + TaxRate / 100), 2);
}
