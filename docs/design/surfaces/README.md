# Surfaces: the pages a player looks at

One page per player-facing surface. `../architecture/surfaces.md` is their index: each of its rows names the authority a
surface is judged against, its native counterpart and its accepted record. This directory expands those rows.

## The surfaces

| Surface | The page | Status |
|---|---|---|
| The console (command and chat) | `console.md` | GAP |
| The online window (lobby, members, network, administration) | `online-window.md` | GAP |
| The member menu (right-click a player) | `member-menu.md` | GAP |
| The backpack panel (a teammate's pack) | `backpack-panel.md` | GAP |
| The medical panel (a patient's wound view) | `medical-panel.md` | GAP |
| The status line (notices and refusals) | `status-line.md` | page |
| The world overlays (nameplates, network readout, pings) | `world-overlays.md` | GAP |
| The drag cue (the use circle and its label) | `drag-cue.md` | page |

A `GAP` page is written from the surface's accepted record and its rules; until it exists, a change to that surface
records the design first.

## What every surface page states

The entry (which input opens it and what can still be used while it is open), the components it draws, the states it can
be in, the text keys it renders, what it must reproduce from the game's own surface, and its records.
