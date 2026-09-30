# A dead or unconscious carried body's own simulation stops

- Status: Done
- Priority: Medium
- Category: Carry/piggyback presentation / own-client body simulation
- Source: Scope boundary recorded while implementing the rider-simulation rule (2026-09-21): `done/carried-rider-own-body-stops-simulating.md` covers a conscious/alive rider, and a dead or unconscious carried body was left with the pinned-ragdoll presentation while this ticket stayed open.
- Related: `done/carried-rider-own-body-stops-simulating.md` (the rule this ticket extends), `done/carry-piggyback-rider-position-smoothing.md`, `docs/evidence/selfchecks/players/carried-unconscious-body-simulation-selfcheck.md`
- Acceptance record: docs/evidence/acceptance/carried-unconscious-body-simulation-20260927.md (batch `20260927-d`: rows 1–4 `pass`)

## The defect

On the rider's own client a dead or unconscious carried body skipped its whole per-frame simulation
(`CarriedBodySimulation.SkipsNativeSimulation` was true for it), so `Body.Update` never ran: an
unconscious teammate who was bleeding did not bleed out while carried, temperature, radiation, the
periodic checks and `Limb.Update` (wound/infection) were frozen, and every peer kept seeing the
vitals of the last simulated frame.

## Why the rider rule did not cover it

A conscious rider stands natively, so the body can keep its own pose while the carry placement owns
the transform. A dead or unconscious carried body is a ragdoll: its pose is physics-driven and it is
pinned rigidly to a moving carrier, so the pose, physics, ground and sound stages of the native pass
cannot run — they belong to the carry relation. The vitals stages can, and they are what this cycle
separated out.

## What landed (2026-09-26 cycle)

The body's own client now runs the VITALS half of the game's per-frame pass under the same pinned
pose, and the rule that decides it is still one place.

- **One three-way decision** — `CarriedBodySimulation.Treatment(isRemoteClone, isLocalCarriedBody,
  alive, conscious)` returns `Mode.Proxy` (a remote clone: presentation only), `Mode.VitalsOnly` (a
  dead or unconscious LOCAL carried body: the vitals stages under a pinned pose) or `Mode.Full` (every
  other local body). `KeepsNativeSimulation`, `SkipsNativeSimulation` and the new `RunsVitalsSubset`
  are views of it, so no patch can disagree with another about which treatment a body gets.
- **The vitals subset runs for `VitalsOnly`** — `BodyUpdatePatch` calls the five vital-sign stages of
  `Body.Update` (`Body.cs:2574-2591`) in the game's own order through cached `MethodInfo`s:
  `HandleVariableUpdates`, `HandleBody`, `HandleBodyTemperature`, `HandleRadiationSickness`,
  `HandlePeriodicChecks`. The counter stage is in because it is the only writer of
  `tempDiffFromNormal` (`Body.cs:3367`), which the circulation stage reads (`Body.cs:712`).
- **The pose, physics, ground and sound stages stay out** — `HandleDogWaterShaking` (a conscious-only
  animation trigger), `HandleGroundedState` (the carrier is the body standing on the terrain),
  `HandlePhysics` (the ragdoll stand timer and the pose), `HandleVisuals` (the pinned presentation
  path still owns the visuals) and `HandleSounds` (the fall scream reads the rigidbody velocity,
  which is the carrier's).
- **The rigidbodies are frozen on both sides of the subset** — `HandleBody` can ragdoll a body
  (`Body.cs:2772-2780`) and `Ragdoll()` re-enables the limb rigidbodies (`Body.cs:1723`); a limb left
  simulating under a root the placement teleports every frame is the twitch family the rider rule
  removes. `Ragdoll()`'s own `standing` guard covers the first `HandleBody` call site; the
  `averagePain > 99` call site is not behind it, so the trailing freeze is what guarantees no limb is
  left simulating under a teleported root.
- **`Limb.Update` follows the same family** — only a remote clone's limb pass is skipped
  (`CarriedBodySimulation.RunsLimbSimulation`); every local body's limbs keep simulating, including a
  carried corpse's, exactly as the game runs them for a body lying on the ground (`Limb.Update` has no
  alive guard and nothing disables the component on death).
- **Dead code deleted with its last caller** — `CarriedBodyDriver.IsCarryingInParent` had no caller
  left once the limb gate stopped asking the carry state.
- **Observability** — the carry trace's `mode=` field now names all three treatments
  (`native-simulation` / `vitals-only` / `pinned-ragdoll`), so a real session's log says which
  simulation a carried body actually got.

No carry authority, wire protocol, save shape or host rule changed.

## Acceptance criteria

| # | Scenario | Expected | Verified by |
|---|---|---|---|
| 1 | A bleeding unconscious player is carried to safety | Their vitals keep advancing on their own client while carried | rule + pin; the real run is the user's |
| 2 | A carried corpse | No change: limp ragdoll presentation, no standing up, no limb state regrown | rule + pin; presentation is the user's run |
| 3 | The carrier moves, turns and crouches with a limp body | The body stays attached and does not come apart | unchanged clone machinery; the user's run |
| 4 | Release | Exactly the pre-carry state, immediately | `CarriedBodyReleaseTests`; the user's run |

Row 2 is read as presentation: the corpse's internal wound/infection numbers keep advancing exactly as
the game advances them for a body lying on the ground, and its pose stays the pinned ragdoll.

## Evidence

- Selfcheck: `docs/evidence/selfchecks/players/carried-unconscious-body-simulation-selfcheck.md`
- Rule: `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/CarriedBodySimulation.cs`
- Subset + pinned path: `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyUpdatePatch.cs`
- Limb gate: `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyPatches.cs`
- Trace: `src/CasualtiesUnknownOnline.GameAdapter/Character/CarrySimulationTrace.cs` + `PlayerInteractionApply.cs`
- Tests: `tests/CasualtiesUnknownOnline.Tests/Session/CarriedBodySimulationTests.cs`,
  `tests/CasualtiesUnknownOnline.Tests/Patching/CarriedBodyVitalsSubsetPinTests.cs`

## Limits

- The automated suite proves the decision, the contract and the stage set; it cannot see a rendering.
  Rows 1-4 need a real two-client session on the deployed build.
- The subset consumes `Random` where the native stages do (`HandlePeriodicChecks`, `Limb.Update`) —
  the same consumption the game performs for any body in that state, but a change of this client's
  RNG stream relative to the frozen presentation.
- `HandlePeriodicChecks` can start native coroutines and play local clips (vomit, cry, growl) exactly
  as the game does for a body in that state; nothing new rides the wire because no capture scope wraps
  those paths.

## Non-goals

- Not changing who owns the carry relation or its authority.
- Not a carry-specific ragdoll network.
