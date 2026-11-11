using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UblTr.Validator.CodeLists;
using static UblTr.Validator.Rules.InvoiceContext;

namespace UblTr.Validator.Rules;

internal static partial class HeaderChecks
{
    private static readonly string[] ReturnProfiles = ["TEMELFATURA", "EARSIVFATURA", "ILAC_TIBBICIHAZ", "YATIRIMTESVIK", "IDIS", "KAMU"];
    private static readonly string[] ReturnTypes = ["IADE", "TEVKIFATIADE", "YTBIADE", "YTBTEVKIFATIADE"];

    private static readonly Dictionary<string, string[]> TypesByProfile = new(StringComparer.Ordinal)
    {
        ["ILAC_TIBBICIHAZ"] = ["SATIS", "ISTISNA", "TEVKIFAT", "TEVKIFATIADE", "IADE", "IHRACKAYITLI"],
        ["YATIRIMTESVIK"] = ["SATIS", "ISTISNA", "IADE", "TEVKIFAT", "TEVKIFATIADE"],
        ["IDIS"] = ["SATIS", "ISTISNA", "IADE", "TEVKIFAT", "TEVKIFATIADE", "IHRACKAYITLI"],
    };

    private static readonly DateOnly EarliestIssueDate = new(2005, 1, 1);

    // GIB InvoiceIDCheck: ^[A-Z0-9]{3}20[0-9]{2}[0-9]{9}$
    [GeneratedRegex("^[A-Z0-9]{3}20[0-9]{2}[0-9]{9}$", RegexOptions.CultureInvariant)]
    private static partial Regex InvoiceIdPattern();

    // GIB UUIDCheck.
    [GeneratedRegex("^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}$", RegexOptions.CultureInvariant)]
    private static partial Regex UuidPattern();

    public static void Run(InvoiceContext ctx)
    {
        var root = ctx.Root;
        var profileEl = root.Element(Ns.Cbc + "ProfileID");
        var typeEl = root.Element(Ns.Cbc + "InvoiceTypeCode");
        var profile = ctx.ProfileId;
        var type = ctx.InvoiceTypeCode;

        var version = root.Element(Ns.Cbc + "UBLVersionID");
        if (version?.Value.Trim() != "2.1")
        {
            ctx.Report(RuleCatalog.UblVersion, (XObject?)version ?? root, version?.Value.Trim());
        }

        var customization = root.Element(Ns.Cbc + "CustomizationID");
        if (customization?.Value.Trim() is not ("TR1.2" or "TR1.2.1"))
        {
            ctx.Report(RuleCatalog.Customization, (XObject?)customization ?? root, customization?.Value.Trim());
        }

        var profileList = ctx.Kind == DocumentKind.EArsiv ? CodeListSet.ProfileIdsEArchive : CodeListSet.ProfileIds;
        if (!ctx.CodeLists.Contains(profileList, profile))
        {
            ctx.Report(RuleCatalog.ProfileId, (XObject?)profileEl ?? root, profile,
                string.Join(", ", ctx.CodeLists.Get(profileList)), KindName(ctx.Kind));
        }

        if (!ctx.CodeLists.Contains(CodeListSet.InvoiceTypeCodes, type))
        {
            ctx.Report(RuleCatalog.InvoiceTypeCode, (XObject?)typeEl ?? root, type);
        }

        CheckProfileTypeCombinations(ctx, (XObject?)typeEl ?? root, profile, type);
        CheckIdentity(ctx);

        var lineCount = root.Element(Ns.Cbc + "LineCountNumeric");
        if (lineCount is not null && Amount(lineCount) is var declared && declared != ctx.Lines.Count)
        {
            ctx.Report(RuleCatalog.LineCount, lineCount, lineCount.Value.Trim(), ctx.Lines.Count);
        }

        if (type is not null && ReturnTypes.Contains(type))
        {
            var refs = root.Elements(Ns.Cac + "BillingReference").Elements(Ns.Cac + "InvoiceDocumentReference").ToList();
            var valid = refs.Count > 0 && refs.TrueForAll(r =>
                Text(r, Ns.Cbc + "DocumentTypeCode") is "IADE" or "İADE" && (Text(r, Ns.Cbc + "ID") ?? "").Length == 16);
            if (!valid)
            {
                ctx.Report(RuleCatalog.ReturnReference, typeEl, type);
            }
        }
    }

    private static void CheckProfileTypeCombinations(InvoiceContext ctx, XObject at, string? profile, string? type)
    {
        if (type == "IADE" && !ReturnProfiles.Contains(profile))
        {
            ctx.Report(RuleCatalog.ReturnProfile, at, profile);
        }

        // GIB evaluates the ENERJI pairing only for e-Fatura (type parameter 'efatura').
        if (ctx.Kind == DocumentKind.EFatura)
        {
            var isEnergy = profile == "ENERJI";
            var isCharging = type is "SARJ" or "SARJANLIK";
            if (isEnergy != isCharging)
            {
                ctx.Report(RuleCatalog.EnergyProfile, at, profile, type);
            }
        }

        if (type == "TEKNOLOJIDESTEK" && profile != "EARSIVFATURA")
        {
            ctx.Report(RuleCatalog.TechSupportProfile, at, profile);
        }

        if (profile is not null && TypesByProfile.TryGetValue(profile, out var allowed) && !allowed.Contains(type))
        {
            ctx.Report(RuleCatalog.ProfileTypeSet, at, profile, type, string.Join(", ", allowed));
        }

        if (ctx.CodeLists.Contains(CodeListSet.InvestmentIncentiveEArchiveTypes, type) && profile != "EARSIVFATURA")
        {
            ctx.Report(RuleCatalog.InvestmentEArchiveTypes, at, type, profile);
        }
    }

    private static void CheckIdentity(InvoiceContext ctx)
    {
        var root = ctx.Root;
        var idEl = root.Element(Ns.Cbc + "ID");
        var id = idEl?.Value.Trim();
        var idValid = id is not null && InvoiceIdPattern().IsMatch(id);
        if (!idValid)
        {
            ctx.Report(RuleCatalog.InvoiceIdFormat, (XObject?)idEl ?? root, id);
        }

        var issueEl = root.Element(Ns.Cbc + "IssueDate");
        DateOnly? issueDate = DateOnly.TryParseExact(issueEl?.Value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;

        if (idValid && issueDate is { } date && int.Parse(id.AsSpan(3, 4), CultureInfo.InvariantCulture) != date.Year)
        {
            ctx.Report(RuleCatalog.InvoiceIdYear, idEl, id![3..7], date.Year);
        }

        if (issueDate is { } issued)
        {
            if (issued > ctx.Options.Today)
            {
                ctx.Report(RuleCatalog.IssueDateRange, issueEl, issueEl!.Value.Trim(),
                    new LocalizedText($"bugünden ({ctx.Options.Today:yyyy-MM-dd}) ileri bir tarih", $"is after today ({ctx.Options.Today:yyyy-MM-dd})"));
            }
            else if (issued < EarliestIssueDate)
            {
                ctx.Report(RuleCatalog.IssueDateRange, issueEl, issueEl!.Value.Trim(),
                    new LocalizedText("01.01.2005'ten önce", "is before 2005-01-01"));
            }
        }

        var uuidEl = root.Element(Ns.Cbc + "UUID");
        var uuid = uuidEl?.Value.Trim();
        if (uuid is null || !UuidPattern().IsMatch(uuid))
        {
            ctx.Report(RuleCatalog.UuidFormat, (XObject?)uuidEl ?? root, uuid);
        }

        var copy = root.Element(Ns.Cbc + "CopyIndicator");
        if (copy?.Value.Trim() != "false")
        {
            ctx.Report(RuleCatalog.CopyIndicator, (XObject?)copy ?? root, copy?.Value.Trim());
        }
    }

    internal static LocalizedText KindName(DocumentKind kind) => kind == DocumentKind.EArsiv
        ? new LocalizedText("e-Arşiv faturası", "an e-Arşiv invoice")
        : new LocalizedText("e-Fatura", "an e-Fatura");
}
