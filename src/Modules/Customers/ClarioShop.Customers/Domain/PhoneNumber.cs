namespace ClarioShop.Customers.Domain;

/// <summary>
/// Bangladeshi mobile numbers. <c>01712-345678</c>, <c>+8801712345678</c>, <c>8801712345678</c> and
/// <c>008801712345678</c> are the same number and normalize to E.164 <c>+8801712345678</c>. Bangla digits
/// (<c>০১৭১২৩৪৫৬৭৮</c>) are read as their ASCII equivalents.
/// </summary>
internal static class PhoneNumber
{
    public const string CountryPrefix = "+880";

    /// <summary>Longest input accepted, to keep obviously bogus text out before parsing.</summary>
    public const int MaxInputLength = 32;

    private const char BanglaZero = '\u09E6';

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input) || input.Length > MaxInputLength)
        {
            return false;
        }

        var text = ToAsciiDigits(input.Trim());
        var plus = text.StartsWith('+');
        var digits = new string([.. (plus ? text[1..] : text).Where(c => !IsSeparator(c))]);
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        // The national significant number is ten digits: 1, an operator digit 3-9, then eight more.
        var national = digits switch
        {
            _ when plus => digits.StartsWith("880", StringComparison.Ordinal) ? digits[3..] : null,
            _ when digits.StartsWith("00880", StringComparison.Ordinal) => digits[5..],
            _ when digits.StartsWith("880", StringComparison.Ordinal) => digits[3..],
            _ when digits.StartsWith('0') => digits[1..],
            _ => digits,
        };

        if (national is not { Length: 10 } || national[0] != '1' || national[1] is < '3' or > '9')
        {
            return false;
        }

        normalized = CountryPrefix + national;
        return true;
    }

    /// <summary>
    /// The digits worth matching when staff search by a fragment of a number written in any format:
    /// country code and trunk zero stripped, so <c>01712</c> and <c>+8801712</c> both find <c>+8801712345678</c>.
    /// </summary>
    public static string SearchDigits(string input)
    {
        var digits = new string([.. ToAsciiDigits(input).Where(char.IsAsciiDigit)]);
        if (digits.StartsWith("00880", StringComparison.Ordinal))
        {
            return digits[5..];
        }

        if (digits.StartsWith("880", StringComparison.Ordinal))
        {
            return digits[3..];
        }

        return digits.TrimStart('0');
    }

    private static string ToAsciiDigits(string text) =>
        string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = source[i] is >= BanglaZero and <= (char)(BanglaZero + 9) ? (char)('0' + (source[i] - BanglaZero)) : source[i];
            }
        });

    private static bool IsSeparator(char c) => c is ' ' or '-' or '.' or '(' or ')' || char.IsWhiteSpace(c);
}
