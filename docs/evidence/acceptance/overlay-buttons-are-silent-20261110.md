# The overlay's buttons: the click is played and recorded

Batch `20261110-overlay` on the release install (1.0, app id 4576490), the host client
running the acceptance channel. The reading answers the ticket's last row: does a CUO
control's report play the game's own click.

## The instrument

- `tools/acceptance/recipes/sound-log.cs` — the batch recorder over `Sound.Play(string, …)`,
  the entry every native one-shot goes through. `mode=start` clears and arms it,
  `mode=read` answers `raw=<calls seen>|clip@time;…`.
- `tools/acceptance/recipes/ui-sound-probe.cs` — calls the game's own
  `PlayerCamera.PlayUISound("click", 1f)`, the call the surface's report path makes.
- `tools/acceptance/recipes/ui-intent-probe-a.cs` — reports one `ControlInvoked` intent
  through the surface host's own `ReportIntent` (reflection: services → the concrete
  `GameAdapter` → its `_onlineUiSurface` field → the private method), the door the
  window's polling uses; the window view receives this callback at construction.

## The readings

1. The game's entry: `ui-sound-probe` → the recorder read
   `raw=568|click@803.1;click@803.1` — the game's click plays and the batch sees it.
2. The surface's report path: after arming, `ui-intent-probe-a control=home.refresh` →
   the recorder read `raw=630|click@1003.7;click@1003.7` — an intent reported through
   `ReportIntent` plays the game's click on the release client.
3. The uGUI door is NOT the window's door: `cuo-button-click label=` invoked `onClick` on
   46 captions (the window's tabs and buttons among them) and the recorder read no click —
   the window's views poll the pointer (`OnlineUiWindowView.PollPointer` →
   the callback passed at construction), so an `onClick` invocation never reaches the
   report path. Recorded as the reason the audible row needed the door in (2), not a
   defect in the sound.
4. The mapping is unit-pinned: `OnlineUiSoundTests` (4 facts: a button → `"click"`; a
   toggle and a dropdown pick → `"miniClick"`; typing, editing, a committed number and a
   hover → silent), and the surface pin test requires every view to report through
   `ReportIntent`.

## What it settles

The ticket's row — "the overlay's buttons play the game's click" — passes: the mapping is
pinned, the report path is the single door, and a reported intent plays and records the
game's own click on the release build. The uGUI event source is the window's polling, a
named limit of the fixture rather than of the sound.

## Limits

- The recorder counts calls to `Sound.Play(string, …)`; the click's clip name is what the
  game's own `PlayUISound` passes, so the reading proves the call, not the audible volume.
- The `onClick` attempt (3) is a negative reading: it proves the polling door, it does not
  prove the pointer path never plays a click.
