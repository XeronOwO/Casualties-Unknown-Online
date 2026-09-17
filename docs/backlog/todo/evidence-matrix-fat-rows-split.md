# Evidence matrix rows carry their whole evidence trail; the gate should point at the JSON instead

- Status: Todo
- Priority: Low-Medium
- Category: Evidence / gates / workflow efficiency
- Source: workflow-iteration review 2026-09-17 — the rejection cycle could not read the E3 row
  without dumping it in chunks, and had to hand-maintain the same evidence in two files
- Related: `docs/evidence/sync-coverage-matrix.md`, `docs/evidence/sync-coverage-evidence.json`,
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests/SyncCoverageGateTests.cs`,
  `docs/evidence/normative-gates.md`

## Measured

- `docs/evidence/sync-coverage-matrix.md`: 245 KiB, 429 lines, 183 table rows; a row averages
  1 283 characters and the E3 row is **12 619** — longer than most source files it cites.
- `docs/evidence/sync-coverage-evidence.json`: 191 KiB, 821 anchored quotes.
- The same evidence therefore exists twice, hand-maintained: the row repeats every
  `path 'quote'` pair that the JSON already holds.

## Why it is not a drive-by edit

`SyncCoverageGateTests.SyncCoverageMatrix_InlineRefsAreEvidenceAnchoredAndQuoted` requires every
inline reference in a matrix row to be anchored in `sync-coverage-evidence.json` **and** its
inline quote to be present verbatim in the cited file. The fat rows exist to satisfy that
contract, so thinning them means changing what the gate demands — a change to the evidence gate
itself, which every future cycle depends on.

The rejection cycle also measured that the declared `count` in the JSON is not checked by any
gate (815 declared vs 821 entries at `80350526`, caught only by the adversarial pass).

## Required outcome (decide at implementation)

1. A matrix row keeps only the decision-facing cells: domain/trigger/direction, verdict, loss
   semantics / recovery, gap status, and a reference to its evidence (row id or anchor list).
2. The evidence text stays in `sync-coverage-evidence.json` (it already lives there); the gate
   verifies the row's declared anchors against the JSON instead of re-parsing quotes out of the
   row prose.
3. The gate contract is updated in the same cycle — `SyncCoverageGateTests` plus the rule map in
   `docs/evidence/normative-gates.md` — including its negative-contract self-tests (missing
   anchor, stale anchor id, unanchored row reference).
4. The count/entries mismatch is removed by construction: either drop the hand-written `count`
   field or make the gate assert `count == entries.Length`.
5. Row ids and verdicts stay stable: `docs/backlog` tickets, `review/` acceptance notes and
   `docs/evidence/normative-gates.md` reference rows by id (W1, E3, N1, …), so the ids must not
   be renumbered.

## Notes

- Full cycle (independent adversarial review) because it changes a gate contract, not because it
  is large: the diff touches one matrix file, one JSON file and one gate class.
- Row E3 is the acceptance case for "a human can read the row in one screen"; measure the new
  maximum row length and record it in the ticket when it lands.
