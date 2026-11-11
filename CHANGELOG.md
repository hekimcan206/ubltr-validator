# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.0] - 2026-11-11

### Added

- Validator for UBL-TR 1.2.1 invoices (e-Fatura and e-Arşiv) in one parse: well-formedness, security (DTD/XXE refused,
  `xsi:schemaLocation` ignored), XSD validation against GIB's UBL-TR XSD set, and 49 business rules with stable ids.
- Rules mapped to GIB's Schematron (`UBL-TR_Common_Schematron.xml`, history `20260701`): UBLVersionID,
  CustomizationID, ProfileID per regime, InvoiceTypeCode, ProfileID x InvoiceTypeCode combinations (IADE, ENERJI/SARJ,
  TEKNOLOJIDESTEK, ILAC_TIBBICIHAZ/YATIRIMTESVIK/IDIS), invoice number and ETTN formats, CopyIndicator, issue date,
  return references, VKN/TCKN party identification, PartyName/Person, identifier schemes, currency codes, exchange
  rate presence and format, amount formats, tax type codes, zero-VAT exemption reasons, exemption codes per invoice
  type, withholding types and code/rate pairs, tax code 4171.
- Consistency rules GIB's Schematron does not have: VKN and TCKN check digits, ID year vs issue date, LineCountNumeric,
  exchange rate currencies, amount currencyIDs, line amount = quantity x price - allowances + charges, lines vs
  LegalMonetaryTotal, the totals chain to PayableAmount (withholding deducted), tax subtotal = base x rate, TaxTotal
  vs its subtotals, document tax breakdown vs line breakdowns. Kuruş-level tolerances; GIB conventions for
  non-charged subtotals (`CalculationSequenceNumeric` -1) and net-of-withholding tax totals.
- Every finding in Turkish (default) or English: title, specific message with values, explanation and fix, plus line,
  column and path. XSD errors from System.Xml are translated too.
- Code lists read from GIB's `UBL-TR_Codelist.xml` by GIB's own names, with a built-in snapshot as fallback and a
  drift test that keeps the two equal.
- Build-time download of GIB's UBL-TR 1.2.1 and e-Fatura packages with pinned SHA-256 hashes (not redistributed).
- `ubltr` CLI: files, directories and globs, parallel validation, text/JSON/GitHub annotation output, step summary,
  `--kind`, `--disable`, `--fail-on`, `--output-json`, `rules` and `explain` commands.
- HTTP API: `POST /api/v1/validate` (raw XML), rules endpoints, Turkish/English via `?lang=` or `Accept-Language`,
  20 MB body limit, concurrency limiter, OpenAPI + Scalar, health probes, Serilog JSON logs, OpenTelemetry traces and
  metrics.
- Composite GitHub Action (`action.yml`) that builds the CLI from source on the runner and annotates findings, with a
  sample consumer workflow and a self-test job in CI.
- Synthetic invoice generator (Bogus Turkish locale, checksum-valid random VKN/TCKN): 8 valid scenarios and one
  invalid golden sample per rule, each tripping exactly its rule.
- Tests: golden (samples, findings in both languages), FsCheck properties (no false positives on random invoices, any
  line amount change caught, aggregate rounding accepted, TCKN catches every single-digit substitution), security,
  XSD message translation, CLI, API, and GIB's own sample invoices with pinned findings.
- Throughput and memory benchmarks (harness + BenchmarkDotNet), Docker image (chiseled, non-root) and
  `docker compose up` demo.
