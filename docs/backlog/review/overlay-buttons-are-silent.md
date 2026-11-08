# The overlay's buttons are silent

- Status: Review — user-reported (2026-11-08): "CUO's UI has no sound on many buttons; add the sounds." The game's own
  buttons click; CUO's window does not.
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

## Acceptance

- Press each CUO button → the game's own click sound plays (read from the acceptance channel's `sound-log`).
- The game's own buttons still play theirs (no double sound).
- A silent environment (master volume 0) shows no new error.
