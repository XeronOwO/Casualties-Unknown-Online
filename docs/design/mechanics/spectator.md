# Spectator: following the party after dying

Watching another player's body after your own has died.

## What the player does

Dies, sees the death screen, and either watches the run from above or possesses a teammate to see through their eyes. The
player expects to see through that member's eyes, to control the mode with the keys the reference co-op mod uses, to stop
and return to the death screen, and to come back to their own body when revived.

## What it is

Two modes on one death screen: PURE OBSERVATION (a free camera over the run) and POSSESSION (the camera inside a
teammate's body, with that body's screen effects and sounds). The accepted record says the pivot landed "mode and its two
entrances, pure observation, possession, the KrokMP-aligned controls, the mirrored screen effects and sounds, the native
status icons presenting the watched body", and that the superseded host rules were removed end to end
(`done/spectator-superseded-host-rules.md`). The user's request behind it, in their own words, was to be able to watch
others and to see through them after the controlled character dies.

## What the game itself does

The game's own spectator mode attaches the camera to another body. In co-op the followed body is a teammate's, whose state
the spectator's client already holds as a projection; possession shows that body's own presentation rather than a copy.

## Rules and states

- Following is one camera on one body: a request naming several players is refused rather than resolved to the first
  match.
- A spectator judges nothing about the followed body: it sees the state the owner's client produces, and its own actions
  do not reach that body.
- Stopping returns the player to the death screen; a revive returns them to their own body.
- A member who leaves the world while being followed cannot be followed any more; the spectator returns to the death
  screen.

## Refusals

| Condition | The player reads |
|---|---|
| the selector names more than one player | the line names how many matched and asks for one |
| the named member cannot be watched | the line names why (not in spectator mode, or their body is not present here) |

## Who judges

The spectator's own client decides what it renders; the followed body's state is the owner's, projected to the spectator.
No rule about the world is evaluated by a spectator.

## What travels

The choice of whom to follow, and the followed body's state as it already travels to every client.

## What the player sees and hears

The followed player's view, with the game's own spectator presentation; the spectator hears what that position hears.

## Records

`../../architecture/surfaces.md` (console row for the `/spectate` verb), decision 184 (a spectator judges nothing). Accepted: `../../backlog/done/dead-player-spectating.md` — the 2026-10-24 pivot, quoted above — and `done/spectator-superseded-host-rules.md` for the cleanup it created.

## Gaps

- The exact key that stops following, and what happens when the followed body dies while being followed, is not recorded
  here yet.
