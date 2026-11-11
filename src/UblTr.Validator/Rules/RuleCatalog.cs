namespace UblTr.Validator.Rules;

/// <summary>
/// Every rule this validator can report, with Turkish and English texts. Rule ids are stable: they appear in CI
/// annotations, golden files and suppressions, so a rule is never renumbered (a retired id stays retired).
/// </summary>
public static class RuleCatalog
{
    private const string Main = "GIB e-Fatura package, UBL-TR_Common_Schematron.xml";

    // ---- structure --------------------------------------------------------------------------------------------

    public static readonly RuleDescriptor MalformedXml = new(
        "XML-001", "xml", Severity.Error, RuleOrigin.Structure, "XML 1.0",
        new("XML iyi biçimli değil", "XML is not well-formed"),
        new("Belge XML olarak ayrıştırılamadı; hiçbir şema veya iş kuralı kontrolü yapılamaz.",
            "The document could not be parsed as XML, so no schema or business rule could run."),
        new("XML ayrıştırma hatası: {0}", "XML parse error: {0}"),
        new("Belirtilen satır ve sütundaki kapanmamış etiketi, eksik tırnağı veya geçersiz karakteri düzeltin; dosyanın UTF-8 olarak kaydedildiğinden emin olun.",
            "Fix the unclosed tag, missing quote or invalid character at the reported line and column, and make sure the file is saved as UTF-8."));

    public static readonly RuleDescriptor DtdForbidden = new(
        "XML-002", "xml", Severity.Error, RuleOrigin.Structure, "Security policy (XXE / entity expansion)",
        new("DTD ve varlık (entity) tanımı yasak", "DTD and entity declarations are forbidden"),
        new("UBL-TR belgeleri DTD kullanmaz. DTD işlemek dış varlık (XXE) ve 'billion laughs' saldırılarına kapı açar, bu yüzden reddedilir.",
            "UBL-TR documents never use a DTD. Processing one opens the door to external entity (XXE) and 'billion laughs' attacks, so it is rejected."),
        new("Belge bir DOCTYPE/DTD bildirimi içeriyor: {0}", "The document contains a DOCTYPE/DTD declaration: {0}"),
        new("<!DOCTYPE ...> bildirimini ve tanımlı varlıkları kaldırın; özel karakterleri doğrudan UTF-8 olarak yazın.",
            "Remove the <!DOCTYPE ...> declaration and any entity definitions; write special characters directly as UTF-8."));

    public static readonly RuleDescriptor NotAnInvoice = new(
        "DOC-001", "xml", Severity.Error, RuleOrigin.Structure, "UBL 2.1 Invoice",
        new("Kök eleman UBL Invoice değil", "Root element is not a UBL Invoice"),
        new("Bu doğrulayıcı UBL 2.1 Invoice belgelerini (e-Fatura ve e-Arşiv faturası) denetler.",
            "This validator checks UBL 2.1 Invoice documents (e-Fatura and e-Arşiv invoices)."),
        new("Kök eleman '{0}'; beklenen '{{urn:oasis:names:specification:ubl:schema:xsd:Invoice-2}}Invoice'. {1}",
            "Root element is '{0}'; expected '{{urn:oasis:names:specification:ubl:schema:xsd:Invoice-2}}Invoice'. {1}"),
        new("Faturayı doğru isim alanıyla (xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\") tek başına doğrulayın.",
            "Validate the invoice on its own, with the correct namespace (xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\")."));

    public static readonly RuleDescriptor SchemaViolation = new(
        "XSD-001", "schema", Severity.Error, RuleOrigin.Structure, "UBL-TR 1.2.1 XSD (GIB xsdrt, UBL-Invoice-2.1.xsd)",
        new("XSD şema ihlali", "XSD schema violation"),
        new("Belge GİB'in yayımladığı UBL-TR 1.2.1 XSD şemalarına uymuyor: eleman sırası, zorunlu alanlar ve veri tipleri şemayla tanımlıdır.",
            "The document does not conform to the UBL-TR 1.2.1 XSDs published by GIB, which fix element order, mandatory fields and data types."),
        new("{0}", "{0}"),
        new("{1}", "{1}"));

    // ---- header -----------------------------------------------------------------------------------------------

    public static readonly RuleDescriptor UblVersion = new(
        "HDR-001", "header", Severity.Error, RuleOrigin.GibSchematron, Main + " / UBLVersionIDCheck",
        new("UBLVersionID 2.1 olmalı", "UBLVersionID must be 2.1"),
        new("UBL-TR, UBL 2.1 üzerine kuruludur; başka bir sürüm numarası GİB tarafından reddedilir.",
            "UBL-TR is built on UBL 2.1; any other version number is rejected by GIB."),
        new("cbc:UBLVersionID değeri '{0}'; '2.1' olmalı.", "cbc:UBLVersionID is '{0}'; it must be '2.1'."),
        new("<cbc:UBLVersionID>2.1</cbc:UBLVersionID> yazın.", "Write <cbc:UBLVersionID>2.1</cbc:UBLVersionID>."));

    public static readonly RuleDescriptor Customization = new(
        "HDR-002", "header", Severity.Error, RuleOrigin.GibSchematron, Main + " / CustomizationIDCheck",
        new("CustomizationID TR1.2 veya TR1.2.1 olmalı", "CustomizationID must be TR1.2 or TR1.2.1"),
        new("CustomizationID belgenin hangi UBL-TR özelleştirmesine göre hazırlandığını söyler.",
            "CustomizationID states which UBL-TR customisation the document follows."),
        new("cbc:CustomizationID değeri '{0}'; 'TR1.2' veya 'TR1.2.1' olmalı.", "cbc:CustomizationID is '{0}'; it must be 'TR1.2' or 'TR1.2.1'."),
        new("<cbc:CustomizationID>TR1.2</cbc:CustomizationID> yazın.", "Write <cbc:CustomizationID>TR1.2</cbc:CustomizationID>."));

    public static readonly RuleDescriptor ProfileId = new(
        "HDR-003", "header", Severity.Error, RuleOrigin.GibSchematron, Main + " / ProfileIDCheck (ProfileIDType, ProfileIDTypeEarchive)",
        new("Geçersiz senaryo (ProfileID)", "Invalid scenario (ProfileID)"),
        new("ProfileID faturanın senaryosudur (TEMELFATURA, TICARIFATURA, EARSIVFATURA ...). e-Fatura ve e-Arşiv için geçerli senaryolar farklıdır.",
            "ProfileID is the invoice scenario (TEMELFATURA, TICARIFATURA, EARSIVFATURA ...). e-Fatura and e-Arşiv allow different scenarios."),
        new("cbc:ProfileID '{0}' {2} için geçerli değil. Geçerli değerler: {1}.", "cbc:ProfileID '{0}' is not valid for {2}. Valid values: {1}."),
        new("Senaryoyu listedeki değerlerden biriyle değiştirin; e-Arşiv faturası düzenliyorsanız EARSIVFATURA kullanın.",
            "Use one of the listed scenarios; for an e-Arşiv invoice use EARSIVFATURA."));

    public static readonly RuleDescriptor InvoiceTypeCode = new(
        "HDR-004", "header", Severity.Error, RuleOrigin.GibSchematron, Main + " / InvoiceTypeCodeCheck (InvoiceTypeCodeList)",
        new("Geçersiz fatura tipi (InvoiceTypeCode)", "Invalid invoice type (InvoiceTypeCode)"),
        new("InvoiceTypeCode faturanın tipidir (SATIS, IADE, TEVKIFAT, ISTISNA ...) ve GİB kod listesinden gelmelidir.",
            "InvoiceTypeCode is the invoice type (SATIS, IADE, TEVKIFAT, ISTISNA ...) and must come from the GIB code list."),
        new("cbc:InvoiceTypeCode '{0}' GİB kod listesinde yok.", "cbc:InvoiceTypeCode '{0}' is not in the GIB code list."),
        new("Kod listesindeki bir tipi kullanın (ör. SATIS, IADE, TEVKIFAT, ISTISNA, OZELMATRAH, IHRACKAYITLI).",
            "Use a type from the code list (e.g. SATIS, IADE, TEVKIFAT, ISTISNA, OZELMATRAH, IHRACKAYITLI)."));

    public static readonly RuleDescriptor ReturnProfile = new(
        "HDR-005", "header", Severity.Error, RuleOrigin.GibSchematron, Main + " / InvoiceTypeCodeCheck",
        new("IADE faturası bu senaryoda düzenlenemez", "A return (IADE) invoice is not allowed in this scenario"),
        new("İade faturaları yalnızca TEMELFATURA, EARSIVFATURA, ILAC_TIBBICIHAZ, YATIRIMTESVIK, IDIS ve KAMU senaryolarında düzenlenebilir; ticari faturada iade, ret yanıtıyla yapılır.",
            "Return invoices are only allowed in the TEMELFATURA, EARSIVFATURA, ILAC_TIBBICIHAZ, YATIRIMTESVIK, IDIS and KAMU scenarios; a commercial invoice is reversed with a rejection response instead."),
        new("Fatura tipi IADE iken senaryo '{0}' olamaz.", "Invoice type IADE cannot use scenario '{0}'."),
        new("Senaryoyu TEMELFATURA (veya e-Arşiv için EARSIVFATURA) yapın.", "Change the scenario to TEMELFATURA (or EARSIVFATURA for e-Arşiv)."));

    public static readonly RuleDescriptor EnergyProfile = new(
        "HDR-006", "header", Severity.Error, RuleOrigin.GibSchematron, Main + " / InvoiceTypeCodeCheck",
        new("ENERJI senaryosu ile SARJ/SARJANLIK tipi birlikte kullanılmalı", "ENERJI scenario and SARJ/SARJANLIK types go together"),
        new("Elektrikli araç şarj faturalarında senaryo ENERJI, tip SARJ veya SARJANLIK olur; biri varken diğeri de olmalıdır.",
            "Electric vehicle charging invoices use scenario ENERJI with type SARJ or SARJANLIK; one requires the other."),
        new("Senaryo '{0}' ile fatura tipi '{1}' uyumsuz.", "Scenario '{0}' and invoice type '{1}' do not match."),
        new("Şarj hizmeti için ENERJI + SARJ/SARJANLIK kullanın; şarj dışı faturalarda bu ikisini kullanmayın.",
            "Use ENERJI + SARJ/SARJANLIK for charging services and neither of them otherwise."));

    public static readonly RuleDescriptor TechSupportProfile = new(
        "HDR-007", "header", Severity.Error, RuleOrigin.GibSchematron, Main + " / InvoiceTypeCodeCheck",
        new("TEKNOLOJIDESTEK yalnızca e-Arşiv faturasında kullanılabilir", "TEKNOLOJIDESTEK is only allowed on e-Arşiv invoices"),
        new("Teknoloji destek faturası (TEKNOLOJIDESTEK tipi) GİB kuralına göre EARSIVFATURA senaryosunda düzenlenir.",
            "Under GIB rules a technology support invoice (type TEKNOLOJIDESTEK) is issued under the EARSIVFATURA scenario."),
        new("Fatura tipi TEKNOLOJIDESTEK iken senaryo '{0}'; EARSIVFATURA olmalı.", "Invoice type TEKNOLOJIDESTEK uses scenario '{0}'; it must be EARSIVFATURA."),
        new("Senaryoyu EARSIVFATURA yapın.", "Change the scenario to EARSIVFATURA."));

    public static readonly RuleDescriptor ProfileTypeSet = new(
        "HDR-008", "header", Severity.Error, RuleOrigin.GibSchematron,
        Main + " / IlacTibbiCihazInvoiceTypeCodeCheck, YatirimTesvikInvoiceTypeCodeCheck, IdisInvoiceTypeCodeCheck",
        new("Senaryo bu fatura tipine izin vermiyor", "The scenario does not allow this invoice type"),
        new("ILAC_TIBBICIHAZ, YATIRIMTESVIK ve IDIS senaryolarının her biri yalnızca belirli fatura tiplerine izin verir.",
            "The ILAC_TIBBICIHAZ, YATIRIMTESVIK and IDIS scenarios each allow only specific invoice types."),
        new("Senaryo '{0}' için fatura tipi '{1}' geçersiz. İzin verilenler: {2}.", "Invoice type '{1}' is not allowed in scenario '{0}'. Allowed: {2}."),
        new("Fatura tipini izin verilenlerden biri yapın ya da doğru senaryoyu seçin.", "Use one of the allowed types or pick the right scenario."));

    public static readonly RuleDescriptor InvestmentEArchiveTypes = new(
        "HDR-009", "header", Severity.Error, RuleOrigin.GibDerived, "GIB UBL-TR_Codelist.xml / YatirimTesvikEArsivInvoiceTypeCodeList",
        new("YTB* fatura tipleri yalnızca e-Arşiv faturasında", "YTB* invoice types are e-Arşiv only"),
        new("YTBSATIS, YTBIADE, YTBISTISNA, YTBTEVKIFAT ve YTBTEVKIFATIADE GİB kod listesinde e-Arşiv yatırım teşvik tipleri olarak tanımlıdır; e-Fatura'da YATIRIMTESVIK senaryosu ve standart tipler kullanılır.",
            "YTBSATIS, YTBIADE, YTBISTISNA, YTBTEVKIFAT and YTBTEVKIFATIADE are listed by GIB as e-Arşiv investment-incentive types; e-Fatura uses the YATIRIMTESVIK scenario with the standard types."),
        new("Fatura tipi '{0}' senaryo '{1}' ile kullanılmış; EARSIVFATURA olmalı.", "Invoice type '{0}' is used with scenario '{1}'; it requires EARSIVFATURA."),
        new("e-Arşiv için senaryoyu EARSIVFATURA yapın; e-Fatura için YATIRIMTESVIK senaryosu ile SATIS/ISTISNA/IADE/TEVKIFAT tiplerini kullanın.",
            "For e-Arşiv set the scenario to EARSIVFATURA; for e-Fatura use scenario YATIRIMTESVIK with SATIS/ISTISNA/IADE/TEVKIFAT."));

    public static readonly RuleDescriptor InvoiceIdFormat = new(
        "HDR-010", "header", Severity.Error, RuleOrigin.GibSchematron, Main + " / InvoiceIDCheck",
        new("Fatura numarası biçimi hatalı", "Invoice number has the wrong format"),
        new("Fatura numarası 16 karakterdir: 3 karakterlik seri öneki (büyük harf/rakam) + 4 haneli yıl (20YY) + 9 haneli sıra numarası, ör. ABC2026000000001.",
            "The invoice number has 16 characters: a 3-character series prefix (upper-case letters/digits) + a 4-digit year (20YY) + a 9-digit sequence, e.g. ABC2026000000001."),
        new("cbc:ID '{0}' 'ABC2026000000001' biçiminde değil.", "cbc:ID '{0}' does not match the 'ABC2026000000001' format."),
        new("Numarayı seri + yıl + 9 haneli sıra olarak yeniden üretin; sıra numarasını soldan sıfırla doldurun (ör. 42 → 000000042).",
            "Regenerate the number as series + year + 9-digit sequence, left-padding the sequence with zeros (e.g. 42 → 000000042)."));

    public static readonly RuleDescriptor InvoiceIdYear = new(
        "HDR-011", "header", Severity.Warning, RuleOrigin.GibDerived, "GIB e-Fatura guide: invoice number = series + year + sequence",
        new("Fatura numarasındaki yıl düzenleme tarihiyle uyuşmuyor", "The year in the invoice number differs from the issue date"),
        new("Numaradaki 4 haneli yıl, faturanın düzenlendiği yılı gösterir; sıra numaraları her yıl yeniden başlar.",
            "The 4-digit year in the number is the year of issue; sequences restart every year."),
        new("cbc:ID yılı {0}, cbc:IssueDate yılı {1}.", "cbc:ID year is {0}, cbc:IssueDate year is {1}."),
        new("Numarayı düzenleme yılının serisinden verin ya da düzenleme tarihini düzeltin.", "Take the number from the issue year's sequence or correct the issue date."));

    public static readonly RuleDescriptor UuidFormat = new(
        "HDR-012", "header", Severity.Error, RuleOrigin.GibSchematron, Main + " / UUIDCheck",
        new("ETTN (UUID) biçimi hatalı", "ETTN (UUID) has the wrong format"),
        new("cbc:UUID faturanın evrensel tekil numarasıdır (ETTN) ve 8-4-4-4-12 onaltılık UUID biçiminde olmalıdır.",
            "cbc:UUID is the invoice's universal unique number (ETTN) and must be an 8-4-4-4-12 hexadecimal UUID."),
        new("cbc:UUID '{0}' UUID biçiminde değil.", "cbc:UUID '{0}' is not a UUID."),
        new("Her fatura için yeni bir UUID üretin (ör. .NET Guid.NewGuid(), PostgreSQL gen_random_uuid()).",
            "Generate a fresh UUID per invoice (e.g. .NET Guid.NewGuid(), PostgreSQL gen_random_uuid())."));

    public static readonly RuleDescriptor CopyIndicator = new(
        "HDR-013", "header", Severity.Error, RuleOrigin.GibSchematron, Main + " / CopyIndicatorCheck",
        new("CopyIndicator false olmalı", "CopyIndicator must be false"),
        new("Gönderilen e-belge asıl nüshadır; kopya işareti kullanılmaz.", "A transmitted e-document is always the original; the copy flag is not used."),
        new("cbc:CopyIndicator değeri '{0}'; 'false' olmalı.", "cbc:CopyIndicator is '{0}'; it must be 'false'."),
        new("<cbc:CopyIndicator>false</cbc:CopyIndicator> yazın.", "Write <cbc:CopyIndicator>false</cbc:CopyIndicator>."));

    public static readonly RuleDescriptor IssueDateRange = new(
        "HDR-014", "header", Severity.Warning, RuleOrigin.GibSchematron, Main + " / TimeCheck (disabled in GIB's main file, kept as a warning)",
        new("Düzenleme tarihi geçerli aralıkta değil", "Issue date is outside the valid range"),
        new("Fatura ileri tarihli olamaz ve 01.01.2005'ten önceye ait olamaz. GİB ana şematronda bu kontrolü kapatmıştır; burada uyarı olarak tutulur.",
            "An invoice cannot be future-dated or older than 2005-01-01. GIB disabled this check in its main Schematron; it is kept here as a warning."),
        new("cbc:IssueDate '{0}' {1}.", "cbc:IssueDate '{0}' {1}."),
        new("Düzenleme tarihini faturanın gerçekten düzenlendiği gün yapın.", "Set the issue date to the day the invoice is actually issued."));

    public static readonly RuleDescriptor LineCount = new(
        "HDR-015", "header", Severity.Error, RuleOrigin.Consistency, "UBL-TR guide: cbc:LineCountNumeric",
        new("Satır sayısı (LineCountNumeric) tutmuyor", "Line count (LineCountNumeric) does not match"),
        new("cbc:LineCountNumeric faturadaki cac:InvoiceLine sayısını bildirir; farklı olması eksik veya fazla satır gönderildiğini gösterir.",
            "cbc:LineCountNumeric declares how many cac:InvoiceLine elements the invoice has; a mismatch means lines were lost or duplicated."),
        new("cbc:LineCountNumeric {0}, gerçek satır sayısı {1}.", "cbc:LineCountNumeric is {0}, actual line count is {1}."),
        new("cbc:LineCountNumeric değerini {1} yapın.", "Set cbc:LineCountNumeric to {1}."));

    public static readonly RuleDescriptor ReturnReference = new(
        "RET-001", "header", Severity.Error, RuleOrigin.GibSchematron, Main + " / IADEInvioceCheck",
        new("İade faturası iade edilen faturaya referans vermeli", "A return invoice must reference the returned invoice"),
        new("IADE, TEVKIFATIADE, YTBIADE ve YTBTEVKIFATIADE faturalarında her cac:BillingReference/cac:InvoiceDocumentReference, DocumentTypeCode 'IADE' ve 16 karakterlik fatura numarası taşımalıdır.",
            "IADE, TEVKIFATIADE, YTBIADE and YTBTEVKIFATIADE invoices need cac:BillingReference/cac:InvoiceDocumentReference entries, each with DocumentTypeCode 'IADE' and a 16-character invoice number."),
        new("Fatura tipi '{0}' için geçerli iade referansı yok veya hatalı.", "Invoice type '{0}' has no valid return reference."),
        new("<cac:BillingReference><cac:InvoiceDocumentReference> içine iade edilen faturanın numarasını (cbc:ID), tarihini ve <cbc:DocumentTypeCode>IADE</cbc:DocumentTypeCode> değerini ekleyin.",
            "Add <cac:BillingReference><cac:InvoiceDocumentReference> with the returned invoice's number (cbc:ID), its date and <cbc:DocumentTypeCode>IADE</cbc:DocumentTypeCode>."));

    // ---- parties ----------------------------------------------------------------------------------------------

    public static readonly RuleDescriptor PartyTaxId = new(
        "PTY-001", "parties", Severity.Error, RuleOrigin.GibSchematron, Main + " / PartyIdentificationPartyNamePersonCheck",
        new("Tarafın tek bir VKN veya TCKN'si olmalı", "The party needs exactly one VKN or TCKN"),
        new("Satıcı (AccountingSupplierParty) ve alıcı (AccountingCustomerParty), schemeID'si VKN veya TCKN olan tam olarak bir cac:PartyIdentification taşımalıdır.",
            "The supplier (AccountingSupplierParty) and the customer (AccountingCustomerParty) must each carry exactly one cac:PartyIdentification with schemeID VKN or TCKN."),
        new("{0}: {1}.", "{0}: {1}."),
        new("Şirketler için <cbc:ID schemeID=\"VKN\">, şahıslar için <cbc:ID schemeID=\"TCKN\"> ekleyin; ikisini birden yazmayın.",
            "Add <cbc:ID schemeID=\"VKN\"> for companies or <cbc:ID schemeID=\"TCKN\"> for individuals, never both."));

    public static readonly RuleDescriptor VknLength = new(
        "PTY-002", "parties", Severity.Error, RuleOrigin.GibSchematron, Main + " / PartyIdentificationTCKNVKNCheck",
        new("VKN 10 haneli olmalı", "VKN must have 10 digits"),
        new("Vergi Kimlik Numarası (VKN) 10 rakamdan oluşur.", "The tax number (VKN) consists of 10 digits."),
        new("{0} VKN değeri '{1}' 10 haneli bir sayı değil.", "{0} VKN '{1}' is not a 10-digit number."),
        new("VKN'yi boşluk ve ayraç olmadan 10 rakam olarak yazın; 11 haneliyse schemeID'yi TCKN yapın.",
            "Write the VKN as 10 digits without spaces or separators; if it has 11 digits, set schemeID to TCKN."));

    public static readonly RuleDescriptor VknChecksum = new(
        "PTY-003", "parties", Severity.Error, RuleOrigin.Consistency, "VKN check digit algorithm",
        new("VKN kontrol hanesi hatalı", "VKN check digit is wrong"),
        new("VKN'nin son hanesi ilk dokuz haneden hesaplanan kontrol hanesidir; uyuşmaması genellikle yazım hatasıdır. GİB şematronu yalnızca uzunluğu denetler.",
            "The last VKN digit is a check digit computed from the first nine; a mismatch is usually a typo. GIB's Schematron only checks the length."),
        new("{0} VKN '{1}' geçersiz: kontrol hanesi {2} olmalı.", "{0} VKN '{1}' is invalid: the check digit should be {2}."),
        new("VKN'yi vergi levhası veya GİB mükellef sorgusundan doğrulayıp yeniden yazın.", "Re-enter the VKN after checking it against the tax certificate or GIB's taxpayer lookup."));

    public static readonly RuleDescriptor TcknLength = new(
        "PTY-004", "parties", Severity.Error, RuleOrigin.GibSchematron, Main + " / PartyIdentificationTCKNVKNCheck",
        new("TCKN 11 haneli olmalı", "TCKN must have 11 digits"),
        new("T.C. Kimlik Numarası 11 rakamdan oluşur ve 0 ile başlamaz.", "The national ID number (TCKN) has 11 digits and does not start with 0."),
        new("{0} TCKN değeri '{1}' 0 ile başlamayan 11 haneli bir sayı değil.", "{0} TCKN '{1}' is not an 11-digit number that does not start with 0."),
        new("TCKN'yi 11 rakam olarak yazın; 10 haneliyse schemeID'yi VKN yapın.", "Write the TCKN as 11 digits; if it has 10 digits, set schemeID to VKN."));

    public static readonly RuleDescriptor TcknChecksum = new(
        "PTY-005", "parties", Severity.Error, RuleOrigin.Consistency, "TCKN check digit algorithm",
        new("TCKN kontrol haneleri hatalı", "TCKN check digits are wrong"),
        new("TCKN'nin son iki hanesi ilk dokuz haneden hesaplanır; uyuşmaması yazım hatası demektir. GİB şematronu yalnızca uzunluğu denetler.",
            "The last two TCKN digits are computed from the first nine; a mismatch means a typo. GIB's Schematron only checks the length."),
        new("{0} TCKN '{1}' geçersiz: son iki hane {2} olmalı.", "{0} TCKN '{1}' is invalid: the last two digits should be {2}."),
        new("TCKN'yi kimlik belgesinden kontrol ederek yeniden yazın.", "Re-enter the TCKN after checking the identity document."));

    public static readonly RuleDescriptor PartyName = new(
        "PTY-006", "parties", Severity.Error, RuleOrigin.GibSchematron, Main + " / PartyIdentificationPartyNamePersonCheck",
        new("VKN'li tarafın unvanı olmalı", "A VKN party needs a name"),
        new("VKN ile tanımlanan taraf bir işletmedir ve boş olmayan cac:PartyName/cbc:Name (unvan) taşımalıdır.",
            "A party identified by VKN is a business and must carry a non-empty cac:PartyName/cbc:Name (trade name)."),
        new("{0} VKN ile tanımlanmış ama cac:PartyName/cbc:Name yok veya boş.", "{0} is identified by VKN but cac:PartyName/cbc:Name is missing or empty."),
        new("<cac:PartyName><cbc:Name>Unvan</cbc:Name></cac:PartyName> ekleyin.", "Add <cac:PartyName><cbc:Name>Trade name</cbc:Name></cac:PartyName>."));

    public static readonly RuleDescriptor PartyPerson = new(
        "PTY-007", "parties", Severity.Error, RuleOrigin.GibSchematron, Main + " / PartyIdentificationPartyNamePersonCheck",
        new("TCKN'li tarafın adı ve soyadı olmalı", "A TCKN party needs a first and family name"),
        new("TCKN ile tanımlanan taraf gerçek kişidir ve cac:Person altında boş olmayan cbc:FirstName ile cbc:FamilyName taşımalıdır.",
            "A party identified by TCKN is a person and must carry non-empty cbc:FirstName and cbc:FamilyName under cac:Person."),
        new("{0} TCKN ile tanımlanmış ama cac:Person/cbc:FirstName veya cbc:FamilyName yok ya da boş.", "{0} is identified by TCKN but cac:Person/cbc:FirstName or cbc:FamilyName is missing or empty."),
        new("<cac:Person><cbc:FirstName>Ad</cbc:FirstName><cbc:FamilyName>Soyad</cbc:FamilyName></cac:Person> ekleyin.",
            "Add <cac:Person><cbc:FirstName>First</cbc:FirstName><cbc:FamilyName>Family</cbc:FamilyName></cac:Person>."));

    public static readonly RuleDescriptor PartySchemeId = new(
        "PTY-008", "parties", Severity.Error, RuleOrigin.GibSchematron, Main + " / PartyIdentificationSchemeIDCheck (PartyIdentificationIDType)",
        new("Geçersiz kimlik türü (schemeID)", "Invalid identifier type (schemeID)"),
        new("cac:PartyIdentification/cbc:ID'nin schemeID niteliği GİB kod listesinden olmalıdır (VKN, TCKN, MERSISNO, TICARETSICILNO, MUSTERINO ...).",
            "The schemeID of cac:PartyIdentification/cbc:ID must come from the GIB code list (VKN, TCKN, MERSISNO, TICARETSICILNO, MUSTERINO ...)."),
        new("{0} kimlik türü '{1}' GİB kod listesinde yok.", "{0} identifier type '{1}' is not in the GIB code list."),
        new("schemeID'yi kod listesindeki bir değerle değiştirin (büyük harf, ör. MERSISNO).", "Use a schemeID from the code list (upper case, e.g. MERSISNO)."));

    // ---- currency ---------------------------------------------------------------------------------------------

    public static readonly RuleDescriptor CurrencyCode = new(
        "CUR-001", "currency", Severity.Error, RuleOrigin.GibSchematron, Main + " / CurrencyCodeCheck (CurrencyCodeList)",
        new("Geçersiz para birimi kodu", "Invalid currency code"),
        new("Para birimi kodları ISO 4217 tabanlı GİB kod listesinden olmalıdır (TRY, USD, EUR ...).",
            "Currency codes must come from GIB's ISO 4217 based code list (TRY, USD, EUR ...)."),
        new("{0} değeri '{1}' geçerli bir para birimi kodu değil.", "{0} value '{1}' is not a valid currency code."),
        new("Üç harfli ISO 4217 kodunu büyük harfle yazın (ör. TRY, EUR). 'TL' veya 'YTL' geçerli değildir.",
            "Write the three-letter ISO 4217 code in upper case (e.g. TRY, EUR). 'TL' or 'YTL' are not valid."));

    public static readonly RuleDescriptor ExchangeRateRequired = new(
        "CUR-002", "currency", Severity.Error, RuleOrigin.GibSchematron, Main + " / CurrencyCodeCheck",
        new("Döviz faturasında kur bilgisi zorunlu", "A foreign-currency invoice needs an exchange rate"),
        new("Para birimi TRY olmayan belgelerde vergi matrahının TL karşılığı için cac:PricingExchangeRate/cbc:CalculationRate zorunludur.",
            "Documents not in TRY must carry cac:PricingExchangeRate/cbc:CalculationRate so the tax base can be converted to TRY."),
        new("Belge para birimi {0} ama cac:PricingExchangeRate/cbc:CalculationRate yok.", "Document currency is {0} but cac:PricingExchangeRate/cbc:CalculationRate is missing."),
        new("<cac:PricingExchangeRate> ekleyin: SourceCurrencyCode {0}, TargetCurrencyCode TRY, CalculationRate fatura tarihindeki kur.",
            "Add <cac:PricingExchangeRate> with SourceCurrencyCode {0}, TargetCurrencyCode TRY and CalculationRate set to the rate on the invoice date."));

    public static readonly RuleDescriptor ExchangeRateFormat = new(
        "CUR-003", "currency", Severity.Error, RuleOrigin.GibSchematron, Main + " / CurrencyCodeCheck",
        new("Kur değeri hatalı", "Exchange rate value is invalid"),
        new("CalculationRate pozitif olmalı; noktadan önce en fazla 15, sonra en fazla 6 hane içerebilir.",
            "CalculationRate must be positive with at most 15 digits before and 6 after the decimal point."),
        new("cbc:CalculationRate '{0}' geçerli değil.", "cbc:CalculationRate '{0}' is not valid."),
        new("Kuru nokta ondalık ayıracıyla ve en fazla 6 ondalıkla yazın (ör. 34.123456).", "Write the rate with a dot decimal separator and at most 6 decimals (e.g. 34.123456)."));

    public static readonly RuleDescriptor ExchangeRateCurrencies = new(
        "CUR-004", "currency", Severity.Error, RuleOrigin.Consistency, "UBL 2.1 PricingExchangeRate semantics",
        new("Kurun para birimleri belgeyle uyuşmuyor", "Exchange rate currencies do not match the document"),
        new("PricingExchangeRate belge para biriminden TRY'ye çevrimi tanımlar: SourceCurrencyCode belge para birimi, TargetCurrencyCode TRY olmalıdır.",
            "PricingExchangeRate defines the conversion from the document currency to TRY: SourceCurrencyCode must be the document currency and TargetCurrencyCode TRY."),
        new("Kaynak '{0}', hedef '{1}'; beklenen kaynak '{2}', hedef 'TRY'.", "Source '{0}', target '{1}'; expected source '{2}' and target 'TRY'."),
        new("SourceCurrencyCode'u {2}, TargetCurrencyCode'u TRY yapın.", "Set SourceCurrencyCode to {2} and TargetCurrencyCode to TRY."));

    public static readonly RuleDescriptor AmountCurrency = new(
        "CUR-005", "currency", Severity.Error, RuleOrigin.Consistency, "UBL 2.1 amount currencyID",
        new("Tutarın para birimi belge para biriminden farklı", "An amount's currency differs from the document currency"),
        new("Faturadaki tüm tutarlar belge para biriminde (DocumentCurrencyCode) ifade edilir; farklı currencyID toplamları anlamsız kılar.",
            "All amounts on the invoice are in the document currency (DocumentCurrencyCode); a different currencyID makes the totals meaningless."),
        new("{0} currencyID '{1}', belge para birimi '{2}'.", "{0} has currencyID '{1}', document currency is '{2}'."),
        new("currencyID niteliğini '{2}' yapın.", "Set the currencyID attribute to '{2}'."));

    // ---- totals -----------------------------------------------------------------------------------------------

    public static readonly RuleDescriptor LineAmount = new(
        "TOT-001", "totals", Severity.Error, RuleOrigin.Consistency, "UBL 2.1 line arithmetic",
        new("Satır tutarı miktar × fiyat ile uyuşmuyor", "Line amount does not equal quantity × price"),
        new("Satır tutarı (LineExtensionAmount) = miktar × birim fiyat − satır iskontoları + satır artırımlarıdır (kuruş yuvarlaması toleransı ile).",
            "The line amount (LineExtensionAmount) is quantity × unit price − line allowances + line charges (within rounding tolerance)."),
        new("Satır {0}: cbc:LineExtensionAmount {1}, hesaplanan {2}.", "Line {0}: cbc:LineExtensionAmount is {1}, computed {2}."),
        new("Satır tutarını {2} yapın ya da miktar, fiyat ve iskonto değerlerini düzeltin.", "Set the line amount to {2} or correct the quantity, price and allowance values."));

    public static readonly RuleDescriptor LineTotal = new(
        "TOT-002", "totals", Severity.Error, RuleOrigin.Consistency, "UBL 2.1 LegalMonetaryTotal",
        new("Satır toplamı LegalMonetaryTotal ile uyuşmuyor", "Sum of lines does not match LegalMonetaryTotal"),
        new("LegalMonetaryTotal/LineExtensionAmount, tüm satırların LineExtensionAmount toplamına eşit olmalıdır.",
            "LegalMonetaryTotal/LineExtensionAmount must equal the sum of all line LineExtensionAmount values."),
        new("LegalMonetaryTotal/cbc:LineExtensionAmount {0}, satırların toplamı {1}.", "LegalMonetaryTotal/cbc:LineExtensionAmount is {0}, the lines add up to {1}."),
        new("Toplamı {1} yapın ya da eksik/fazla satırı düzeltin.", "Set the total to {1} or fix the missing/extra line."));

    public static readonly RuleDescriptor TaxExclusive = new(
        "TOT-003", "totals", Severity.Error, RuleOrigin.Consistency, "UBL 2.1 LegalMonetaryTotal",
        new("Vergi hariç tutar hatalı", "Tax-exclusive amount is wrong"),
        new("Vergi hariç tutar = satır toplamı (LineExtensionAmount) − belge iskontoları (AllowanceTotalAmount) + belge artırımları (ChargeTotalAmount).",
            "TaxExclusiveAmount = LineExtensionAmount − AllowanceTotalAmount + ChargeTotalAmount."),
        new("cbc:TaxExclusiveAmount {0}, hesaplanan {1}.", "cbc:TaxExclusiveAmount is {0}, computed {1}."),
        new("cbc:TaxExclusiveAmount değerini {1} yapın.", "Set cbc:TaxExclusiveAmount to {1}."));

    public static readonly RuleDescriptor TaxInclusive = new(
        "TOT-004", "totals", Severity.Error, RuleOrigin.Consistency, "UBL 2.1 LegalMonetaryTotal",
        new("Vergi dahil tutar hatalı", "Tax-inclusive amount is wrong"),
        new("TaxInclusiveAmount = TaxExclusiveAmount + belge düzeyindeki vergi alt toplamlarının (TaxSubtotal/TaxAmount) toplamı. Tevkifatlı faturada tevkif edilen KDV burada düşülmez.",
            "TaxInclusiveAmount = TaxExclusiveAmount + the document-level tax subtotals (TaxSubtotal/TaxAmount). On a withholding invoice the withheld VAT is not deducted here."),
        new("cbc:TaxInclusiveAmount {0}, hesaplanan {1}.", "cbc:TaxInclusiveAmount is {0}, computed {1}."),
        new("cbc:TaxInclusiveAmount değerini {1} yapın.", "Set cbc:TaxInclusiveAmount to {1}."));

    public static readonly RuleDescriptor Payable = new(
        "TOT-005", "totals", Severity.Error, RuleOrigin.Consistency, "UBL 2.1 LegalMonetaryTotal; GIB TEVKIFAT sample",
        new("Ödenecek tutar hatalı", "Payable amount is wrong"),
        new("PayableAmount = TaxInclusiveAmount − PrepaidAmount + PayableRoundingAmount − tevkif edilen vergi (WithholdingTaxTotal).",
            "PayableAmount = TaxInclusiveAmount − PrepaidAmount + PayableRoundingAmount − withheld tax (WithholdingTaxTotal)."),
        new("cbc:PayableAmount {0}, hesaplanan {1}.", "cbc:PayableAmount is {0}, computed {1}."),
        new("cbc:PayableAmount değerini {1} yapın.", "Set cbc:PayableAmount to {1}."));

    public static readonly RuleDescriptor AmountFormat = new(
        "TOT-006", "totals", Severity.Error, RuleOrigin.GibSchematron, Main + " / decimalCheck",
        new("Tutar biçimi hatalı", "Amount format is invalid"),
        new("Toplam tutarlar noktadan önce en fazla 15, sonra en fazla 2 hane (kuruş) içerebilir.",
            "Total amounts may have at most 15 digits before and 2 digits (kuruş) after the decimal point."),
        new("{0} değeri '{1}' en fazla 2 ondalıklı bir tutar değil.", "{0} value '{1}' is not an amount with at most 2 decimals."),
        new("Tutarı kuruşa yuvarlayın (ör. 1234.567 → 1234.57) ve ondalık ayıracı olarak nokta kullanın.", "Round the amount to kuruş (e.g. 1234.567 → 1234.57) and use a dot as decimal separator."));

    // ---- tax --------------------------------------------------------------------------------------------------

    public static readonly RuleDescriptor TaxTotalSum = new(
        "TAX-001", "tax", Severity.Error, RuleOrigin.Consistency, "UBL 2.1 TaxTotal; GIB TEVKIFAT sample",
        new("Vergi toplamı alt toplamlarla uyuşmuyor", "Tax total does not match its subtotals"),
        new("TaxTotal/TaxAmount, altındaki TaxSubtotal/TaxAmount değerlerinin toplamıdır. Tevkifatlı faturalarda GİB'in kendi örneği net (tevkifat düşülmüş) tutarı kullandığı için o değer de kabul edilir.",
            "TaxTotal/TaxAmount is the sum of its TaxSubtotal/TaxAmount values. On withholding invoices GIB's own sample uses the net amount (withholding deducted), so that value is accepted too."),
        new("cac:TaxTotal/cbc:TaxAmount {0}, alt toplamların toplamı {1}{2}.", "cac:TaxTotal/cbc:TaxAmount is {0}, the subtotals add up to {1}{2}."),
        new("cac:TaxTotal/cbc:TaxAmount değerini {1} yapın.", "Set cac:TaxTotal/cbc:TaxAmount to {1}."));

    public static readonly RuleDescriptor TaxSubtotalRate = new(
        "TAX-002", "tax", Severity.Error, RuleOrigin.Consistency, "UBL 2.1 TaxSubtotal",
        new("Vergi tutarı matrah × oran ile uyuşmuyor", "Tax amount does not equal taxable amount × rate"),
        new("Her TaxSubtotal'da TaxAmount = TaxableAmount × Percent / 100 olmalıdır (kuruş yuvarlaması toleransı ile).",
            "In every TaxSubtotal, TaxAmount = TaxableAmount × Percent / 100 (within rounding tolerance)."),
        new("Matrah {0} × %{1} = {3}, ama cbc:TaxAmount {2}.", "Taxable {0} × {1}% = {3}, but cbc:TaxAmount is {2}."),
        new("Vergi tutarını {3} yapın ya da oranı/matrahı düzeltin.", "Set the tax amount to {3} or correct the rate/taxable amount."));

    public static readonly RuleDescriptor TaxTypeCode = new(
        "TAX-003", "tax", Severity.Error, RuleOrigin.GibSchematron, Main + " / TaxTypeCheck (TaxType)",
        new("Geçersiz vergi türü kodu", "Invalid tax type code"),
        new("TaxScheme/TaxTypeCode GİB vergi kodu listesinden olmalıdır (ör. 0015 KDV, 0071 ÖTV I. liste, 4080 ÖİV).",
            "TaxScheme/TaxTypeCode must come from GIB's tax code list (e.g. 0015 VAT, 0071 SCT list I, 4080 special communication tax)."),
        new("Vergi türü kodu '{0}' GİB listesinde yok.", "Tax type code '{0}' is not in the GIB list."),
        new("Dört haneli GİB vergi kodunu baştaki sıfırlarla yazın (KDV için 0015).", "Write the four-digit GIB tax code with leading zeros (0015 for VAT)."));

    public static readonly RuleDescriptor TaxBreakdown = new(
        "TAX-004", "tax", Severity.Error, RuleOrigin.Consistency, "UBL 2.1 TaxTotal (document vs line breakdown)",
        new("Belge vergi dökümü satırlarla uyuşmuyor", "Document tax breakdown does not match the lines"),
        new("Belge düzeyindeki her (vergi kodu, oran) alt toplamı, satırlardaki aynı vergi ve orandaki alt toplamların toplamına eşit olmalıdır.",
            "Each document-level (tax code, rate) subtotal must equal the sum of the line subtotals with the same tax and rate."),
        new("Vergi {0} %{1}: belge alt toplamı {2}, satır toplamı {3}.", "Tax {0} at {1}%: document subtotal is {2}, the lines add up to {3}."),
        new("Belge alt toplamını {3} yapın ya da satır vergilerini düzeltin.", "Set the document subtotal to {3} or correct the line taxes."));

    public static readonly RuleDescriptor ZeroVatReason = new(
        "TAX-005", "tax", Severity.Error, RuleOrigin.GibSchematron, Main + " / TaxExemptionReasonCheck",
        new("Sıfır KDV için muafiyet sebebi zorunlu", "Zero VAT requires an exemption reason"),
        new("KDV (0015) tutarı 0 olan bir alt toplamda TaxExemptionReason yazılmalıdır (IADE, YTBIADE, IHRACKAYITLI, OZELMATRAH, SGK ve KONAKLAMAVERGISI hariç).",
            "A VAT (0015) subtotal with a zero amount needs a TaxExemptionReason (except for IADE, YTBIADE, IHRACKAYITLI, OZELMATRAH, SGK and KONAKLAMAVERGISI)."),
        new("Fatura tipi '{0}': KDV tutarı 0 ama cbc:TaxExemptionReason boş.", "Invoice type '{0}': VAT amount is 0 but cbc:TaxExemptionReason is empty."),
        new("cac:TaxCategory içine cbc:TaxExemptionReasonCode (ör. 351) ve cbc:TaxExemptionReason açıklamasını ekleyin.",
            "Add cbc:TaxExemptionReasonCode (e.g. 351) and a cbc:TaxExemptionReason text inside cac:TaxCategory."));

    public static readonly RuleDescriptor ExemptionCode = new(
        "TAX-006", "tax", Severity.Error, RuleOrigin.GibSchematron, Main + " / TaxExemptionReasonCodeCheck (TaxExemptionReasonCodeType)",
        new("Geçersiz muafiyet/istisna kodu", "Invalid exemption code"),
        new("TaxExemptionReason yazıldığında TaxExemptionReasonCode GİB kod listesinden olmalıdır; 308 ve 339 yalnızca yatırım teşvik faturalarında geçerlidir.",
            "When TaxExemptionReason is present, TaxExemptionReasonCode must come from the GIB code list; 308 and 339 are only valid on investment-incentive invoices."),
        new("cbc:TaxExemptionReasonCode '{0}' geçerli değil.", "cbc:TaxExemptionReasonCode '{0}' is not valid."),
        new("Kod listesinden faturaya uyan muafiyet kodunu yazın (ör. 301 mal ihracatı, 351 diğer).", "Use the matching exemption code from the code list (e.g. 301 export of goods, 351 other)."));

    public static readonly RuleDescriptor ExemptionType = new(
        "TAX-007", "tax", Severity.Error, RuleOrigin.GibSchematron, Main + " / TaxExemptionReasonCodeCheck",
        new("Muafiyet kodu fatura tipiyle uyumsuz", "Exemption code does not fit the invoice type"),
        new("İstisna kodları ISTISNA, özel matrah kodları (801-812) OZELMATRAH, ihraç kayıtlı kodları (701-704) IHRACKAYITLI tipinde (ve IADE/SGK'da) kullanılır.",
            "Exemption codes belong to type ISTISNA, special-base codes (801-812) to OZELMATRAH and export-registered codes (701-704) to IHRACKAYITLI (plus IADE/SGK)."),
        new("Muafiyet kodu '{0}' fatura tipi '{1}' ile kullanılamaz. İzin verilen tipler: {2}.", "Exemption code '{0}' cannot be used with invoice type '{1}'. Allowed types: {2}."),
        new("Fatura tipini {2} değerlerinden biri yapın ya da doğru muafiyet kodunu seçin.", "Change the invoice type to one of {2} or choose the right exemption code."));

    public static readonly RuleDescriptor WithholdingType = new(
        "TAX-008", "tax", Severity.Error, RuleOrigin.GibSchematron, Main + " / GeneralWithholdingTaxTotalCheck",
        new("Tevkifat yalnızca tevkifatlı fatura tiplerinde", "Withholding is only allowed on withholding invoice types"),
        new("cac:WithholdingTaxTotal yalnızca TEVKIFAT, YTBTEVKIFAT, IADE, YTBIADE, SGK, SARJ ve SARJANLIK faturalarında bulunabilir.",
            "cac:WithholdingTaxTotal may only appear on TEVKIFAT, YTBTEVKIFAT, IADE, YTBIADE, SGK, SARJ and SARJANLIK invoices."),
        new("Fatura tipi '{0}' iken cac:WithholdingTaxTotal kullanılmış.", "cac:WithholdingTaxTotal is used on invoice type '{0}'."),
        new("Tevkifat uygulanıyorsa fatura tipini TEVKIFAT yapın; uygulanmıyorsa cac:WithholdingTaxTotal'ı kaldırın.",
            "If VAT is withheld, set the invoice type to TEVKIFAT; otherwise remove cac:WithholdingTaxTotal."));

    public static readonly RuleDescriptor WithholdingCode = new(
        "TAX-009", "tax", Severity.Error, RuleOrigin.GibSchematron, Main + " / WithholdingTaxTotalCheck (WithholdingTaxType, WithholdingTaxTypeWithPercent)",
        new("Tevkifat kodu veya oranı geçersiz", "Withholding code or rate is invalid"),
        new("Tevkifat alt toplamında kod (601-627, 801-825) GİB listesinden olmalı ve oran o koda tanımlı oranla (ör. 603 için %70) aynı olmalıdır.",
            "A withholding subtotal needs a code from the GIB list (601-627, 801-825) and the rate defined for that code (e.g. 70% for 603)."),
        new("Tevkifat kodu '{0}' ile oran '%{1}' geçerli bir kombinasyon değil.", "Withholding code '{0}' with rate '{1}%' is not a valid combination."),
        new("GİB tevkifat listesindeki kod/oran çiftini kullanın (ör. 603 + 70, 616 + 50).", "Use a code/rate pair from GIB's withholding list (e.g. 603 + 70, 616 + 50)."));

    public static readonly RuleDescriptor Tax4171 = new(
        "TAX-010", "tax", Severity.Error, RuleOrigin.GibSchematron, Main + " / GeneralWithholdingTaxTotalCheck",
        new("4171 vergi kodu bu fatura tipinde kullanılamaz", "Tax code 4171 is not allowed on this invoice type"),
        new("4171 (petrol ve doğalgaz ürünlerine ilişkin ÖTV tevkifatı) yalnızca TEVKIFAT, IADE, SGK ve YTBIADE faturalarında bulunabilir.",
            "Tax code 4171 (withholding on SCT for petroleum and natural gas products) may only appear on TEVKIFAT, IADE, SGK and YTBIADE invoices."),
        new("Fatura tipi '{0}' iken 4171 vergi kodu kullanılmış.", "Tax code 4171 is used on invoice type '{0}'."),
        new("Fatura tipini TEVKIFAT yapın ya da 4171 alt toplamını kaldırın.", "Change the invoice type to TEVKIFAT or remove the 4171 subtotal."));

    /// <summary>All descriptors in catalog order.</summary>
    public static IReadOnlyList<RuleDescriptor> All { get; } =
    [
        MalformedXml, DtdForbidden, NotAnInvoice, SchemaViolation,
        UblVersion, Customization, ProfileId, InvoiceTypeCode, ReturnProfile, EnergyProfile, TechSupportProfile,
        ProfileTypeSet, InvestmentEArchiveTypes, InvoiceIdFormat, InvoiceIdYear, UuidFormat, CopyIndicator,
        IssueDateRange, LineCount, ReturnReference,
        PartyTaxId, VknLength, VknChecksum, TcknLength, TcknChecksum, PartyName, PartyPerson, PartySchemeId,
        CurrencyCode, ExchangeRateRequired, ExchangeRateFormat, ExchangeRateCurrencies, AmountCurrency,
        LineAmount, LineTotal, TaxExclusive, TaxInclusive, Payable, AmountFormat,
        TaxTotalSum, TaxSubtotalRate, TaxTypeCode, TaxBreakdown, ZeroVatReason, ExemptionCode, ExemptionType,
        WithholdingType, WithholdingCode, Tax4171,
    ];

    public static RuleDescriptor? Find(string id) =>
        All.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
}
