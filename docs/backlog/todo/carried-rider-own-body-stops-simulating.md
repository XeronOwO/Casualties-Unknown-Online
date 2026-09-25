# A carried rider's own body stops simulating (flat ECG, twitching limbs)

- Status: Todo
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
