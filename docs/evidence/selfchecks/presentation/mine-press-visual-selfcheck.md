# Mine 0.8 s press visual sync

Date: 2026-08-18
ProtocolVersion: 22 (`EntityEventKind.MinePressed` — a v21 peer would drop the
transient press visual; the handshake refuses cross-version mixing)

## Problem

The native landmine has a visible two-stage experience:

1. `MineScript.OnCollisionEnter2D` (MineScript.cs:44-51): a non-kinematic
   collider within 50 units presses the mine — `pressed = true`, the `"mine"`
   sound plays, and the sprite changes to `pressedSprite`.
2. `MineScript.Update` (MineScript.cs:28-39): 0.8 s later `exploded` flips and
   the explosion runs.

CUO already synced the explosion (`MineExploded`), but the 0.8 s press visual
was trigger-side-only: peers saw a normal mine suddenly explode. This was a
recorded presentation gap in `../backlog.md` ("mine 0.8 s press visual").

## Change

A new **transient** entity event `MinePressed` (EntityEventKind 31) reports at
the event's true start — the false→true `pressed` edge in
`MineScript.OnCollisionEnter2D` — and replays the press visual on every other
side:

- `TrapMinePressPatch` observes the edge (prefix captures `pressed`, postfix
  detects the rise) and calls `PatchBridge.Impl.OnTrapTriggered(MinePressed,
  position)`.
- `TrapStateActions.ApplyMinePressed` replays the native side of the press:
  `pressedSprite` + the `"mine"` sound. It deliberately does **not** write the
  game's private `pressed` latch — writing it would make the peer's
  `MineScript.Update` count down and explode the mine naturally, double-applying
  the world effects that the `MineExploded` event already replays.
- A tiny `MinePressReplayMarker` component (added by the replay action) owns the
  duplicate guard for this transient one-way edge: a second guest's report of
  the same press returns false and is dropped with the standard trace. The
  marker lives on the mine and dies with it when the `MineExploded` replay
  consumes the entity.
- `MinePressed` is deliberately **not** a one-shot consumption in
  `EntityEventProfiles`: it is a transient visual, not a durable world fact.
  The durable consumption remains `MineExploded`, so the late-joiner snapshot
  cannot be clobbered by a same-position press entry.
- `TrapEntityScan` / `TrapEffectApplier` / `TrapVisualReplay` all dispatch the
  new kind (the entity-event dispatch gate covers the three tables).

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| `MineScript.OnCollisionEnter2D` | Native press (sound + sprite + `pressed=true`) stays the single source of truth on the trigger side | Decompiled `MineScript.cs:44-51` |
| `MineScript.Update` | Native explosion timing unchanged; peers never receive a natural `pressed=true`, so they cannot locally double-explode | `MineScript.cs:28-39`; `TrapStateActions.ApplyMinePressed` |
| `TrapMinePressPatch` | Reports `MinePressed` on the false→true `pressed` edge | `TrapMinePressPatch.cs` |
| `TrapStateActions.ApplyMinePressed` | Replays `pressedSprite` + `"mine"` sound; adds `MinePressReplayMarker`; returns false on already-pressed / exploded / marker | `TrapStateActions.ApplyMinePressed` |
| `MinePressReplayMarker` | Inert CUO-owned duplicate guard on the mine object | `MinePressReplayMarker.cs` |
| Event classification | `MinePressed` stays repeatable-classified / non-snapshot; `MineExploded` remains the durable one-shot consumption | `EntityEventArchives`, `EntityEventProfiles`, `EntityEventSimulationTests.MinePressed_DoesNotClobberMineExplodedSnapshotFact` |
| Late joiner | MineExploded remains the only snapshot fact; a late joiner still sees the explosion replay, not a stale press | `EntityEventSimulationTests.MinePressed_TransientEdge_NotInLateJoinerSnapshot` |

## Why this is safe

- The press visual is pure presentation: sprite + sound. No physics, no damage,
  no world mutation is replayed.
- The receiver's `pressed` latch is untouched, so the peer's own mine never
  runs a natural explosion and cannot double-apply the crater/damage/drop
  pipeline.
- The duplicate guard is explicit (`MinePressReplayMarker`) instead of relying
  on the game's `pressed` latch, because the peer's latch is intentionally not
  set.
- The protocol bump makes old peers refuse the new event instead of silently
  dropping it.

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- L0 simulation:
  - `MinePressed_TransientEdge_NotInLateJoinerSnapshot` — the transient event
    never occupies a snapshot slot.
  - `MinePressed_DoesNotClobberMineExplodedSnapshotFact` — after press +
    explosion, the snapshot carries exactly `MineExploded`.
  - The combinatorial `EntityEventBehaviorTests` automatically run the new kind
    through the star-relay / duplicate / race scenario families.
- Reflective patch/field contracts: `PatchContractTests` resolves the new
  `TrapMinePressPatch` target; `GameFieldContractTests` resolves
  `MineScript.pressed` with the exact `bool` type.
- Gates: `check-architecture`, `check-event-replay` (31 rows),
  `check-entity-event-dispatch` (31 kinds × 3 tables) all pass.
- Full suite: 987 tests green.
- Development-period rule: L0/static evidence; **no manual acceptance**
  (user 2026-08-16 mandate).