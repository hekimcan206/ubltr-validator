# GIB packages: sources, versions and how the build fetches them

The validator depends on two files that GIB (Gelir İdaresi Başkanlığı, the Turkish Revenue Administration) publishes
on its e-Belge portal. They are **not committed** to this repository (no licence grants redistribution, see
[ADR 0002](adr/0002-fetch-gib-packages-at-build-time.md)); the build downloads and verifies them.

## Pinned versions

| Package | URL | What it contains | Version evidence | SHA-256 |
|---|---|---|---|---|
| UBL-TR 1.2.1 package | `https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/UBL-TR1.2.1_Paketi.zip` | `xsdrt/` (UBL 2.1 XSDs as profiled by GIB), `xml/` (GIB's sample documents) | `Last-Modified: Mon, 12 Jan 2026 08:48:25 GMT`; newest sample files dated 2026-01-12 | `CB583941B8A8A239C59902C6BC455C0F75D48F2BB81D7D3FBE1AE827F981F7DB` |
| e-Fatura package | `https://ebelge.gib.gov.tr/dosyalar/kilavuzlar/e-FaturaPaketi (29).zip` | `schematron/UBL-TR_Main_Schematron.xml`, `UBL-TR_Common_Schematron.xml`, `UBL-TR_Codelist.xml`, `History.txt` | `Last-Modified: Mon, 24 Aug 2026 14:05:42 GMT`; last `History.txt` entry `20260701`; Schematron files dated 2026-08-21 | `12901476CD424FD65C779A4FB44CEE798F9921CE55B003F8A076C936DDF8B583` |

Both are linked from GIB's e-Fatura documentation page (`https://ebelge.gib.gov.tr/efaturamevzuat.html`, checked
2026-11-11), which also links `UBLTR_1.2.1_Kilavuzlar.zip` (the PDF guides; not used by the build).

What is used from them:

- The XSD set in `xsdrt/common` and `xsdrt/maindoc/UBL-Invoice-2.1.xsd`: XSD validation (`XSD-001`).
- `UBL-TR_Codelist.xml`: every code list the rules use, read by the names GIB gives them (`ProfileIDType`,
  `InvoiceTypeCodeList`, `TaxType`, `WithholdingTaxTypeWithPercent`, `CurrencyCodeList`, ...).
- `UBL-TR_Common_Schematron.xml` / `UBL-TR_Main_Schematron.xml`: read by people, not by the code. Each C# rule names the
  abstract rule it re-implements ([ADR 0001](adr/0001-business-rules-in-csharp-mapped-to-gib-schematron.md)).
- `xml/*.xml`: GIB's own samples, run by a test ([gib-samples.md](gib-samples.md)).

Secondary sources consulted to date the packages (GIB announcements are the authority; these only helped to find them):
PwC Türkiye e-Dönüşüm Bülteni 2026/03 ("UBL-TR 1.2.1 paketi güncellenmiştir", announcement of 12.01.2026), KPMG
"UBL-TR (Kod Listeleri) Kılavuzu ve e-Fatura Paketi Güncellenmiştir", and VATupdate (29 July 2026) on the e-Fatura,
e-Arşiv and code-list package updates effective 14 September 2026.

## How the build fetches them

`build/GibPackages.targets`, imported by `src/UblTr.Validator`, runs before the first build of the validator:

1. `curl -fsSL --retry 6 -A "ubltr-validator-build/1.0" -o .gib/downloads/<file> <url>` (a `User-Agent` is required:
   the server stalls requests without one).
2. `GetFileHash` (SHA-256) and fail the build on a mismatch.
3. `Unzip` only the needed members into `.gib/ubltr-<hash prefix>/` and `.gib/efatura-<hash prefix>/`, then write a
   `.verified` marker so later builds skip the network.
4. Link `xsdrt/**.xsd` and `UBL-TR_Codelist.xml` into the output folder as `gib/...`. Every project that references the
   validator (CLI, API, tests, benchmarks) gets the same files.

Knobs:

| Setting | Effect |
|---|---|
| `-p:UblTrFetchGib=false` | No network. The CLI then needs `--gib-dir`, `UBLTR_GIB_DIR` or `--no-schema` |
| `-p:GibCacheDir=<dir>` | Download/extract location (default `<repo>/.gib/`) |
| `UBLTR_GIB_DIR=<dir>` / `--gib-dir <dir>` | Run time: a folder laid out as `xsdrt/...` and `schematron/...` |

## Offline or air-gapped machines

Download the two zips on a connected machine, check the hashes above, and either copy `.gib/` into the repository
before building, or extract them and point `UBLTR_GIB_DIR` at a folder with this layout:

```
<dir>/xsdrt/common/*.xsd
<dir>/xsdrt/maindoc/UBL-Invoice-2.1.xsd
<dir>/schematron/UBL-TR_Codelist.xml
```

## When GIB publishes a new package

GIB replaces files in place and sometimes renames them (`e-FaturaPaketi.zip` became `e-FaturaPaketi (29).zip` in
August 2026). A changed file fails the build with a hash mismatch. To move to the new version:

1. Download the new file and read `schematron/History.txt` for the changed abstract rules and code lists.
2. Update the URL and SHA-256 in `build/GibPackages.targets`.
3. Run the tests. `Builtin_snapshot_matches_the_fetched_gib_code_lists` names any changed code list; update
   `CodeListSnapshot.cs` from the new `UBL-TR_Codelist.xml`. Review the C# rules whose GIB source changed.
4. Note the change in `CHANGELOG.md`.

## Docker images

Images built from this repository contain the fetched XSDs and code list. Build them yourself and keep them in a
private registry; the CI pipeline builds the image to test it and never pushes it.
