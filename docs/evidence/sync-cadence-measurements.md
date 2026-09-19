# Sync cadence measurements

Evidence for `docs/backlog/review/sync-cadence-review.md` (the sync-coverage audit's cadence
findings). Every number below is produced by the production code the claim is about —
`AdaptiveStreamCatalog` + `AdaptiveRatePolicy` for the stretch caps, the simulation world for the
byte costs — so the table can be re-derived instead of trusted, and the executable half asserts
each value.

## How to reproduce

```text
dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~SyncCadenceDecision"
dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~EntryRepairConvergence"
dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~NetworkTrafficBaseline"
```

`SyncCadenceDecisionTests` computes the first table from the production catalog and policy (no
copy of the arithmetic) and asserts every cell of it; `EntryRepairConvergenceTests` drives the
entry repair through a real host/guest simulation (a dropped entry group, the guest's repeat, the
repair, the ordering of the marker, the per-entry bound, third-party isolation) and measures the
repair pass's bytes; `NetworkTrafficBaselineTests` is the recorded traffic baseline the decision
must not regress.

## Worst-case divergence (the fallback's own staleness, in ms)

| Stream (priority) | Optimal | Moderate | High | Critical | Profile cap | Decision |
|---|---|---|---|---|---|---|
| `WorldItemSnapshotStream` (2), 5 s base | 5 000 | 8 333 | 10 000 | 10 000 | 10 000 | **tightened** from 30 000 (the cap now binds at High too) |
| `TraderStateStream` (2), 5 s base | 5 000 | 8 333 | 14 286 | 15 000 | 15 000 | **tightened** from 30 000 (High is unchanged) |
| `FluidRegionFullStream` (2), 1 s base | 1 000 | 1 667 | 2 857 | 6 667 | 10 000 | **accepted** — the cap never binds, so 6 667 ms is the measured worst case |

The values are `round(base / pressureFactor)` for the interval-based profiles
(`AdaptiveRatePolicy.GetEffectiveIntervalMs`); the Critical factors are 0.15 for priority 2, and
the cap is applied after the byte budget, so a tightened cap deliberately outranks byte
preservation. What the caps cost under pressure is bounded by the payloads the traffic baseline
already measures: its 600-item checkpoint is asserted at under 24 KB, so the item keyframe's
worst case at 10 s stays in the low KB/s against the profile's 1 MB/s budget.

## Entry repair (finding 4: the 60 s first resend)

| Quantity | Value |
|---|---|
| Guest readiness window (`SessionControlConvergence`) | 5 000 ms × 12 reports = 60 000 ms |
| Repair cadence while that window is open (`EntryRepairSchedule`) | 10 000 ms |
| Repairs per entry, worst case | 6 (the window is spent before a seventh claim) |
| First repair after the entry edge | the member's first repeat ≈ 5 000 ms after the edge |
| Repairs for an entry whose window closed on BOTH control facts | 0 |
| Repairs for an entry whose gate release is held (nothing swallowed) | ≤ 4 inside the 30 s force-start — the host cannot tell that window from a swallowed one, so it is answered and bounded (`EntryRepairConvergenceTests.HeldGate_PaysBoundedRepairsWithoutASwallow`) |
| One repair pass, measured in the simulation world | 2 438 bytes (`EntryRepairConvergenceTests.SwallowedEntryGroup_IsHealedByTheRepeatAnswer` asserts exactly this number, so it is reproducible and a change in the pass's cost fails loudly) |

The 2 438-byte figure is the whole answer to the swallowed entry group in
`EntryRepairConvergenceTests.SwallowedEntryGroup_IsHealedByTheRepeatAnswer` — the absolute
in-session tables (kernel checkpoint, block state, block damage, trap layout, enemy snapshot,
runtime entities, recipe unlocks, roster) plus the start-gate answer and the completion marker.
The simulation world is deliberately minimal; the dominant term in a real session is the kernel
checkpoint, which the traffic baseline pins (< 24 KB for 600 items).

## Carried-inventory registration (row I8)

| Quantity | Value |
|---|---|
| Burst step (first-resend latency of a swallowed registration) | 5 000 ms |
| Burst window | 12 × 5 000 ms = 60 000 ms |
| Steady cadence after the window | 60 000 ms |

## The 60 s steady cycles that remain

`WorldEntryFanout.SendInSessionRepair` (block state, block damage, trap layout, kernel
checkpoint, enemy snapshot, runtime entities, recipe unlocks, roster), the keypad codes
(`WorldEventSync`), the geyser liquid types (`GeyserStateSync`) and the kernel checkpoint cycle
stay at their hardcoded 60 s and are not adapted by the governor. They are the durable fallback
behind the entry repair, which is what changes their FIRST resend after a world entry.

## What is not measured here

Real transport timing. The guest's 5 s window, the lazy-P2P swallow window (~30 s documented),
the 10 Hz diff stream between two fluid full viewports and every steady 60 s cycle are simulation
and code facts; whether a real dual-client session converges inside them is the user's
acceptance pass.
