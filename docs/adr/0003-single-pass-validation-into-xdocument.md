# 0003: Validate in one pass: the XSD-validating reader feeds an XDocument the rules query

- Status: accepted
- Date: 2026-11-11

## Context

Each document needs three things: well-formedness and security checks, XSD validation against GIB's schema set, and
the business rules, which look across the whole document (lines against totals, line tax breakdowns against the
document breakdown). Invoices range from one line to tens of thousands (energy, telecom and marketplace settlements).
Every finding must carry a line and column, because the GitHub Action turns findings into inline annotations.

## Options considered

1. **Two passes:** an `XmlReader` with schema validation, then parse again for the rules. Simple, but every byte is
   parsed twice.
2. **Streaming rules on `XmlReader`, no tree.** Lowest memory, but cross-document rules need their own aggregation
   state (per-line accumulators, totals that arrive before the lines in UBL order), which turns 49 readable rules into
   a hand-written state machine.
3. **`XPathDocument` + XPath rules.** Read-only and compact, but rules become strings, errors move to run time, and
   UBL-TR's namespaces make every expression long.
4. **One `XmlReader` with `ValidationType.Schema` feeding `XDocument.Load(reader, LoadOptions.SetLineInfo)`**
   (chosen). The schema validator sees every node as the tree is built; the rules then run over the tree with LINQ.

## Decision

- `InvoiceValidator.Validate(Stream)` creates one reader: DTDs prohibited, no resolver, comments and processing
  instructions skipped, `xsi:schemaLocation` ignored (`ProcessSchemaLocation` off), schema errors collected with
  their line and column and capped at 50 per document (later ones are cascades of the first).
- The compiled `XmlSchemaSet` is built once per process and shared; the validator is safe to call concurrently.
- Line info is kept on the tree, and a finding stores the offending node, so `line`, `column` and an XPath-like path
  (`/Invoice/cac:InvoiceLine[3]/cbc:LineExtensionAmount`) are computed only for findings.
- Findings are capped per rule (default 100) and the report says how many were dropped, so a systematic error on
  50 000 lines produces a readable report.

## Consequences

- Memory grows linearly with document size: the tree is held for the duration of one validation. The cost is measured
  in `docs/benchmarks.md` (allocations per document and peak working set for 1 to 50 000 lines). The HTTP API caps the
  body at 20 MB by default (`Validation:MaxDocumentBytes`) and limits concurrent validations to the processor count,
  so worst-case memory is bounded.
- The HTTP API buffers the body before validating: Kestrel forbids synchronous body reads, and `XDocument.Load` over a
  validating reader is synchronous. The buffer is bounded by the same limit.
- The parse-only / xsd-only / rules-only / full breakdown in `docs/benchmarks.md` shows where the time goes, so the
  cost of the tree is visible rather than assumed.
