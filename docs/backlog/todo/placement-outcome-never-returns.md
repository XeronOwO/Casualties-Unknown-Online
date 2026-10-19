# The host's ledger keeps the destination it was told, not where the item went

- Status: Todo
- Priority: Medium
- Category: Item sync / kernel item facts / cross-player custody
- Source: Read on the machine in 2026-10-18's give session, while the container half of
  `done/give-item-to-another-player.md` was being verified (`[PlayerInteraction] bandage … not placed into
  container … falling back to a slot.` on the receiver, `bandage [carried]` on the host). Split out on
  2026-10-19 when that ticket's row-4 item loss was fixed, because the slot fallback is now the path the fix
  itself produces, and the finding needs its own home.
- Related: `done/give-item-to-another-player.md` (the custody path and its acceptance rows),
  `todo/container-content-event-gap-on-repick.md` (the adjacent event-carrier gap: a container child moved
  into a slot announces nothing to the peers' fact tables), `done/item-identity-across-transforms.md`

## What it is

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

## The "no room at all" case is the same edge, and it needs the host

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
