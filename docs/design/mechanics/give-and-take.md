# Give and take: moving an item between players

Handing an item to a teammate, and taking one from their displayed backpack.

## What the player does

Opens a teammate's backpack by dragging an item onto them (or through the member menu), then either releases an item of
their own over a slot or a container inside that pack (a GIVE), or drags an item out of the pack into their own inventory
(a TAKE). The player expects the item to change hands exactly once: it leaves the giver's inventory and appears in the
receiver's, in the place the player aimed at, with the container it was aimed inside if it was aimed inside one.

**The drag may start on an item lying in the world.** The game's own press branch takes the item under the pointer from the
`Item` layer FIRST and makes it the dragged item — `PlayerCamera.TryPickupFromWorld` (`PlayerCamera.cs:1423`, `:1423-1441`:
`Physics2D.OverlapPoint(…, LayerMask.GetMask(new string[] { "Item" }))` → `if (collider2D.TryGetComponent<Item>(ref item)) { this.dragItem = item; return; }`),
with the body/limb layers and the descriptor/ground layers as the fallbacks and `this.dragItem = invButton.GetItem()`
(`:1413`) / `this.dragItem = itemLabel.refItem` (`:1407`) as the OTHER two entrances.

A world item is therefore a legal drag start, and its HAND-OVER has an explicit DESTINATION — the slot or container the player aimed at on the
teammate. That is the whole design input (user ruling 2026-10-25): the transfer is one move from the world to that destination, NOT a pickup into
some slot of the giver's body followed by a second move. The intermediate step a first implementation tried (pick it up into the first free slot,
then give) was rejected on the machine twice: it swallowed the item, and once the give was refused the item stayed in the giver's body **at the
arbitrary slot the pickup had chosen** — the user's "the items ended up in the host's backpack, in completely wrong positions". A destination the
player did not choose is never a place to put their item, not even for one frame.

Until the direct transfer lands, the gesture is refused by name and the finding is
`docs/backlog/review/give-from-the-ground-swallows-the-item.md`; the player picks the item up first, which is the native two-step and never loses it.

**Which of the two a release is, is decided by the DATA, not by how the gesture started.** The release asks the authority whether this
body's own row carries the item (`IItemControl.OwnsCarriedItem` → `ItemKernelAuthority.OwnsCarriedItem` → `ItemCustodyRows.OwnsCarried`):
carried means the give is legal whatever the pointer was over, and not-carried means the named world-item refusal above. That rule replaced
a classifier that had answered "world item" for every release — a 2026-10-25 session showed a waterbottle that had just been picked up
natively and was sitting in the giver's own slot refused as a ground item (`.acceptance/batch-givesound`, `[RemoteIntent] refused the release
of the ground item waterbottle(Clone)`), which made every give impossible and was reported by the player as the missing transfer sound.
The refusal is never a fallback for "not sure": an unclassifiable release keeps it, and a release that IS carried never takes it.

## What the game itself does

Both directions are the game's own custody operations: an item moves into another holder through the game's container and
pickup path (`Container.LoadItem` for a container, the body's own pickup for a slot), and leaves one through the
container's unload path. The transfer's sound, the container's refresh and the item's own presentation are produced by
that path, not by the gesture.

**The sound is the game's own call, made on the moving player's client.** The native release that loads an item into a
backpack runs `container.UnloadItem` → `container.LoadItem` → `this.PlayBackpackSound()` (`PlayerCamera.cs:1557-1570`), and
`PlayBackpackSound` is `Sound.Play("backpack" + Random.Range(1, 3).ToString(), …)` (`PlayerCamera.cs:44`). A cross-player
give is that same release with somebody else's backpack as the destination, so the giver's client makes the same call when
the item really leaves its body (`src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionApply.cs`: "The game's own move
sound: natively this release is UnloadItem + LoadItem + PlayBackpackSound, and a cross-player give is the same action with
somebody else's backpack").

## Rules and states

- **A proxied drag's intents ARE the native body's own calls, so the body must run.** The release window does not classify a gesture on its own:
  `RemoteDragIntentCapture` records what the native body did (`CaptureDropItem`, `CapturePickUp`, `CaptureUseItem`, `CaptureWearItem`, …). Clearing the
  drag before the body ran — an attempt to stop a proxy being mutated — removed those calls, and every proxied release answered "produced no intent"
  (measured 2026-10-26, then reverted). What must NOT happen is a REPORTED local drop for an item this client does not own; that half is the open
  ticket `../backlog/review/drop-out-of-a-remote-backpack-keeps-the-item.md`.
- **A local item released onto the other player's DISPLAY PROXY is refused, not landed.** The accepted reading is that "the
  release is refused at the release seam before the native body runs — the owner's probe names the target as the other
  player's display proxy and reads its contents unchanged (`castContentsBefore: 0` → `castContentsAfter: 0`,
  `childInCast: false`), one `[RemoteIntent]` line names the item and the target, the owner's item tree is byte-identical to
  the pre-gesture one", and the local control still runs the native move.
- **The gesture's context is one pure decision, re-read every frame.** `src/CasualtiesUnknownOnline.GameAdapter/GiveGestureModePolicy.cs`
  decides three modes — own UI wins over a displayed backpack, and a displayed backpack wins over the world — reading them
  from the game's own state every frame rather than from the last input that changed it, "because the native while-dragging
  body forces the ring open when the cursor crosses a body collider, so a mode inferred from 'the player pressed Tab' would
  be wrong the moment the native path changes it".
- **The destination is a LADDER, and its rung travels with the transfer.** The user's 2026-10-17 ruling gives the order the
  code implements: `src/CasualtiesUnknownOnline.GameState/Domains/Players/PlayerInventoryDestination.cs` declares `BodySlot`
  (the default, "every transfer that names no container"), `Container` (a container the recipient carries or wears) and `Drop`
  ("a world drop at the recipient's own position — the last rung, when no slot and no container"), and the chosen rung
  "travels with the transfer fact so the receiving client applies the rung the host chose instead of re-deciding it".
- **A transfer between two bags never becomes a world item.** `TransferItemCommand` is its own command because "a carried
  item changes owner without leaving the carried location (a cross-player transfer from one bag to another). This is not a
  pickup/drop: the item never enters World" — so a transfer is one custody change, not a drop followed by a pickup.
- The two destinations are mutually exclusive: a release over an ITEM means "into that container", a release over a slot
  ring means "onto that body slot".
- **The displayed backpack's ring shows its USE CIRCLE, and the release on that circle is R10.** The remote view draws the focused clone's own
  native ring — body slots around it and the inner use circle — because the highlight a player reads is `radialCircle.color`, which only the
  game's own while-dragging body sets (`PlayerCamera.cs:1791-1800`, when the pointer is on the `RadialCenter` tag AND the ring is open AND the
  pointer is inside the circle). Hiding the component therefore hid the affordance and removed the tag from every UI raycast at once, and the
  release fell through to the world drop: the user's 2026-10-25 report ("dragging the flashlight to the central use area showed no circular
  highlight, and the item fell out") is that pair. The rule: the ring including its circle is the game's, the centre release is R10's
  (`PlayerCamera.cs:1640`), and the intent it becomes is the owner's replay.
- A pack that is view-only accepts neither direction, and the gesture is refused before anything is sent.
- An item without an authoritative identity cannot be named, and the gesture is refused instead of transferring "the
  first match".
- A destination that no longer holds when the host applies the transfer is refused by name; the item stays where it was.
- **A refused inventory intent comes back as an answer, not as silence.** `InventoryIntentRefusedHandler` states the defect
  it closed: "before this arrived, a refused gesture was indistinguishable from a gesture that silently did nothing … the
  client had already written its positive capture line, and every refusal lived in the host's log" — and the rule it serves
  is the family's own: "a report the receiving side contradicts is answered, [so both sides end] up agreeing".
- **The answer names where the item WENT, not where it was aimed.** The host's ledger keeps the destination it was told and
  must be corrected by the placement outcome: the recorded defect is "The host's ledger keeps the destination it was told,
  not where the item went", with rows read on the machine and its 60 ms window judged by the user's ruling because it
  cannot be produced by hand.
- A container that cannot hold the item (weight, kind) refuses it by name.
- **A trade is priced from the item's own value, and the trader's multiplier is a curve over reputation.**
  `ItemInfo.GetValue` (`ItemInfo.cs:28-50`) is "`value * condition`", scaled by remaining rounds for ammo and by the item's
  liquids ("`amount * 0.001f * valuePerLiter`", capped at `50f`); `DefaultValue()` caps a default liquid stack at `1.5f`; and
  `TraderScript.ValueMultiplier()` is `valueMultiplierCurve.Evaluate(reputation * 0.01f)` — so a trade is arithmetic on the
  item's own state and the trader's standing, never a CUO price table.

## Refusals

| Condition | The player reads |
|---|---|
| the pack is view-only | the backpack notice's own "gesture refused" line |
| the item has no identity, or the target is not a member | the gesture is refused with no wire traffic |
| the destination is gone or full | the answer names the destination that no longer holds |
| the host rule forbids remote inventory manipulation | the host's rule name |

Each of these reaches the player as a sentence in their own language; the wire carries the key, never prose.

**A release names ONE destination, and the applier reads that one value.** A hand-over's destination is either a body slot or a container — never
both, never neither — and the receiver's placement branch consumes the SAME value the sender's release named. The rule exists because the two operands
travelled separately and one branch read only the slot: a ground item dropped into a teammate's trashbag landed in their body instead, because the
world-item branch of `RemoteIntentApplier.ApplyPickUpToSlot` never looked at the container operand (user report, 2026-10-26). Every placement branch
now takes both operands and answers the container first (`ApplyMoveIntoContainer`, the game's own `CanHoldItem` guard), so a destination cannot be
silently replaced by another.
## Who judges

The host applies the custody change, because custody is a host fact. The receiving client then materialises the item in
its own inventory and shows it; the giving client removes it from its own. A member's request travels to the host and the
host's answer travels back, so a refusal is visible on the side that asked.

## What travels

The item's identity, the two players, the destination (a slot or a container), and — when it fits — the container inside
the destination. Nothing about the item's appearance travels: each client shows the item from the state it received.

## What the player sees and hears

The item leaves the giver's hands and appears in the receiver's pack or slot. The transfer is the game's own, so its
sound and the container's refresh accompany it.

## Records

`../../architecture/surfaces.md` (inventory give/take row), `../model/item-custody.md`, decisions 155 and 156. Accepted:
`../../backlog/done/local-item-into-remote-display.md`, `done/remote-backpack-access-policy.md`.

## Gaps

- The transfer's own sound is not yet reproduced; recorded in `../../backlog/done/give-into-backpack-plays-no-sound.md`.
- A take that lands in the taker's own backpack container (rather than a body slot) has no dedicated page yet.
