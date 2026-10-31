# Batch 20261024-r — a member-pressed mine, applied by the host, with its victims marked

- Ticket: `docs/backlog/review/host-blast-rolls-a-second-drop-set.md`
- Artifact: `0.1.0+62cabe42`, deployed and verified against `<game-dir>` before the run
- Session: host + one member. The mine and a crate were staged by `trap-stage` (the game's own `landmine` and a named
  crate prefab), the mine was PRESSED by the member's client through `trap-trigger` (the field `MineScript.pressed`, i.e.
  the step a player's body takes), and both clients' logs were read.

## What the run read

Two blasts, each: member presses → the game runs its own 0.8 s sequence → the mine explodes natively on the member's
client → the patch reports → the host applies → the scope marks the victims.

| evidence | first blast | second blast |
|---|---|---|
| member's own report | `[TrapEvent] kind=MineExploded pos=(0.0,0.5) origin=Report` | `… pos=(0.0,30.5) origin=Report` |
| host received and applied | `origin=HostApply from <member>` then `host applied mine explosion at (0.00, 0.50)` | same, at `(0.00, 30.50)` |
| **the fix's own line** | **`[TrapBlast] marked 1 blast victim(s) as remote deaths`** | **`[TrapBlast] marked 2 blast victim(s) as remote deaths`** |
| member's drop hold | `[TrapDropPending] kind=MineExploded pos=(0.0,0.5) held for drop collection` | same at `(0.0,30.5)` |

So the member's native trigger reaches the host as a report, the host applies the same blast, and the scope marks EVERY
victim it damaged — one in the first run, two in the second (mine + crate). That is slice 1 doing its job on a live
member-triggered event, which is the duplicate's cause removed.

## What the count row needs that these prefabs cannot give

Both staged crates (`containercrate`, `medcrate`) left **zero** items in either world (`item-world-read` near the crate
position: `0` on both clients), and the member's `[TrapDropPending]` never completed — so neither prefab carries a drop
table when it is placed by hand (the world generator's containers carry loot, not `itemsDropOnDestroy` rows). The
"EXACTLY ONE set" row therefore needs a victim whose table is NOT empty; the census already names one: the **mine itself**
(`rowCount` 1), or a worldgen-placed container.

## Two tooling facts this batch fixed

- `trap-stage` now takes `crate=s`, so the crate prefab is the caller's choice (and `containercrate` is recorded as
  drop-less rather than assumed droppable).
- A recipe body that is "too much" fails on the client as `(1,1): InteractiveHost` with no diagnostic: a version of
  `trap-trigger` with a nearest-point loop plus `Vector2.Distance` failed, and the same recipe reduced to
  "find the mines, set the field" ran first try. Keep a staging recipe minimal and move arithmetic to the batch.
