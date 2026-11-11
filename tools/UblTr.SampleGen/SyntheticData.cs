using System.Globalization;
using Bogus;
using UblTr.Validator.Identifiers;

namespace UblTr.SampleGen;

/// <summary>
/// Synthetic parties and invoices. Tax numbers are checksum-valid but randomly generated; names and addresses come from
/// Bogus' Turkish locale. Nothing here belongs to a real person or company.
/// </summary>
public static class SyntheticData
{
    private static readonly decimal[] VatRates = [20m, 10m, 1m];

    public static string Vkn(Random random)
    {
        var digits = string.Concat(Enumerable.Range(0, 9).Select(_ => random.Next(10).ToString(CultureInfo.InvariantCulture)));
        return digits + TaxIdentifiers.VknCheckDigit(digits).ToString(CultureInfo.InvariantCulture);
    }

    public static string Tckn(Random random)
    {
        var digits = random.Next(1, 10).ToString(CultureInfo.InvariantCulture)
                     + string.Concat(Enumerable.Range(0, 8).Select(_ => random.Next(10).ToString(CultureInfo.InvariantCulture)));
        var (tenth, eleventh) = TaxIdentifiers.TcknCheckDigits(digits);
        return $"{digits}{tenth}{eleventh}";
    }

    /// <summary>Fixed supplier used by the committed samples, so golden files stay stable.</summary>
    public static PartySpec Supplier { get; } = new("VKN", WithVknCheck("123456789"), "Örnek Yazılım ve Danışmanlık A.Ş.", null, null,
        "Ankara", "Çankaya", "Atatürk Bulvarı No:1", "Kavaklıdere");

    public static PartySpec CompanyCustomer { get; } = new("VKN", WithVknCheck("987654321"), "Deneme Gıda Ticaret Ltd. Şti.", null, null,
        "İstanbul", "Kadıköy", "Bağdat Caddesi No:200", "Göztepe");

    public static PartySpec PersonCustomer { get; } = new("TCKN", WithTcknCheck("100000001"), null, "Ayşe", "Yılmaz",
        "İzmir", "Konak", "Cumhuriyet Meydanı No:5", "Konak");

    public static string WithVknCheck(string firstNine) => firstNine + TaxIdentifiers.VknCheckDigit(firstNine).ToString(CultureInfo.InvariantCulture);

    public static string WithTcknCheck(string firstNine)
    {
        var (tenth, eleventh) = TaxIdentifiers.TcknCheckDigits(firstNine);
        return $"{firstNine}{tenth}{eleventh}";
    }

    /// <summary>A random but always-valid TEMELFATURA/TICARIFATURA/EARSIVFATURA invoice. Same seed, same invoice.</summary>
    public static InvoiceSpec RandomInvoice(int seed, int lineCount)
    {
        var random = new Random(seed);
        var faker = new Faker("tr") { Random = new Randomizer(seed) };
        var person = random.Next(4) == 0;
        var supplier = Company(faker, random);
        var customer = person
            ? new PartySpec("TCKN", Tckn(random), null, faker.Name.FirstName(), faker.Name.LastName(), faker.Address.City(),
                faker.Address.County(), faker.Address.StreetAddress(), faker.Address.City())
            : Company(faker, random);
        var profile = person ? "EARSIVFATURA" : random.Next(2) == 0 ? "TEMELFATURA" : "TICARIFATURA";
        var foreign = !person && random.Next(5) == 0;

        var lines = new List<LineSpec>(lineCount);
        for (var i = 0; i < lineCount; i++)
        {
            var quantity = random.Next(1, 50);
            var price = Math.Round((decimal)random.NextDouble() * 5000m + 1m, random.Next(2) == 0 ? 2 : 4);
            var allowance = random.Next(6) == 0 ? Math.Round(quantity * price * 0.05m, 2) : 0m;
            lines.Add(new LineSpec(faker.Commerce.ProductName(), quantity, price, VatRates[random.Next(VatRates.Length)], allowance));
        }

        var year = 2026;
        return new InvoiceSpec
        {
            Id = $"{(char)('A' + random.Next(26))}{(char)('A' + random.Next(26))}{random.Next(10)}{year}{random.Next(1, 999_999_999):D9}",
            Uuid = new Guid(faker.Random.Bytes(16)),
            IssueDate = new DateOnly(year, random.Next(1, 11), random.Next(1, 28)),
            Profile = profile,
            Currency = foreign ? "EUR" : "TRY",
            ExchangeRate = foreign ? 39.512345m : null,
            Supplier = supplier,
            Customer = customer,
            Lines = lines,
            LineTaxTotals = random.Next(3) != 0,
        };
    }

    private static PartySpec Company(Faker faker, Random random) => new("VKN", Vkn(random), faker.Company.CompanyName(), null, null,
        faker.Address.City(), faker.Address.County(), faker.Address.StreetAddress(), faker.Address.City());
}
