# Manual world acceleration must not end when a player moves

- Status: Todo
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

## Non-goals

- Not per-player clocks and not a local-only clock.
- Not changing the sleep gate or the start gate.
