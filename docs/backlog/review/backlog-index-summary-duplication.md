# The backlog index duplicates every ticket's summary; make it a table of pointers

- Status: Review
- Priority: Low-Medium
- Category: Backlog hygiene / workflow efficiency
- Source: workflow-iteration review 2026-09-17 — a status change had to be written in two places
  and the two copies drifted twice in one cycle
- Related: `docs/backlog/README.md`, the ticket folders under `docs/backlog/`,
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests/BacklogIntegrityGateTests.cs`

## Problem

Each index line re-stated the ticket's problem, decision and status history. The index was
therefore not an index but a second, hand-maintained summary: every landing/status edit had to
touch two files, and the copy that was not read drifted. It also made the README the de-facto
home of facts that exist nowhere else (stage-split progress, landing dates, acceptance state).

## Measured

Both columns come from the same frame: the `docs/backlog/README.md` blobs as git stores them (LF),
read with `git cat-file blob`. The "132 index lines" this ticket originally recorded was itself
stale — the tree has 136 tickets and the index listed all 136, before and after.

| | before | after |
|---|---|---|
| bytes | 53 340 | 20 011 |
| lines | 194 | 200 |
| index rows | 136 | 136 |
| longest index row | 1 560 | 150 |
| rows over the 160-character budget | 106 | 0 |

## Landed (2026-09-18)

1. **Row shape.** `- [Title](path) — **Priority** — one clause`. The clause says what the ticket
   IS, never its history, and the row's SECTION is its status — so the review section's repeated
   "awaiting the final unified acceptance pass" boilerplate is gone with the section already
   meaning it. The contract is stated in the README's Workflow section.
2. **Priority.** Taken from the ticket's own `- Priority:` field (first token, so a parenthetical
   provenance note stays in the ticket). The closed records — `done/`, `resolved/`, `watchlist/`
   and two landed review tickets that never declared one — carry no priority in their row rather
   than an invented value.
3. **Gate.** `BacklogIntegrityGateTests.EveryIndexRowIsAPointerWithinItsBudgetCarryingItsTicketsPriority`
   enforces the row budget and the priority agreement, with a 130-row census floor so a parser
   that silently stopped matching cannot pass by checking nothing; its negative-contract self-test
   is `BacklogIntegrityGateTests.IndexRowFailures_FindAnOverlongRowAndAPriorityThatDisagrees`. The
   160-character budget is tighter than the 240 this ticket sketched, per the handoff instruction.
   The existing index/section/status/anchor rules are untouched, and the rule is recorded in
   `docs/evidence/normative-gates.md`.
4. **No fact left the index.** The rows whose old text carried a dated, counted or numbered fact —
   landing dates, suite/gate counts, `ProtocolVersion` bumps, decision numbers, S3.5/S4 stage
   progress — were checked against their tickets: every one of them already lived in its ticket,
   so nothing had to be moved in. The acceptance state is the section's meaning, and the ticket
   stays the source of truth for everything else.

Red→green: with the new rule in the tree and the old index still in place, the focused gate run
was 1 failed / 11 passed (only `EveryIndexRowIsAPointerWithinItsBudgetCarryingItsTicketsPriority`,
reporting 106 over-budget rows); after the rewrite the same filter is 12/12.

## Notes

- A backlog-hygiene cycle: no runtime and no `src/` change. It touches `tests/` (the gate rule), so
  the full build / format / test gate applies.
