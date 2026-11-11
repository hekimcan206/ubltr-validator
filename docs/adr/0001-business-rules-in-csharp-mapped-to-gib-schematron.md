# 0001: Implement the UBL-TR business rules in C#, each mapped to a GIB Schematron rule

- Status: accepted
- Date: 2026-11-11

## Context

GIB (Gelir İdaresi Başkanlığı, the Turkish Revenue Administration) publishes the business rules for e-Fatura and
e-Arşiv as ISO Schematron in its e-Fatura package: `UBL-TR_Main_Schematron.xml`, `UBL-TR_Common_Schematron.xml` (133
abstract rules) and `UBL-TR_Codelist.xml` (the code lists as Schematron variables). The version pinned here carries
history entry `20260701` ([ebelge.gib.gov.tr, e-Fatura paketi](https://ebelge.gib.gov.tr/efaturamevzuat.html)).

Three facts shape the choice:

1. The Schematron uses XPath 2.0: `matches()` for the invoice number and UUID, `xs:date()` and `current-date()` for
   the issue date, `exists()`. .NET's built-in XSLT engine (`XslCompiledTransform`) is XSLT 1.0 / XPath 1.0 only.
   Running the official file therefore needs an XSLT 2.0 processor: Saxon-HE (Java), or SaxonCS (.NET, commercial).
2. Its messages are Turkish only, terse ("Geçersiz cbc:ID elemanı değeri"), and never say how to fix the problem. This
   project's purpose is the explanation and the fix, in Turkish and English.
3. It checks codes and structure, never arithmetic. A total that does not match its lines, the most common reason a
   buyer rejects an invoice, passes GIB's Schematron.

## Options considered

1. **Run the official Schematron with Saxon-HE on a JVM.** Exact parity with GIB, but the tool becomes a .NET + Java
   hybrid (JVM in the image and the GitHub Action, two runtimes to patch), messages stay Turkish-only and fix-free, and
   arithmetic checks would still have to be written separately.
2. **SaxonCS.** Same parity inside .NET, but a commercial licence for an MIT-licensed portfolio tool.
3. **C# rules, one per assertion that matters, each naming the GIB abstract rule it re-implements** (chosen). Plain
   unit-testable code, bilingual messages with fixes, and room for checks GIB does not make.

## Decision

- Rules live in five check classes (header, parties, currency, totals, tax) over one parsed `XDocument`. Every rule has
  a stable id (`HDR-010`), a severity, and an **origin** that tells a reader how much to trust it:
  `GibSchematron` (re-implements a named assertion, e.g. `InvoiceIDCheck`), `GibDerived` (follows from a GIB code list
  or guide that the Schematron does not assert), `Consistency` (arithmetic and check digits that GIB never checks) and
  `Structure` (XML, security, XSD). `ubltr rules` and `GET /api/v1/rules` list them with their source.
- Code lists are not hard-coded into rules. They are read from the fetched `UBL-TR_Codelist.xml` by the names GIB uses
  (`ProfileIDType`, `InvoiceTypeCodeList`, `WithholdingTaxTypeWithPercent`, ...). A built-in snapshot is the fallback
  when the file is absent, and a test fails if the snapshot and the fetched file disagree.
- Where GIB disabled a check in its main file (the issue-date `TimeCheck`), the rule is kept as a warning, not an error.
- Where GIB evaluates a rule only for e-Fatura (its `type` parameter), `--kind`/`?kind=` exposes the same switch.

## Consequences

- Scope is deliberate, not complete: 49 rules cover the invoice header, parties, currency, totals and tax. GIB's file
  also covers envelopes, application responses, despatch advices, signatures, HKS/ILAC/IDIS/ENERJI specifics and many
  per-scenario details that are not re-implemented (README, Limitations).
- Parity with the official Schematron is checked by hand, by the rule-to-source mapping and by running GIB's 28 sample
  invoices (`docs/gib-samples.md`), not by executing the Schematron. A JVM-based parity job is the next step if
  exactness matters more than footprint.
- When GIB publishes a new package, the pin changes (ADR 0002), the code list drift test points at changed lists, and
  `History.txt` in the package names the changed abstract rules to review.
