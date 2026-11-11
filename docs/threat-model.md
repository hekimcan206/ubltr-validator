# Threat model

ubltr-validator parses untrusted XML: invoices uploaded to the HTTP API, files in a repository checked by the GitHub
Action, files on disk checked by the CLI. It stores nothing and calls no external service at run time. The build
downloads GIB's packages.

## Assets

| Asset | Why it matters |
|---|---|
| Invoice content | Real invoices carry personal data protected by KVKK (Kişisel Verilerin Korunması Kanunu, Law 6698): a private buyer's TCKN, name and address, and commercial terms |
| Availability of the API | A validation gate in an invoicing pipeline; if it hangs, invoices stop |
| Integrity of the verdict | A tampered schema or code list would let invalid invoices pass (or block valid ones) |
| The host and its files | An XML parser that follows entities or URLs can read local files or reach internal hosts |
| CI secrets of repositories that use the Action | Composite actions run with the workflow's token and environment |

## Actors

- A client of the HTTP API sending crafted XML (malicious or just broken).
- A contributor opening a pull request with crafted XML files or crafted action inputs.
- A network attacker between the build machine and ebelge.gib.gov.tr.
- An operator who reads logs and traces.

## Threats and mitigations (STRIDE)

| # | Threat | Category | Mitigation | Verified by |
|---|---|---|---|---|
| T1 | XXE: an entity that reads `/etc/passwd` or calls an internal URL | Information disclosure | `DtdProcessing.Prohibit`, `XmlResolver = null` on every document reader; a DOCTYPE is reported as `XML-002` and parsing stops | `External_entity_pointing_at_a_local_file_is_rejected` |
| T2 | Billion laughs / quadratic blow-up via entities | Denial of service | Same: DTDs are never processed for documents | `Billion_laughs_is_rejected_before_any_expansion` |
| T3 | The document picks its own lenient or remote schema through `xsi:schemaLocation` | Tampering / SSRF | `ProcessSchemaLocation` is off; the schema set is fixed at startup | `Schema_location_in_the_document_is_ignored` |
| T4 | A tampered schema file pulls in remote imports | Tampering / SSRF | Schema imports go through `DirectoryBoundResolver`: local files under the schema folder only. The schemas' own internal DTD subset is parsed with entity expansion capped at 64 K characters | Code review; `DirectoryBoundResolver` |
| T5 | Man-in-the-middle or a replaced file on ebelge.gib.gov.tr | Tampering | HTTPS download, SHA-256 pinned per file, build fails on mismatch ([gib-package.md](gib-package.md)) | Build |
| T6 | Huge request bodies exhaust memory | Denial of service | 20 MB default body limit (`Validation:MaxDocumentBytes`, enforced on the stream, not only on `Content-Length`), 413 problem response | `Oversized_body_is_413_problem` |
| T7 | Many concurrent large validations exhaust CPU and memory | Denial of service | Concurrency limiter: processor-count permits, bounded queue (64), then 503. Memory per validation is measured in [benchmarks.md](benchmarks.md) | `Concurrent_requests_all_get_correct_answers` |
| T8 | A document with errors on every one of 50 000 lines produces a gigantic report | Denial of service | Findings capped per rule (100) and XSD findings per document (50); the report states how many were dropped | `A_systematic_error_is_capped_per_rule_and_the_drop_is_reported` |
| T9 | Regular expression denial of service | Denial of service | All patterns are anchored, have no nested quantifiers and run on short single values (IDs, UUIDs, amounts) | Code review |
| T10 | Script injection through action inputs (`paths: "x; curl evil"`) | Elevation of privilege | Inputs are passed to the script only as environment variables and expanded inside quotes; no `${{ inputs.* }}` in `run:` | Code review; `action-selftest` CI job |
| T11 | Invoice data leaking into logs, traces or metrics | Information disclosure | The API logs request metadata only (method, path, status, duration); traces carry validity, error count and size, never content | Code review |
| T12 | Invoice values leaking into CI annotations | Information disclosure | Messages quote the offending value (a VKN, an ID, an amount) because that is what makes them actionable. Annotations and step summaries are visible to anyone who can read the workflow run, so use the Action on synthetic or test data in public repositories | Documented in README |
| T13 | Verdict differs from GIB's | Tampering (integrity of the result) | Rules name the GIB assertion they re-implement; code lists come from GIB's file; GIB's samples pinned by a test; ADR 0001 states the scope | `GibPackageTests` |

## Out of scope

- Authentication and authorisation of the API. It is a stateless validation function; put it behind your gateway.
- XAdES signature verification. Real e-Fatura documents are signed; this tool does not verify signatures or
  certificates (README, Limitations).
- Rate limiting per client identity (the limiter is global, protecting the process rather than a fair share).
