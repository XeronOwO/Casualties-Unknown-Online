# Transient repeatable trap-state checkpoint replay

Date: 2026-09-05
ProtocolVersion: unchanged (no wire change)

## Problem

The host periodically re-sends the kernel checkpoint to in-world members
(`WorldEventSync.Update`, 60 s lazy-session recovery). `WorldEntityKernelProjection`
was projecting every non-one-shot trap-state fact into `TrapSnapshotProjected`.
`TurretFired` and `GeyserActivated` had been mapped to `TrapPhase.Triggered` in
`TrapStateProfiles`, so after the first trigger the kernel kept a permanent
`Triggered` fact and every periodic checkpoint replayed the old turret shot /
geyser eruption as if it were a fresh live event.

For the turret this showed as the reported "走火": after a completed reload, the
checkpoint resend re-ran `ApplyTurretFired` (warning sound + delayed shot
visuals) even though the native turret had already re-armed. Both events are
repeatable cooldown-driven presentation and the entity re-arms natively; there is
no durable state to restore.

## Change

- Added `EntityEventProfiles.IsTransientTrapState` as an explicit profile for
  `TurretFired` and `GeyserActivated` (repeatable cooldown presentation).
- `TrapStateProfiles.Map` no longer maps these two kinds to a kernel phase, so
  new triggers do not create permanent `Triggered` facts.
- `WorldEntityKernelProjection` also filters existing/saved transient trap-state
  facts, so a checkpoint created before this fix cannot replay a stale shot or
  eruption.
- Durable repeatable state (bear-trap clamp/release, lifepod heat) remains in the
  state table and is still projected.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| `EntityEventProfiles.IsTransientTrapState` | New explicit classification set for transient cooldown-driven trap-state kinds | `EntityEventProfiles.cs`; `EntityEventProfilesTests` |
| `TrapStateProfiles.Map` | `TurretFired`/`GeyserActivated` now return `null` (no durable kernel state) | `TrapStateProfilesTests.TransientRepeatableKinds_RemainUnclassified` |
| `WorldEntityKernelProjection` | Skips transient trap-state facts from checkpoint projection | `WorldEntityProjectionTests.GuestCheckpointRestore_FiltersPreExistingTransientTrapStateFacts` |
| Live relay path | Unchanged: live `TurretFired`/`GeyserActivated` still travel through the normal entity-event channel | `EntityEventBehaviorTests` |
| Protocol | No wire change | No new/changed message fields |

## Verification

- `dotnet test CasualtiesUnknownOnline.slnx` — 2219 passed.
- `dotnet format CasualtiesUnknownOnline.slnx` — applied.
- `tools/check-architecture.ps1` — passed.
- `tools/check-event-replay.ps1` — passed (33 events).
- `tools/check-entity-event-dispatch.ps1` — passed (33 kinds × 3 tables).
