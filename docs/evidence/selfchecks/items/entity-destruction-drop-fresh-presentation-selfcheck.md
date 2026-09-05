# Entity destruction drop fresh presentation — self-check (2026-09-04)

> **Status: Rejected in review (2026-09-05).** User re-tested with a jump-pad
> trap destruction (host destroys the support block). The guest still lacks the
> host-side white border/fresh-drop presentation; the guest also initially sees
> fewer drops than the host. This file is retained as historical evidence; the
> ticket is re-opened as
> `docs/backlog/todo/entity-destruction-drop-guest-fresh-state-loss.md`.

Closes backlog item "Entity destruction drops lose fresh-drop
presentation/initial motion on the guest view". The block-break chain already
materialized the full initial drop state directly from its drop entry; the
destructive-trap/building-death chain only folded `TrapDropEntryMsg` into the
kernel as save-shaped spawn commands, so the guest (and the host when the trap
was guest-triggered) got a world item with zero velocity and no fresh flag.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Block-break chain preserves full drop state | `BlockDropSync` maps `BlockDropEntryMsg` to `WorldItem` including fresh/velocity/rotation/angular velocity |
| 2 | Trap/building drop wire entry already carries full state | `TrapDropEntryMsg.cs:20-27` |
| 3 | Host commits trap drops as kernel spawn commands only | `TrapStateRegistry.ReportBatch` creates `SpawnItemCommand` with `ItemData`, position and no transient motion/fresh fields |
| 4 | Kernel projection intentionally has no transient motion | `KernelBatchItemProjection.ToWorldItem` builds world items with zero velocity, zero rotation, `FreshItemDrop=false` |
| 5 | Entity event replay previously ignored `EntityEventMsg.Drops` | `EntityEventSync.OnRemoteEntityEvent` guest branch replayed only the trap presentation |
| 6 | Guest materialization uses `WorldItem.FreshItemDrop` and frozen start | `RemoteItemSceneOps.SpawnWorldItem` adds `FreshItemDrop` only when `w.FreshItemDrop` is true and freezes guest copies |
| 7 | First position stream tick starts guest local physics | `ItemPositionFollow.StartLocalPhysics` switches kinematic to dynamic and aligns to the host |

## 2. Fix

- **One shared initial-drop mapping** — new
  `Runtime/Session/Items/InitialDropStateMapper.cs` converts both
  `BlockDropEntryMsg` and `TrapDropEntryMsg` to the full `WorldItem`
  presentation. `BlockDropSync` now uses the same mapper, so the two drop
  families cannot drift.
- **Entity-event replay materializes/enriches drops** — `ItemApplication`
  gains `ApplyTrapDropPresentation(IReadOnlyList<TrapDropEntryMsg>)`. It runs
  inside a `RemoteApply` scope: if the item is absent it materializes it with
  the full state; if the kernel batch already created it, it attaches the
  missing `FreshItemDrop` component and writes the captured position, velocity,
  rotation and angular velocity.
- **Both host-apply and guest-replay invoke the same path** —
  `EntityEventSync` passes `ItemApplication`; the host branch applies the
  presentation after the atomic kernel report/broadcast (covers a
  guest-triggered trap on the host), and the guest branch applies it after the
  visual replay (covers host-triggered and third-party views).
- **No wire/protocol change** — no new NetMsg, no `ProtocolVersion` bump, no
  event/entity matrix row touched. The existing `EntityEventMsg.Drops` remains
  the transient presentation source.

## 3. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Trap drop mapping preserves fresh/velocity/rotation/angular | shared pure mapper | `InitialDropStateMapperTests.TrapDrop_PreservesFullInitialState` |
| Block drop mapping still preserves full state | shared pure mapper reused | `InitialDropStateMapperTests.BlockDrop_PreservesFullInitialState` |
| Adapter exposes the entity-drop presentation entry | new `ItemApplication.ApplyTrapDropPresentation` | `EntityDropPresentationContractTests.ItemApplication_ExposesTrapDropPresentation` |
| Kernel remains item-fact authority | no kernel/protocol change | diff touches only Runtime mapper, adapter application path, tests, docs |
| Host sees guest-triggered drops with fresh state | host branch calls the same presentation path | static path in `EntityEventSync` host branch; no manual acceptance by design |
| Guest/third-party sees host-triggered drops with fresh state | guest branch calls the same presentation path | static path in `EntityEventSync` guest branch; no manual acceptance by design |
| Normal block-break path is not regressed | `BlockDropSync` delegates to the same mapping | full test suite passes; block-break tests unchanged |

## 4. Verification (development-period, no manual acceptance)

- **Red**: before implementing the real mapper, the two new mapper tests ran
  against a deliberate no-op mapping and failed at runtime (`Assert.Equal`
  expected velocity 3/0.5, actual 0).
- **Green**: after implementing the mapper, the focused mapper + contract tests
  pass (3/3).
- **Full suite**: `dotnet test CasualtiesUnknownOnline.slnx --no-restore` —
  **2195 passed / 0 failed**.
- **Gates**: `check-architecture.ps1`, `check-event-replay.ps1`,
  `check-entity-event-dispatch.ps1` pass.
- **Format**: `dotnet format CasualtiesUnknownOnline.slnx` run.
- **Runtime acceptance**: not performed; development-period rule is
  simulation/static evidence, user acceptance remains the final step.

## 5. Structure review

- `InitialDropStateMapper` is a small pure static mapper in Runtime; it has no
  Unity/game dependency and is directly unit-testable.
- `ItemApplication` remains a coordinator; the new method delegates scene
  primitives to `RemoteItemSceneOps` and keeps the world-only guard at the
  application boundary.
- `EntityEventSync` gains one collaborator (`ItemApplication`) but does not gain
  state; the dependency direction remains adapter-internal and acyclic.
- No new top-level type exceeds the architecture gate.
