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
- **A checkpoint travels as chunks, and its schema has its own version.**
  `src/CasualtiesUnknownOnline.Protocol/Versioning/ProtocolConstants.cs` keeps the envelope layout and the checkpoint schema as
  separate numbers (`EnvelopeVersion` and `CheckpointSchemaVersion`) and bounds a checkpoint's transport:
  `CheckpointChunkItemCount = 256` items per chunk and a maximum number of chunks per restore — so a large world is carried in
  a deterministic number of pieces rather than one frame.
- **The end-of-layer panel's "Save and exit" takes a CUO cut, not the native save.** The accepted reading is that "the save
  half takes a CUO cut and leaves `save.sv` untouched", which is the same independence the save rules state from the other
  direction. The panel itself follows the living share of the party: it appears for a dead host and is gone for a member.
- **A continue restores the run's OWN world, not a stale one.** The recorded defect was "A Continue after a run opens a stale
  world (the layer-mod baseline then diverges)"; the fix moves the pointer to the run's own world at the first committed cut,
  and the acceptance reading is that all three clients "generated from the same decision entry state … and applied the same
  modifier (index 5) with zero `[LayerMod] baseline divergence`".
- A restore is repair mode with minimal loss, per entry: an entry whose content no longer exists is skipped with a
  recorded reason, and one broken entry never fails the whole cut.
- A member's stored character is applied by the client that can apply it; on a continue, the keys present peers claim are
  bound to those peers, and an unclaimed key waits rather than being handed to someone else.
- **A refused restore child falls to the ground, not into nothing.** The accepted record is that the refused child is dropped into the world and reported, read across all three rows — the identity is kept because the item exists in the world rather than being destroyed.
- **An in-flight restore account releases its arms.** A restore that finishes its account must release the entity and native arms it held rather than leaking them: the family's record ran its four named classes 66/66 with every row re-read as passed.
- A refused restore child keeps its identity where the design says it goes (the ground), rather than being dropped
  silently.

## Values the archive must carry

Three values have each been their own ticket, which is why they are stated here together: the layer time accounting (the timer is carried and restored, read on the fixed seam), the run clock base (a player who joins mid-run receives the archived base), and a new player's starting supplies (a player the world has no character for is supplied as a NEW player, with the grant announced before its use). Each of them is a value that silently disappeared before its ticket, so a restore that drops one is the same defect returning.

The player facts a cut carries are the DURABLE ones and only those: `src/CasualtiesUnknownOnline.GameState/Domains/Players/PlayerState.cs` holds alive/conscious, the carry relation, the limb and body latches, and the skills, while "high-frequency movement fields remain a stream" — and `PlayerSkillsState.cs` says why skills belong there: "they change through permanent progression or run rules (respawn keep/reset), so they belong to the player domain and ride checkpoint/wire/save like the other durable player facts".

## What travels

The checkpoint's typed state, the run baseline, each present member's character data, the world diff and the in-flight classes' verdicts; a restore delivers the same back to the clients that own each part.

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
