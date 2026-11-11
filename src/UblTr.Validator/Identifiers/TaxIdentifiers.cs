namespace UblTr.Validator.Identifiers;

/// <summary>
/// Turkish tax identifiers. VKN (Vergi Kimlik Numarasi) is the 10-digit tax number of companies and sole traders;
/// TCKN (T.C. Kimlik Numarasi) is the 11-digit national ID number that individuals use as their tax number.
/// GIB's Schematron checks only their length; the check digits below catch typos the length check cannot.
/// </summary>
public static class TaxIdentifiers
{
    public static bool IsDigits(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    /// <summary>
    /// VKN check digit: for the first nine digits d(i), i = 0..8, take t = (d + 9 - i) mod 10 and
    /// v = t * 2^(9 - i) mod 9 (with v = 9 when t != 0 and the product is a multiple of 9). The tenth digit is
    /// (10 - sum(v) mod 10) mod 10.
    /// </summary>
    public static int VknCheckDigit(ReadOnlySpan<char> firstNine)
    {
        if (firstNine.Length != 9 || !IsDigits(firstNine))
        {
            throw new ArgumentException("A VKN check digit needs exactly nine digits.", nameof(firstNine));
        }

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var t = (firstNine[i] - '0' + 9 - i) % 10;
            var v = t * (1 << (9 - i)) % 9;
            if (t != 0 && v == 0)
            {
                v = 9;
            }

            sum += v;
        }

        return (10 - sum % 10) % 10;
    }

    public static bool IsValidVkn(ReadOnlySpan<char> vkn) =>
        vkn.Length == 10 && IsDigits(vkn) && VknCheckDigit(vkn[..9]) == vkn[9] - '0';

    /// <summary>
    /// TCKN check digits (published by the civil registry): the first digit is not 0;
    /// d10 = (7 * (d1 + d3 + d5 + d7 + d9) - (d2 + d4 + d6 + d8)) mod 10; d11 = (d1 + ... + d10) mod 10.
    /// </summary>
    public static (int Tenth, int Eleventh) TcknCheckDigits(ReadOnlySpan<char> firstNine)
    {
        if (firstNine.Length != 9 || !IsDigits(firstNine) || firstNine[0] == '0')
        {
            throw new ArgumentException("TCKN check digits need nine digits that do not start with 0.", nameof(firstNine));
        }

        int odd = 0, even = 0;
        for (var i = 0; i < 9; i++)
        {
            var d = firstNine[i] - '0';
            if (i % 2 == 0)
            {
                odd += d;
            }
            else
            {
                even += d;
            }
        }

        var tenth = ((7 * odd - even) % 10 + 10) % 10;
        var eleventh = (odd + even + tenth) % 10;
        return (tenth, eleventh);
    }

    public static bool IsValidTckn(ReadOnlySpan<char> tckn)
    {
        if (tckn.Length != 11 || !IsDigits(tckn) || tckn[0] == '0')
        {
            return false;
        }

        var (tenth, eleventh) = TcknCheckDigits(tckn[..9]);
        return tckn[9] - '0' == tenth && tckn[10] - '0' == eleventh;
    }
}
