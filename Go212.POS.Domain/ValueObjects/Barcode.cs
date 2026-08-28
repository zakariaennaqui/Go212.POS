using System.Text.RegularExpressions;

namespace Go212.POS.Domain.ValueObjects;

/// <summary>
/// Value object representing a validated barcode (EAN-13, EAN-8, UPC-A, or alphanumeric SKU).
/// Includes check digit validation for EAN standards.
/// </summary>
public readonly record struct Barcode
{
    private static readonly Regex NumericRegex = new(@"^\d+$", RegexOptions.Compiled);

    public string Value { get; }
    public BarcodeType Type { get; }

    public Barcode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Barcode cannot be null or empty.", nameof(value));

        var clean = value.Trim();
        Value = clean;
        Type = DetermineBarcodeType(clean);
    }

    public static bool TryParse(string? value, out Barcode barcode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            barcode = default;
            return false;
        }

        try
        {
            barcode = new Barcode(value);
            return true;
        }
        catch
        {
            barcode = default;
            return false;
        }
    }

    private static BarcodeType DetermineBarcodeType(string code)
    {
        if (!NumericRegex.IsMatch(code))
            return BarcodeType.CustomCode;

        return code.Length switch
        {
            8 => IsValidEan8(code) ? BarcodeType.Ean8 : BarcodeType.CustomCode,
            12 => IsValidUpcA(code) ? BarcodeType.UpcA : BarcodeType.CustomCode,
            13 => IsValidEan13(code) ? BarcodeType.Ean13 : BarcodeType.CustomCode,
            _ => BarcodeType.CustomCode
        };
    }

    /// <summary>
    /// Validates standard EAN-13 modulo 10 checksum.
    /// </summary>
    public static bool IsValidEan13(string code)
    {
        if (code.Length != 13 || !NumericRegex.IsMatch(code))
            return false;

        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            int digit = code[i] - '0';
            sum += (i % 2 == 0) ? digit : digit * 3;
        }

        int checkDigit = (10 - (sum % 10)) % 10;
        return (code[12] - '0') == checkDigit;
    }

    /// <summary>
    /// Validates standard EAN-8 modulo 10 checksum.
    /// </summary>
    public static bool IsValidEan8(string code)
    {
        if (code.Length != 8 || !NumericRegex.IsMatch(code))
            return false;

        int sum = 0;
        for (int i = 0; i < 7; i++)
        {
            int digit = code[i] - '0';
            sum += (i % 2 == 0) ? digit * 3 : digit;
        }

        int checkDigit = (10 - (sum % 10)) % 10;
        return (code[7] - '0') == checkDigit;
    }

    /// <summary>
    /// Validates standard UPC-A checksum.
    /// </summary>
    public static bool IsValidUpcA(string code)
    {
        if (code.Length != 12 || !NumericRegex.IsMatch(code))
            return false;

        int sum = 0;
        for (int i = 0; i < 11; i++)
        {
            int digit = code[i] - '0';
            sum += (i % 2 == 0) ? digit * 3 : digit;
        }

        int checkDigit = (10 - (sum % 10)) % 10;
        return (code[11] - '0') == checkDigit;
    }

    public override string ToString() => Value;
}

public enum BarcodeType
{
    CustomCode,
    Ean8,
    Ean13,
    UpcA,
    Code128
}
