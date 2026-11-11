# 0004: Arithmetic rules use kuruş-level tolerances and follow GIB's own conventions

- Status: accepted
- Date: 2026-11-11

## Context

The consistency rules (TOT-001..006, TAX-001..004) compare declared amounts with recomputed ones. Two things make a
naive equality check wrong:

1. **Rounding.** UBL-TR amounts have two decimals, prices often four or more. A line amount is `round(quantity x price)`;
   a document VAT subtotal can be computed either as the sum of rounded line taxes or as one rounding of
   `taxable total x rate`. Both are legitimate and differ by up to half a kuruş (0.005 TRY) per line.
2. **GIB conventions that look like errors.** Running GIB's own sample invoices (UBL-TR 1.2.1 package) through the
   first version of the rules flagged two patterns that are deliberate:
   - `YTB_Istisna_*.xml`: a VAT subtotal of 300.00 inside a `TaxTotal` whose `TaxAmount` is 0, with
     `CalculationSequenceNumeric` = -1 and a comment saying the amount is not added to the invoice total.
     Investment-incentive exemptions show the computed VAT without charging it.
   - `TEVKIFAT.xml` and `YTB_Tevkifat_*.xml`: `TaxTotal/TaxAmount` holds the VAT *net of withholding*
     (360 = 3 600 - 3 240), while `TaxInclusiveAmount` includes the full VAT and `PayableAmount` deducts the withheld part.

## Options considered

1. **Exact equality.** Rejects correct invoices that round the other legitimate way.
2. **A fixed generous tolerance (e.g. 1 TRY).** Hides real one-kuruş errors on small invoices and still fails large
   ones.
3. **One kuruş per rounded amount, half a kuruş per contributing line for aggregates, GIB conventions encoded
   explicitly** (chosen).

## Decision

- A single recomputed amount (a line amount, one tax subtotal, a total built from already-rounded parts) may differ by
  0.01 (`ValidationOptions.RoundingTolerance`).
- An amount aggregated from *n* individually rounded parts (document VAT subtotal against its base x rate, document
  breakdown against the lines) may differ by `max(0.01, 0.005 x n)`. A property test rewrites the document VAT as one
  rounding of the aggregate on random invoices of 1-400 lines and requires zero findings.
- Subtotals with a negative `CalculationSequenceNumeric` are excluded from every sum (TAX-001, TAX-004, TOT-004) but
  still checked for base x rate.
- On invoices with `WithholdingTaxTotal`, `TaxTotal/TaxAmount` may equal either the gross subtotal sum or the sum net of
  withholding (document and line level). `TaxInclusiveAmount` = `TaxExclusiveAmount` + charged subtotals;
  `PayableAmount` = `TaxInclusiveAmount` - prepaid + rounding - withheld tax.
- Line amount = `quantity x price / BaseQuantity` - line allowances + line charges.
- Per-unit taxes (`PerUnitAmount`, e.g. some ÖTV lists) are excluded from the base x rate check.
- Withholding code and rate are matched on the raw text, exactly as GIB's Schematron concatenates them (`603` + `70`),
  so `70.00` is rejected just as GIB would reject it.

## Consequences

- With these conventions, the remaining findings on GIB's samples are real inconsistencies in the demo files (for
  example `OTV.xml`: 6 391.67 + 3 324.79 = 9 716.46, declared 9 718.48). They are listed and pinned by a test in
  `docs/gib-samples.md`.
- The tolerance can be tightened per call (`RoundingTolerance`); a tool that wants bit-exact totals sets it to 0.
