# What the validator says about GIB's own sample invoices

The UBL-TR 1.2.1 package contains 43 sample documents; 28 of them are invoices (the others are despatch advices,
application responses and receipt advices, which this validator reports as `DOC-001`). They are demos, not test
vectors, and running them is the best available check that the rules agree with how GIB itself writes UBL-TR.

`GibPackageTests.Gib_official_samples_produce_the_documented_findings` runs them on every build and pins the outcome
below. The files are read from the fetched package in `.gib/`; none is copied into this repository.

Run it yourself after a build:

```bash
dotnet src/UblTr.Cli/bin/Debug/net10.0/ubltr.dll validate .gib/ubltr-*/UBLTR_1.2.1_Paketi/xml --today 2026-11-11
```

## Summary (package pinned on 2026-11-11)

| Outcome | Invoices |
|---|---|
| No findings at all | 1 (`HASTANE.xml`) |
| XSD-valid | 26 of 28 (the two HKS samples have an empty `ext:ExtensionContent`, which GIB's own XSD rejects) |
| Only identifier findings (placeholder VKN/TCKN that fail the check digit or length) | 16 |
| XSD error plus identifier findings | 1 (`HKS-Ornek1.xml`) |
| Other errors, each checked by hand | 10 |

GIB's demos use placeholder tax numbers such as `3333333888`, `9205121120` and `1111111157`, so `PTY-003` (VKN check digit) fires on
almost every sample. That is expected: GIB's Schematron checks only the length of a VKN, while this validator also
checks the check digit (the algorithm is verified against two real published institution VKNs, see
`TaxIdentifierTests`). Use `--disable PTY-003` for test fixtures that use placeholders.

## The 10 samples with other errors

| Sample | Finding | What is in the file |
|---|---|---|
| `TemelFaturaOrnegi.xml` | HDR-010 | ID `GIB20090000000001` has 17 characters, not 16 |
| `TemelFaturaOrnegi.xml` | TAX-004 | The document breakdown declares the 2.73 VAT at 0 %, the lines carry it at 18 % |
| `TicariFaturaOrnegi.xml` | TOT-002 | `LineExtensionAmount` 26 003.4, the lines add up to 25 216.5 |
| `ISTISNA-1.xml` | CUR-002, TOT-003 | USD invoice without an exchange rate; `TaxExclusiveAmount` 26 475 vs 22 425 computed |
| `ISTISNA-2.xml` | CUR-002, TOT-001, TOT-002 | USD without exchange rate; line 1 amount 264 000 vs 254 000 = quantity x price |
| `IadeFaturasiOrnegi.xml` | HDR-005, RET-001 | An IADE invoice under TICARIFATURA (GIB's current rule forbids it) without a 16-character return reference; the file dates from 2014 and does not follow the current rules |
| `OTV.xml` | TOT-004 | 6 391.67 + 3 324.79 = 9 716.46, declared 9 718.48 |
| `OZELMATRAH.xml` | TOT-004 | `TaxInclusiveAmount` 24.94, computed 28.95 |
| `HKS-Ornek2.xml` | TOT-003, TOT-005 | `TaxExclusiveAmount` 1 000 000 vs 1 000 220 computed; payable follows |
| `YTB_TevkifatIade_EArşiv.xml`, `YTB_TevkifatIade_Efatura.xml` | TAX-002 | 2.25 x 10 % = 0.23, declared 0.90 |

## Conventions learned from the samples

Two patterns looked like errors in the first run and are deliberate GIB conventions; the rules now follow them
([ADR 0004](adr/0004-rounding-tolerance-and-gib-conventions.md)):

- `CalculationSequenceNumeric` = -1 marks a tax subtotal that is shown but not added to the total
  (`YTB_Istisna_*.xml`).
- On withholding invoices `TaxTotal/TaxAmount` may be net of the withheld VAT (`TEVKIFAT.xml`, `YTB_Tevkifat_*.xml`).
