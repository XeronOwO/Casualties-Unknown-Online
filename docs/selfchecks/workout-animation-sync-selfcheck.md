# Workout/exercise animation sync — self-check (2026-08-23)

The animation audit noted that `Body.DoWorkout` (`Body.cs:368-435`) makes the
owner play `ExperimentPushups` / `ExperimentSquats` / `ExperimentPlank` and
the matching arms clips, but the render clone had no way to know which
workout was active, so remote players stayed in the standing pose while the
owner exercised.

## 1. Mechanism inventory (evidence-first)

| Mechanism | Evidence |
|---|---|
| The game's workout coroutine | `Body.DoWorkout(Body.WorkoutType)` sets `exercising = true`, plays the body + arms exercise clips, and clears `exercising` when it stops (`Body.cs:368-435`). It exposes no public current-type property. |
| Existing player state stream | `EntityStateMsg` already carries the 20 Hz pose facts (flags + gaze + face timers); `RunCoordinator.PublishBodyState` is the local-body source and `SessionStatePump` applies them to render clones. |
| Clone render path | `RemoteBodyDriver` already tracks discrete pose transitions (sitting/sleeping/lying/swing); `BodyPatches.BodyUpdatePatch` runs `HandleVisuals` on the proxy and re-asserts climbing/crouch from synced state. |
| Wire compatibility | The existing player entity stream is additive-protobuf; adding a byte field is a normal versioned entity-state extension (ProtocolVersion 42 so older peers are rejected before mixed-version rendering). |

## 2. Changes

- **Runtime state** — `PlayerEntity.WorkoutType` + `EntityStateMsg.WorkoutType`
  (ProtoMember 13), round-tripped via `ApplyTo` / `ToEntityStateMsg`.
- **Local capture** — `BodyWorkoutPatch` (Harmony prefix on
  `Body.DoWorkout`) records the requested `Body.WorkoutType` on a
  `LocalWorkoutTracker` component attached to the local body. The
  authoritative active/inactive decision remains `Body.exercising`, so a
  failed guard or stopped coroutine never sends a stale workout.
- **Publisher** — `RunCoordinator.PublishBodyState` sends
  `workoutTracker.WorkoutType` only while `body.exercising` is true, else 0.
- **Replay** — `SessionStatePump` detects a `WorkoutType` change and plays the
  matching clip pair on the render clone; returning to 0 clears the
  `exercising` animator flag and restores `Grounded` on both animators.
- **Pure rule** — `WorkoutPresentation` owns the byte → clip mapping so the
  visual rule has an L0 test face.
- **No new NetMsg** — the fact rides the existing 20 Hz `PlayerState` /
  `PlayerStateReport` entity stream. `ProtocolVersion`: 41 → 42.

## 3. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx --no-restore` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-restore` | 1319 passed |
| `WorkoutAnimationSyncTests` | 7 passed |
| `EntityStateRoundtripTests` additional workout cases | 3 passed |
| `dotnet format CasualtiesUnknownOnline.slnx --no-restore` | passed |
| `tools/check-architecture.ps1` | passed |
| `tools/check-event-replay.ps1` / `tools/check-entity-event-dispatch.ps1` | no event mechanism touched |
| Manual acceptance | Not required by the developer-cycle rule; L0 + static evidence, no manual acceptance. |

## 4. L0 proof

- `WorkoutAnimationSyncTests.ClipMapping_*` exercises the exact byte → clip
  mapping used by `SessionStatePump.ReplayWorkout`, including unknown/zero
  values.
- The patch-surface test locks the `Body.DoWorkout` prefix parameter names
  and types; the generic `PatchContractTests` also auto-verifies the new
  `[HarmonyPatch]` contract against the game assembly.
- `EntityStateRoundtripTests.WorkoutType_*` proves the wire field is applied
  into the entity buffer and published back, including the 0-clears case.

## 5. Structure review

- `WorkoutPresentation` is a pure one-concern mapper (no Unity state).
- `LocalWorkoutTracker` is a tiny local-body marker; it is never added to
  render clones.
- `BodyWorkoutPatch` is a thin adapter with no cross-call business state.
- `RunCoordinator` remains under the 600-line gate; the added publishing line
  is in its existing state-shuttle responsibility.
- No new wire message, no duplicate periodic channel, no dead mechanism left
  behind.
