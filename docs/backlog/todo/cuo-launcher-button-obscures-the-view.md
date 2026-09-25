# The CUO Online launcher button covers the play area

- Status: Todo
- Priority: Medium
- Category: Online UI / presentation
- Source: User acceptance finding (2026-09-21): the `CUO 联机` button in the top-right corner blocks the game view; the user asks for a design fix, for example becoming semi-transparent after a period without use.
- Related: `todo/remove-the-online-ui-console-page.md` (the other Online UI window change from the same acceptance pass), `done/player-list-polish.md`

## Evidence

- `OnlineUiWindow.DrawLauncherButton` draws a fixed `new Rect(Screen.width - 170f, 12f, 158f, 34f)`
  with an opaque background every frame (`OnlineUiTheme.DrawBackground` plus
  `OnlineUiTheme.Launcher`). There is no idle state, no fade and no way for the player to shrink or
  move it, so it always covers the same part of the world view.

## Requirement and design

- The launcher must not obstruct the view while it is not being used: after a short idle window (on
  the order of a few seconds) it fades to a clearly translucent state; hovering it restores full
  opacity; it stays clickable in both states and does not flicker while the window itself is open.
- The idle timer is presentation-only local state (the `OnlineUiWindowState` family), never session
  state, and the fade must not allocate per frame.
- The exact alpha, idle delay and whether the fade is instant or eased are the implementation
  cycle's call; the visual result is confirmed in the user's acceptance run.

## Acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | No interaction for the idle window | The launcher is translucent and the world behind it is readable |
| 2 | The cursor moves over it | Full opacity, immediately |
| 3 | Clicked in either state | The window opens and closes exactly as today |
| 4 | The window is open and the mouse is elsewhere | No flicker, no per-frame allocation increase |
| 5 | Solo menu / no session | Same behaviour |

## Non-goals

- Not replacing the Online UI window framework and not moving the window itself.
