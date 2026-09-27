# Acceptance record — Full-qualified name cleanup

- Ticket: `full-qualified-name-cleanup` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

The ticket carries no acceptance section and no landing section; its rows are the scope it states.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | All `src/` projects. | machine | pass | `FullyQualifiedNameGateTests.Repository_HasNoUnnecessaryFullyQualifiedTypeNames` walks `src/` recursively (build output excluded) and asserts zero violations and zero parse failures — passed in the gate suite 288/288 |
| 2 | All `tests/` projects. | machine | pass | the same test walks `tests/` in the same pass over the same assertion, so a violation in any test project fails it; it passed |
| 3 | No behavior change; refactor only. | machine | pass | the gate is a Roslyn source-shape check with no runtime path; the repository's behavioural evidence stayed green in the same run — main suite 4482/4482 and gate suite 288/288, 0 failed and 0 not executed in both |
| 4 | Preserve architecture line-count gates; extract real types where cleanup would push a class over a gate. | machine | pass | `SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` passed in the same gate run, so the one-top-level-type-per-file rule and the aggregate limits hold over the tree |

## Residuals for the user
None.

## Limits
No client, no rendering and no deployed artifact were exercised; this is a source-shape convention ticket and every row is a gate outcome. The convention holds as the repository's own gate defines it: the gate's documented exemptions (namespace declarations and using directives, the ambiguity-collision exception, and the accepted `using` forms) are not violations, and the rows do not claim that no fully qualified name of any kind exists in the tree. Row 4 is decided by the architecture gate plus the gate run, not by a per-file measurement of every class.
