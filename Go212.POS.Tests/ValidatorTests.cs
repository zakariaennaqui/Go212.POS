using FluentAssertions;
using Go212.POS.Application.Commands;
using Go212.POS.Application.Validators;
using Go212.POS.Domain.Enums;
using Xunit;

namespace Go212.POS.Tests;

public class ValidatorTests
{
    [Fact]
    public void CreateProductValidator_ValidProduct_PassesValidation()
    {
        var validator = new CreateProductValidator();
        var command = new CreateProductCommand(
            Name: "Café Lavazza 250g",
            CategoryId: 1,
            PriceHT: 45.00m,
            TaxRate: 20m,
            Barcode: "6111234567894",
            Unit: "pcs",
            StockAlertThreshold: 5
        );

        var result = validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateProductValidator_InvalidTva_FailsValidation()
    {
        var validator = new CreateProductValidator();
        var command = new CreateProductCommand(
            Name: "Produit Test",
            CategoryId: 1,
            PriceHT: 50.00m,
            TaxRate: 15m, // Invalid TVA rate in Morocco (legal: 0, 7, 10, 14, 20)
            Barcode: "1234567890128",
            Unit: "pcs",
            StockAlertThreshold: 5
        );

        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TaxRate");
    }

    [Fact]
    public void LoginCommandValidator_EmptyPin_FailsValidation()
    {
        var validator = new LoginCommandValidator();
        var cmd = new LoginCommand("admin", "");

        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Pin");
    }

    [Fact]
    public void AddExpenseValidator_NegativeAmount_FailsValidation()
    {
        var validator = new AddExpenseValidator();
        var cmd = new AddExpenseCommand(1, 1, "Fournitures bureau", -50.00m, "Fournitures");

        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Amount");
    }

    [Fact]
    public void CreateUserValidator_ValidManager_PassesValidation()
    {
        var validator = new CreateUserCommandValidator();
        var cmd = new CreateUserCommand("Karim Alami", "kalami", "5678", UserRole.Manager);

        var result = validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }
}
