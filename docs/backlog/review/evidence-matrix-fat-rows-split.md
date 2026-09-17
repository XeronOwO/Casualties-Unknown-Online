# Evidence matrix rows carry their whole evidence trail; the gate should point at the JSON instead

- Status: Review (landed 2026-09-17; awaiting the final unified acceptance pass)
- Priority: Low-Medium
- Category: Evidence / gates / workflow efficiency
- Source: workflow-iteration review 2026-09-17 — the rejection cycle could not read the E3 row
  without dumping it in chunks, and had to hand-maintain the same evidence in two files
- Related: `docs/evidence/sync-coverage-matrix.md`, `docs/evidence/sync-coverage-evidence.json`,
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests/SyncCoverageGateTests.cs`,
  `docs/evidence/normative-gates.md`

## What landed

1. **A matrix data row carries decisions only.** The nine-cell row became ten:
   `ID | Feature / domain | Direction + roles | Event sync | Periodic fallback | Backfill /
   recovery | Loss semantics | Verdict | Anchors | Gap ticket`. Every `path 'quote'` pair is gone
   from the data rows — the quoted text exists once, in
   `docs/evidence/sync-coverage-evidence.json`, keyed by the row id.
2. **The `Anchors` cell is the row's whole evidence claim** — the number of entries that file
   holds for the row id — and the gate checks that declaration against the JSON instead of
   re-parsing quotes out of the row prose.
3. **The gate contract moved with it** (`SyncCoverageGateTests`): a row must declare at least one
   anchor; the declared count must equal the entries the evidence file holds for that row id; an
   entry may not name an unknown row id (the two historical entries whose `row` was a file path
   are now marked `(none)`, and that marker is capped at five so it cannot park coverage evidence
   out of every row); a row may not repeat a `path 'quote'` pair; and the evidence file's
   declared `count` must equal its entries, which removes the hand-written-count drift class
   (the ticket's 815-vs-821 reading) by construction. Negative-contract self-tests cover each new
   failure mode.
4. **The documents follow the contract**: the matrix `## Matrix` guide, its `## Guard` list (with
   an explicit "what the guard proves, and what it does not" paragraph), its `Known limitation`
   note, and the rule row in `docs/evidence/normative-gates.md`.
5. **Row ids and verdicts are untouched**: the 64 ids, their order, every verdict and every gap
   ticket are identical to the pre-change matrix.

## Measured (the ticket's requirement 6)

| | before (`9bc8dea7`) | after |
|---|---|---|
| matrix file | 250 032 bytes / 429 lines | 189 336 bytes / 453 lines |
| data-row text | 227 812 chars | 164 097 chars (−28%) |
| E3 row (the acceptance case) | 12 619 chars | 4 370 chars (−65%) |
| longest row | E3 12 619 | W1 4 444 |
| evidence JSON | 821 entries, `count` 821 (the ticket's 815 reading was stale) | 821 entries, unchanged but for two `row` fields |

Four rows are still longer than 4 000 characters (W1 4 444, E3 4 370, F1 4 103, F4 4 047); their
length is now decision prose rather than evidence text.

## Recorded residuals (deliberately not fixed here)

- **The count is a declaration check, not a completeness proof.** An edit that deletes entries
  and lowers the `Anchors` number in the same change is not caught mechanically, and neither is
  copying another row's entries onto a row. The 700-entry floor and a review of the evidence diff
  are the remaining guards; the `## Guard` paragraph states this instead of claiming otherwise.
- **Nine `(row, file, quote)` groups are registered twice** in the evidence file (a collector
  entry plus an inline-anchor entry). It is registration redundancy, not lost evidence; a
  uniqueness rule was implemented, found to leave the attack that matters uncovered while failing
  on this legitimate history, and dropped.
- Historical counts elsewhere still read 793 / 807 / 815 (`docs/backlog/README.md` row summaries,
  `docs/evidence/selfchecks/**`, older `review/` tickets). Those are dated records or index
  summaries; `todo/backlog-index-summary-duplication.md` owns that family.
- `SyncCoverageGateTests.cs` is 642 lines, over the delivery checklist's 600-line advisory for a
  touched class. The repository's line cap (`SourceShapeGateTests`) enumerates `src` only, this
  project's own `TestClassSizeGateTests` caps xUnit cases (12 here, limit 40), and eight test
  classes elsewhere already exceed 600 lines. The independent review recommended splitting the
  class into a support file plus the test class; that refactor was NOT taken in this cycle (it
  would have invalidated a finished review of a frozen tree for no behavioural gain), so the
  class is left intact and the deviation is recorded here.

## Verification

- `dotnet format CasualtiesUnknownOnline.slnx` → exit 0.
- `SyncCoverageGateTests` 12/12; full suite 3 202 + 32 green.
- Independent adversarial review **before** the commit (fresh context, frozen tree): it
  independently recomputed every row's anchor count (64/64 match), confirmed the 64 row ids,
  order, verdicts and gap tickets are unchanged, confirmed the JSON diff is two `row` fields
  only, and confirmed zero residual evidence text and zero empty cells. Its findings — 33
  mechanical-deletion residues (6 of them with lost meaning), two documentation claims the gate
  did not enforce, and the unbounded `(none)` marker — are all fixed in this same change; the
  first version of the split was rejected by that review.
