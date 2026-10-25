# Saves and recovery: putting a run back

What a save holds, how it is taken, and what a restore may and may not do.

## What the player does

Cuts a save mid-run, continues it later with the same party, and expects the world, the items and every player's body to
come back where they were when the cut was taken.

## What a save holds

- The kernel checkpoint: every domain's authoritative state at the cut.
- The run baseline: what the run was generated from.
- Each present member's character data, keyed by the identity that player presents with.
- The world diff: what changed in the world since the baseline.
- The in-flight classes: each class of transient state with its declared verdict — captured, resolved before the save, or
  dropped.

## Rules

- The save is a repository of worlds, not a list of slots: one archive per world, keyed by an immutable identity; the
  display name may change without touching the archive.
- CUO's save is independent of the game's own save file: it never reads or writes it.
- A cut is armed by the event that asks for it and taken by the host's pump at one seam.
- A restore is repair mode with minimal loss, per entry: an entry whose content no longer exists is skipped with a
  recorded reason, and one broken entry never fails the whole cut.
- A member's stored character is applied by the client that can apply it; on a continue, the keys present peers claim are
  bound to those peers, and an unclaimed key waits rather than being handed to someone else.
- A refused restore child keeps its identity where the design says it goes (the ground), rather than being dropped
  silently.

## Refusals and recovery

| Condition | What happens |
|---|---|
| an entry's content no longer exists | the entry is skipped and the skip is reported |
| a stored key has no present owner | the entry waits for its owner |
| the archive's world identity no longer matches | the archive is not applied to the wrong world |
| a restore throws mid-way | the archive is left intact for a second attempt |

## Who judges

The host takes and holds the cut; a restore is applied by the host and delivered to the clients that own each part.

## What the player sees and hears

A cut produces no player-visible interruption; a continue puts each player back on their own body in the same world.

## Records

Decisions 162–170 (the save family), and the acceptance records of the continue/layer work in `../../backlog/`.

## Gaps

- The exact list of in-flight classes and their verdicts is declared by the transient policy; it belongs on this page
  once read from there.
- What a player sees when their own stored character cannot be applied is not recorded here yet.
