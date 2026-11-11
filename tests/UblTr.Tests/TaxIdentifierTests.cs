using FsCheck;
using FsCheck.Xunit;
using UblTr.SampleGen;
using UblTr.Validator.Identifiers;

namespace UblTr.Tests;

public sealed class TaxIdentifierTests
{
    [Theory]
    [InlineData("10000000146")] // the textbook valid TCKN used in civil-registry documentation
    [InlineData("11111111110")]
    public void Known_valid_tckn_is_accepted(string tckn) => TaxIdentifiers.IsValidTckn(tckn).Should().BeTrue();

    [Theory]
    [InlineData("10000000147")] // last digit off
    [InlineData("01000000146")] // leading zero
    [InlineData("1000000014")] // 10 digits
    [InlineData("1000000014a")]
    [InlineData("")]
    public void Malformed_or_wrong_tckn_is_rejected(string tckn) => TaxIdentifiers.IsValidTckn(tckn).Should().BeFalse();

    /// <summary>
    /// Independent check of the VKN algorithm against real, published VKNs of public institutions that GIB's own
    /// Schematron names (its central receiver account and the customs ministry account).
    /// </summary>
    [Theory]
    [InlineData("3900892152")]
    [InlineData("1460415308")]
    public void Published_institution_vkn_is_valid(string vkn) => TaxIdentifiers.IsValidVkn(vkn).Should().BeTrue();

    [Theory]
    [InlineData("3900892153")]
    [InlineData("390089215")]
    [InlineData("39008921 2")]
    public void Wrong_vkn_is_rejected(string vkn) => TaxIdentifiers.IsValidVkn(vkn).Should().BeFalse();

    [Theory]
    [InlineData("123456789", 0)]
    [InlineData("987654321", 7)]
    [InlineData("000000000", 1)]
    public void Vkn_check_digit_matches_the_published_algorithm(string firstNine, int expected) =>
        TaxIdentifiers.VknCheckDigit(firstNine).Should().Be(expected);

    [Theory]
    [InlineData("12345678")]
    [InlineData("12345678a")]
    public void Vkn_check_digit_needs_nine_digits(string input) =>
        FluentActions.Invoking(() => TaxIdentifiers.VknCheckDigit(input)).Should().Throw<ArgumentException>();

    [Property(MaxTest = 1000)]
    public bool Generated_vkn_is_always_valid(int seed) => TaxIdentifiers.IsValidVkn(SyntheticData.Vkn(new Random(seed)));

    [Property(MaxTest = 1000)]
    public bool Generated_tckn_is_always_valid(int seed) => TaxIdentifiers.IsValidTckn(SyntheticData.Tckn(new Random(seed)));

    [Property(MaxTest = 1000)]
    public bool Changing_the_vkn_check_digit_is_always_detected(int seed, PositiveInt delta)
    {
        var vkn = SyntheticData.Vkn(new Random(seed));
        var bump = delta.Get % 9 + 1;
        var tampered = vkn[..9] + (char)('0' + (vkn[9] - '0' + bump) % 10);
        return !TaxIdentifiers.IsValidVkn(tampered);
    }

    /// <summary>
    /// The TCKN scheme detects every single-digit substitution: d10 weights odd positions by 7 and even positions by
    /// -1 (both invertible mod 10), and d11 covers d10 itself. This property checks that claim on random numbers.
    /// </summary>
    [Property(MaxTest = 1000)]
    public bool Any_single_digit_substitution_in_a_tckn_is_detected(int seed, PositiveInt position, PositiveInt delta)
    {
        var tckn = SyntheticData.Tckn(new Random(seed)).ToCharArray();
        var i = position.Get % 11;
        var bump = delta.Get % 9 + 1;
        var original = tckn[i] - '0';
        tckn[i] = (char)('0' + (original + bump) % 10);
        var tampered = new string(tckn);
        return tampered[0] == '0' || !TaxIdentifiers.IsValidTckn(tampered);
    }
}
