# Spectator: following the party after dying

Watching another player's body after your own has died.

## What the player does

Dies, sees the death screen, and chooses to follow a member of the party. The player expects to see through that member's
eyes, to be able to stop, and to return to the death screen or to their own body when they are revived.

## What the game itself does

The game's own spectator mode attaches the camera to another body. In co-op the followed body is a teammate's, whose
state the spectator's client already holds as a projection.

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

`../../architecture/surfaces.md` (console row for the `/spectate` verb), decision 184 (a spectator judges nothing).

## Gaps

- The exact key that stops following, and what happens when the followed body dies while being followed, is not recorded
  here yet.
