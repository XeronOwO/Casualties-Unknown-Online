# A carried rider's own body stops simulating (flat ECG, twitching limbs)

- Status: Review
- Priority: Critical
- Category: Carry/piggyback presentation / own-client body simulation
- Source: User acceptance finding (2026-09-21): the guest carries the host on their back; on the host's own screen the host's own medical panel shows an almost flat ECG although the heart rate is real (only occasional tiny waveforms), and the panel's limbs twitch with a frequency that keeps growing; releasing the carry restores normal behaviour.
- Related: `todo/carry-piggyback-rider-position-smoothing.md` (the same mechanism), `review/carried-player-idle-sit-suppression.md`, `review/carrier-sit-while-carrying.md`, `review/carry-piggyback-vertical-placement-asymmetry.md`, `review/remote-medical-panel-acceptance-issues.md`

## Evidence

- `CarriedBodyDriver` (GameAdapter/Character) states the mechanism: "While this component is present
  the body's own per-frame simulation is skipped (BodyPatches treats it like a render proxy) and
  GameAdapter drives its transform from the carrier's entity state each frame". The apply side adds
  that component and sets `body.standing = false`
  (`PlayerInteractionApply.ApplyCarryStateToBody`).
- Everything the game advances inside `Body.Update` therefore stops on the carried player's OWN
  client. The codebase already patches the symptoms one at a time: `FacePresentationVitals`
  documents "A render clone's `Body.Update` is skipped, so these body values would otherwise stay
  at the template defaults", and the remote medical display body's `heartProg` had to be advanced
  by hand so the redirected ECG animates (see the remote medical panel acceptance record). The
  reported flat ECG is the same family, now on the rider's own body, where no display projection
  exists to refresh it.
- The limb twitching and its growing frequency are NOT root-caused yet. Candidates to prove or
  exclude, in order: the render-proxy limb handling (`RenderProxyPose.EffectiveVisualStanding` with
  `isCarryRenderProxy`), the per-frame carry placement writes fighting the native visual pass, and
  the report-rate cadence driving corrections. The implementation cycle must make the cadence
  observable (log/trace) and confirm the cause before fixing it.

## Goal

A carried rider's own client keeps its body's native per-frame simulation running; the carry
relation drives position and pose through the game's own mechanisms instead of freezing the body.
Vitals, heart progression, breathing, moodles, facial state and limb animation keep advancing while
carried, and no new "advance this value by hand" workaround is added.

## Acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest carries the host; the host opens their own medical panel | The real heart rate and a normal ECG waveform |
| 2 | Host carries the guest; the guest opens their own medical panel | Same, reverse direction |
| 3 | The carried rider's own screen, every limb | Normal animation, no twitching and no frequency growth |
| 4 | The carrier moves, turns and crouches | The rider stays attached, no regression of the attach guarantee |
| 5 | Breathing, moodles, facial expression and the other per-frame body values while carried | Advance normally on the rider's own client |
| 6 | Release | Exactly the pre-carry state, immediately |
| 7 | A third peer watching the carry | Unchanged |
| 8 | The family's existing suppressions (`carried-player-idle-sit-suppression`, `carrier-sit-while-carrying`, the vertical placement asymmetry) | Re-evaluated against the new mechanism; each kept or deleted with a reason |

## Non-goals

- Not client prediction and not a new carry-specific render network.
- Not changing who owns the carry relation or its authority.

## Notes

Whatever the design turns out to be, it must state how a carried body's own simulation coexists
with the carrier's authoritative position. A wire or protocol change is acceptable and is made in
the same change as the behaviour (the pre-release policy applies).

## What landed (2026-09-21 cycle)

A LOCAL carried body is no longer treated as a render proxy: the carry relation owns its TRANSFORM,
not its simulation. Recorded as decision 216.

- `CarriedBodySimulation` (new, `src/CasualtiesUnknownOnline.Runtime/Session/EntitySync/`) makes the
  skip decision in ONE place for `Body.FixedUpdate`, `Body.Update` and `Limb.Update`: a remote clone
  skips (presentation proxy), a conscious/alive carried rider does not, and a dead/unconscious
  carried body keeps the frozen pinned-ragdoll presentation (its pose is a physics ragdoll pinned to
  a moving carrier — recorded as its own ticket, `todo/carried-unconscious-body-simulation.md`).
- `BodyUpdatePatch` now gates only the rider's movement input before the original Update runs:
  `moveDir` is zeroed and the game's own private `movingAllowed` is held false, which is the
  movement-force (`Body.cs:127`) and jump (`Body.cs:2114`) gate AND the native idle-sit condition
  (`Body.cs:3162`) — the rider can neither walk off the carrier's back nor sit down while carried.
  `Body.standing` is no longer written at all, so the body's own state decides the pose.
- `CarriedBodyPlacement` splits the two riders: `ApplyLocalRiderPose` (position, velocity, facing,
  crouch pose, look target, and `rb.simulated = false` so the game's own integrator cannot fight the
  placement) for the local rider, `ApplyRidePose` (the same follow plus the proxy pose gates) for the
  remote rider clone. `RestoreLocalBody(body, keptNativeSimulation)` restores exactly the mode that
  was in force: a simulating rider gets its transform ownership and movement gate back, the
  pinned-ragdoll case keeps the physics/pose restore.
- `RenderProxyPose` loses its carry-proxy overload: a local rider never reaches the proxy path.
- `CarrySimulationTrace` (new) makes the report measurable in a real session: one Information line
  per carry-relation change (mode, pose, rigidbody and the ECG driver's value) and a per-second Debug
  line carrying the heart progression and its rate, the pose and movement gates, and the frame versus
  placement-write counts — the cadence a twitch would show up in. The per-second line needs
  `Logging.MinimumLevel=Debug`; the relation lines are Information.

### Suppression family re-evaluated (acceptance row 8)

| Family member | Verdict | Reason |
|---|---|---|
| `review/carried-player-idle-sit-suppression.md` | KEPT, mechanism changed for the local rider | The rider's own sit is now impossible natively (`movingAllowed` false removes the `Body.cs:3162` condition) and the idle timer is held at zero as belt-and-braces; the clone halves still need `ShouldZeroIdleTimer`/`ShouldExitSit`/`ShouldReplaySit` because a clone's idle timer and a stale `Sitting` snapshot are not driven by a simulation. |
| `review/carrier-sit-while-carrying.md` | KEPT unchanged | The carrier is a fully simulating local body, so its idle timer still has to be held at zero by the patch to keep the native sit pose off the carry relationship. |
| `review/carry-piggyback-vertical-placement-asymmetry.md` | KEPT, scope narrowed | The rule itself only takes `isCarried` (`ShouldPublishBodyRoot(bool isCarried) => isCarried`); what narrows its reach is its only caller, `RunCoordinator.PublishBodyState`, whose torso anchor is `!body.standing && !ShouldPublishBodyRoot(...) && body.limbs.Length > 1`, so it can only matter for a carried body that is not standing. |
| `todo/carry-piggyback-rider-position-smoothing.md` | STAYS OPEN | Its own reopened note tracks this ticket's defect; the teleport/mismatch acceptance it carries was not re-tested in this cycle and still needs a real two-client run. |

### Adversarial review round (same cycle)

An independent reviewer (fresh context, frozen working tree; full report at
`%TEMP%/cuo-review-carried-rider.md`) rejected the first cut with 3 High / 3 Medium / 4 Low
findings. Every finding was fixed in the same commit:

- **H1/H2 — the movement gate could stay shut.** `RestoreLocalBody` handed the game's private
  `movingAllowed` back only in one of its two modes, and that mode argument made the other branch
  unreachable: a rider picked up conscious and released while unconscious kept
  `movingAllowed == false` (its only writers are the nap coroutines, `Body.cs:2506`/`:2512`/`:2523`/
  `:2527`), i.e. unable to walk or jump after revival. The restore now takes no mode at all and
  hands the gate, the root physics and the limb physics back from the body's OWN current state, so
  no release path can leave the player stuck and no recorded mode can go stale.
- **H3 — a conscious rider whose legs are gone or who is in shock is ragdolled by the native pass
  every frame** (`HandleBody`, `Body.cs:2772`) and `Ragdoll()` re-enables limb physics
  (`Body.cs:1723`), while the placement teleports the root every frame. The first cut left those
  limbs as free physics bodies under a moved root — the very twitch family this ticket exists to
  remove, in exactly the population a carry exists to move. The carry relation now owns the rider's
  physics: body and limb rigidbodies are frozen before the native pass AND re-frozen after it, so a
  rider the native pass collapses stays limp but frozen instead of jittering, and a healthy rider
  keeps the animator-driven standing pose the old frozen presentation also produced.
- **M4 — no ground contact while carried.** The landing dust, the `Grounded` clip, the
  impact/footstep sounds, the toxicity and slippery reads and the low-health-block damage query
  (`Body.cs:2702-2711`) belong to the body that actually stands on the terrain.
  `HandleGroundedState` is therefore skipped for a carried rider that simulates
  (`CarriedBodySimulation.CarrierOwnsGroundContact`), `grounded` stays false and the stale
  wall-slide flags are cleared — which also removes the doubled landing the rider would otherwise
  have heard on top of the carrier's own relayed sound, and keeps a guest rider from editing the
  world from a position it is only carried at.
- **M5 — the carrier-velocity coupling is an explicit decision, not an accident.** The carry follow
  keeps copying the carrier's velocity into the rider's rigidbody: the rider IS moving at that
  velocity, that value is what the 20 Hz stream publishes for the peers' clone, and with the ground
  pass skipped the two things it used to arm (the auto-stand at `Body.cs:2364-2367` and the landing
  impact at `Body.cs:2716-2737`) are out of reach. What remains is the fall scream (`HandleSounds`,
  `Body.cs:2751`): a rider whose carrier falls fast screams on the rider's own client. The
  per-second trace logs `grounded` and the velocity so a real session can read the coupling.
- **M6 — the local rider keeps the active sit-clip exit.** Zeroing the idle timer cannot leave an
  already-playing `ExperimentSit`/`ArmsSit`, and the native exit branch needs a non-idle frame
  (`Body.cs:3145-3166`) which a rider with gated input and a stationary carrier never produces. The
  rider's own path asserts the exit after the native pass, exactly as the proxy path does.
- **L7-L10** — the rule's doc no longer implies "conscious implies standing"; the suppression row
  above now quotes the caller its claim is about; the trace counts one frame per carried frame and
  one carry-follow per written frame (the two numbers only mean something when a missing carrier
  anchor shows up), counts the first frame of each window, and logs `grounded` and the velocity.

The reviewer stated its own limits with the report: it had no shell tool in its session, so it could
not re-run the build or the suite (every number in this ticket is this cycle's own measured run),
and its caller-level checks of `RemotePlayerRenderer`, `RunCoordinator`, `SessionStatePump` and
`RagdollPoseApplication` were outside its budget — those paths are unchanged by this cycle and
remain covered by decision 216's reasoning and the clone code.


## Verification

- Red before the fix, against a behaviour-preserving extraction of the pre-fix rule (the extraction
  keeps HEAD's decision: `SkipsNativeSimulation(remote clone, local carried body) = true`):
  `dotnet test tests/CasualtiesUnknownOnline.Tests --filter "FullyQualifiedName~CarriedBodySimulationTests"`
  = 2 failed / 5 passed. The failures are the reported defect (`KeepsNativeSimulation(local carried
  conscious) == false`, `SkipsNativeSimulation(local carried conscious) == true`), not a missing type.
- After the fix: the same filter = 7 passed; the whole carry/proxy family
  (`--filter "FullyQualifiedName~Carr|FullyQualifiedName~RenderProxy"`) = 221 passed / 0 failed;
  `dotnet build CasualtiesUnknownOnline.slnx` = 0 warnings / 0 errors.
- `RenderProxyPoseTests`' two carry-proxy cases are deleted together with the overload they pinned
  (they asserted the defect: a frozen local rider presented as standing); `CarriedBodyReleaseTests`
  pins the two-argument restore plus the new `ApplyLocalRiderPose` entry point.
- The per-second trace and the relation lines are the runtime half: they turn "the simulation ran"
  and the correction cadence into log facts for the next real session.

## Remaining boundary

Rows 1, 2 and 3 of the acceptance matrix — the real ECG waveform on the rider's own panel and the
absence of limb twitching, in both directions — can only be confirmed in a real two-client session
on the deployed build. The automated suite proves the skip decision, the physics/pose/restore
contracts and the log surface; it cannot see a rendering. Row 4's attach guarantee rides the
unchanged clone machinery (mount/one shared follow), and row 7's third-party view is unchanged by
construction: no clone path was touched.

Named limits this cycle did NOT close, each with what would close it:

- **The frame ordering between the carry follow and the native pass** (`GameAdapter`'s Update pump
  vs `Body.Update`/`Body.FixedUpdate` vs the LateUpdate carrier pin) is a Unity execution-order
  fact; the freeze and the single-argument restore make the outcome order-independent in the cases
  examined, but the visual stability of a moving carrier stays a run-verified claim (the repo's
  rejected rider-teleport ticket was explicitly not re-tested here).
- **The fall scream coupling** (M5): the rider's client can scream when the carrier falls fast,
  because the follow copies the carrier's velocity. Accepted deliberately for this cycle and
  measurable through the trace's velocity and `grounded` fields.
- **A rider revived while carried, or one that was already limp**, stays limp until release: the
  animator limb copy needs `standing`, and the native re-stand condition is out of reach under a
  carried body (review area 1). The trace's `standing` field shows which pose a real session is in.
- **A carry state that arrives while the local body does not exist is retained** for the body that
  appears next (`PlayerInteractionApply._pendingCarryState`) — deliberate for a world that is still
  generating, but a relation that ended during a scene change could re-attach to the next world's
  body. Closing it needs a session/scene edge for the pending state.
