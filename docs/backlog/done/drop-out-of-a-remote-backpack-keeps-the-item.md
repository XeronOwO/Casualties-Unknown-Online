# A ground item dragged into an opened remote backpack lands in the LOCAL body (and disappears from the player's view)

- Status: Done — the proxy half is fixed (2026-10-26, `PlayerCameraDragUsePatch` clears the drag so the native body never mutates a display proxy); the machine row is owed. Original report: from the user's report (2026-10-26, translated): "I put it in the trashbag indirectly, then dragged it straight out to the screen edge to
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

## The system audit the report asked for (2026-10-26), and the first fix

Every entrance that moves an item into another player's custody, and what guarantees its destination — the table this family never had:

| Entrance | Destination operand | Receiver's branch | State |
|---|---|---|---|
| release on a remote SLOT, carried item | `GiveToBody{TargetSlotIndex}` | slot-release plan (`RemoteIntentSlotRelease.Plan`) | holds |
| release on a remote CONTAINER, carried item | `GiveToBody{TargetContainerInstanceId}` | `ApplyMoveIntoContainer` | holds |
| ground item onto a remote SLOT | forward `PickUpToSlot{slot}` | native `body.PickUpItem` | holds (judged 2026-10-26) |
| ground item into a remote CONTAINER | forward `PickUpToSlot{slot=-1, container}` | **was: the world branch read only the slot** | **fixed here** |
| take out of a remote container | `TakeOutOfContainer` | native unload/placement | holds |
| drop out of an opened remote backpack | `Drop` | local drop + report | **open** — reported `Committed` while the row stayed `Carried` |

The structural cause of the fourth row is that a destination travelled as TWO operands and one branch read one of them. `RemoteIntentApplier`'s
`PickUpToSlot` dispatch now passes both, and `ApplyPickUpToSlot` answers the container first through `ApplyMoveIntoContainer` — the game's own
`Container.LoadItem` with its `CanHoldItem` guard — so a container destination can no longer be replaced by a body slot. Written back on
`mechanics/give-and-take.md` and audit ledger row 193.

**Still open from this audit**: the last row (a drop out of an opened remote backpack commits a drop report and keeps the item — the "disappeared" half
of the report), and the machine row for the container destination (a ground item into a teammate's trashbag, read back with `item-row` as `Contained`
by that bag on both clients).

## The container destination lands in the bag — and the ROW still calls it a pickup (2026-10-26 21:36)

On the deployed fix, a ground plastic chunk handed to a teammate with its trashbag as the destination:

```
21:36:06.869 (eater side) [ItemTrace] op=30 item=1387595417539 origin=OnItemLoadedIntoContainer result=Committed(1) events=[Pickup]
21:36:06.869 [RemoteIntent] item plasticchunk entered container 5197989525 through the native guard.
21:36:06.869 [RemoteIntent] replayed native PickUpToSlot on item 1387595417539 (container 5197989525, slot -1).
```

So the OBJECT is in the bag now (the game''s own `Container.LoadItem` accepted it) — the destination fix works. But the kernel row for it reads

```
{"kind":"Carried","owner":76561198863287957,"parent":0,"slot":-1,"revision":2}
```

i.e. the report path recorded the load as a PICKUP, not as a `Contained` row with that parent. That is the next row of the same audit: the container-load report
(`ContainerItemSync`''s `OnItemLoadedIntoContainer`) and the kernel command it produces. The scene and the row disagree — exactly the class this ticket
exists for — and the custody projection will report it on the receiver''s next tick.

**Also observed, and the reason one attempt failed first**: a bag the guest itself created got a 13-digit LOCAL id (`1366120581059`) that the receiver''s
lookup could not resolve ("container … is not resolvable on the owner''s body"), while an older bag (`5197989525`) resolved — an id-space question for the
same audit (a freshly created item''s id is not yet the authoritative one when another client names it).

## The cause of the "disappeared" half, and why the first fix was WRONG (2026-10-26)

`PlayerCameraDragUsePatch` opens the intent window for a proxied drag and then let the native body run. `HandleReleaseDragging` acts on `dragItem`, so a
proxy released outside the remote view (dragged to the screen edge to drop it) went through the native drop path: the host reported a drop for an item it
does not own, the authority kept the row with its owner, and the proxy object vanished from the screen — nothing moved, which is exactly what the player
saw. The patch now clears the drag after opening the window, so the native body takes its own "nothing held" path (lower the drag image, close the ring
unless the pointer is over UI) and the Finalizer still emits the intents the window classified. Written back on `mechanics/give-and-take.md` and audit
ledger row 194.

**Owed**: the machine row — open a teammate's backpack, drag a proxy to the screen edge, and read: no local `[ItemDropped]` line for an item the client
does not own, the proxy still drawn, and the owner''s row unchanged.

## The first fix was reverted the same evening — the window CAPTURES the native calls

The `ClearDrag` fix (committed as `59de181a`, deployed) stopped the native body from touching a proxy by clearing the drag before it ran. Reading
`RemoteDragIntentCapture` afterwards shows why that is wrong at the root: the window does not classify a release by itself — its intents ARE what the
native body did (`CaptureDropItem`, `CapturePickUp`, `CaptureUseItem`, `CaptureWearItem`, `CaptureContainerLoad`, …). Clearing the drag removed the calls
the window records, so EVERY proxied release answered `produced no intent — unclassified native gesture`, and the centre-use seam built on top of it
(`6135269b`) could never work. Both are measured: three centre-release attempts with the staged pointer all answered that line.

So the fix is reverted (the comment in `PlayerCameraDragUsePatch` now records this), and the real half is where the DROP IS REPORTED: the proxied drop
produced a local `[ItemDropped]` report for an item the client does not own, instead of being captured as the window''s own drop intent. That is the
next read: `ItemReportCommitter`/`ItemWorldSync.OnItemDropped` under an open proxy window, and which of them decides a local drop instead of the
captured intent.

**Kept from this evening''s work**: the pointer seam (`DragUseCenter.StagedPointer` + `stage-pointer`) is independent of this and stays deployed; the
in-process give-into-a-remote-slot capability stays.

## What the native release actually does with a proxy, traced call by call (2026-10-26, last read of the night)

`PlayerCamera.HandleReleaseDragging` → `TryPerformWorldActions` (`PlayerCamera.cs:1686-1710`) does three things in order, and the window already covers
two of them for a display proxy:

1. `container.UnloadItem(this.dragItem, null)` (`:1691`) — absorbed by `RemoteDragContainerUnloadPatch` (the window records it and the native call is
   skipped), so a proxy is never detached from the remote backpack by the local release;
2. `this.body.DropItem(this.dragItem)` (`:1696`) — reached only when `this.body.HoldingItem(dragItem)` is true, which is false for a proxy, and
   `Body.DropItem(Item)` is patched for a proxy anyway (`RemoteDragBodyDropItemPatch`: captured, not run);
3. a world container under the pointer (`:1704-1709`) — its `LoadItem` is patched the same way.

So a proxied release **cannot** drop or move the proxy locally; it either produces the window''s intents (a slot or the centre) or nothing at all. That
leaves the two explanations for the report, and both are LOSS-FREE — the item''s row never left its owner:

- the release produced no intent (the open remote panel hid nothing: the item was never moved, and the next snapshot re-draws it), or
- the drop landed on the player''s OWN ring (`GiveGestureMode.OwnUi`), where the native local pickup is the correct behaviour and the item really is in
  the player''s own body — which is exactly the row this ticket''s evidence recorded (`Carried`, owner = the player themselves, slot 2, both clients
  agreeing) and what the body census confirmed.

**What is left is a player-facing question, not a custody defect**: while a teammate''s backpack is displayed, a drop can land on either the remote panel or
the player''s own ring, and nothing on screen says which one a given slot belongs to.

## Judged on the deployed build (2026-10-27, `3b3c9a81`): the release is an INTENT, nothing vanishes

With the guest''s backpack open and a proxy of its lantern dragged to empty space (`remote-gesture mode=release cast=-1`), the host''s log shows the release
CAPTURED and forwarded, and no local drop at all:

```
21:32:08.994 [RemoteIntent] DropItem captured for item 9492956821 of 76561198863287957 (container 0, slot -1, body 0, limb -1, …)
21:32:08.997 [RemoteIntent] 76561198281246659 → 76561198863287957 DropItem (item 9492956821, …)
```

and the owner''s own client then dropped its item:

| Row | Verdict |
|---|---|
| 1 — the item ends on the ground on every client, its row says `World` | **PASS**: `item-row` reads `{"kind":"World","owner":0,"parent":0,"slot":-1,"revision":2}` and the guest''s own body census no longer lists the lantern |
| 2 — the item is drawn where it was dropped, the former slot empty | **PASS on data, RESIDUAL on the frame**: the slot is empty on the owner''s body and the object is a world item; the frame taken (`capture-frame`) shows the CUO panel over the world, so the on-screen half was not judged this cycle |
| 3 — a ground item dragged into a container of an opened remote backpack lands in it | **PASS** (judged 2026-10-26): the object entered the bag through the native guard and the bag''s census shows it inside |

The old behavior this ticket was opened for — a local `[ItemDropped]` for an item the client does not own, the proxy destroyed and the item "gone" — does not
occur on this build.