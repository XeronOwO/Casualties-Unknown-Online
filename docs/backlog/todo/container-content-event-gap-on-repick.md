# A container child moved into a slot loses its container fact when the pick-up cancels the departure

- Status: Todo
- Priority: Medium
- Category: Item sync / event carriers (container contents vs the clone fact table)
- Source: batch `20261006-f`'s readings (2026-10-06) — the operator's and the third peer's clone-fact
  monitors warn twice each over a normal remote gesture. Filed rather than fixed in the batch because an
  acceptance run changes no code (`docs/acceptance/AGENTS.md` rule 8, and the sibling tickets' rejection).
  Batch `20261006-g` (2026-10-06) added the reference reading in its item-shaped form (the section above).
- Related: `todo/drop-pending-single-slot-overwrite.md` (the batch that read it; its
  monitor row failed on this gap), `todo/remote-inventory-native-parity-rework.md` (the same gesture family),
  `done/container-move-snapshot-only-sync.md` (the container-to-container route, which reports through the
  TARGET's fact), `docs/evidence/acceptance/drop-pending-single-slot-overwrite-20261006-f.md`,
  `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-f.md`,
  `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-g.md` (the reference reading)

## Symptom (read from batch `20261006-f`, three clients, one lobby)

The operator (physical-machine host, `76561198281246659`) released the focused owner's container child — the
guest `76561198863287957`'s `dogfood` (id `18082891413`) inside its carried `trashbag` (id `9492956821`) —
onto a ring slot of that owner. The child landed (the owner: `[SlotMoved] dogfood (id 18082891413) → slot 3
(Drag) reported.`, `[PickUpResult] dogfood → slot (slot 3)`, `[RemoteIntent] item dogfood now holds the
owner's slot 3.`), and the operator and the third peer each logged, per gesture:

```text
[WRN] […CloneFactTable] [CharSync] divergence for 76561198863287957's trashbag (id 9492956821): nested container contents changed without an event sync (the 1 Hz snapshot carried it).
[WRN] […CloneFactTable] [CharSync] divergence for 76561198863287957's dogfood (id 18082891413): a new carried item the fact table never saw — a pickup without an event sync (the 1 Hz snapshot carried it).
```

Both configurations warn: the occupied destination slot at 13:59:13.029 (operator) / .056 (third peer) and
the empty destination slot at 13:59:47.427 / .451 (`span-host-divergence.txt`, `span-alt-divergence.txt` in
the batch's artifact directory). The owner itself logs no divergence, and the slot-item control — the same
release with a child that was already a slot item — produces none on either viewer (`m5-host-slotitem-onto-
empty.json`).

## Mechanism the traces show

1. The operator's release classifies as `PickUpToSlot` (`[RemoteIntent] PickUpToSlot captured for item
   18082891413 of 76561198863287957 (container 0, slot 3 …)`), and the owner's replay runs
   `RemoteIntentApplier.ApplyPickUpToSlot`'s plan.
2. Its `PickUp` step is a native `Body.PickUpItem`, which first unloads the child from its container
   (`item.TryGetParentContainer(out container)` → `container.UnloadItem(item, null)`). The unload registers a
   departure: `[ItemTrace] op=46 begin item=18082891413 origin=OnItemUnloadedFromContainer event=Unload` +
   `[ContainerUnload] dogfood … left its container into the world from the CarriedInventory side — the drop
   report waits one frame`.
3. The same bracket then parents the child into the slot, and the re-pick CANCELS that departure —
   `[ItemTrace] op=46 … result=Cancelled events=[RePick]`, the rule `ContainerItemSync` states as "this
   departure replaces that one — the item cannot leave twice".
4. A cancelled departure sends no report, so the container's own fact ("the trashbag holds one child fewer")
   leaves the owner's client only through the periodic character snapshot — which is what the peers' clone
   fact tables then report. The slot carrier does report the new slot, but not what the child left.
5. The same route in the other direction is fine, which is why the gap is narrow: a child released into the
   WORLD keeps its departure and reports it (`[ItemDropped] bandage (id 13787924117) …`,
   `op=43 … Flush result=Committed(1)`), and a container-to-container move reports the TARGET's fact.

## The reference reading (batch `20261006-g`, 2026-10-06)

The acceptance below asked for the one reading that separates this gap from the intent replay — the OWNER
moving the same kind of item locally. Batch `20261006-g` took it, on three clients, on the deployed artifact:

1. **The operator's swap of a slot item draws the warnings.** Releasing the owner's `bandage` proxy
   (id `9492956821`) onto the owner's OCCUPIED slot made the owner report both `(Swap)` moves, and the
   operator and the third peer each logged two lines of the shape `[CharSync] divergence for …'s bandage
   (id 9492956821): slot 3 → 0 — a carried move without an event sync (the 1 Hz snapshot carried it).`
2. **The owner's own local swap draws exactly the same warnings.** The owner dragged its own slot item onto
   its own occupied slot through the native path — no CUO intent anywhere in the gesture — and both viewers
   logged the same two lines. **The gap is the native item path's, not the intent replay's.**
3. **A move into free space draws none**, in both shapes: the batch's row 6a read
   `[SlotMoved] bandage → slot 2 (Drag) reported.` plus `[PickUpResult] bandage → slot (slot 2)` on the owner
   and zero `divergence` lines on every client, and a custody transfer (`TransferToBody`) draws exactly one
   line per viewer with its own message — `left the inventory without an event sync`.

So the discriminator is the move's SHAPE, not who drove it: a move whose destination displaces an occupant,
and a move that removes an item from the inventory, leave the peers to learn the change from the periodic
snapshot; a move into free space reports an event that reaches them. The reading is item-shaped (a slot item
swapped onto a slot item); the container-child shape its acceptance names still has to be driven.

## What a fix has to decide

- Which carrier owns the container-contents fact on a re-pick: the unload carrier (a cancellation that still
  states the container change), or the slot carrier (`ItemSlotSync.ReportCarried` naming the container the
  item left). One owner, not two — the reports must stay idempotent for the peers' fact tables.
- Whether the fix covers the whole family in the same change: the R5 expansion loop, the local
  container-child drag onto a ring slot, the `TransferToBody` half, and the take-out into the world (which
  must not start double-reporting).
- The clone-fact table's own rule: whether "a new carried item the fact table never saw" is the right
  judgement for an item whose arrival the fact table can legitimately learn from a slot report.

## Acceptance

A three-client run on the deployed artifact, with the operator driving a focused owner's container child
onto (a) an OCCUPIED and (b) an EMPTY ring slot of that owner, read on all three clients: the child ends in
the ring slot and the container reads one child fewer on every client, and every client's window carries
**zero** `[CharSync] divergence` for the container and the child over each gesture's window. The same run
drives the reference reading this ticket lacked — the OWNER moving the same CHILD into a slot locally — so
the fix can say whether the gap is the intent replay's alone. Batch `20261006-g` took that reference reading
in its item-shaped form (a slot item, not a container child) and answered it — the warnings are the native
path's, section above — so what remains is the same control in the container-child shape.

## Readings taken

- Batch `20261006-f` (2026-10-06): the container-child readings this ticket was filed from — the operator's
  and the third peer's two warnings per gesture, in both destination states.
- Batch `20261006-g` (2026-10-06): the reference reading in its item shape, plus the empty-destination and
  custody-transfer contrasts — `docs/evidence/acceptance/remote-inventory-native-parity-rework-20261006-g.md`.
