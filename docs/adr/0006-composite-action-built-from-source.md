# 0006: Ship the GitHub Action as a composite action that builds the CLI on the runner

- Status: accepted
- Date: 2026-11-11

## Context

Repositories that keep UBL-TR documents (integration fixtures, exported invoices, test data for an ERP connector)
want every pull request checked, with findings shown on the changed lines. The action must not redistribute GIB's
packages (ADR 0002).

## Options considered

1. **Docker container action** with a prebuilt image on a registry. Fast start, but the image would contain GIB's XSDs,
   i.e. redistribution, and container actions only run on Linux runners.
2. **JavaScript action** wrapping a downloaded release binary. Same redistribution problem for the files bundled with
   the binary, plus a Node wrapper to maintain.
3. **Composite action that runs `setup-dotnet`, publishes the CLI from the action's own source and runs it** (chosen).

## Decision

- `action.yml` at the repository root: `setup-dotnet` from the action's `global.json`, a cache of `.gib/` keyed on the
  pinned targets file, `dotnet publish` of `src/UblTr.Cli` (which fetches and verifies the GIB packages on the user's
  runner), then `ubltr validate --format github`.
- Inputs: `paths` (files, directories, globs; newline or comma separated), `lang`, `kind`, `fail-on`, `disable`.
  Outputs: `valid` and `report` (path of the JSON report, ready for `upload-artifact`).
- Inputs reach the script only through environment variables, never by `${{ }}` interpolation inside `run:`, so a
  crafted input cannot inject shell commands.
- Findings become `::error` / `::warning` workflow commands with file, line, column and the rule title, plus a Markdown
  table in the job summary.
- The repository's own CI uses the action as a consumer would (`uses: ./`): valid samples must pass, invalid samples
  must fail with the expected rule ids in the report, and a suppressed rule must pass.

## Consequences

- A cold run pays for a .NET SDK setup and a publish; later runs reuse the GIB cache. That is slower than a container
  action, and it is the price of not redistributing GIB's files. The measured duration of the self-test job is in the
  CI run history.
- `samples/workflows/validate-invoices.yml` is the copy-paste starting point for consumers.
- The action is written for any runner OS where `setup-dotnet`, `bash` and `curl` exist; CI exercises it on
  `ubuntu-latest` only.
