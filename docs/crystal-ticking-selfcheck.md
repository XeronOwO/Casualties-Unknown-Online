# CrystalUnstable 5 s Pre-Explosion Ticking Sync — Self-Check

## Mechanism

The unstable crystal (`CrystalUnstable`, internal, extends `CrystalEffect`):

- Touch (`Touched`) or attack (`Hit`) → `StartTimer` → `timerStarted = true` +
  body talk "..!" + eyeScareTime + `Sound.Play("crystaltick", …)` (3D, parent is
  the crystal transform) — `CrystalUnstable.cs:31-37`.
- While `timerStarted`, `Update` runs the 5 s ticking visual every frame:
  `crystal.light.intensity += dt * 4f` (glow ramp, line 45) and
  `transform.position = origPos + Random.insideUnitCircle * timer * 0.07f`
  (jitter, line 46); `timer > 5f` → `Object.Destroy` + collider disable +
  `WorldGeneration.CreateExplosion(...)` (lines 47-61).

The `timerStarted`/`timer` latches are PRIVATE (lines 67-70).

## Change

New transient event kind `CrystalUnstableTicked = 32` (ProtocolVersion 23).

- **Report** (`TrapCrystalPatch.UnstableTimerStartPrefix/Postfix`, installed
  DYNAMICALLY on the internal `CrystalUnstable.StartTimer` private method): the
  prefix captures `timerStarted`, the postfix reports the false→true rise.
  This is the ticking's TRUE start (the touch/hit moment), exactly like the
  mine's `pressed` edge. The existing `UnstableUpdatePrefix/Postfix` on
  `CrystalUnstable.Update` keeps reporting the `CrystalUnstableExploded` event
  at `timer > 5`.
- **Replay** (`CrystalStateActions.ApplyCrystalUnstableTicked`, routed by both
  `TrapEffectApplier` (host executor) and `TrapVisualReplay` (guest replay)):
  the receiving side replays the SAME ticking visual — `crystaltick` sound +
  `CrystalTickingReplay` component driving the glow ramp and jitter from ITS
  OWN elapsed clock for 5 s — WITHOUT writing the private `timerStarted`/`timer`
  latches (a written latch would make the local `CrystalUnstable.Update` count
  down and explode the crystal naturally, double-applying the world effects the
  `CrystalUnstableExploded` replay already owns — the mine-press rule, mirrored
  by `MinePressReplayMarker`).
- **Duplicate guard**: `CrystalTickingReplay` component presence + the native
  `timerStarted` check (`CrystalUnstableAccess.IsTimerStarted`) — a local copy
  already ticking natively (its player touched it, the two-trigger race) or
  already replaying drops the duplicate.
- **Architecture split**: the six crystal-family actions moved out of
  `TrapStateActions` (585 lines) into a new `CrystalStateActions` — the new
  method pushed the file over the 600-line gate, and the crystal family is a
  single domain, so the split is both required and cohesive. The old call
  sites (TrapEffectApplier / TrapVisualReplay) now reference
  `CrystalStateActions.ApplyCrystal*` directly (no forwarders).

Transient: NOT a one-shot consumption (not in `EntityEventProfiles`), so a
late joiner never replays an old ticking — the durable `CrystalUnstableExploded`
stays the only snapshot fact.

## Dispatch tables (check-entity-event-dispatch)

All three GameAdapter tables reference the new kind:
`TrapEntityScan` (`"CrystalUnstable" => [Ticked, Exploded]`),
`TrapEffectApplier.ApplyEvent` (host executor),
`TrapVisualReplay.Replay` (guest replay).

## Verification evidence

| Mechanism | Change | Evidence |
|---|---|---|
| Report edge = StartTimer timerStarted rise | dynamic patch on private `StartTimer` | `GameFieldContractTests` locks `CrystalUnstable.timerStarted` (bool); `PatchContractTests.CrystalUnstableTickingPatchSet_IsComplete` asserts StartTimer + Update hooks; patch-contract inventory covers the 8th dynamic patch |
| Replay replays visual, never writes latch | `CrystalTickingReplay` + `CrystalUnstableAccess.IsTimerStarted` guard | `GameFieldContractTests` locks `CrystalBehaviour.light` (untyped); reflective simulation tests |
| Transient not in snapshot | not a one-shot consumption | `EntityEventSimulationTests.CrystalUnstableTicked_*` mirror the MinePressed transient tests |
| Crystal-family split < 600 lines | `CrystalStateActions` | `tools/check-architecture.ps1` passes |
| Full-chains green | 993 tests | `dotnet test` |
| Repo gates | format + event-replay + dispatch | all pass |

**L0 simulation + refinery + static evidence, no manual acceptance**
(development-period no-manual-acceptance rule).
