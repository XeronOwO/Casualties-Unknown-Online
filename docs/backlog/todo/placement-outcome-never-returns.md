# The host's ledger keeps the destination it was told, not where the item went

- Status: Todo — the design is the receiver's placement answer BEFORE custody moves (2026-10-22, below); the
  gesture it serves is defined in `todo/give-item-to-another-player.md`.
- Priority: Medium
- Category: Item sync / kernel item facts / cross-player custody
- Blocked by: nothing external. It shares its wire change with `todo/give-item-to-another-player.md` unit 2.
- Source: Read on the machine in 2026-10-18's give session, while the container half of
  `todo/give-item-to-another-player.md` was being verified (`[PlayerInteraction] bandage … not placed into
  container … falling back to a slot.` on the receiver, `bandage [carried]` on the host). Split out on
  2026-10-19 when that ticket's row-4 item loss was fixed, because the slot fallback is now the path the fix
  itself produces, and the finding needs its own home.
- Related: `todo/give-item-to-another-player.md` (the custody path, the gesture and its acceptance rows),
  `done/container-content-event-gap-on-repick.md` (the adjacent event-carrier gap: a container child moved
  into a slot announces nothing to the peers' fact tables — closed by batch `20261025-b`),
  `done/item-identity-across-transforms.md`

## Design (2026-10-22 — the receiver's answer comes BEFORE custody; this supersedes the routes below)

The give gesture is defined (`todo/give-item-to-another-player.md`), and the user's 2026-10-22 ruling on the
no-room case is part of its design: **when the receiver's own rules cannot take the item, the item stays with
the giver.** That settles the route this ticket exists to choose: the host asks the receiving client BEFORE it
records anything, and the receiver's answer is the placement.

### The sequence

1. The giver releases on the displayed backpack; the give intent travels to the host as today.
2. The host validates what it can validate itself — membership, the access policy, the item's ownership, the
   operands, both ends in the world — and then asks the RECEIVING client to resolve the placement for that
   item at that destination. Nothing is written to either character snapshot and no custody entry moves.
3. The receiving client resolves the placement with its OWN rules — a free body slot, a container's
   `Container.CanHoldItem`, and the hand-only rule — without creating the item, and answers with either the
   destination it will use (the slot index, or the container's instance id) or a refusal and its reason.
4. On acceptance the host records the receiver's row at the ANSWERED destination, moves the ledger entry there
   and publishes the transfer. The receiving client materializes the item at that destination; the host's
   ledger already states the truth, so no correction has to travel back.
5. On refusal the host answers the giver with the receiver's reason; nothing moved, and the item is still in
   the giver's inventory.

### Why the answer has to come first

The alternative this ticket used to consider — commit, then let the receiver report where it landed — is
refused by the user's rule: an item that cannot be placed would have to leave the giver before it comes back.
The two-phase answer also removes the defect this ticket was filed for: the host never stores the giver's
requested destination, only the receiver's resolved one.

### What the receiving client needs to answer

The item's definition and properties (the same `CharacterItemMsg` the transfer already carries) and the
destination operand. It runs the same rules the placement runs, asked without materializing the item where the
game's API allows it, and with a throwaway prefab instance where a rule needs a real `Item` (no instance id,
so its destroy reports nothing). The answer's destination is what the transfer then uses: the check and the
placement must be one resolution, not two that can drift.

### The residual

A body can change between the answer and the transfer (the receiver picks something up in that window). If the
placement then fails, the receiving client reports the failure, the host returns the row to the giver and
publishes the return; the item is back with the giver and one line says why. The window is one round trip, and
a refusal creates nothing on the receiving side. The ticket records this instead of claiming it away.

### Acceptance (a two-client run on the deployed artifact, real gestures)

1. A give into a container that refuses the item (weight/tag): the item never leaves the giver, one line names
   the container's reason, and neither side has a row for it.
2. A give to a body with no free slot: same, with "no room" named.
3. A give whose requested slot is occupied so the receiver substitutes another: the host's row names the
   substituted slot, and the receiver's tree agrees.
4. An accepted give into a container: the host's row names the container and its child, and the receiver's
   tree agrees.
5. The same refusal shape under the access policy's deny arm: refused on the actor's side, no row written.
6. The residual: a body that changes between the answer and the transfer ends with the item back in the
   giver's inventory and a line naming the return.

## Design defect: the give interaction this ticket assumes cannot be performed (2026-10-22)

> **Superseded 2026-10-22 by the design above.** The defect was real: the order the earlier design assumed
> (the other player's backpack already open when the drag starts) is not performable, and the readings below
> came from a probe. The gesture the user then defined opens the backpack from the drag itself, which is what
> unblocks this ticket. Kept as history.

The premise below — the receiving client runs a placement for a destination the giver named on the other
player's inventory — is not an interaction the player can perform. A give starts from the giver's own
backpack: the backpack is open and the item is dragged with the right mouse button held. Opening another
player's backpack needs a right-click on that player, and the held right button cannot also make that
click. The give readings this ticket rests on — and the drag half of
`todo/give-item-to-another-player.md` — came from a probe calling the release seam directly, never from
a real gesture.

The user's ruling of 2026-10-22: this is a design defect, not an acceptance gap, and the process must start
every player-facing behaviour from the real keyboard/mouse sequence (root `AGENTS.md`, *Engineering
Discipline*, and the adversarial review's A0). The ticket is therefore blocked on the give gesture's
definition: it is re-derived from an input the player can actually perform, and the user decides which
one. Everything below records the code as it stands; it is not a design to implement.

## What it is (history — the code as it stood when the ticket was filed; superseded by the design above)

A give's destination is decided TWICE, and the host only hears the first answer. The giver's release names a
destination (a ring slot, or an item shown in the receiver's inventory), the host validates it against the
RECEIVER's snapshot and records the item where the message said it went, and then the receiving client runs
the placement on its own body — where its own container rules may refuse it, and its own slot rule may
substitute another slot.

The receiver's own line is written and correct:

```text
[PlayerInteraction] bandage (id 9492956821) not placed into container 1245861496771 (the target is not a
container) — falling back to a slot.
[PlayerInteraction] placed bandage (id 9492956821) in local slot 4.
```

The host's ledger still reads `bandage [carried]` with no slot — "carried, nowhere" — and re-reading it a
minute later reads the same. The character snapshot does carry the item at its real slot, so the peers' clones
show it where it is; what stays wrong is the authoritative item row (`/data get` and the kernel's own view),
which claims the container it was told about, or no placement at all.

After the row-4 fix this no longer costs the item: the object the refused container materialized is placed in
a slot instead of being destroyed. What it still costs is the agreement between the two sides — the same class
of defect as a placement the peers learn only from the periodic snapshot.

## Why the host cannot simply be told

The receiving client's answer never travels back except through the character re-report, and that report
updates the character snapshot, not the kernel's item row. The wire already has the shape for answering a
REFUSED report (`CommandRejected` with a `RefusedKind`); what it does not have is a positive "it went here
instead" correction. Two routes are on the table and the ticket exists to choose one deliberately:

1. The receiving client reports its placement back (a new wire member for the success case), and the host
   corrects its row. Cost: a second owner of the location for the window between the transfer and the
   correction.
2. The host consults the receiving client before recording (a confirmation round trip). Cost: a synchronous
   answer on the custody path, which the family has avoided so far.

The third option — leave the row as it is and treat the character snapshot as the only placement truth — is
what happens today; the readings above are its cost, and it is the status quo this ticket is filed to end.

## The "no room at all" case is the same edge, and it needs the host (history — superseded 2026-10-22)

> **The user's 2026-10-22 ruling replaced this section's answer.** The outcome is not a world drop and not a
> re-recorded row: the item stays with the giver, because the give is answered before custody moves (the
> design above). The drop analysis below is kept because it is the evidence that a post-hoc correction cannot
> be built from the receiving side.

One placement has nowhere to go on the receiving side: a body with every slot filled (or an item its native
rules refuse outside a hand) cannot take a handed-over item, and the item has already left the giver. The
obvious answer is the give ladder's last rung — drop it at the receiver's feet — and a first version of the
row-4 fix did exactly that. The independent review of batch `20261021-b` proved that route impossible from
the receiving side: the host has ALREADY recorded the id as a carried row before it publishes the transfer
(`PlayerInventoryTakeService.HandleTakeCore`'s `AdoptTransferredItem`/`CommitCarriedToHost`, and
`DeliverCreatedItem`'s `Carry`), so `ItemDomainModule.DecideSpawn` refuses the drop rung's spawn report as
`Conflict`; on a host receiver no peer ever learns of the object, and on a guest receiver the next world-item
keyframe deletes it with no trace (`ItemReconcile.OnRemoteItemSnapshot` → `RemoteItemSceneOps.KillNow` zeroes
the ids). The rung was withdrawn, and that case now reports the item's end instead.

So the world-drop answer belongs to THIS ticket: the receiving client has to tell the host "I could not place
it", and the HOST has to re-record the row as a world item (or as a different destination) and publish that
correction. Every route below therefore also carries the placement-outcome correction, not just the slot
number.

## Acceptance

A two-client run on the deployed artifact, reading the host's own `/data get` for the item:

1. A give whose destination container refuses the item on the RECEIVER's client: after the refusal the host's
   row names the slot the receiver actually used — not the container, not "carried, no slot" — and the
   receiver's tree agrees.
2. A give whose destination slot was taken so the receiver substituted another: the host's row names the
   substituted slot.
3. An ordinary accepted give into a container: the row still names that container and its child.
4. The same refusal shape under the access policy's deny arm: refused on the actor's side, and no row is
   written at all.

## Where the change lands (surveyed from the readings)

- The host records the destination in `PlayerInventoryTakeService.HandleTakeCore` (the carry/`CommitCarriedToHost`
  half) and in `DeliverCreatedItem`'s `Carry` — both write the row from the message before the receiver's
  client has run anything.
- The receiving client's answer is `PlayerInventoryPlacement`'s return value, already logged and already
  false-able; nothing consumes it beyond the local `changed` flag.
- The existing refusal-answer machinery is the `CommandRejected`/`RefusedKind` pair the give family already
  uses for a host-side refusal, so the wire shape for a positive correction is the one thing to design here.
