# The backlog index duplicates every ticket's summary; make it a table of pointers

- Status: Todo
- Priority: Low-Medium
- Category: Backlog hygiene / workflow efficiency
- Source: workflow-iteration review 2026-09-17 — a status change had to be written in two places
  and the two copies drifted twice in one cycle
- Related: `docs/backlog/README.md`, the ticket folders under `docs/backlog/`,
  `tests/CasualtiesUnknownOnline.NormativeGates.Tests/RepositoryGateTests.cs`

## Measured

- `docs/backlog/README.md`: 193 lines carrying **132** index entries; entries average 371
  characters, the longest is 1 560, and 20 exceed 600.
- 34 entries end with the boilerplate "awaiting the final unified acceptance pass".
- Observed drift in a single cycle (2026-09-17): the E3 index line still described the removed
  unmaterializable relay after the ticket said REJECTED; the `runtime-entity-creation-rejection`
  index line disagreed with its own ticket about the test count (5 vs 6).

## Problem

Each index line re-states the ticket's problem, decision and status history. The index therefore
is not an index but a second, hand-maintained summary: every landing/status edit must touch two
files, and the copy that is not read drifts. It also makes the README the de-facto home of facts
that exist nowhere else (stage-split progress, landing dates, acceptance state), so trimming it
naively loses information.

## Required outcome (decide at implementation)

1. An index row becomes `- [Title](path) — **Priority** — one clause`; the clause says what the
   ticket IS, never its history (history lives in the ticket).
2. Any fact that only exists in the index today moves into its ticket first (stage-split status,
   landing dates, acceptance state), or is represented by the section the ticket sits in; the
   index must not be the only home of a fact. Verify this per ticket, not by assumption.
3. A gate keeps the duplication from growing back: index lines over a fixed length budget (for
   example 240 characters) fail, and every index path must resolve to an existing ticket file.
4. Land it section by section (Todo / In progress / Review / Done / Future / Resolved), reviewing
   each section's diff on its own; do not bulk-truncate — the information must move, not vanish.

## Notes

- Documentation-only change, but it touches ~132 lines with per-line judgement, so it is its own
  cycle rather than a drive-by edit.
- The `review/` section is the largest and the most repetitive; dropping the acceptance
  boilerplate there is the single biggest win and needs no information to move (the section
  already means "waiting for the unified acceptance pass").
