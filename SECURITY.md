# Security policy

## Supported versions

Only the latest release receives fixes.

## Reporting a vulnerability

Please **do not open a public issue**. Use GitHub's private vulnerability reporting instead:
**Security → Report a vulnerability** on this repository. You can expect an acknowledgement within 5 working days. If
the report is confirmed, a fix or mitigation plan follows within 30 days.

Please include the affected version or commit, a minimal XML document or action configuration that reproduces the
problem, and the impact you observed. Never send real invoices; reproduce with synthetic data.

## Security-relevant design

See [docs/threat-model.md](docs/threat-model.md) for assets, threats and mitigations. In short:

| Area | Default |
|---|---|
| XML parsing | DTDs prohibited, no resolver: no XXE, no entity expansion, no network or file access from a document |
| Schema choice | Fixed at startup; `xsi:schemaLocation` in a document is ignored; schema imports cannot leave the schema folder |
| Supply chain of GIB files | HTTPS download, SHA-256 pinned per file, build fails on mismatch |
| Input limits | 20 MB request body (enforced on the stream), findings capped per rule, XSD findings capped per document |
| Overload | Concurrency limiter (processor count, queue 64), then 503 |
| Data handling | Nothing is stored; logs and traces never contain document content |
| GitHub Action | Inputs reach the shell only as environment variables (no expression injection) |
| Dependencies | Pinned versions, `dotnet list package --vulnerable` in CI, CodeQL |
