# Acceptance — the mid-run cut's entity, in-flight and fingerprint rows (batch 20261108-n)

Ticket: `docs/backlog/review/save-mid-run-consistent-cut.md`, rows 3, 4 and 5.
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
the deadline. A `DropWithLog` class needs a live medical session or craft batch on the host, and neither
was staged this round.

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
