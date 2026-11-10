# The overlay's buttons are silent

- Status: Done — the click is wired and played on the release client (2026-11-10): every control reports through
  `OnlineUiSurfaceHost.ReportIntent`, which plays the game's own `"click"` for a button and `"miniClick"` for a toggle or
  dropdown pick (`PlayerCamera.PlayUISound`). The mapping is machine-pinned (4 facts, `OnlineUiSoundTests`); the call
  plays and records on the release build (`ui-sound-probe` → `raw=568|click@803.1;click@803.1`) and so does an intent
  reported through the surface's own door (`ui-intent-probe-a control=home.refresh` → `raw=630|click@1003.7;click@1003.7`).
  The uGUI `onClick` is not the window's door — 46 invoked captions produced no click, because the views poll the pointer
  — named as a fixture limit, not a sound defect. Readings: `docs/evidence/acceptance/overlay-buttons-are-silent-20261110.md`.
- Priority: Medium
- Category: UI / audio
- Source: the user, 2026-11-08 (translated: "CUO's UI sounds — many buttons have none, add them")
- Related: `done/plugin-host-shell.md` (the window's layout), `docs/design/surfaces/online-window.md` (the surface's own page)

## Design

The surface is the overlay window: `docs/design/surfaces/online-window.md` (its tabs and buttons) and
`docs/design/surfaces/world-overlays.md` (the drag-use cue). The sound must be the game's own click, played through the
game's own audio entry, so the overlay hears what the rest of the game hears.

## The defect

CUO's Online UI window is drawn with IMGUI (`OnlineUiHost`), and none of its buttons plays a sound. The game's own
UI clicks (its `Sound.Play("click")` path) but the overlay is a second surface that never joined it.

Read before designing: the game's own button sound call sites (`reversing/Assembly-CSharp/…`, the click paths the
native menus use), the native audio entry (`Sound.Play`), and whether it needs a listener position (the overlay is
screen-space; a UI sound is usually 2D — the game may call `Sound.Play` with a null/2D variant).

## What it affects

Every clickable row in the window: the six tabs, the session buttons (copy lobby id, close room, leave, open player
page), the join/leave flows, the save panel's buttons when they land, and the drag-use cue's own confirm.

## The chain (read 2026-11-08)

1. The game's own menu buttons play `PlayerCamera.PlayUISound("click", 1f)` on their click —
   `reversing/Assembly-CSharp/Assembly-CSharp/AdaptiveButton.cs:51/61/65`, and its small controls play
   `PlayerCamera.PlayUISound("miniClick", 1f)` (`SettingsMenu.cs:192`).
2. `PlayerCamera.PlayUISound(string, float)` is the game's own static entry:
   `reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:38` → `Sound.Play(sound, Vector2.zero, true, false, null, 1f,
   pitch, true, true)` — a 2D UI sound, no listener position needed.
3. CUO's window is uGUI built from the game's own row prefab: `src/CasualtiesUnknownOnline.GameAdapter/OnlineUi/OnlineUiControlView.cs`
   wires the `Button.onClick`, `Toggle.onValueChanged` and `Dropdown.onValueChanged` listeners that report
   `OnlineUiIntent`s; `OnlineUiSurfaceHost.cs` owns the queue the views report into and hands each view
   `_intents.Enqueue`.
4. Every intent reaches the surface as a fact, then the plugin drains it (`OnlineUiHost.DrainSurfaceIntents`) and runs the
   action registered under the control's id. So the click is known in exactly two places: the control's listener and the
   surface's report path.

## The fix (2026-11-08)

`OnlineUiSurfaceHost` passes `ReportIntent` to every view instead of `_intents.Enqueue`; `ReportIntent` plays the game's
own sound for the click-shaped intents and then enqueues the intent unchanged: a button press (`ControlInvoked`) plays
`"click"`, a toggle or dropdown pick (`ControlToggled`/`ControlSelected`) plays `"miniClick"`; typing, dragging a slider
and a committed number stay silent. Both are `PlayerCamera.PlayUISound`, guarded by `PlayerCamera.main != null` so a
control that fires before the player exists has nothing to play.

## Acceptance

- Press each CUO button → the game's own click sound plays (read from the acceptance channel's `sound-log`).
- The game's own buttons still play theirs (no double sound).
- A silent environment (master volume 0) shows no new error.
