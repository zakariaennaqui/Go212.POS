using FluentValidation;
using Go212.POS.Domain.Entities;

namespace Go212.POS.Application.Commands;

// ── Create / Update Product ───────────────────────────────────────────────────

/// <summary>Command to create or update a product in the catalog.</summary>
public record CreateProductCommand(
    string  Name,
    long    CategoryId,
    decimal PriceHT,
    decimal TaxRate,
    string? Barcode,
    string  Unit,
    int     StockAlertThreshold,
    bool    IsActive = true
);

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    private static readonly decimal[] ValidTaxRates = [0m, 7m, 10m, 14m, 20m];

    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Le nom du produit est obligatoire.")
            .MaximumLength(200).WithMessage("Le nom ne peut pas dépasser 200 caractères.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Veuillez sélectionner une catégorie.");

        RuleFor(x => x.PriceHT)
            .GreaterThan(0).WithMessage("Le prix HT doit être supérieur à 0.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Le prix HT semble anormalement élevé (max 1 000 000).");

        RuleFor(x => x.TaxRate)
            .Must(r => ValidTaxRates.Contains(r))
            .WithMessage($"Le taux de TVA doit être l'un des suivants : {string.Join(", ", ValidTaxRates)}%.");

        RuleFor(x => x.Barcode)
            .MaximumLength(50).WithMessage("Le code-barres ne peut pas dépasser 50 caractères.")
            .Matches(@"^\d{8,13}$").When(x => !string.IsNullOrEmpty(x.Barcode))
            .WithMessage("Le code-barres doit être numérique et contenir entre 8 et 13 chiffres (EAN-8 / EAN-13).");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("L'unité est obligatoire (ex: pcs, kg, L).")
            .MaximumLength(20);

        RuleFor(x => x.StockAlertThreshold)
            .GreaterThanOrEqualTo(0).WithMessage("Le seuil d'alerte stock ne peut pas être négatif.");
    }
}

// ── Create Sale ───────────────────────────────────────────────────────────────

/// <summary>Command to initiate a new sale on the POS screen.</summary>
public record CreateSaleCommand(
    long  SessionId,
    long  UserId,
    long? CustomerId,
    IEnumerable<SaleItemCommand> Items
);

public record SaleItemCommand(
    long    ProductId,
    int     Quantity,
    decimal DiscountPercent = 0m
);

public class CreateSaleValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleValidator()
    {
        RuleFor(x => x.SessionId)
            .GreaterThan(0).WithMessage("Une session de caisse active est requise.");

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("Un utilisateur connecté est requis.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Une vente doit contenir au moins un article.");

        RuleForEach(x => x.Items).SetValidator(new SaleItemValidator());
    }
}

public class SaleItemValidator : AbstractValidator<SaleItemCommand>
{
    public SaleItemValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0).WithMessage("Produit invalide.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("La quantité doit être supérieure à 0.")
            .LessThanOrEqualTo(9999).WithMessage("La quantité maximale par article est 9999.");

        RuleFor(x => x.DiscountPercent)
            .InclusiveBetween(0, 100).WithMessage("La remise doit être comprise entre 0% et 100%.");
    }
}

// ── Add Expense (Petty Cash) ──────────────────────────────────────────────────

/// <summary>Command to record a cash drawer expense.</summary>
public record AddExpenseCommand(
    long    SessionId,
    long    UserId,
    string  Description,
    decimal Amount,
    string? Category
);

public class AddExpenseValidator : AbstractValidator<AddExpenseCommand>
{
    public AddExpenseValidator()
    {
        RuleFor(x => x.SessionId)
            .GreaterThan(0).WithMessage("Une session de caisse active est requise.");

        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("Un utilisateur connecté est requis.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La description de la dépense est obligatoire.")
            .MaximumLength(500).WithMessage("La description ne peut pas dépasser 500 caractères.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Le montant de la dépense doit être supérieur à 0.")
            .LessThanOrEqualTo(100_000).WithMessage("Le montant semble anormalement élevé (max 100 000 MAD). Vérifiez.");

        RuleFor(x => x.Category)
            .MaximumLength(100).When(x => x.Category is not null);
    }
}

// ── Close Session ─────────────────────────────────────────────────────────────

/// <summary>Command to close a cash session with counted cash reconciliation.</summary>
public record CloseSessionCommand(
    long    SessionId,
    decimal CountedCash,
    string? Notes
);

public class CloseSessionValidator : AbstractValidator<CloseSessionCommand>
{
    public CloseSessionValidator()
    {
        RuleFor(x => x.SessionId)
            .GreaterThan(0).WithMessage("Identifiant de session invalide.");

        RuleFor(x => x.CountedCash)
            .GreaterThanOrEqualTo(0).WithMessage("Le montant comptabilisé ne peut pas être négatif.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).When(x => x.Notes is not null)
            .WithMessage("Les notes de clôture ne peuvent pas dépasser 1000 caractères.");
    }
}
