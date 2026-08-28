using FluentValidation;
using Go212.POS.Application.DTOs;
using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Enums;
using Go212.POS.Domain.ValueObjects;

namespace Go212.POS.Application.Validators;

public record LoginCommand(string Username, string Pin);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Le nom d'utilisateur est obligatoire.");

        RuleFor(x => x.Pin)
            .NotEmpty().WithMessage("Le code PIN est obligatoire.")
            .Matches(@"^\d{4,8}$").WithMessage("Le code PIN doit comporter entre 4 et 8 chiffres.");
    }
}

public record CreateUserCommand(string Name, string Username, string Pin, UserRole Role);

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Le nom complet est obligatoire.")
            .MaximumLength(100);

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Le nom d'utilisateur est obligatoire.")
            .MinimumLength(3).WithMessage("L'identifiant doit contenir au moins 3 caractères.")
            .MaximumLength(50);

        RuleFor(x => x.Pin)
            .NotEmpty().WithMessage("Le code PIN est obligatoire.")
            .Matches(@"^\d{4,8}$").WithMessage("Le code PIN doit comporter entre 4 et 8 chiffres.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Le rôle sélectionné est invalide.");
    }
}

public record CreateCustomerCommand(string Name, string? Phone, string? Email, decimal CreditLimit);

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Le nom du client est obligatoire.")
            .MaximumLength(150);

        RuleFor(x => x.Phone)
            .Matches(@"^(\+212|0)[5-7]\d{8}$")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Le format du numéro de téléphone marocain est invalide (ex: 0612345678 ou +212612345678).");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("L'adresse email est invalide.");

        RuleFor(x => x.CreditLimit)
            .GreaterThanOrEqualTo(0).WithMessage("Le plafond de crédit ne peut pas être négatif.");
    }
}

public record ProcessReturnCommand(long SaleId, long UserId, string Reason, List<ReturnItemRequest> Items);

public record ReturnItemRequest(long SaleItemId, int Quantity, bool Restock);

public class ProcessReturnCommandValidator : AbstractValidator<ProcessReturnCommand>
{
    public ProcessReturnCommandValidator()
    {
        RuleFor(x => x.SaleId).GreaterThan(0).WithMessage("Vente d'origine requise.");
        RuleFor(x => x.UserId).GreaterThan(0).WithMessage("Utilisateur requis.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Le motif de retour est obligatoire.").MaximumLength(255);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Au moins un article doit être sélectionné pour le retour.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.SaleItemId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("La quantité retournée doit être supérieure à 0.");
        });
    }
}

public record StockAdjustmentCommand(long ProductId, int NewQuantity, string Reason, long UserId);

public class StockAdjustmentCommandValidator : AbstractValidator<StockAdjustmentCommand>
{
    public StockAdjustmentCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.NewQuantity).GreaterThanOrEqualTo(0).WithMessage("Le stock ne peut pas être négatif.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Le motif de l'ajustement est obligatoire.").MaximumLength(200);
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}
