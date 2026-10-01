# Acceptance record — The backlog index duplicates every ticket's summary

- Ticket: `backlog-index-summary-duplication` — verdict: moved to `done/` (the fact-preservation row
  passes with the four named family-level exceptions under Limits)
- Batch: `20261001-t` — offline batch; scope and limits: `docs/evidence/acceptance/20261001-t-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working
  tree over `15ffc42a`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+1b088511d555cf09ca19b336c2f3715f94c7df4e` —
  `tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"; no client was started
- Run: 2026-10-01 — preflight 17:46 (11 present, exit 0); focused evidence run 17:47
  (`BacklogIntegrityGateTests` 14/14); this run's index re-measurement and fact check over
  `7c4075a4^` / `7c4075a4`; full suite with build (4,573 + 300, 0 failed)
- Dependencies: `dotnet` (both suites), the repository's own history (`git show`), repository
  inspection; no game client
- Artifacts: the `20261001-t` directory under `acceptance-artifacts-dir` — `index-facts-compare.py`,
  `measured-tables.py`, `focused-20261001-t.log`, `full-suite-20261001-t.log`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Row shape: `- [Title](path) — **Priority** — one clause`, within the 160-character budget | machine | pass | `BacklogIntegrityGateTests` 14/14 (`EveryIndexRowIsAPointerWithinItsBudgetCarryingItsTicketsPriority` and its negative-contract self-test among them); this run's reading of the index: 188 rows, longest row 160 chars, 0 rows over the budget |
| 2 | Priority comes from the ticket's own `- Priority:` field; a closed record that declares none carries none | machine | pass | the same gate case asserts the priority agreement against every ticket; 14/14 `Passed` |
| 3 | The gate carries a 130-row census floor, matcher samples and the recorded rule in `docs/evidence/normative-gates.md` | machine | pass | both gate cases `Passed`; the normative-gates row names `EveryIndexRowIsAPointerWithinItsBudgetCarryingItsTicketsPriority` and its self-test; the floor and samples are read from the test source |
| 4 | No fact left the index: the old rows' dated, counted and numbered facts already lived in their tickets | machine + re-derived comparison | pass, with four named family-level exceptions | the run's mechanical check over the pre-rewrite index (`7c4075a4^`) read 95 fact tokens (dates, decision numbers, stage tags, protocol numbers, unit-bearing counts) out of 136 rows; 91 are present in the pointed ticket at the rewrite revision. Four are not in the pointed file: `S3`/`S3.3` stage references live in the save family's umbrella (`save-system-mid-run-and-layer-end`) and in `save-mid-run-consistent-cut`, and decision 181 lives in the row's own cited sibling (`save-interval-autosave-and-backup-recovery`). Nothing was orphaned and nothing had to be moved in; the ticket's "its ticket" covers the ticket family |
| 5 | The measured table: bytes 53,340 → 20,011; lines 194 → 200; rows 136 → 136; longest row 1,560 → 150; rows over the budget 106 → 0 | machine + re-derived measurement | pass | re-derived from `7c4075a4^` and `7c4075a4`: every figure matches exactly. The current tree (later ticket moves) reads 28,407 bytes / 260 lines / 188 rows / longest 160 / 0 over the budget, so the budget still holds |

## Residuals for the user

None.

## Limits

- Row 4's check is mechanical and names its vocabulary (ISO dates, `#`/decision numbers, `S`-stage
  tags, protocol numbers, unit-bearing counts); the four exceptions above are where a fact lives in the
  ticket family rather than the exact linked file, and they are recorded rather than smoothed over.
- Row 5's numbers are the rewrite's measurements re-derived at its own revisions; the current index's
  larger size is later ticket traffic, not a regression of the row shape.
