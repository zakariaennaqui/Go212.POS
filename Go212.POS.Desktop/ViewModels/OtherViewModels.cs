using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Go212.POS.Application.Interfaces;
using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Collections.ObjectModel;
using System.IO;

namespace Go212.POS.Desktop.ViewModels;

/// <summary>Products catalog management ViewModel (Admin / Manager only).</summary>
public partial class ProductsViewModel : ObservableObject
{
    private readonly IProductService  _productService;
    private readonly IUnitOfWork      _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ProductsViewModel> _logger;

    public ProductsViewModel(IProductService productService, IUnitOfWork uow, ICurrentUserService currentUser, ILogger<ProductsViewModel> logger)
    {
        _productService = productService;
        _uow            = uow;
        _currentUser    = currentUser;
        _logger         = logger;
    }

    [ObservableProperty] private ObservableCollection<Product>  _products  = [];
    [ObservableProperty] private ObservableCollection<Category> _categories = [];
    [ObservableProperty] private Product?  _selectedProduct;
    [ObservableProperty] private string    _searchQuery = string.Empty;
    [ObservableProperty] private bool      _isLoading;
    [ObservableProperty] private bool      _isEditing;
    [ObservableProperty] private string?   _errorMessage;

    // Edit form fields
    [ObservableProperty] private string  _editName        = string.Empty;
    [ObservableProperty] private long    _editCategoryId;
    [ObservableProperty] private decimal _editPriceHT;
    [ObservableProperty] private decimal _editTaxRate     = 20m;
    [ObservableProperty] private string? _editBarcode;
    [ObservableProperty] private string  _editUnit        = "pcs";
    [ObservableProperty] private int     _editAlertThreshold = 5;
    [ObservableProperty] private bool    _editIsActive    = true;
    [ObservableProperty] private string? _editImagePath;

    // Category form fields
    [ObservableProperty] private string  _newCategoryName  = string.Empty;
    [ObservableProperty] private string  _newCategoryColor = "#00BF63";
    [ObservableProperty] private string? _categoryStatusMessage;

    public bool CanEditProducts => _currentUser.IsManagerOrAbove;

    private bool RequireEditPermission()
    {
        if (!CanEditProducts)
        {
            ErrorMessage = "Autorisation refusée : seul un Manager ou Administrateur peut modifier le catalogue.";
            System.Windows.MessageBox.Show(
                "Autorisation refusée : vous n'avez pas le droit de modifier le catalogue produits.",
                "Accès Interdit",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Stop);
            return false;
        }
        return true;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var products   = await _productService.GetAllActiveAsync();
            var categories = await _uow.Categories.GetAllAsync();
            Products   = new ObservableCollection<Product>(products);
            Categories = new ObservableCollection<Category>(categories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load products");
            ErrorMessage = "Impossible de charger les produits.";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) { await LoadAsync(); return; }
        var results = await _productService.SearchAsync(SearchQuery);
        Products = new ObservableCollection<Product>(results);
    }

    [RelayCommand]
    private void NewProduct()
    {
        if (!RequireEditPermission()) return;
        SelectedProduct = null;
        EditName = string.Empty;
        EditCategoryId = Categories.Count > 0 ? Categories[0].Id : 0;
        EditPriceHT = 0; EditTaxRate = 20;
        EditBarcode = null; EditUnit = "pcs"; EditAlertThreshold = 5; EditIsActive = true;
        IsEditing = true; ErrorMessage = null;
    }

    [RelayCommand]
    private void EditProduct(Product product)
    {
        if (!RequireEditPermission()) return;
        SelectedProduct      = product;
        EditName             = product.Name;
        EditCategoryId       = product.CategoryId;
        EditPriceHT          = product.PriceHT;
        EditTaxRate          = product.TaxRate;
        EditBarcode          = product.Barcode;
        EditUnit             = product.Unit;
        EditAlertThreshold   = product.StockAlertThreshold;
        EditIsActive         = product.IsActive;
        EditImagePath        = product.ImagePath;
        IsEditing = true; ErrorMessage = null;
    }

    [RelayCommand]
    private void PickImage()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Choisir une image produit",
            Filter = "Images (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|Tous fichiers (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            // Compress and save to local images folder
            var imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images");
            Directory.CreateDirectory(imagesDir);
            var destFile  = Path.Combine(imagesDir, $"{Guid.NewGuid():N}.jpg");

            using var img = SixLabors.ImageSharp.Image.Load(dlg.FileName);
            img.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode    = ResizeMode.Max,
                Size    = new SixLabors.ImageSharp.Size(400, 400)
            }));
            img.SaveAsJpeg(destFile, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 80 });
            EditImagePath = destFile;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image compression failed");
            EditImagePath = dlg.FileName; // fallback: use original
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!RequireEditPermission()) return;
        if (string.IsNullOrWhiteSpace(EditName)) { ErrorMessage = "Le nom du produit est obligatoire."; return; }
        if (EditCategoryId <= 0) { ErrorMessage = "Veuillez sélectionner une catégorie."; return; }
        if (EditPriceHT <= 0)    { ErrorMessage = "Le prix doit être supérieur à 0."; return; }

        try
        {
            if (SelectedProduct is null)
            {
                // Create new
                var newProduct = new Product
                {
                    Name = EditName, CategoryId = EditCategoryId,
                    PriceHT = EditPriceHT, TaxRate = EditTaxRate,
                    Barcode = EditBarcode, Unit = EditUnit,
                    StockAlertThreshold = EditAlertThreshold, IsActive = EditIsActive,
                    ImagePath = EditImagePath,
                };
                await _productService.CreateAsync(newProduct);
            }
            else
            {
                // Update existing
                SelectedProduct.Name = EditName; SelectedProduct.CategoryId = EditCategoryId;
                SelectedProduct.PriceHT = EditPriceHT; SelectedProduct.TaxRate = EditTaxRate;
                SelectedProduct.Barcode = EditBarcode; SelectedProduct.Unit = EditUnit;
                SelectedProduct.StockAlertThreshold = EditAlertThreshold;
                SelectedProduct.IsActive = EditIsActive;
                SelectedProduct.ImagePath = EditImagePath;
                await _productService.UpdateAsync(SelectedProduct);
            }

            IsEditing = false;
            await LoadAsync();
        }
        catch (Domain.Exceptions.BusinessRuleException ex) { ErrorMessage = ex.Message; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save product");
            ErrorMessage = "Erreur lors de la sauvegarde.";
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync(Product product)
    {
        if (!RequireEditPermission()) return;
        var confirm = System.Windows.MessageBox.Show(
            $"Désactiver '{product.Name}' ?", "Confirmation",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try { await _productService.DeactivateAsync(product.Id); await LoadAsync(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate product {Id}", product.Id);
            ErrorMessage = "Erreur lors de la désactivation.";
        }
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    // ── Category CRUD ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task CreateCategoryAsync()
    {
        if (!RequireEditPermission()) return;
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            CategoryStatusMessage = "Le nom de la categorie est obligatoire.";
            return;
        }

        var cat = new Category
        {
            Name         = NewCategoryName.Trim(),
            Color        = string.IsNullOrWhiteSpace(NewCategoryColor) ? "#00BF63" : NewCategoryColor.Trim(),
            IsActive     = true,
            DisplayOrder = 0,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow,
        };
        try
        {
            await _uow.Categories.InsertAsync(cat);
            NewCategoryName  = string.Empty;
            NewCategoryColor = "#00BF63";
            await LoadAsync();
            CategoryStatusMessage = $"Categorie '{cat.Name}' creee avec succes.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create category");
            CategoryStatusMessage = "Erreur lors de la creation de la categorie.";
        }
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(Category category)
    {
        if (!RequireEditPermission()) return;

        var confirm = System.Windows.MessageBox.Show(
            $"Supprimer la categorie '{category.Name}' ?\nLes produits associes seront conserves mais sans categorie.",
            "Confirmation",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            // Soft-delete: deactivate
            category.IsActive  = false;
            category.UpdatedAt = DateTime.UtcNow;
            await _uow.Categories.UpdateAsync(category);
            await LoadAsync();
            CategoryStatusMessage = $"Categorie '{category.Name}' supprimee.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete category {Id}", category.Id);
            CategoryStatusMessage = "Erreur lors de la suppression.";
        }
    }
}

/// <summary>Stock management ViewModel.</summary>
public partial class StockViewModel : ObservableObject
{
    private readonly IStockService  _stockService;
    private readonly IProductService _productService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<StockViewModel> _logger;

    public StockViewModel(IStockService stockService, IProductService productService, ICurrentUserService currentUser, ILogger<StockViewModel> logger)
    { _stockService = stockService; _productService = productService; _currentUser = currentUser; _logger = logger; }

    [ObservableProperty] private ObservableCollection<Product> _lowStockProducts = [];
    [ObservableProperty] private ObservableCollection<StockMovement> _recentMovements = [];
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private int      _adjustmentQuantity;
    [ObservableProperty] private string   _adjustmentReason = string.Empty;
    [ObservableProperty] private bool     _isLoading;
    [ObservableProperty] private string?  _statusMessage;

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var lowStock = await _stockService.GetLowStockProductsAsync();
            LowStockProducts = new ObservableCollection<Product>(lowStock);
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to load stock"); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task AdjustStockAsync()
    {
        if (SelectedProduct is null) { StatusMessage = "Sélectionnez un produit."; return; }
        if (AdjustmentQuantity == 0) { StatusMessage = "La quantité doit être différente de 0."; return; }
        if (string.IsNullOrWhiteSpace(AdjustmentReason)) { StatusMessage = "Un motif est obligatoire."; return; }

        try
        {
            await _stockService.AdjustStockAsync(SelectedProduct.Id, AdjustmentQuantity, AdjustmentReason, _currentUser.UserId);
            StatusMessage = $"Stock de '{SelectedProduct.Name}' mis à jour: {(AdjustmentQuantity > 0 ? "+" : "")}{AdjustmentQuantity}";
            AdjustmentQuantity = 0; AdjustmentReason = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stock adjustment failed");
            StatusMessage = "Erreur lors de l'ajustement du stock.";
        }
    }

    [RelayCommand]
    private async Task LoadMovementsAsync(Product product)
    {
        SelectedProduct = product;
        var movements = await _stockService.GetHistoryAsync(product.Id);
        RecentMovements = new ObservableCollection<StockMovement>(movements.Take(50));
    }
}

/// <summary>Reports ViewModel.</summary>
public partial class ReportsViewModel : ObservableObject
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportsViewModel> _logger;

    public ReportsViewModel(IReportService reportService, ILogger<ReportsViewModel> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private Application.Interfaces.DailySalesReport? _dailyReport;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;

    [RelayCommand]
    public async Task LoadDailyReportAsync()
    {
        IsLoading = true;
        StatusMessage = null;
        try
        {
            DailyReport = await _reportService.GetDailyReportAsync(SelectedDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate daily sales report");
            StatusMessage = "Erreur lors de la génération du rapport.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        try
        {
            var saveDlg = new SaveFileDialog
            {
                Title = "Exporter le Rapport Journalier (PDF)",
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"rapport_journalier_{SelectedDate:yyyyMMdd}.pdf"
            };

            if (saveDlg.ShowDialog() != true) return;

            var pdf = await _reportService.ExportDailyReportPdfAsync(SelectedDate);
            await System.IO.File.WriteAllBytesAsync(saveDlg.FileName, pdf);
            StatusMessage = $"✓ Rapport PDF exporté : {saveDlg.FileName}";
            System.Windows.MessageBox.Show($"Rapport PDF exporté avec succès :\n{saveDlg.FileName}", "Export PDF", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export PDF report");
            StatusMessage = "Erreur lors de l'export PDF.";
            System.Windows.MessageBox.Show($"Erreur lors de l'export PDF : {ex.Message}", "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        try
        {
            var saveDlg = new SaveFileDialog
            {
                Title = "Exporter le Rapport Journalier (CSV)",
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"rapport_journalier_{SelectedDate:yyyyMMdd}.csv"
            };

            if (saveDlg.ShowDialog() != true) return;

            var csv = await _reportService.ExportDailyReportCsvAsync(SelectedDate);
            await System.IO.File.WriteAllBytesAsync(saveDlg.FileName, csv);
            StatusMessage = $"✓ Rapport CSV exporté : {saveDlg.FileName}";
            System.Windows.MessageBox.Show($"Rapport CSV exporté avec succès :\n{saveDlg.FileName}", "Export CSV", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export CSV report");
            StatusMessage = "Erreur lors de l'export CSV.";
            System.Windows.MessageBox.Show($"Erreur lors de l'export CSV : {ex.Message}", "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}

/// <summary>Settings ViewModel.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IBackupService _backupService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(IBackupService backupService, ICurrentUserService currentUser, ILogger<SettingsViewModel> logger)
    { _backupService = backupService; _currentUser = currentUser; _logger = logger; }

    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool    _isLoading;

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        IsLoading = true;
        try
        {
            var path = await _backupService.CreateBackupAsync();
            StatusMessage = $"✓ Sauvegarde créée:\n{path}";
            _logger.LogInformation("Manual backup created: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");
            StatusMessage = "Erreur lors de la sauvegarde. Consultez les logs.";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Sélectionner un fichier de sauvegarde",
            Filter = "SQL Files (*.sql)|*.sql|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        var confirm = System.Windows.MessageBox.Show(
            "ATTENTION: La restauration remplacera TOUTES les données actuelles.\n\nÊtes-vous sûr de vouloir continuer ?",
            "Restauration — Confirmation requise",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            bool valid = await _backupService.ValidateBackupAsync(dialog.FileName);
            if (!valid) { StatusMessage = "Fichier de sauvegarde invalide."; return; }

            await _backupService.RestoreBackupAsync(dialog.FileName, _currentUser.UserId);
            StatusMessage = "✓ Restauration réussie. Redémarrez l'application.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed");
            StatusMessage = "Erreur lors de la restauration.";
        }
        finally { IsLoading = false; }
    }
}

/// <summary>Management ViewModel (users, customers, expenses, sessions, and cash closure).</summary>
public partial class ManagementViewModel : ObservableObject
{
    private readonly IUnitOfWork _uow;
    private readonly ISessionService _sessionService;
    private readonly IReceiptService _receiptService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ManagementViewModel> _logger;

    public ManagementViewModel(
        IUnitOfWork uow,
        ISessionService sessionService,
        IReceiptService receiptService,
        ICurrentUserService currentUser,
        ILogger<ManagementViewModel> logger)
    {
        _uow = uow;
        _sessionService = sessionService;
        _receiptService = receiptService;
        _currentUser = currentUser;
        _logger = logger;
    }

    [ObservableProperty] private string _activeTab = "Sessions";

    [ObservableProperty] private ObservableCollection<CashSession> _sessions  = [];
    [ObservableProperty] private ObservableCollection<Customer>    _customers = [];
    [ObservableProperty] private ObservableCollection<Expense>     _expenses  = [];
    [ObservableProperty] private ObservableCollection<User>        _users     = [];

    [ObservableProperty] private CashSession? _openSession;
    [ObservableProperty] private bool _hasOpenSession;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;

    // ── User form properties ─────────────────────────────────
    [ObservableProperty] private string  _newUserName     = string.Empty;
    [ObservableProperty] private string  _newUserUsername = string.Empty;
    [ObservableProperty] private string  _newUserPin      = string.Empty;
    [ObservableProperty] private string  _newUserRole     = "Cashier";
    [ObservableProperty] private User?   _selectedUser;
    public string[] AvailableRoles => ["Cashier", "Manager", "Admin"];

    public bool CanManageUsers   => _currentUser.IsAdmin;
    public bool CanRecordExpense => _currentUser.IsManagerOrAbove;

    private bool RequirePermission(bool condition, string message)
    {
        if (!condition)
        {
            System.Windows.MessageBox.Show(
                message,
                "Accès Interdit",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Stop);
            return false;
        }
        return true;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            // 1. Sessions
            var sessions = await _uow.CashSessions.GetByDateRangeAsync(DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1));
            Sessions = new ObservableCollection<CashSession>(sessions);

            OpenSession = await _sessionService.GetCurrentOpenSessionAsync();
            HasOpenSession = OpenSession is not null;

            // 2. Customers
            var customers = await _uow.Customers.GetAllAsync();
            Customers = new ObservableCollection<Customer>(customers);

            // 3. Expenses
            if (OpenSession is not null)
            {
                var expenses = await _uow.Expenses.GetBySessionAsync(OpenSession.Id);
                Expenses = new ObservableCollection<Expense>(expenses);
            }
            else
            {
                Expenses = [];
            }

            // 4. Users
            var users = await _uow.Users.GetActiveUsersAsync();
            Users = new ObservableCollection<User>(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load management data");
            StatusMessage = "Erreur lors du chargement des données.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenSessionAsync()
    {
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "Entrez le fond de caisse initial (MAD) :",
            "Ouverture de Caisse",
            "200.00");

        if (string.IsNullOrWhiteSpace(input) || !decimal.TryParse(input, out decimal openingFloat))
            return;

        try
        {
            await _sessionService.OpenSessionAsync(_currentUser.UserId, openingFloat);
            StatusMessage = $"Session de caisse ouverte avec un fond de {openingFloat:N2} MAD ✓";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open cash session");
            System.Windows.MessageBox.Show($"Erreur lors de l'ouverture : {ex.Message}", "Erreur",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task CloseCurrentSessionAsync()
    {
        if (OpenSession is null) return;

        // Prompt cashier for counted cash
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            "Entrez le montant total des espèces comptées dans le tiroir-caisse (MAD) :",
            "Clôture de Caisse (Rapport Z)",
            "0.00");

        if (string.IsNullOrWhiteSpace(input) || !decimal.TryParse(input, out decimal countedCash))
        {
            return;
        }

        try
        {
            var closed = await _sessionService.CloseSessionAsync(OpenSession.Id, countedCash, "Clôture manuelle");
            var zReport = await _receiptService.GenerateZReportTextAsync(closed.Id);

            System.Windows.MessageBox.Show(
                $"SESSION #{closed.Id} CLÔTURÉE AVEC SUCCÈS !\n\n" +
                $"--- RAPPORT Z DE CLÔTURE ---\n{zReport}",
                "GO212 POS — Rapport Z",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close cash session");
            System.Windows.MessageBox.Show($"Erreur lors de la clôture : {ex.Message}", "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ShowZReportAsync(CashSession session)
    {
        try
        {
            var zReport = await _receiptService.GenerateZReportTextAsync(session.Id);
            System.Windows.MessageBox.Show(
                zReport,
                $"Rapport Z — Session #{session.Id}",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Z-report for session {Id}", session.Id);
            System.Windows.MessageBox.Show($"Erreur : {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AddExpenseAsync()
    {
        if (!RequirePermission(CanRecordExpense,
                "Autorisation refusée : seul un Manager ou Administrateur peut enregistrer une dépense.")) return;

        if (OpenSession is null)
        {
            System.Windows.MessageBox.Show("Veuillez d'abord ouvrir une session de caisse pour enregistrer une dépense.");
            return;
        }

        var amountStr = Microsoft.VisualBasic.Interaction.InputBox("Montant de la dépense (MAD) :", "Nouvelle Dépense de Caisse", "0.00");
        if (!decimal.TryParse(amountStr, out decimal amount) || amount <= 0) return;

        var reason = Microsoft.VisualBasic.Interaction.InputBox("Motif de la dépense (ex: Fournitures, Transport, Pain) :", "Motif de la Dépense", "Fournitures");
        if (string.IsNullOrWhiteSpace(reason)) return;

        var expense = new Expense
        {
            CashSessionId = OpenSession.Id,
            UserId        = _currentUser.UserId,
            Amount        = amount,
            Category      = "Divers",
            Description   = reason,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        };

        await _uow.Expenses.InsertAsync(expense);
        await LoadAsync();
        System.Windows.MessageBox.Show($"Dépense de {amount:N2} MAD enregistrée avec succès ✓");
    }

    [RelayCommand]
    private async Task UnlockUserAsync(User user)
    {
        if (!RequirePermission(CanManageUsers,
                "Autorisation refusée : seul un Administrateur peut débloquer un compte utilisateur.")) return;

        await _uow.Users.UpdateFailedAttemptsAsync(user.Id, 0, null);
        await LoadAsync();
        System.Windows.MessageBox.Show($"Compte '{user.Username}' débloqué avec succès ✓");
    }

    [RelayCommand]
    private async Task CreateUserAsync()
    {
        if (!RequirePermission(CanManageUsers,
                "Autorisation refusée : seul un Administrateur peut créer un utilisateur.")) return;

        if (string.IsNullOrWhiteSpace(NewUserName) || string.IsNullOrWhiteSpace(NewUserUsername) || string.IsNullOrWhiteSpace(NewUserPin))
        {
            StatusMessage = "Veuillez remplir tous les champs (Nom, Identifiant, PIN).";
            return;
        }

        if (NewUserPin.Length < 4 || !NewUserPin.All(char.IsDigit))
        {
            StatusMessage = "Le PIN doit contenir au moins 4 chiffres.";
            return;
        }

        // Check username uniqueness
        var existing = await _uow.Users.GetByUsernameAsync(NewUserUsername);
        if (existing is not null)
        {
            StatusMessage = $"L'identifiant '{NewUserUsername}' est déjà utilisé. Choisissez-en un autre.";
            return;
        }

        var role = NewUserRole switch
        {
            "Admin"   => Domain.Enums.UserRole.Administrator,
            "Manager" => Domain.Enums.UserRole.Manager,
            _         => Domain.Enums.UserRole.Cashier,
        };

        var newUser = new User
        {
            Name          = NewUserName.Trim(),
            Username      = NewUserUsername.Trim(),
            PinHash       = BCrypt.Net.BCrypt.HashPassword(NewUserPin),
            Role          = role,
            IsActive      = true,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        };

        await _uow.Users.InsertAsync(newUser);
        await _uow.Audit.LogAsync(new AuditEvent
        {
            UserId = _currentUser.UserId, Action = Domain.Enums.AuditAction.UserCreated,
            TargetEntity = nameof(User), TargetId = newUser.Id,
            Details = $"Nouvel utilisateur '{newUser.Username}' créé avec le rôle '{role}'",
            IpOrMachine = Environment.MachineName
        });

        // Clear form
        NewUserName = string.Empty;
        NewUserUsername = string.Empty;
        NewUserPin = string.Empty;
        NewUserRole = "Cashier";

        await LoadAsync();
        StatusMessage = $"Utilisateur '{newUser.Username}' créé avec succès ✓";
    }

    [RelayCommand]
    private async Task ResetUserPinAsync(User user)
    {
        if (!RequirePermission(CanManageUsers,
                "Autorisation refusée : seul un Administrateur peut réinitialiser un PIN.")) return;

        var newPin = Microsoft.VisualBasic.Interaction.InputBox(
            $"Entrez le nouveau PIN (4+ chiffres) pour '{user.Username}' :",
            "Réinitialisation PIN",
            "");

        if (string.IsNullOrWhiteSpace(newPin)) return;

        if (newPin.Length < 4 || !newPin.All(char.IsDigit))
        {
            System.Windows.MessageBox.Show("Le PIN doit contenir au moins 4 chiffres numériques.",
                "PIN Invalide", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        user.PinHash   = BCrypt.Net.BCrypt.HashPassword(newPin);
        user.UpdatedAt = DateTime.UtcNow;
        await _uow.Users.UpdateAsync(user);

        await _uow.Audit.LogAsync(new AuditEvent
        {
            UserId = _currentUser.UserId, Action = Domain.Enums.AuditAction.UserModified,
            TargetEntity = nameof(User), TargetId = user.Id,
            Details = $"PIN réinitialisé pour l'utilisateur '{user.Username}'.",
            IpOrMachine = Environment.MachineName
        });

        StatusMessage = $"PIN de '{user.Username}' réinitialisé avec succès ✓";
    }

    [RelayCommand]
    private async Task DeactivateUserAsync(User user)
    {
        if (!RequirePermission(CanManageUsers,
                "Autorisation refusée : seul un Administrateur peut désactiver un compte.")) return;

        if (user.Id == _currentUser.UserId)
        {
            System.Windows.MessageBox.Show("Vous ne pouvez pas désactiver votre propre compte.",
                "Action Interdite", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"Voulez-vous vraiment désactiver le compte de '{user.Username}' ?\nCette action peut être annulée en réactivant l'utilisateur.",
            "Confirmation", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        user.IsActive   = false;
        user.UpdatedAt  = DateTime.UtcNow;
        await _uow.Users.UpdateAsync(user);

        await _uow.Audit.LogAsync(new AuditEvent
        {
            UserId = _currentUser.UserId, Action = Domain.Enums.AuditAction.UserModified,
            TargetEntity = nameof(User), TargetId = user.Id,
            Details = $"Compte utilisateur '{user.Username}' désactivé.",
            IpOrMachine = Environment.MachineName
        });

        await LoadAsync();
        StatusMessage = $"Compte '{user.Username}' désactivé ✓";
    }
}

/// <summary>Home/Dashboard ViewModel.</summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly IReportService  _reportService;
    private readonly ISessionService _sessionService;
    private readonly IStockService   _stockService;

    public HomeViewModel(IReportService reportService, ISessionService sessionService, IStockService stockService)
    { _reportService = reportService; _sessionService = sessionService; _stockService = stockService; }

    [ObservableProperty] private decimal _todayRevenue;
    [ObservableProperty] private int     _todaySaleCount;
    [ObservableProperty] private int     _lowStockCount;
    [ObservableProperty] private bool    _hasOpenSession;
    [ObservableProperty] private Application.Interfaces.DailySalesReport? _todayReport;

    public async Task LoadAsync()
    {
        try
        {
            TodayReport     = await _reportService.GetDailyReportAsync(DateTime.Today);
            TodayRevenue    = TodayReport.TotalTTC;
            TodaySaleCount  = TodayReport.SaleCount;
            var session     = await _sessionService.GetCurrentOpenSessionAsync();
            HasOpenSession  = session is not null;
            var lowStock    = await _stockService.GetLowStockProductsAsync();
            LowStockCount   = lowStock.Count();
        }
        catch { /* Non-critical — dashboard loads best-effort */ }
    }
}
