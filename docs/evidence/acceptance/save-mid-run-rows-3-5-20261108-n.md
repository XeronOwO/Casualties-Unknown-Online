# Acceptance — the mid-run cut's entity, in-flight and fingerprint rows (batch 20261108-n)

Ticket: `docs/backlog/done/save-mid-run-consistent-cut.md`, rows 3, 4 and 5.
Session: the row-1/row-2 batch (`20261108-n`), host in world `w-20261108-9711`; the host quit and
relaunched (`20261108-n2`, host started 11:32:46) for row 5's second entry. The clients were left running.

## Row 3 — buildings, traps, fluids, enemies

Fixture on the live host (body at `(3.085, 497.615)`):

| step | device | reading |
|---|---|---|
| baseline | `world-object-census` | `total 1847` (prefab-grouped; the largest groups are the map's plants and crates) |
| baseline | `enemy-table-read` | `count 43` |
| stage | `trap-stage x=3 y=497 dx=3 crate=foodbox` | `mineX 3, mineY 497.5, crateX 6, crateY 497.5, mineFound true, crateFound true` |
| fire | `trap-trigger x=3 y=497` | `mines 1, mineX 3, mineY 497.5, health 100` — the game's own pressed flag, then its native explosion |
| after the blast, +3 s | `world-object-census` | `total 1842` |
| after the blast, +3 s | `item-world-read x=3 y=497 radius=10` | `count 0` (the trap's drop hold: the event waits for the death branch's items) |
| cut | `/save` | `Cut Command committed … revision 5231, mid-run cut taken at frame-end, … 322 item row(s) … 117 world-block row(s)` |
| continue | `leave-world` → `continue-run` | world restored, host in world |
| after the Continue | `world-object-census` | `total 1842` — the same as the post-blast count |
| after the Continue | `enemy-table-read` | `count 43` — the same as the baseline |
| after the Continue | `item-world-read x=3 y=497 radius=10` | `count 3` at `(3.0, 497.5)`, `(6.0, 495.825)`, `(0.232, 493.712)` — the trap's drops |

Settled: the mine and the crate stay consumed across the cut and the Continue (the census is five lower
than before the staging and does not return), the enemy table holds its row count, and the trap's drops
are carried by the cut and come back in the live world — no re-trigger and no resurrection at the level
the census can see.

Limits: the census is prefab-grouped, not an entity-by-entity signature; the trap's drops were read by
position, not by comparing each drop's identity to a pre-cut list; fluids are proven by the cut's
`252 fluid chunk(s)` row and the restore line, not by a per-cell comparison; opened lockables were read
in an earlier batch (`save-mid-run-open-lockables-20261107-v.md`).

## Row 4 — the in-flight policy

The policy table (`src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldTransientPolicy.cs`)
names one verdict per in-flight class: `ResolveBeforeSave` for the block-break, trap-drop and drop
windows; `DropWithLog` for the three medical session services, the craft batch, the deferred entity
report, the item motion path, the native run fields, the world clock and the earthquake countdowns;
`Capture` for `NativeWorldFacts`.

A `/save` at an ordinary moment committed: `Cut Command committed … revision 6053, mid-run cut taken at
frame-end, 9 file(s) … 329 item row(s), 3 player row(s), 217 enemy row(s), 252 fluid chunk(s), 14
world-entity row(s), 1 character(s), 232 world-block row(s), 137 transient row(s) …` — the cut's report
carries the transient rows it took.

Not driven: the `ResolveBeforeSave` deferral window is one to two frames, and the driver's per-call
latency (about a second) cannot land a `/save` inside it; the deferral line exists in the product
(`the … cut is waiting for in-flight state to resolve`), and the seam's tests pin both the deferral and
the deadline. A `DropWithLog` class needs a live medical session or craft batch on the host: the craft
drive ran `rippeddressing` to completion in its own call (the craft coroutine finishes inside `TryMake`,
so the save that followed took a quiescent world), and the member-side path was blocked — Steam1 sat in
the host's lobby (`already-in-lobby`) while `inWorld` stayed false after `leave-world` and a fresh
`join-lobby`, so no patient existed to open a remote medical session on. Restarting a client for the
fixture is forbidden (the stuck state is the reading), so row 4's in-game half stays partial.

## Row 5 — save → load → save → load, the fingerprint

The product's own `[WorldFingerprint]` line is logged at a world entry (RunCoordinator). The session's
entry line:

```
1024x1024: 2CD0B09BB739F96A 30AE700474CC68A7 9346F2929DC7386A BBDCAEB5737C5CB8
           E02550E6B4C21F96 0937D79875D4FB8F 8BAF6EBF4DF61E94 FE4C961DFCC39F10  total 52D60D97F79E11F3
```

After the row-2 cut, the quit and the relaunch, `continue-run` entered the world and logged:

```
1024x1024: 2CD0B09BB739F96A 30AE700474CC68A7 9346F2929DC7386A BBDCAEB5737C5CB8
           E02550E6B4C21F96 0937D79875D4FB8F 8BAF6EBF4DF61E94 FE4C961DFCC39F10  total 52D60D97F79E11F3
```

— identical, chunk for chunk. The mid-session re-captures (`world-fingerprint`) differed in the eighth
chunk on each call (`1D49934DB53C6DA5`, `83517D7988A97F53`, `F9D60FC8CC9B1DFD`): the live scene's last
chunk changes between two captures, so the entry line is the comparable instrument, not a mid-frame
re-capture.

Limit: one in-game save/load cycle was read (the machine test
`WorldSaveContinueTests.TryContinue_Twice_KeepsTheSameFingerprintAndFacts` is the two-load half).

Row 4 — the in-flight window, read three ways (2026-11-08, the same session).

The wait is unit-proven: `tests/CasualtiesUnknownOnline.Tests/Persistence/WorldSaveCutSeamTests.cs`
covers it directly — `BreakWindow_Resolved_TakesTheCutOnTheNextFrame` (a live `TrapDropHoldKey` value 1
answers `WorldCutResult.Deferred` and the next frame takes the cut), `BreakWindow_OutlastingTheDeadline_IsNamedInTheReport`
(a stuck `DropFlushKey` defers to `WorldCutDeferral.MaxFrames` and the cut then names the drop report(s) it
could not take) and `ReArmingTheSameTrigger_KeepsItsDeferralWindow` (a re-arm does not restart the wait).

Three live instruments asked the product's own `IWorldSaveControl.TryRequestCut(WorldCutReason.Command)` and
read the answer plus the host log:

1. `cut-during-drop type=dogfood` — the game's own release path (`PlayerCamera.HandleReleaseDragging`) drops a
   carried item and the cut is requested in the SAME frame: `{"ok":true,"item":"dogfood","dropped":true,
   "asked":true,"refusal":null}`; the host log carries the drop trace
   (`[ItemTrace] op=67 begin item=1434840057795 origin=OnItemDropped event=Drop`) and the cut commits
   (`Cut Command committed … revision 2252`) — no waiting line.
2. `trap-stage x=0 y=493 dx=2 crate=foodbox` → `mineX 0, mineY 493.5`, `trap-trigger` → the mine explodes
   natively 0.8 s later; `cut-request` ~900 ms after the press → `asked:true, refusal:null`; the log carries
   `[TrapDropPending] kind=MineExploded pos=(0.0,493.5) held for drop collection.` and
   `[TrapDropFlush] … drops=1.`, and the cut commits (`revision 2235`) — the hold had already flushed when the
   request arrived.
3. `cut-during-trap` (press + request in one call) — the explosion lands after the press, so the request precedes
   the pending state; the cut commits.

Limit: at the console's frame boundary every transient resolves inside the frame it is created (the drop's
flush registers in that frame; the trap's hold is shorter than one driver round trip), so the deferral LINE is
observable only through the seam test. The live half reads that each attempted in-flight state resolved
without loss: no `[TrapDropPending]` record survived to a cut, and the dropped item is in the cut's rows.
