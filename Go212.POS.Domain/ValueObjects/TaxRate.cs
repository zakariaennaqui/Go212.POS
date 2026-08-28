namespace Go212.POS.Domain.ValueObjects;

/// <summary>
/// Value object encapsulating legal tax rates in Morocco (0%, 7%, 10%, 14%, 20%).
/// Provides accurate VAT calculation without floating-point drift.
/// </summary>
public readonly record struct TaxRate
{
    public decimal RatePercent { get; }

    public static readonly decimal[] LegalRates = [0m, 7m, 10m, 14m, 20m];

    public static readonly TaxRate Zero = new(0m);
    public static readonly TaxRate Reduced7 = new(7m);
    public static readonly TaxRate Intermediate10 = new(10m);
    public static readonly TaxRate Specific14 = new(14m);
    public static readonly TaxRate Standard20 = new(20m);

    public TaxRate(decimal ratePercent)
    {
        if (ratePercent < 0 || ratePercent > 100)
            throw new ArgumentOutOfRangeException(nameof(ratePercent), "Tax rate must be between 0% and 100%.");

        RatePercent = ratePercent;
    }

    public static bool IsLegalMoroccanRate(decimal rate) => LegalRates.Contains(rate);

    /// <summary>
    /// Calculates the tax amount from a HT (before-tax) price.
    /// </summary>
    public decimal CalculateTax(decimal priceHT) =>
        Math.Round(priceHT * (RatePercent / 100m), 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Calculates the TTC (all-tax-included) price from a HT price.
    /// </summary>
    public decimal CalculateTTC(decimal priceHT) =>
        priceHT + CalculateTax(priceHT);

    /// <summary>
    /// Extracts the HT price from a TTC price.
    /// </summary>
    public decimal ExtractHT(decimal priceTTC) =>
        Math.Round(priceTTC / (1m + (RatePercent / 100m)), 2, MidpointRounding.AwayFromZero);

    public override string ToString() => $"{RatePercent:0.##}%";
}
