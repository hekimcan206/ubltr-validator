# Contributing

Thanks for taking the time to improve ubltr-validator.

## Prerequisites

- .NET SDK 10.0.400 or later (`global.json` rolls forward to the latest feature band)
- Network access to `ebelge.gib.gov.tr` for the first build: the GIB packages are downloaded, hash-checked and cached
  in `.gib/` ([docs/gib-package.md](docs/gib-package.md)). `curl` must be on `PATH` (it is on Windows 10+, macOS,
  the .NET SDK images and GitHub runners).
- Docker only for `docker compose up`; the test suite needs no containers.

## Workflow

1. Open an issue first for anything larger than a small fix, so we can agree on the approach.
2. Branch from `main` and keep each pull request to one concern.
3. Before pushing:

   ```bash
   dotnet build                 # must be warning-free: warnings are errors
   dotnet test                  # unit, property, golden, CLI and API tests
   dotnet format --verify-no-changes
   ```

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/) are required. They keep the history readable and make the
hand-written CHANGELOG easy to assemble at release time:

- `feat:` new capability (minor bump)
- `fix:` bug fix (patch bump)
- `feat!:` or a `BREAKING CHANGE:` footer (major bump)
- `docs:`, `test:`, `build:`, `ci:`, `refactor:`, `perf:`, `chore:` for everything else

## Adding or changing a rule

Rules are a contract with every pipeline that gates on them, so a rule change is reviewed like an API change:

1. Add or edit the descriptor in `src/UblTr.Validator/Rules/RuleCatalog.cs`: a new, never-used id (`AAA-NNN`), the
   origin (`GibSchematron` only if it re-implements a named assertion of GIB's Schematron, and then name it in the
   source), and Turkish and English title, explanation, message and fix.
2. Implement the check in the matching `*Checks.cs` class.
3. Add one mutation to `tools/UblTr.SampleGen/SampleCatalog.cs` that breaks exactly this rule, then regenerate:

   ```bash
   dotnet run --project tools/UblTr.SampleGen -- samples samples/invoices
   UBLTR_UPDATE_GOLDEN=1 dotnet test --project tests/UblTr.Tests
   ```

   Review the new `samples/invoices/expected/<id>.json` like code: it is what users will read.
4. If the rule changes what GIB's own samples produce, update `docs/gib-samples.md` and the pinned counts in
   `GibPackageTests`, with the reason.

Never renumber or reuse a rule id: ids appear in CI annotations and `--disable` lists.

## Updating the GIB packages

See [docs/gib-package.md](docs/gib-package.md#when-gib-publishes-a-new-package). In short: new URL and SHA-256 in
`build/GibPackages.targets`, read GIB's `History.txt`, let the code-list drift test tell you what changed.

## Architecture decisions

A change to a decision recorded in `docs/adr/` needs a new ADR that supersedes the old one. Do not edit the old ADR in
place. Use the existing files as the format (MADR: context, options considered, decision, consequences).

## Data

Only synthetic data. Never commit real invoices, real tax numbers or GIB's package files.
