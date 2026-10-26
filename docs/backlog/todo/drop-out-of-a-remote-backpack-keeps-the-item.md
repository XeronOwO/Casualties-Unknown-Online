# A ground item dragged into an opened remote backpack lands in the LOCAL body (and disappears from the player's view)

- Status: Todo — from the user's report (2026-10-26, translated): "I put it in the trashbag indirectly, then dragged it straight out to the screen edge to
  drop it — and the plastic chunk was gone."
- Priority: High — the gesture reports success and does nothing; the player cannot tell where the item went.
- Category: Remote backpack / drop / custody

## Design

`docs/design/mechanics/give-and-take.md` owns this gesture's rules — where a hand-over lands and what the receiver's own pickup does. The reading tool this ticket used is `tools/acceptance/recipes/item-row.cs`, the kernel row of one id, which is how the evidence below was obtained.

## What the player does and sees

The host opens a teammate's backpack, drops an item out of it, then drags that ground item back INTO the open (remote) inventory — aimed at the other
player's slots. The item disappears from the open backpack, and the machine says it landed in the HOST's own slot 2: the gesture's destination and the
custody change disagree, and the player cannot find the item because it is behind the open panel, in their own body.

## What the machine says (2026-10-26 21:25-21:26, `.acceptance` logs)

The drop chain fired in full on the host:

```
21:25:26.563 [ItemTrace] op=38 begin item=1340350777283 origin=OnItemDropped event=Drop
21:25:26.596 [ItemDropped] plasticchunk (id 1340350777283) at (3.0,468.1), vel (0.0,0.0) — container contents 0
21:25:26.596 [ItemTrace] op=38 item=1340350777283 origin=FlushPendingDrop result=Committed(1) events=[Drop, Flush]
```

and the GUEST even converged on it as a world item (`ItemDrop on item 1340350777283 converged on its first report`, `[ItemDrop] … present — re-placing at
(4.8,469.5)`), then re-read it as the host's carried item 30 seconds later (`[CarriedSync] added plasticchunk … to <host>'s snapshot`).

Both clients' kernel rows, read with the new `item-row` recipe, agree — and they do NOT say world:

```
port 18590 (host)  {"kind":"Carried","owner":76561198281246659,"parent":0,"slot":2,"revision":15}
port 18591 (guest) {"kind":"Carried","owner":76561198281246659,"parent":0,"slot":2,"revision":15}
```

and the host's own body census lists `slot2=plasticchunk#1340350777283`. **So the item is not lost**: it is in the host's slot 2, exactly where it was
before the gesture, and the player's view of it disappeared somewhere along the way.

## What has to be established first

1. **Which body the applier picked up into.** The row says the HOST owns it at slot 2 — the same client that made the gesture — so either the forward
   carried a target of 0/local, or the host's own `PickUpToSlot` ran as a local pickup instead of being forwarded to the aimed body. The line to read is
   `PlayerRemoteInventoryIntentService`'s forward: it logs `ground hand-over: sent PickUpToSlot for item … to <target>`, and the target in that line
   decides this.
2. **Why the drop report at 21:25:26 was `Committed` while the row stayed `Carried`** — the operation trace commits the REPORT; the kernel command it
   carries is the next thing to read.
3. The companion refusal: dragging a ground item straight into a remote backpack's trashbag was refused while a body slot worked (one `GiveToBody …
   container 5197989525` did reach the forward path, so the refusal is conditional — probably the container's own `CanHoldItem` on a display proxy).

## Acceptance rows

| Row | The check |
|---|---|
| 1 | A drop out of an opened remote backpack puts the item on the ground, on every client, and the row says `World` |
| 2 | The same item is drawn where it was dropped, and its former slot is empty on every view |
| 3 | A ground item dragged into a container of an opened remote backpack lands in that container (or is refused with a sentence naming why) |

## Limits

- Read from logs and rows after the fact; the gesture itself needs a hand or a driver that can open a remote backpack, which this session did not have.
