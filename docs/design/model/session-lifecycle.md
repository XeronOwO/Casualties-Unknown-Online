# Session lifecycle: from a lobby to a world

How a party comes together, plays, leaves and returns.

## What the player does

One player opens a lobby and starts a run; the others join the lobby and wait; when the host starts the run, everyone
enters the world together. Players leave and rejoin, save and continue a run, and finish a run. A player expects a
rejoined party to find the same world, the same items and the same progress.

## Rules and states

- The lobby is a membership: who is present, whose Steam identity, and which host rule set applies. Membership and the
  world are two different facts — joining the lobby does not place a body in the world, and re-entering a world is done
  from the running client, never by restarting it.
- The run is the host's world. A member's character is restored by the client that can apply it, on a continue, from the
  stored keys the present peers claim.
- A save is a multi-world repository: one archive per world, keyed by an immutable world identity, and the display name is
  free to change.
- A save holds every member present at the cut; a restore is repair mode with minimal loss, per entry, and an entry whose
  content no longer exists is skipped with a recorded reason rather than failing the whole cut.
- The save is independent of the game's own save file: CUO never writes it and never reads it.
- A mid-run cut is armed by the event that asks for it and taken by the host's pump at one seam, never inside the
  callback that asked for it.
- State that is in flight at a cut is classified per class — captured, resolved before the save, or dropped — and never
  silently kept or silently lost.

## Refusals

| Condition | The player reads |
|---|---|
| a member tries to act in the world before it is placed | the world interaction is refused; the placement is retried |
| a continue finds a stored key no present peer claims | that entry waits for its owner rather than being given to another |
| a restore entry's content no longer exists | the entry is skipped and the skip is reported |

## Who judges

The host owns the run, the world and the cut. A member owns its own body and its own character data; on a continue the
stored character is handed to the client that can apply it.

## What travels

Membership and host rules; the world's state as the kernel streams it; the checkpoint at a cut; the stored character data
and world diff a restore delivers.

## What the player sees and hears

A lobby screen listing the party; the world appearing when the host starts the run; the same world again after a
continue, with each player back on their own body.

## Records

Decisions 162–170 (save identity, salvage, repository, independence from the native save, cut arming, in-flight classes),
242 (layer ownership). Human pages: `../../en/internals/state-and-snapshots.md`, `../../en/internals/save-archive.md`.

## Gaps

- The exact member-facing steps of a rejoin (what a member sees while the host has not started the run) are not recorded
  here yet.
