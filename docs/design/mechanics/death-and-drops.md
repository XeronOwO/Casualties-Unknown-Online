# Death, drops and revival

What happens to a player who dies, what they leave behind, and how they come back.

## What the player does

Dies — to a wound, a blast, a trap, an enemy — drops what they carried, and later is revived by a teammate or returns as
a spectator. The player expects the party's shared world to see the death and the drops, the body to stay where it fell,
and the items to keep their identity and their state.

## What the game itself does

The game kills the body, runs its own death handling, drops the carried items into the world and leaves a corpse the
medical view can act on. A revive is the game's own revival of that body.

## Rules and states

- A death does not duplicate or destroy an item's identity: what the dead body dropped is the same item, with the same
  instance identity and state, that the party can pick up.
- Drops are world facts: the host owns them, every client shows them, and a member who joins or re-enters sees them.
- The body stays where it died and is still a treatment target until it is revived or despawned; a dead body cannot
  consume anything.
- Revival restores the body the game's own revival restores; the reviving player spends what the game charges them.- **A fresh joiner sees the drops that already exist.** Entering a world where the party has already broken something must show the same drops, not an empty world: the accepted reading joined a fresh member AFTER the host broke a support cell (`blockBefore=2, blockAfter=0`, `item-world-read` 0 → **5**) and "the fresh guest answered **5 items with the identical set the host holds** — `blobflesh` plus four `stonefruitclosed`, compared as sorted ids".
- A death that happens only on the host's side (a host-only cause, a host-side blast) must still produce the same drops
  for the party, because the drops are part of the death.

## Refusals

| Condition | The player reads |
|---|---|
| treating a dead body | `use.refused.target_dead` |
| feeding a dead body | `use.refused.target_dead` |
| reviving without the means | the game's own condition, no CUO line |

## Who judges

The body's death is the body owner's fact, reported to the host; the host applies it and owns the resulting world state
(the corpse and the drops). A revive is executed by the body's own client, admitted by the host.

## What travels

The death, the body's state as a corpse, the dropped items with their identity and state, and the revival.

## What the player sees and hears

The party sees the body fall and the dropped items land, with the game's own death and drop sounds; a dead player sees the
death screen and may follow the party as a spectator.

## Records

`../../architecture/surfaces.md` (cross-player use row and world overlays row), decisions 156 (ownership consistency and
death preservation) and 161 (accept-first only for what the host can represent). Accepted:
`done/entity-destruction-drop-guest-fresh-state-loss.md` for the boundary rule. Open:
`../../backlog/todo/host-blast-rolls-a-second-drop-set.md`.

## Gaps

- The host-only-death drop path's acceptance row is still blocked on a staged carrier; the ticket carries the measured
  evidence and the missing capability.
- What a corpse blocks (collision, interaction) and for how long is the game's own rule and is not recorded here yet.
