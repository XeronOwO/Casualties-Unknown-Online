# check-architecture.ps1 performance

- Status: Resolved
- Priority: Medium
- Category: Tooling / developer experience / build gates

The historical PowerShell performance fix landed, but the ticket is now moot:
the PowerShell script was removed when the architecture gate was ported to the
C# `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits`
unit test. No further performance work is needed on the removed script.

Selfcheck (historical): `docs/evidence/selfchecks/tooling/check-architecture-performance-selfcheck.md`.
