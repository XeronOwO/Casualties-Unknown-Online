# Restored entity rows: a throwing row still costs the rows behind it

- Status: Review — landed 2026-09-17 (the row-level containment; decision 183, below decision 175's
  half-level rule). Awaiting the final unified acceptance pass.
- Priority: Low-Medium
- Category: Sync / restore accounting
- Source: the hardening cycle's containment question (decision 175) — the half-scoped containment
  bounds the blast radius to the half, and this is the level below it
- Related: `docs/decisions/active.md` 173, 175, 183,
  `docs/backlog/review/trap-action-divergence-hardening.md`,
  `docs/backlog/review/restore-live-object-loops-containment.md`,
  `src/CasualtiesUnknownOnline.Runtime/Session/World/ContainedRowLoop.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/World/RestoredWorldFactReplay.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/EntityEventSync.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/WorldBuildingEntitySync.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/WorldBlockStateTable.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/GameBlockDamageTable.cs`

## The gap (as it was split out)

A restored cut's world-entity half is written through three appliers whose row loops had no per-row
containment: `EntityEventSync.OnTrapStateProjected` (the trap rows),
`WorldBuildingEntitySync.OnOpenedEntitiesProjected` and
`WorldBuildingEntitySync.OnBuildingHealthProjected`. One row throwing therefore cost every row BEHIND it
in that loop, and the two appliers that had not run yet — while the half's report could only say "not
fully written", because the throw left no count.

The Runtime seam's containment (decision 175) bounds this to the half: the world-fact half keeps its
accounting and its commit. It cannot bound it further, because `LiveWorldWriteOutcome` carries counts
and the row loops live in the Game Adapter.

## Landed (2026-09-17)

`ContainedRowLoop` is the one rule, in two shapes: `Run` (the applier says whether the live world TOOK
the row, so applied plus refused is always the row count) and `RunContained` (the applier keeps its own
written/unchanged/refused accounting and gets back only the number of rows that THREW, which it adds to
its refused total — no row is counted twice). Both catch per ROW, never around the loop; both log the
row's identity with the exception at error level; and both bound the log: the first five throwing rows
are named, the rest are counted in one summary line, because a systematic failure is a demonstrated
shape here (decision 175 exists because every copy of one entity threw) and a flood of stack traces
must not replace an exact count.

The rule lives in the RUNTIME rather than in the adapter (this ticket's own wording suggested "a small
adapter helper"). Two structural reasons: the account it feeds is the Runtime's — decision 173 moved the
sibling "did this row reach the live world" rule there for the same reason — and the test project loads
`GameAdapter.dll` reflectively (`ExcludeAssets="compile"` in the test csproj), so an adapter-internal
helper could not be driven by the Runtime-seam test this ticket asks for. The adapter keeps what only it
knows: the per-row action and the row's identity, which is the string the error line carries —
`{Kind} at ({X},{Y})` for a trap row, `({X},{Y})` for an entity position or a block cell.

Five loops are converted, because this cycle's independent adversarial pass found the family wider than
the ticket named:

| applier | loop | accounting |
|---|---|---|
| `EntityEventSync.OnTrapStateProjected` | the trap rows | `Run`: refused = rows the applier did not take, throws included |
| `WorldBuildingEntitySync.OnOpenedEntitiesProjected` | the opened-entity positions | `Run` |
| `WorldBuildingEntitySync.OnBuildingHealthProjected` | the building-health entries | `Run` |
| `WorldBlockStateTable.Apply` | the restored block cells (one per cell, unbounded) | `RunContained`: a row counts as applied only once its cell write AND its air-write settle completed, so a row whose settle threw is refused once, not both |
| `GameBlockDamageTable.Apply` | the partial-damage rows | `RunContained`: threw rows join the refusals this table already computes (air / range / cap) |

The three loops that were NOT converted in this cycle, and the per-site reason each one is genuinely
a different shape, were recorded when this ticket was split and landed in the cycle after this one
(`review/restore-live-object-loops-containment.md`, decision 196).

Two behaviours were preserved deliberately: the trap applier's
`CallContext.Enter(CallContext.Origin.RemoteApply)` scope still wraps the whole loop, so a replayed row
cannot re-report; and a row an applier refuses by returning false is still refused WITHOUT an error line
(the appliers log their own reason — the trap path's missing-entity line is `LogGoneWithNearest`, the
building appliers' is their own "no entity there" warning).

## Evidence

| claim | how it is proven | where |
|---|---|---|
| a throwing row costs only itself and the rows behind it still land | the rule's own contract: a throw in the middle, every row attempted exactly once, an exact outcome | `ContainedRowLoopTests` |
| the refused count reaches the restore's account as an exact number, never "the write threw" | Runtime-seam test: a sink whose trap loop contains row 1 of 3 reports exactly `1 world-entity row(s)`, releases that half, and keeps the world-fact half's account and commit | `RestoredWorldFactReplayTests.ApplyIfPending_AThrowingWorldEntityRowCostsOnlyItself` |
| the error line names the ROW, not the loop | the rule's tests assert the row's identity and the ERROR level | `ContainedRowLoopTests` |
| a row the applier merely refuses stays silent | the rule's test asserts no log entry for a false-returning row | `ContainedRowLoopTests` |
| a systematic failure is bounded, and the count stays exact | nine throwing rows produce five named lines plus one summary naming the remaining four and the total nine | `ContainedRowLoopTests` |
| the five converted loops really use the rule | read-only review of the five call sites — their bodies are game-typed (`TrapVisualReplay.Replay`, `Physics2D.OverlapPoint`, `world.SetBlock`, `world.GetBlockInfo` cannot be bound by the test host) | `EntityEventSync.cs`, `WorldBuildingEntitySync.cs`, `WorldBlockStateTable.cs`, `GameBlockDamageTable.cs` |

## Not proven reachable

No unguarded engine call in these loops is demonstrated to throw today — the three cases that did (the
lifepod shower/heater and the crystal effect lookups) are closed by decision 175. This ticket is about
the blast radius when one does, not about a known failing path. The seam test's fake calls the rule
itself, so it proves the RULE and the accounting path, not that the five production call sites are
wired to it — that half is the read-only review the table above names.
