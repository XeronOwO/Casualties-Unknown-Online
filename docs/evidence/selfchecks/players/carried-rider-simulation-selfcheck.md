# Carried rider keeps its own per-frame simulation — self-check (2026-09-21)

Ticket: `docs/backlog/review/carried-rider-own-body-stops-simulating.md` (decision 216). Cycle scope:
a carried rider's own client must keep the game's per-frame body simulation; only the transform and
the movement input belong to the carry relation.

## Mechanism inventory

| # | Mechanism | Evidence (game) | Change | Evidence (ours) |
|---|---|---|---|---|
| 1 | The rider's own client skipped `Body.Update`, the only caller of `HandleBody` (heart rate + `heartProg`, the ECG animation driver), `HandleVariableUpdates`, `HandleBodyTemperature`, `HandlePeriodicChecks`, `HandleGroundedState`, `HandlePhysics`, `HandleSounds` | `Body.cs:2574-2591` (Update body), `Body.cs:818` (heartRate), `Body.cs:888` (heartProg) | `CarriedBodySimulation.SkipsNativeSimulation` now returns false for a conscious/alive carried body; `BodyUpdatePatch` runs the original Update for it | `CarriedBodySimulationTests` (7 cases), the whole carry/proxy filter 221 passed |
| 2 | `Body.FixedUpdate` and `Limb.Update` skipped for the same body | `Body.cs:2297` (FixedUpdate), `Limb.cs:498` (Limb.Update wound/infection + shader params) | Both prefixes read the same rule; the limb's decision comes from `Limb.body` | `BodyPatches.cs` prefixes; build 0 warnings |
| 3 | The carry placement wrote the body root while the native pass was skipped, and the frozen presentation faked the pose | `Body.cs:3224-3252` (animLimb copy is skipped when not standing) | `ApplyLocalRiderPose` writes only position/velocity/facing/crouch/look and takes the rigidbody out of the integrator; `Body.standing` is never written | `CarriedBodyReleaseTests` entry-point contract; `RenderProxyPoseTests` carry cases deleted |
| 4 | Movement input would fight the placement and play walk/jump/sit poses | `Body.cs:127` (moveForce gate), `Body.cs:2114` (jump gate), `Body.cs:3162` (idle-sit condition) | `BodyUpdatePatch` zeroes `moveDir` and holds the private `movingAllowed` false before the original Update runs | `CarriedBodySimulation.SuppressesMovement` + tests |
| 5 | Release left the body with the frozen presentation | `Stand` re-enables limbs only from a ragdoll (`Body.cs:1687`), so a wrong mode left the body unable to move | `RestoreLocalBody(body, keptNativeSimulation)` restores the mode in force; the simulating rider gets its transform ownership and movement gate back | `CarriedBodyReleaseTests.RestoreLocalBody` two-argument contract |
| 6 | The twitch cadence was unobservable | — | `CarrySimulationTrace`: one Information line per relation change, one Debug line per second (heart progression + rate, pose/movement gates, frames vs placement writes) | log text review; the runtime half is the next real session |

## Red → green record

- Red (before the fix): `CarriedBodySimulation` was first written as a behaviour-preserving extraction
  of HEAD's decision (`SkipsNativeSimulation = isRemoteClone || isLocalCarriedBody`), and the new
  expectations ran against it:
  `dotnet test tests/CasualtiesUnknownOnline.Tests --filter "FullyQualifiedName~CarriedBodySimulationTests"`
  → **2 failed / 5 passed**, the two failures being exactly the reported defect
  (`KeepsNativeSimulation(local carried conscious) == false`,
  `SkipsNativeSimulation(local carried conscious) == true`). No missing type was involved.
- Green (after the fix): same filter 7 passed; family filter
  `--filter "FullyQualifiedName~Carr|FullyQualifiedName~RenderProxy"` → 221 passed / 0 failed.

## Verification

| Claim | How it was checked | Result |
|---|---|---|
| The skip decision is one rule with the intended matrix | `CarriedBodySimulationTests` (conscious rider, remote clone, unconscious, dead, movement gate, ordinary body) | 7 passed |
| Nothing in the carry family regressed | `dotnet test ... --filter "FullyQualifiedName~Carr|FullyQualifiedName~RenderProxy"` | 221 passed / 0 failed |
| The build is clean | `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| The repository gates hold | `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` | 148 passed; the delivery-checklist gate is reset for this cycle and checked at its end |
| The defect-encoding tests are gone with the mechanism | `RenderProxyPoseTests` carry-proxy cases deleted; `RenderProxyPose` overload deleted | no stale caller or test |

## Adversarial review round

An independent reviewer rejected the first cut (3 High / 3 Medium / 4 Low; report at
`%TEMP%/cuo-review-carried-rider.md`). All findings were fixed in the same commit: the movement gate
is handed back unconditionally from the body's own state (H1/H2), the carry relation owns the
rider's limb physics before and after the native pass so a leg-destroyed or shocked rider cannot
have limbs simulating under a teleported root (H3), the native ground pass is skipped for a carried
body so the surface effects and the low-health-block damage query stay with the carrier (M4), the
carrier-velocity coupling is an explicit decision with `grounded` and the velocity in the trace (M5),
the local rider keeps the active sit-clip exit (M6), and the rule doc, the ticket's caller claim and
the trace counters were corrected (L7-L10). The reviewer had no shell tool in its session, so it
could not re-run the build or the suite; every number in this file is this cycle's own measured run.

## Limits

- The automated suite proves the decision, the contracts and the log surface; it cannot see a
  rendering. Acceptance rows 1-3 (a real ECG waveform on the rider's own panel, no limb twitching,
  both directions) need a real two-client session on the deployed build.
- The limb-twitch root cause is not proven by static reasoning: the cycle removed the frozen-proxy
  presentation that the twitch candidates all lived in (fake visual standing, per-frame re-freeze of
  the limbs, pose-input neutralization) and made the cadence measurable, but the confirmation is the
  runtime trace, not this record.
- A dead or unconscious carried body is deliberately out of scope and keeps the frozen presentation;
  its own ticket records what it would take (`todo/carried-unconscious-body-simulation.md`).
