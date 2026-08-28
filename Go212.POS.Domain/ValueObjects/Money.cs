namespace Go212.POS.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a monetary amount in a specific currency (default "MAD").
/// Prevents floating-point rounding errors and enforces Moroccan Dirham precision.
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public static readonly Money Zero = new(0m, "MAD");

    public Money(decimal amount, string currency = "MAD")
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency code cannot be empty.", nameof(currency));

        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.ToUpperInvariant();
    }

    public static Money FromMad(decimal amount) => new(amount, "MAD");

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    public static Money operator *(Money m, decimal factor) =>
        new(m.Amount * factor, m.Currency);

    public static Money operator *(decimal factor, Money m) =>
        new(m.Amount * factor, m.Currency);

    public static Money operator /(Money m, decimal divisor)
    {
        if (divisor == 0)
            throw new DivideByZeroException("Cannot divide Money by zero.");
        return new Money(m.Amount / divisor, m.Currency);
    }

    public static bool operator >(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a.Amount > b.Amount;
    }

    public static bool operator <(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a.Amount < b.Amount;
    }

    public static bool operator >=(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a.Amount >= b.Amount;
    }

    public static bool operator <=(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return a.Amount <= b.Amount;
    }

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException($"Cannot operate on different currencies: {a.Currency} and {b.Currency}");
    }

    public override string ToString() => $"{Amount:N2} {Currency}";

    public string ToFormattedString() => $"{Amount:0.00} {Currency}";
}
