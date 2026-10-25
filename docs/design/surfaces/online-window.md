# Online window: the session's own panel

The window that shows who is in the session and lets the player reach the session surfaces.

## What the player does

Opens the session window, reads who is in the session, and moves between its pages: the lobby (who is here, join or leave),
the network (the connection's own state), the administration page (the host rules a player may read) and the world pages.
The player expects the window to show the session as it is, and the controls it offers to be the ones that apply right now.

## What it is

A CUO panel drawn on the game's canvas, built from one action table per frame, with one page visible at a time. Its pages
are the session's own facts, not the world's.

**The pages are a closed list, six of them.** `Plugin/OnlineUiPage.cs` declares `Home`, `Players`, `Network`, `Admin`, `Worlds`
("the world library: the worlds on disk, their backups, and the restore (decision 198)") and `Preferences` — so a page that is
not one of those six is a different surface, not a new tab.

## Rules and states

- The window has no page for the console: the command surface is its own overlay, and the window does not duplicate it.
- A page shows the facts it owns and offers the controls that apply to them; a control whose action does not apply is not
  offered rather than offered and refused.
- The window's pointer handling never blocks the game's own input outside its own rect, and its own controls are reachable
  while the world is being played.
- **The window may be dragged almost anywhere, but its TITLE BAR must stay grabbable.**
  `src/CasualtiesUnknownOnline.Runtime/OnlineUi/OnlineUiWindowDrag.cs` gives the constraint and the defect behind it: "the one
  grab target is its TITLE BAR, not the frame at large", and the earlier rule that kept "a strip of the frame on the canvas
  on every side … at the top edge was the wi[ndow's] bottom: the title bar ended 411 screen pixels (616 canvas units) above
  the canvas and the window could not be grabbed again".
- **The launcher fades because the player stopped using it, and the fade is a pure function of (now, hovered).**
  `OnlineUiLauncherFade.cs`: the top-right launcher "is opaque while the player is using it and settles to a translucent floor
  after an idle window, so an untouched launcher stops covering the play area", and the rule "derives the alpha from (now,
  hovered) alone, so every draw pass on the same frame returns the same value: an IMGUI frame draws a Layout pass and a
  Repaint pass, and a rule that accumulated deltas would count the idle time twice and flicker between the passes".
- **A page that overflows has a scroll affordance whose arithmetic is the design's.** `OnlineUiPageScroll.cs` decides "whether
  a page needs one at all, how much of the track its handle fills, where the handle stands … and what position a click on the
  track asks for" — the answer to the user's report that a page "has a part that goes p[ast what is] shown, and I don't see
  [a scrollbar] on the right".
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
