# 0002: Fetch the GIB packages at build time, hash-pinned, instead of committing them

- Status: accepted
- Date: 2026-11-11

## Context

Validation needs GIB's UBL-TR 1.2.1 XSDs (the UBL 2.1 schemas as profiled by GIB: `xsdrt/`) and the code lists. Both
are published as zip files on ebelge.gib.gov.tr. The packages contain no licence file and no statement that permits
redistribution; the site is a public authority's download page, not an open-source release. The OASIS UBL 2.1
schemas themselves are redistributable, but GIB's copies are modified (for example `UBL-CommonAggregateComponents-2.1.xsd`
makes `cac:PostalAddress` mandatory in a party and drops elements UBL-TR does not use), so the OASIS originals would
validate a different language.

The brief for this project is explicit: when redistribution is not clearly allowed, fetch at build time and document
how, do not vendor.

## Options considered

1. **Commit the files.** Simplest, works offline, but redistributes material without a licence to do so.
2. **Use the OASIS UBL 2.1 schemas (redistributable) instead.** Legal, but wrong: invoices GIB rejects would pass.
3. **Download at build time, verify a pinned SHA-256, copy next to the binaries** (chosen).

## Decision

- `build/GibPackages.targets` (imported by the validator project) downloads two files once into `.gib/` (git-ignored):
  `UBL-TR1.2.1_Paketi.zip` (XSDs and GIB's sample documents, `Last-Modified` 2026-01-12) and
  `e-FaturaPaketi (29).zip` (Schematron and code lists, Schematron history `20260701`, `Last-Modified` 2026-08-24).
  Each is checked against a pinned SHA-256. A mismatch fails the build with a message that says GIB replaced the file.
- The download uses `curl`, not MSBuild's `DownloadFile` task. ebelge.gib.gov.tr stalls requests without a
  `User-Agent` header (measured on 2026-11-11: both requests without one hit a 40 s timeout; with one, 4 of 5
  requests finished in 1.9-12.6 s and one also timed out, so retries are needed either way), and `DownloadFile`
  cannot set headers. The target retries six times. `curl` ships with Windows 10+, the .NET SDK images and GitHub runners.
- The extracted XSDs and the code list are linked into the build output under `gib/`, so they flow to every project
  that references the validator (CLI, API, tests, benchmarks) without hard-coded paths. At run time the location can
  be overridden with `UBLTR_GIB_DIR` or `--gib-dir`, for air-gapped machines that receive the package another way.
- CI and the GitHub Action cache `.gib/` keyed on the targets file, so the GIB server is hit only when the pin changes.

## Consequences

- The first build needs network access to ebelge.gib.gov.tr. `-p:UblTrFetchGib=false` builds without it; the CLI then
  refuses to run XSD validation (`--no-schema` runs the rules alone with the built-in code list snapshot).
- GIB updates packages in place and sometimes renames them (`e-FaturaPaketi.zip` became `e-FaturaPaketi (29).zip`).
  A changed file breaks the build on purpose: someone must read GIB's `History.txt`, update the pin and the rules.
- Docker images built from this repository contain GIB's files. They are for private use; the README and the
  Dockerfile say so, and the CI pipeline never pushes an image.
- The repository's samples are synthetic and generated (ADR 0005); GIB's own samples are only read from `.gib/` by a
  test, never copied.
