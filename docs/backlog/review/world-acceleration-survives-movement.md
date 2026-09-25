# Manual world acceleration must not end when a player moves

- Status: Review
- Priority: High
- Category: World / session (world-time initiation)
- Source: User acceptance finding (2026-09-21) and ruling the same day: the host started the acceleration and it stopped as soon as the host moved. In-world actions during an acceleration (mining and the like) are normal, vanilla-supported play and CUO must not gate them; the acceleration ends only when the player ends it (the accelerate key back to 1x), through the sleep gate, or through the death/pause transitions.
- Related: `todo/world-time-local-initiation.md` (the delivery this reopens), `resolved/sleep-behavior-policy.md`, `done/world-time-manual-acceleration.md`, `review/enemy-hit-determination-local.md`

## Evidence (what actually ends an acceleration)

- Native, `reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs`: pressing Left or Right is the
  only movement rule —
  `if (Input.GetKeyDown(KeyBinds.GetBind("right")) || Input.GetKeyDown(KeyBinds.GetBind("left"))) { this.SetTimeScale(PlayerCamera.SpeedType.Normal, false, false); }`
  (lines 921-924). In vanilla this reset is per-client, and with one player there is nobody else to
  affect.
- Native: `Body.cs` and `Item.cs` contain no `SetTimeScale` call at all. Mining a block, eating,
  using an item or working a container never ends an acceleration in vanilla. The only other resets
  are the explicit speed keys (`speed1`/`speed2`/`speed3`, PlayerCamera.cs:885-896), the
  death/unconscious transitions and the pause path.
- CUO: that key press travels `PlayerCameraSetTimeScalePatch` -> `WorldTimeSync.OnTimeScaleSetRequested`
  (guest, local-first) / `OnLocalTimeScaleChanged` (host) -> the shared clock, so ONE member's
  left/right tap ends the acceleration for the whole session. That blast radius is the CUO-added
  part of what the user experienced.

## Requirement

- Movement no longer changes the world speed. `Normal`/`Fast`/`SuperFast` change only through an
  explicit accelerate-key press, the sleep gate, or the death/pause transitions.
- Mining, eating, item use and container work must never touch the shared clock; the implementation
  shows that path is absent, as it is natively, and keeps it absent.
- This is a deliberate deviation from vanilla and is recorded as such in the implementation cycle's
  decision record. The acceptance matrix of the reopened ticket is rewritten to match — its
  movement-cancel rows are superseded.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Host starts the acceleration, then walks | The acceleration continues for everyone |
| 2 | Guest starts the acceleration, then walks | Same |
| 3 | A teammate walks while the session is accelerated | No change to the shared clock |
| 4 | Anyone mines, eats or uses an item while accelerated | No change |
| 5 | The accelerate key is pressed back to 1x | Everyone returns to 1x |
| 6 | Everyone falls asleep | Unchanged native sleep acceleration |
| 7 | Pause, menu, death and the start gate | Unchanged |
| 8 | The HUD speed indicator and its sound through all of the above | Follow the actual clock; no spurious switch sound from a movement event |
| 9 | Enforcement of the host's value while an acceleration stands | Never fights the standing acceleration (no corrective ramp caused by a movement event) |

## What landed (2026-09-25)

The router asks the native flags what a call IS (`WorldTimeScaleCall`, Runtime, pure), and only an
ANNOUNCED change (`switchSound`) owns the shared clock. `PlayerCameraSetTimeScalePatch` carries both
flags into the bridge; `WorldTimeSync` consults the pure decision table `WorldTimeScaleCall.Route` —
which the adapter executes verbatim, so the rule is tested without Unity — on the entry side and in the
host's postfix.

- The movement rule — and with it the whole silent-reset family (the in-world event resets, waking
  up, the scene start) — no longer reaches the shared clock: the call does not run on either side,
  the host's postfix does not adopt it as the standing request, and this screen keeps the session's
  speed. Nothing is written when the clock already is the session speed, so a movement key causes no
  dip and no speed sound; a local presentation effect that held the screen elsewhere (the survivor
  note's Slowmo) is put back silently.
- Unchanged: an announced speed change (host local-first + adoption, guest local-first + request and
  the host's answer), the forced pause/menu/death transitions, Slowmo/Paused, the host-owned sleep
  suppression and the start gate.
- `ProtocolVersion.Current` is bumped in the same change — a mixed session would otherwise reproduce
  this defect — and `WorldTimeScaleCallTests` + `PlayerCameraSetTimeScalePatchTests` pin the rule and
  the router surface.

Mechanism inventory (the whole native call-site family), the red and the limits:
`docs/evidence/selfchecks/world/world-acceleration-survives-movement-selfcheck.md`.

## Verification (2026-09-25)

| # | Scenario | Evidence |
|---|---|---|
| 1-3 | Movement during an acceleration changes nothing, for the mover and for every other screen | the reset is classified as an automatic reset and swallowed on both sides (`WorldTimeScaleCall`; `WorldTimeSync.OnTimeScaleSetRequested`/`OnLocalTimeScaleChanged`) — the on-screen half is the session's |
| 4 | Mining, eating, item use and container work touch no clock | those paths call no `SetTimeScale` (the ticket's own evidence) and the in-world event resets that DO exist follow the same rule |
| 5 | The accelerate key back to 1× ends it for everyone | unchanged announced path (`Kind.Deliberate`: local-first + request + answer) |
| 6 | Everyone falls asleep | unchanged sleep gate (`WorldTimePolicy`); the sleep calls stay suppressed by their own scope |
| 7 | Pause, menu, death and the start gate | unchanged: forced transitions keep their branch, and the gate keeps the clock — a silent reset is swallowed WITHOUT writing while the gate holds timeScale 0 |
| 8 | The HUD speed indicator and its sound | no clock write happens on a movement event (nothing is written when the clock already is the session speed), so no switch sound can come from it; the indicator itself is the session's |
| 9 | Enforcement never fights a standing acceleration | the host adopts only announced/forced changes; the guest's ramp state machine is untouched and nothing is written while a lead window is open |

Cycle facts: red on the frozen HEAD `0e7693f4` (`PlayerCameraSetTimeScalePatchTests`, 2 failed /
0 passed), focused family run 155 passed (the world-time family plus the patch/bridge contract classes this cycle touched), full suite 3953/3953 with build, normative gates 148/148 in
the evidence run (the delivery-checklist case runs on its own), build 0 warnings / 0 errors,
`dotnet format` exit 0. The on-screen rows are NOT verified here — no session was run (selfcheck §5).

## Non-goals

- Not per-player clocks and not a local-only clock.
- Not changing the sleep gate or the start gate.
