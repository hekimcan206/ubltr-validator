# 0005: Stable rule ids, bilingual messages as data, one golden sample per rule

- Status: accepted
- Date: 2026-11-11

## Context

The people who fix e-Fatura errors are accountants and integration developers, mostly Turkish speakers; the code and
CI logs are also read by developers who do not read Turkish. A finding is only useful if it says what is wrong with
*this* document (values, location), why the rule exists, and what to change. Findings also become CI annotations and
suppressions (`--disable PTY-003`), so their identity must not change between releases.

## Options considered

1. **.resx resources per language.** Standard localisation, but the message, explanation and fix of one rule live in
   different files from the rule and from each other, and missing translations show up only at run time.
2. **Messages inline in each rule's code.** Easy to write, hard to review as a set, impossible to list.
3. **A rule catalog: one descriptor per rule with id, category, severity, origin, GIB source and Turkish + English
   title, explanation, message template and fix template** (chosen).

## Decision

- `RuleCatalog` holds all 49 descriptors. Findings carry the descriptor, the node and arguments; rendering to a
  language happens at report time (`ValidationResult.ToReport(language)`), so one validation can be rendered in both.
  Arguments may themselves be bilingual (`LocalizedText`), e.g. "Satıcı" / "Supplier".
- XSD errors from System.Xml (English, full of namespace URNs) are translated by pattern into short Turkish and English
  messages with prefixes (`cbc:PayableAmount`) and a concrete fix; unknown shapes fall back to the original text.
- Ids are `AAA-NNN` and never reused. Tests require every rule to have non-empty texts in both languages, a Turkish
  title different from the English one, and templates that format.
- Every rule has one committed invalid sample (`samples/invoices/invalid/<id>.xml`), built by changing one thing in a
  valid synthetic invoice. The golden test requires that sample to produce **exactly** that rule id and nothing else,
  and compares the rendered findings (location, both languages) with `samples/invoices/expected/<id>.json`.

## Consequences

- Adding a rule means: descriptor, check, mutation, regenerated sample and golden file. The coverage test fails until
  all of them exist.
- "Exactly one rule per mutation" doubles as a design check: a mutation that trips two rules exposes overlapping rules
  or an imprecise mutation.
- Turkish is the default language (`tr`); English is one flag away (`--lang en`, `?lang=en`, `Accept-Language: en`).
