# Acceptance record — Evidence matrix rows carry their whole evidence trail

- Ticket: `evidence-matrix-fat-rows-split` — verdict: moved to `done/` (one historical byte figure is
  not reproducible; named under Limits)
- Batch: `20261001-t` — offline batch; scope and limits: `docs/evidence/acceptance/20261001-t-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working
  tree over `15ffc42a`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+1b088511d555cf09ca19b336c2f3715f94c7df4e` —
  `tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"; no client was started
- Run: 2026-10-01 — preflight 17:46 (11 present, exit 0); focused evidence run 17:47
  (`SyncCoverageGateTests` 12/12); this run's re-derived comparison and measurements; full suite with
  build (4,573 + 300, 0 failed)
- Dependencies: `dotnet` (both suites), the repository's own history (`git show 9bc8dea7` /
  `bfe0189f`), repository inspection; no game client
- Artifacts: the `20261001-t` directory under `acceptance-artifacts-dir` — `matrix-compare.py`,
  `measured-tables.py`, `focused-20261001-t.log`, `full-suite-20261001-t.log`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A matrix data row carries decisions only, and the `Anchors` cell is the row's whole evidence claim | machine | pass | this run's strict parse: every data row has the ten-cell shape (`ID` … `Verdict` `Anchors` `Gap ticket`); the quoted `path 'quote'` pairs no longer occur in data rows — they live in `docs/evidence/sync-coverage-evidence.json` |
| 2 | The gate checks the anchor declaration against the JSON | machine | pass | `SyncCoverageGateTests` 12/12 (`Passed`, negative-contract self-tests included); this run's reading: the evidence file declares `count` 1,025 and holds 1,025 entries |
| 3 | The gate contract covers every new failure mode: at least one anchor per row, count equality, unknown row ids refused, the `(none)` marker capped, no repeated `path 'quote'` pair, the declared count equal to the entries | machine | pass | the four named `SyncCoverageGateTests` cases and their self-tests `Passed` (12/12); the rule row is documented in `docs/evidence/normative-gates.md`; the matrix's `## Guard` section is present |
| 4 | Row ids and verdicts are untouched by the split: the 64 ids, their order, every verdict and every gap ticket are identical to the pre-change matrix | machine + re-derived comparison | pass | the run compared `bfe0189f^` (the split's parent, `9bc8dea7`) against `bfe0189f`: 64/64 ids, identical order, 0 verdict diffs, 0 gap-ticket diffs. For context, the current tree carries 65 rows (R9 added) and eight verdict advances plus ten gap-path updates — all from sibling tickets landed after the split, each visible in its own commit |
| 5 | The measured table: matrix file 250,032 → 189,336 bytes / 429 → 453 lines; data-row text 227,812 → 164,097 chars; E3 12,619 → 4,370; longest row → 4,444; evidence JSON 821 entries, declared 821 | machine + re-derived measurement | pass, with one named exception | re-derived from the two named revisions: lines 429 → 453; data-row text 227,812 → 164,097 chars; E3 12,619 → 4,370; longest row 12,619 → 4,444 (W1); JSON 821 entries with declared 821; the after-bytes 189,336 matches the on-disk CRLF form. **The before-bytes 250,032 does not re-derive**: the blob at `9bc8dea7` is 250,647 bytes (LF) / 251,076 (CRLF), so the recorded figure is 615 bytes short — every other figure matches exactly |

## Residuals for the user

None.

## Limits

- Row 5's before-bytes discrepancy is recorded, not reconciled; the figure is a historical reading and
  no current claim depends on it.
- The gate's own recorded limit — the count is a declaration check, not a completeness proof — stays as
  the ticket states it; this run re-derives the declaration equality, not the relevance of each quote.
- No runtime behaviour is observed; `SyncCoverageGateTests` is a source/JSON gate.
