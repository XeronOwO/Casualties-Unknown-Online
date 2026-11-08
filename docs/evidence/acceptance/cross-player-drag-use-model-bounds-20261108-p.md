# Cross-player drag use: the display-target rows on a live session (batch 20261108-p)

- Artifact: `0.1.0+9869d734` (acceptance channel; the same deploy the cue batch ran on).
- Session: lobby `109775243851228196`, host + Steam1 + Steam2, all in world (the cue batch's session, continued).
- The instrument: `remote-backpack-open` drives the adapter's own `AutoOpenRemoteBackpackForDrag` for the held drag and reads the
  view's own state (`isOpen`, `focused`) — never the call's return, which reports success even when the view refuses.

## Row 1 — a drag onto a standing player's model opens their backpack

Steam1 stood at `(5.721, 505.615)`, Steam2 was teleported away. With a drag held (`drag-start type=paincream` and the
`iteminteract` key held through the acceptance channel) and the pointer on Steam1's projected point (`screen-point2` → `677,617`),
`remote-backpack-open` answered `{"isOpen":true,"focused":76561198863287957}` — Steam1. A second fresh drag repeated the same answer.

## Row 2 — the pointer outside the model opens nothing (with a fresh drag)

With the display closed, a fresh drag held and the pointer one unit outside the model box (`screen-point2` → `628,617` at the
then-current geometry), `remote-backpack-open` answered `{"isOpen":false,"focused":0}` — no backpack, no target.

The complementary behaviour is real and was also read: once the display is OPEN for a target, moving the pointer outside the box
does not close it — the last target is kept (`isOpen true, focused 76561198863287957` after moving to `698,617`). Row 2's "nothing
opens" therefore holds for a fresh gesture, not for one already open, which is the ticket's own wording ("when the cursor leaves every
range the last target is kept, so the displayed backpack continues").

## Row 3 — two overlapping boxes: the pick names exactly one, and the last entered replaces the first

Both members stood on one spot (Steam1 `(5.7, 505.615)`, Steam2 `(6.302, 505.615)`), their boxes overlapping on screen
(Steam1's centre `673,617`, Steam2's `682,617`).

- The pointer in Steam1's own side of the overlap (`670,617`) → `remote-backpack-open` → `focused 76561198863287957` (Steam1).
- The pointer moved into Steam2's own side (`688,617`) while the drag stayed held → the next call → `focused
  76561199526807662` (Steam2): the second range's backpack replaces the first, the row's "last entered wins".
- The pointer back on Steam1's side (`670,617`) → Steam1 again.
- With both boxes holding the pointer exactly (the earlier stacked read), the pick resolved to exactly one member, never both.

Limit: the pick's own line (`[DragUse] display target … picked among N candidate(s) at (x,y)`) is a Debug line; this build's log
channel wrote `[Info]` lines only even with the CUO config's `MinimumLevel` set to Debug before the launch, so the candidate
count is unread and the row's target is read from the view's own state instead.

## Rows 4 and 5 — read by the cue batch on the same session

Row 4: a release on the displayed backpack's centre landed on the displayed player (`[ItemUse] 76561198281246659 offers dogfood …
to 76561198863287957 — the eat runs on the eater's own client`, and Steam1's own client ran the eat).

Row 5: releases off the display's centre took the game's world path (`mode World`, `[ItemDropped]` lines, no use), as the cue batch's
two drops recorded.

## Row 6 — solo: read

Both members quit; the host stayed in its world alone (`role Host, inWorld true`). A staged drag with the pointer on an empty spot →
`remote-backpack-open` → `isOpen false, focused 0`: no display opens with no other player, which is the row's "unchanged".
The session never left the world; the solo check is a separate short batch.

## Limits

- The auto-open was driven through the adapter entry (`remote-backpack-open`), not by the while-dragging frame's own call, so the
  candidate-count log line and the pointer-entry edge are the parts this reading does not cover.
- The members' positions were teleported for the fixture; two teleports did not settle where they were aimed, so the geometry is named
  by the measured positions, not by the requested ones.
