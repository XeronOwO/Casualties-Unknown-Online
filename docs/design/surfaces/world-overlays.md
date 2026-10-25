# World overlays: what is drawn in the world

Nameplates, the network readout, off-screen arrows and transient location pings.

## What the player does

Looks at a teammate and reads their name above them; glances at the readout for the connection's state; follows an arrow to
an off-screen teammate; sees a ping where something happened.

## What it is

Labels and markers drawn on the game's canvas at world positions, belonging to players and events rather than to a panel.

## Rules and states

- An overlay carries no input: it never eats a click the game or a panel needs.
- An overlay is a projection: it is drawn from state the client already holds and asserts nothing of its own.
- A nameplate follows the body it names, including while that body is dead or being carried.- **A name tag has one placement rule.** Its font, its offset above the head and its padding at the screen's edge are part of the design rather than free numbers: the accepted record is the name-tag polish selfcheck, which fixes the font, the head position and the off-screen edge padding together — a tag that drifts from the head or clips at the edge is the defect.
- A readout reports the session's own state (who is connected, the link's health) and never a machine-level detail.
- A ping marks the moment a thing happened and fades; it is not a history.- **A middle-click marks a place.** The marker is a ping with a type: a first placement draws the circle, "a quick second placement upgraded and retargeted it to an exclamation", and "both clients pruned it after the lifetime" — the peer saw "the sender's circle at the same position and type". The marker's look stays the user's judgement.- **The launcher never blocks the play area.** The online button in the screen's corner is an overlay like any other: it must not cover the view, and the accepted design is a fade while idle — "The exact alpha, idle delay and whether the fade is instant or eased are the implementation cycle's call; the visual result is confirmed in the user's acceptance run."

## Refusals

None: an overlay that cannot be drawn is a defect of its own surface, not a refused action.

## Who judges

The client that draws the overlay, from its own projection. The host decides nothing about an overlay.

## What travels

The player names, positions and session facts the overlays are drawn from — all of them already carried for other
purposes.

## What the player sees and hears

Names above bodies, a small readout, arrows at the screen's edge for teammates outside the view, and short-lived pings.

## Records

Decision 232 (the world-space overlays are labels on the network/hud family rather than a new surface). Accepted:
`../../backlog/done/ragdoll-state-not-visible-to-remote.md`, `done/high-sleepiness-squint-not-visible-remotely.md` (both
about a body's state reaching the remote view).

## Gaps

- Which overlays exist today, and which are still missing for a remote body, is not enumerated here yet.
