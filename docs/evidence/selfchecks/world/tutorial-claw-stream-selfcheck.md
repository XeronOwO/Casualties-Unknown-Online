# TutorialHandler claw 20 Hz flow — mechanism inventory and self-check

Owner cycle: backlog TODO "the claw 20 Hz flow todo stays open for a deliberate
tutorial-domain sync pass". Decision: implement the missing **presentation
stream** — the host's `TutorialHandler` claw pose travels at 20 Hz to guests
that are not running their own course. The full tutorial-course state is not
synchronized in this slice; per-side course state and per-player claw props
remain by design (decision #28).

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The claw visual | `TutorialHandler.Update` (`reversing/Assembly-CSharp/Assembly-CSharp/TutorialHandler.cs:184-292`) moves `handPosCurrent` toward `handPos` every frame (`:223-230`) and selects the claw-arm material from `grabInfo` / `armKnifeSpriteOverride` / `blockQueueEmpty` (`:272-287`). |
| 2 | Per-side course state | `TutorialHandler.main` exists in every process (`TutorialHandler.cs:44,488`) and each side runs its own `TutorialCourse` coroutine. This is the prior accepted boundary (tech-decisions #28). |
| 3 | The missing piece | In a live session the host's claw movement is not visible on a guest that is not running its own course; the game only simulates the local claw per side. |
| 4 | Existing stream pattern | The enemy stream (`EnemySyncService`, 20 Hz unreliable + seq gate) and the player state stream are the established host→guest absolute-snapshot patterns; the claw stream follows the same shape. |
| 5 | Prior KrokMP reference | KrokMP used a 0.05 s `handPos` + grabbed-netid fan-out (`reversing/KrokMP/.../TutorialHandler_Update_MarkiplierPatch.cs:55-73`). CUO replaces the hack with a dedicated absolute presentation message and does not reach into grabbed objects (per-player props remain local). |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `TutorialHandler.Update` | Unchanged — the host continues to run the native course/claw; guests with a local active course keep their local claw. |
| `TutorialClawProp` marker / item-entity domain skip | Unchanged — claw-created props remain per-player until pickup (`tutorial-claw-selfcheck.md`). |
| `TutorialClawRemoteDriver` | New guest-side component; overrides only the arm material in `LateUpdate`, never writes course/prop state. |
| `TutorialClawSync` | New Game Adapter domain; host captures/publishes, guest applies when no local `activeCourse`. |
| `TutorialClawService` / `ITutorialClawControl` / handler | New Runtime stream; one absolute state at the configured cadence, no history buffer. |
| `PacketReceiver` / `DirectionTests` | New host→guest direction row. |
| Protocol version | 28 → 29 (new wire message). |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Host capture | `TutorialClawSync.Update` reads `TutorialHandler.handPos` / `handPosCurrent` / material every frame and publishes | Code path; the Runtime service owns the cadence (no adapter timer). |
| 20 Hz fan-out | `TutorialClawService.Update` throttles to `StateStreamOptions.SendIntervalSeconds` and sends only to handshaken in-world guests | `TutorialClawStreamTests.HostPublish_ReachesInWorldGuest_AtStateStreamCadence`, `NonInWorldGuest_DoesNotReceiveStream`. |
| Unreliable seq gate | `ApplyTutorialClawState` drops `Seq <=` last | `StaleAndDuplicateSequences_Dropped_NewerPass`. |
| Guest apply | `TutorialClawSync.OnTutorialClawStateReceived` sets `handPos`/`handPosCurrent`/arm flag and attaches the remote driver when no active local course | Code path; `TutorialClawRemoteDriver` checks `activeCourse` before overriding. |
| Material presentation | Remote driver resolves `Material` to `clawArmOpen/Closed/Place/Knife` in `LateUpdate` | Static evidence of the material fields and TutorialHandler.Update selection. |
| Wire shape | `TutorialClawStateMsg` round-trips through `NetPacket` | `WireRoundTrip_PreservesClawState`. |
| Direction lock | Added to host→guest list | `DirectionTests.EveryNetMsg_IsExplicitlyClassified` + `HostToGuest_AllowedOnGuest_RejectedOnHost`. |
| No course/prop regression | No change to `TutorialClawProp`, item/entity skip or course state | Full suite green (1072). |

## 4. Verification design (development-period, no manual acceptance)

- L0 simulation: the real `TutorialClawService` + handler over the fake
  network — cadence, in-world gate, seq gate, clear, wire round-trip.
- Static evidence: native `TutorialHandler.Update` material/position path and
  the previous tutorial-claw self-check's per-player prop decision.
- Runtime verification box: **L0 simulation + static evidence, no manual
  acceptance** (user rule 2026-08-16).

## 5. Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1072 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | see below (clean after format) |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | passed |
| `tools/deploy.ps1 -GameDir "<game-dir>"` | deployed to the real game dir only |
| Static evidence | TutorialHandler.cs:223-231,272-287; Tech-decisions #28 |
