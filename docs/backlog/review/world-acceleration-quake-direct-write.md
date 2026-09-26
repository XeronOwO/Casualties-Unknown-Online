# An earthquake's direct Time.timeScale write ends a session acceleration

- Status: Review
- Priority: Medium
- Category: World / session (world-time)
- Source: The family audit of the movement-routing fix (decision 223, 2026-09-25): every `PlayerCamera.SetTimeScale` call is now classified, but the game also writes `Time.timeScale` DIRECTLY, and the host pump adopts such a write as the new shared speed by design (`WorldTimeSync.AdoptDirectTimeScaleWrite`).
- Related: `review/world-acceleration-survives-movement.md` (the routing fix this is the remaining sibling of), `review/world-time-local-initiation.md`, `resolved/sleep-behavior-policy.md`, `docs/decisions/active.md` #223 and #226

## The ruling (2026-09-26): vanilla stands, the write is never suppressed

The open question was the gameplay one — may an earthquake interrupt an acceleration the player
started? The owner's ruling was "follow vanilla", and vanilla answers it in its own code: an
earthquake start sets the clock back to normal (`WorldGeneration.Update`: `Time.timeScale = 1f;`,
reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:870). CUO therefore does NOT suppress
the game's write: the host adopts it as the shared speed and the session acceleration ends on every
screen. The ticket's other branch (the acceleration stands through the quake) is closed by that
ruling, and `WorldTimeDirectWrite` carries no suppression verdict at all — re-opening it means
re-ruling it.

## The direct-write family (census, 2026-09-26)

| Writer | Site | What CUO does with it |
|---|---|---|
| Earthquake start | `WorldGeneration.cs:870` | Adopted on the host, so the session leaves the acceleration. A guest never runs it: its quake timer is frozen by `WorldGenerationUpdatePatch` and only the host's broadcast turns its `earthquakeTime` positive |
| Console `timescale` command | `ConsoleScript.cs:815` | Adopted on the host — the admin's value becomes the shared speed, which is what keeps its meaning; on a guest the pump puts this screen back on the host's speed |
| Run start | `PreRunScript.cs:64` | The pump owns no world yet (no session body at the main menu), so it never reaches the rule |
| Scene reload | `WorldGeneration.cs:1036` | The start gate owns the clock across a reload and the pump returns before the rule |

The rule cannot tell these apart by VALUE and does not try to — the quake and the console write the
same numbers. The SIDE decides: the host's live clock is the shared clock, a guest's is not.

## What landed (2026-09-26)

- **A pure rule.** `WorldTimeDirectWrite` (Runtime/Session/World, no Unity dependency) returns
  `Verdict.Adopt` on the host and `Verdict.Restore` on a guest for a live clock that is a domain
  speed other than the applied one, and `Verdict.None` when the live value is not a domain speed
  (Paused, Slowmo and a correction ramp all map to null) or already is the applied speed. There is
  deliberately no suppression member, by the ruling above.
- **The host's adoption re-states the game's own speed state, silently.** The native write moves
  `Time.timeScale` but NOT `PlayerCamera.curTimeScale`, which is what lights the HUD's speed icons
  (`HandleTimescaleIcons`, PlayerCamera.cs:2146-2159: the `x1` text reads the live clock while the
  icon row reads the field). Before this cycle the host's icon row kept showing the acceleration an
  earthquake had just ended, until the next deliberate speed key — vanilla heals that on the next
  movement, and CUO's movement ruling removed that heal. The adoption now writes the adopted value
  through the normal apply path with `switchSound: false, force: false`: no dip (the clock already
  runs it) and no sound (the native reset was silent).
- **The guest's correction is the same rule, behaviour unchanged** (`EnforceAppliedSpeed`): the
  shared clock is the host's, so a drifted guest clock is put back on the applied speed.
- **The sound is unchanged and recorded.** The host stays silent (vanilla's reset is silent) and the
  guests receive the session's standard speed-change cue when the adopted speed arrives. Nothing in
  this cycle changes either; the self-check records the asymmetry as a decision the acceptance run
  can revisit.
- **No wire, protocol, save or gameplay change.** The mechanism is local bookkeeping on the side
  that owns the clock.

## Verification

| # | Acceptance | Evidence |
|---|---|---|
| 1 | An earthquake during a standing acceleration ends it on every screen, and the HUD follows the actual clock | the rule matrix (`WorldTimeDirectWriteTests`); the mechanism pin with its negative samples (`WorldTimeDirectWritePinTests`); the silent re-state in `WorldTimeSync.AdoptDirectTimeScaleWrite` |
| 2 | The console's direct write keeps its meaning (the admin's value becomes the shared speed) | the rule is value-blind and adopts on the host — the console row of the census, and the `SuperFast → Normal` adoption cases of the matrix |
| 3 | The scene-reload and run-start writes keep their meaning | the pump owns no world there (census table); no code touched on those paths |
| 4 | The rejected alternative stays rejected: CUO never swallows the game's own write | `TheRulingHasNoSuppressionVerdict` (the enum census) and the pin's suppressed-adoption negative sample |
| 5 | Build, tests, gates and format | the ladder's own runs — focused, the full suite WITH build, the normative gates and `dotnet format`, with the counts and the captured files in the self-check's §5 |

The on-screen half — a real earthquake during a real acceleration in a two-client run — is the
unified acceptance pass's row, as every screen-only claim is; the self-check's §7 states the limits.

The red that preceded the fix was captured with the adapter's two methods reverted to HEAD while the
rule type stayed present: the rule's own matrix passes there (it exercises the pure type, not the
adapter), and the pin's cases are what fail — the count and its decomposition are recorded in the
self-check's §5 rather than restated here.

## Non-goals

- Not suppressing the native write (ruled out above) and not adding any CUO quake policy.
- Not touching the sleep gate, the start gate, the routing rule (`WorldTimeScaleCall`) or the
  console's authority.
- Not restating the sleep policy's authority: if the all-unconscious gate stands on a sleep speed, its
  own step can restore that speed in the same pump frame the adopted write landed, because the quake
  trigger keys on the body's own BED flag (`!this.body.sleeping`) and not on the consciousness the
  policy reads — the two can coexist, the policy owns the outcome, and the self-check's §7 records it.
- Not syncing the quake itself: the guest's timer freeze and the host's broadcast are the existing
  mechanism and stay as they are.
