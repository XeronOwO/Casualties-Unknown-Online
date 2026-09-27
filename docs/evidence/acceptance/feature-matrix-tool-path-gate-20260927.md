# Acceptance record — Feature-matrix tooling: no gate covers the CSV paths or the tool/page agreement

- Ticket: `feature-matrix-tool-path-gate` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

The ticket carries no acceptance section; its rows are the claims of its `What landed` section.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The script-relative literals have a gate: `FeatureMatrixToolPathGateTests` derives its scan surface from `tools/*.ps1`, reads every `Join-Path $scriptDir` path literal, keeps a discovery floor over the scripts it walked, and asserts every literal names an existing file and each matrix tool still resolves its own matrix. | machine | pass | `FeatureMatrixToolPathGateTests.EveryScriptRelativeLiteral_NamesAFileThatExistsAndResolvesItsOwnMatrix` passed in the gate suite 288/288; the tree's only two such literals are in `tools/item-features.ps1` and `tools/entity-features.ps1` and both name files that exist (`docs/contracts/item-features-matrix.csv`, `docs/contracts/entity-features-matrix.csv`); the class also passed as a whole, 4/4 |
| 2 | Both matrices' columns are one list in three places: each CSV header is compared with the column list under the matching section of both reference pages, in order, so a renamed, added or dropped column fails until CSV and both pages are updated together. | machine | pass | `FeatureMatrixToolPathGateTests.TheMatrixHeaders_EqualTheFeatureColumnListsTheReferencePagesPublish` passed; the item CSV header is `item` plus 12 feature columns and the entity CSV header is `entity` plus 9, and `docs/en/reference/feature-matrices.md` and `docs/zh/reference/feature-matrices.md` both state 12 and 9 feature columns, matching the CSVs; the entity page's earlier "10 columns" wording the ticket records as corrected reads 9 in both blocks |
| 3 | Both matchers are pinned. | machine | pass | `FeatureMatrixToolPathGateTests.TheLiteralMatcher_ReadsTheJoinPathFormAndIgnoresTheOtherForms` and `FeatureMatrixToolPathGateTests.ThePageColumnMatcher_ReadsOnlyTheBacktickedFirstColumn` both passed |

## Residuals for the user
None.

## Limits
No client was started; this ticket is a documentation/tooling gate and every row is a gate outcome plus a file fact about the two CSVs and the two reference pages. The gate's own Limits hold as the ticket states them: it proves that a literal names an existing file and that the column lists agree — it does not read the matrix contents (row counts, per-row verdicts, the narrative's entity tables stay with `EntityFeaturesDocConsistencyTests` and with review), and the 12/9 column counts are pinned because the pages state them in prose. The negative controls the ticket records (restoring the pre-migration literal, renaming a column on the English page) were not re-run by this batch; the shipped matcher pins in row 3 are what this batch decides.
