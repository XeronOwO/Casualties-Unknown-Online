# Online window: the session's own panel

The window that shows who is in the session and lets the player reach the session surfaces.

## What the player does

Opens the session window, reads who is in the session, and moves between its pages: the lobby (who is here, join or leave),
the network (the connection's own state), the administration page (the host rules a player may read) and the world pages.
The player expects the window to show the session as it is, and the controls it offers to be the ones that apply right now.

## What it is

A CUO panel drawn on the game's canvas, built from one action table per frame, with one page visible at a time. Its pages
are the session's own facts, not the world's.

## Rules and states

- The window has no page for the console: the command surface is its own overlay, and the window does not duplicate it.
- A page shows the facts it owns and offers the controls that apply to them; a control whose action does not apply is not
  offered rather than offered and refused.
- The window's pointer handling never blocks the game's own input outside its own rect, and its own controls are reachable
  while the world is being played.
- An action taken from the window is host-validated where it acts on the session, exactly as the same action from a
  command would be.

## Refusals

| Condition | The player reads |
|---|---|
| an action needs the host and this player is not the host | the control is absent or the answer names the missing permission |
| the session ended while the window was open | the window reports the session's state |

## Who judges

The window draws session facts it is given. Any action that changes the session is the host's to validate.

## What travels

Membership, host rules and network state, as the session already carries them.

## What the player sees and hears

A compact window with pages, listing the party and the session's state, opening and closing without interrupting play.

## Records

Decision 228 (the window has no console page), decision 230 (CUO's own guard never blocks CUO's own surface). Accepted:
`../../backlog/done/online-ui-art-and-controls-overhaul.md`, `done/online-ui-panels-request-alpha-blend-false.md`.

## Gaps

- The page list and each page's exact contents are not recorded here yet.
