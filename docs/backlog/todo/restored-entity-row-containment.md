# Restored entity rows: a throwing row still costs the rows behind it

- Status: Todo (split out of `review/trap-action-divergence-hardening.md` by that cycle's adversarial
  review, 2026-09-13; the review showed the per-row containment is cheap and loses nothing)
- Priority: Low-Medium
- Category: Sync / restore accounting
- Source: the hardening cycle's containment question (decision 175) — the half-scoped containment
  bounds the blast radius to the half, and this is the level below it
- Related: `docs/decisions/active.md` 175, `docs/backlog/review/trap-action-divergence-hardening.md`,
  `src/CasualtiesUnknownOnline.Runtime/Session/World/RestoredWorldFactReplay.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/EntityEventSync.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/WorldBuildingEntitySync.cs`

## The gap

A restored cut's world-entity half is written through three appliers whose row loops have no per-row
containment: `EntityEventSync.OnTrapStateProjected` (the trap rows),
`WorldBuildingEntitySync.OnOpenedEntitiesProjected` and `WorldBuildingEntitySync.OnBuildingHealthProjected`.
One row throwing therefore costs every row BEHIND it in that loop, and the two appliers that have not
run yet — while the half's report can only say "not fully written", because the throw left no count.

The Runtime seam's containment (decision 175) bounds this to the half: the world-fact half keeps its
accounting and its commit. It cannot bound it further, because `LiveWorldWriteOutcome` carries counts
and the row loops live in the Game Adapter.

## The change

Contain one row at a time in each of the three loops: catch, log the exception AT ERROR with the row's
identity (kind + position for the trap rows, the entity position for the other two), leave the row
uncounted so it lands in `LiveWorldWriteOutcome.Refused`, and continue with the next row. That keeps
BOTH surfaces the earlier "the report has no room for the reason" argument traded against each other:
the log carries the exception, and the report carries an exact refused-row count instead of a whole half.

## Evidence to produce

- One rule, three sites: extract the loop (a small adapter helper) instead of three ad-hoc
  try/catch blocks, and keep the exact refused-row counting.
- A Runtime-seam test that drives one applier through a seam with a throwing row: one refused row,
  the rest applied, `WorldRestoreAudit` complete only when nothing else was refused.
- The appliers are game-typed, so the loops themselves carry read-only review plus the existing
  simulation suites; record which half of the claim each one proves.

## Not proven reachable

No unguarded engine call in these loops is demonstrated to throw today — the three cases that did
(the lifepod shower/heater and the crystal effect lookups) are closed by decision 175. This ticket is
about the blast radius when one does, not about a known failing path.
