# Auto turret trap fires unexpectedly after reload ("走火")

- Status: Review
- Priority: Medium
- Category: Trap/entity sync / turret presentation
- Source: User report (2026-09-04) — the automatic turret trap sometimes fires a shot unexpectedly after it has finished loading/reloading. Implemented in the 2026-09-05 cycle; waiting for unified acceptance.

## Goal

Eliminate turret "走火" (stray/phantom firing) so a turret only fires when it should, according to the trigger-side authoritative event, and never fires an extra shot after a reload.

## Root cause

The host re-sends the kernel checkpoint to in-world members every 60 seconds
(`WorldEventSync.Update`, lazy-session recovery). `WorldEntityKernelProjection`
projected every non-one-shot trap-state fact, and `TrapStateProfiles` had mapped
both `TurretFired` and `GeyserActivated` to a permanent `TrapPhase.Triggered`.
Consequently:

1. The first turret shot created a durable kernel fact that never transitioned
   to `Cooldown`/`Armed` (the native 15 s reload is not recorded).
2. Every periodic checkpoint replayed that old `TurretFired` fact through
   `TrapStateActions.ApplyTurretFired`, re-running warning + delayed shot
   visuals even though the native turret had already reloaded and re-armed.
3. The same defect existed for `GeyserActivated` (native cooldown re-arms the
   geyser, but the old eruption was replayed by checkpoint resends).

This is therefore a stale checkpoint-projection/protocol issue, not a new
native turret fire path. The dog-food ticket remains separate; no evidence
proved a shared root cause.

## Implementation (2026-09-05)

- Added `EntityEventProfiles.IsTransientTrapState` with an explicit set:
  `GeyserActivated`, `TurretFired`.
- `TrapStateProfiles.Map` no longer classifies these two kinds as durable kernel
  state; new triggers do not create permanent `Triggered` facts.
- `WorldEntityKernelProjection` filters transient trap-state facts from the
  checkpoint projection, so checkpoints created before this fix cannot replay a
  stale shot/eruption either.
- Durable repeatable state (bear-trap clamp/release, lifepod heat) remains in the
  kernel state table and is still projected.
- Live `TurretFired`/`GeyserActivated` relay/replay is unchanged; only the
  periodic checkpoint projection is corrected.
- Evidence: `docs/evidence/selfchecks/world/transient-trap-state-checkpoint-selfcheck.md`.

## Verification

- Added red-green regression tests:
  - `GuestCheckpointRestore_SkipsTransientRepeatableTrapStates`
  - `GuestCheckpointRestore_FiltersPreExistingTransientTrapStateFacts`
  - `TransientRepeatableKinds_RemainUnclassified`
  - `IsTransientTrapState_MatchesTheDeclaredTable`
- `dotnet test CasualtiesUnknownOnline.slnx` — 2219 passed.
- `dotnet format CasualtiesUnknownOnline.slnx` — applied.
- `tools/check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` — passed.

## Acceptance criteria (for the later implementation cycle)

- The turret does not fire unexpectedly after a completed reload.
- Periodic checkpoint resends do not replay old turret/geyser transient events.
- Every visible shot corresponds to an actual live turret engagement event, not a stale checkpoint replay.
- Host and guest views are consistent; no extra sound/tracer/damage without an authoritative trigger.
- Existing turret/trap tests and repo gates remain green.

## Non-goals

- Not changing turret behavior outside CUO sync.
- Not merging with the dog-food ticket (separate investigation).
- No wire/protocol change.
