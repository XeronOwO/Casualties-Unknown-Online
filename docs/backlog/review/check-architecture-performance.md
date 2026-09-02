# check-architecture.ps1 performance

- Status: Review
- Priority: Medium
- Category: Tooling / developer experience / build gates

Landed the architecture-gate performance fix. The dominant cost was per-line
brace counting through PowerShell char pipelines; replaced with split-based
counting and `ReadAllLines`/`ReadAllText`. Full gate runtime on the current
tree dropped from ~32.4s to ~2.15s with no check weakened.

Selfcheck: `docs/evidence/selfchecks/tooling/check-architecture-performance-selfcheck.md`.
