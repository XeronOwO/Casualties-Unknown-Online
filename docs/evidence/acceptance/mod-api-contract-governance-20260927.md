# Acceptance record — Mod API contract governance: visibility rule, stability levels, public-surface baseline

- Ticket: `mod-api-contract-governance` — verdict: moved to `done/`
- Batch: `20260927-b` — offline batch; scope and exclusions: `docs/evidence/acceptance/20260927-b-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working tree over `a23a43b1`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+a23a43b198557aac85fee0febce3ba1ab3d31082` — deployed and hash-verified in this run (`tools/verify-deploy.ps1`: "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-09-27 — preflight 17:14; build and the two suites 17:15 → 17:18; format; deploy + hash verification; no client was started, so host/guest do not apply
- Dependencies: `dotnet` (build, gate suite, main suite), `format`, and the repo's `tools/deploy.ps1` + `tools/verify-deploy.ps1`; preflight exited `0` (9 present, `input` pending — the staged driver is not needed for these rows)
- Artifacts: none — every row is a text verdict; the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The gate is red on a synthetic added public member and green on the current tree; the baseline file is committed and its census is asserted (a gate that scans nothing must not pass). | machine | pass | `ApiSurfaceGateTests.TheMatcher_FlagsAnAddedMemberAsAnApiChange` is `Passed` — it compares a synthetic baseline with a synthetic surface declaring one extra member and asserts exactly one `ADDED` finding naming `Sample.IContract.Extra(int attempts)`; `ApiSurfaceGateTests.AbstractionsPublicSurface_MatchesTheReviewedBaseline` is `Passed` — it re-derives the `Abstractions` surface from the tree, compares it with the baseline and fails outright when the baseline file is absent; `ApiSurfaceGateTests.TheBaselineAndTheScan_MeetTheCensusFloor` is `Passed` — it asserts a source-file floor on the scan and entry/public-type floors on both the scan and the baseline, which is the "a gate that scans nothing must not pass" half; the baseline itself is git-tracked at `docs/contracts/abstractions-api-baseline.txt` (785 lines), and the gate lives in `tests/CasualtiesUnknownOnline.NormativeGates.Tests` |
| 2 | `docs/api/advanced-modification-policy.md` exists and is linked from `docs/api/mod-api.md`. | machine | pass | judged against the current tree, where the page moved and was renamed: `docs/en/reference/modification-policy.md` and its `docs/zh/reference/modification-policy.md` counterpart exist (same page set in both blocks), and `docs/en/reference/mod-api.md` links it five times, including the manifest section the ticket corrects; `docs/evidence/selfchecks/documentation/legacy-tree-migration-selfcheck.md` records that no live document still names the deleted `docs/api/` path |
| 3 | The `AGENTS.md` rule is added as one binding line, not a section. | machine | pass | `AGENTS.md` carries it as a single numbered item — `## Engineering Conventions (binding)` item 14, "**Minimum visibility, declared stability**: ..." — and the file's heading list contains no mod-API section of its own; `docs/evidence/normative-gates.md` records the same rule as convention #14 with its gate rows |

## Residuals for the user
None.

## Limits
No client was started: no rendering, no frame, no live log and no probe. All three rows are decided by the gate suite and by direct inspection of the committed documentation, with no runtime component.

Row 1's "red on a synthetic added public member" is decided by the matcher's own contract test over synthetic sources, not by mutating this tree; the gate's behaviour on the real surface is the second and third `Passed` tests. Row 2 is read against the current tree: the ticket's `docs/api/advanced-modification-policy.md` no longer exists as a path — the page is `docs/en/reference/modification-policy.md` (and its Chinese counterpart) — which the legacy-tree migration selfcheck (`docs/evidence/selfchecks/documentation/legacy-tree-migration-selfcheck.md`) records, so the row's content (page exists and is linked) holds. The census numbers the ticket quotes for the baseline (769 lines, 60/54/450 floors against a measured 92/92/756) are that cycle's measurements and are not repeated here: the file is now 785 lines and the floors are asserted in code by the `Passed` census test. Row 3 reports the rule as it stands in the current `AGENTS.md` (item 14), not the numbering the ticket's prose used.

Batch scope and exclusions are recorded in `docs/evidence/acceptance/20260927-b-scope.md`.
