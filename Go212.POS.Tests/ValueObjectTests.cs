using FluentAssertions;
using Go212.POS.Domain.ValueObjects;
using Xunit;

namespace Go212.POS.Tests;

public class ValueObjectTests
{
    [Fact]
    public void Money_Operations_AddAndSubtractCorrectly()
    {
        var m1 = Money.FromMad(100.50m);
        var m2 = Money.FromMad(49.50m);

        var sum = m1 + m2;
        var diff = m1 - m2;

        sum.Amount.Should().Be(150.00m);
        sum.Currency.Should().Be("MAD");
        diff.Amount.Should().Be(51.00m);
    }

    [Fact]
    public void Money_MultiplyAndDivide_CalculatedWithPrecision()
    {
        var price = Money.FromMad(25.50m);
        var total = price * 3;
        var perUnit = total / 3;

        total.Amount.Should().Be(76.50m);
        perUnit.Amount.Should().Be(25.50m);
    }

    [Theory]
    [InlineData("6111234567894", true)]  // Valid EAN-13 example (Moroccan prefix 611)
    [InlineData("1234567890128", true)]  // Valid standard EAN-13
    [InlineData("1234567890120", false)] // Invalid check digit
    [InlineData("96385074", true)]       // Valid EAN-8
    [InlineData("96385070", false)]      // Invalid EAN-8
    public void Barcode_EanValidation_MatchesStandard(string code, bool expectedValid)
    {
        if (code.Length == 13)
        {
            Barcode.IsValidEan13(code).Should().Be(expectedValid);
        }
        else if (code.Length == 8)
        {
            Barcode.IsValidEan8(code).Should().Be(expectedValid);
        }
    }

    [Fact]
    public void TaxRate_MoroccanVatCalculations_Accurate()
    {
        var vat20 = TaxRate.Standard20;
        var vat7 = TaxRate.Reduced7;

        vat20.CalculateTax(100.00m).Should().Be(20.00m);
        vat20.CalculateTTC(100.00m).Should().Be(120.00m);
        vat20.ExtractHT(120.00m).Should().Be(100.00m);

        vat7.CalculateTax(100.00m).Should().Be(7.00m);
        vat7.CalculateTTC(100.00m).Should().Be(107.00m);
    }

    [Fact]
    public void DateRange_TodayAndContains_Correct()
    {
        var today = DateRange.Today();
        today.Contains(DateTime.Now).Should().BeTrue();
        today.From.Should().BeBefore(today.To);
    }
}
