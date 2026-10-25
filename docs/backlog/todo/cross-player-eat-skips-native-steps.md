# A cross-player eat skips the game's own eating steps and swallows the whole item

- Status: Todo — opened 2026-10-25 from the user's report: "我把煎饼和石果拖到客机背包里,然后拖给客机使用,它一次性把整个煎饼吃完了!然后把石果'吃了'!石果的吃有个去皮的步骤的啊".
- Priority: High
- Category: Cross-player item use / native parity (consume family)
- Source: user report (2026-10-25).

## Goal

A cross-player eat runs the game's OWN consume flow on the client that owns the body, so everything the local eat does happens:
the item's own `useAction`, the peeling step a stone fruit requires, the bite-by-bite consumption and its condition change, the
sound and the presentation. CUO routes the gesture; it must not apply a consume of its own.

## Current behaviour (what the log already shows)

The host's chain for the pancake was

```
[ItemUse] 76561198281246659 offers pancake (id 1151372216259)
[DragUse] used pancake on 76561198863287957 (instance 1151372216259)
[ItemUse] 76561198863287957 ate pancake (id 1151372216259)
[ItemUse] local item 1151372216259 condition set to 0.71 by cross-player use.
```

so the host reports the target as having "eaten" the item and adjusts the item's condition — and the user observed the whole pancake
gone in one gesture, and the stone fruit eaten with no peeling step at all.

## What the code actually does (read before answering the report)

The eat is NOT re-implemented: the eater's own client runs `NativeSolidFoodEat.Apply`, which resolves the offered item and calls
**`body.UseItem(item)`** — the game's own `Body.UseItem → Stats.useAction` — so the hunger clamp, the rolls, the condition cost and
the item's own clips are the game's. The host commits nothing for this family; it admits the request and publishes
`TargetEatsTheItem`, and the eater reports the item's resulting state back.

That makes this ticket a DESIGN question rather than a missing call, and the reports point at three concrete gaps in the shape:

1. **The eat runs on a STANDING OBJECT, not on an item in the eater's own inventory.** The item is materialised for the gesture and
   resolved through `StandingMaterializer`, so `Body.UseItem` is handed an incarnation that is not the eater's carried item. The
   game's own flow for a carried item (the hand, the multi-bite rule, the peel step a closed stone fruit requires) is keyed on the
   carried state, and this is the likeliest reason the whole pancake went at once and the fruit was eaten without peeling.
2. **The item's resulting state disagrees between the two clients.** After the pancake eat the operator's copy read
   `condition 0.71` while the target's copy was gone — the report path is not carrying the state back to the owner's item the way the
   comment claims.
3. **The peel step is a native action of its own.** A closed stone fruit (`stonefruitclosed`) must be peeled before it is eaten; if
   the game reaches that step only for a carried item, the standing-object shape skips it by construction.

The corrected design this ticket exists to argue: the give puts the item into the eater's OWN inventory through the game's own
container/pickup path (so the item IS the eater's carried item), and the eat then runs `Body.UseItem` on that carried item — with no
standing object standing in for it. That is what "reproduce the native flow" means here, and it is the shape the rows below judge.

**With steps 1-2 read, the corrected shape is sharper than "run the native call":** the delegate's own steps are `SlotOf` →
`DropItem` → `Instantiate` → `PickUpItem`, so the eat must run on a CARRIED item (the eater's slot), and the object the delegate
instantiates from `Resources.Load("stonefruitopen")` must be **registered** as a creation the way any other created item is
(`item-creation-registration-first`'s family) — otherwise the replacement fruit exists only on the eater's client. Step 3 answers
where a give lands today, and that decides whether the carried-item shape is reachable.

## The three lines the design must start from (written BEFORE any code, per the rule)

**(1) The player's action and expectation.** I drag an item onto a teammate and let go; that teammate consumes it on the spot, with
the same effects as when I consume it locally (peel, bite, sound, animation, condition, the picture). The item is MINE to hand over;
the player-facing rule is "I use it on them", not "I first give it to them and then they use it".

**(2) What the game itself runs for the same local action.** `Body.cs` invokes an item's `useAction` at two places, both on an item
that a BODY SLOT holds (`this.GetItem(num).Stats.useAction(...)` and the item handed to `Body.UseItem`). The food delegates depend on
that: a closed stone fruit calls `Body.SlotOf(item)`, `DropItem`, then instantiates and picks up the opened replacement. So in the
game's own model, **consume/use means the item is in the consumer's body slot** — a container's contents are not a body slot.

**(3) How the remote case reaches that same chain — SUPERSEDED by the user's ruling below.** This paragraph used to ask the
release to transfer the item into the consumer's slot first. The user rejected that outright on 2026-10-25 and it is kept here
only as the record of what was rejected and why: *"我明确否定过先给后吃，对方背包没有空位怎么办？这个是绝对错误的，没有任何讨价还价
余地的。必须是对方就着我的手吃"*. So the shape is: **the item stays in the giver's hand and the target consumes it out of that
hand** — an extension of the native action rather than a deviation from it, because the game's own `useAction` still runs and
still produces its effects; only the body whose hand holds the item differs from the single-player case. The remaining defect is
that the delegates' own slot calls (`SlotOf`, `DropItem`, `PickUpItem`) must resolve against the OWNER's body while the effects
land on the eater (run sheet step 6).

## Run sheet (cycle 2026-10-25)

- [x] **1. Read the native local eat of a closed stone fruit — DONE, and it is a slot-dependent delegate.**
  `Item.cs` declares `stonefruitclosed`'s `useAction` as: `body.stamina -= 30f; body.energy -= 5f; body.limbs[0].pain += 10f;
  body.DropItem(2); Sound.Play("drop", …); body.eatTime = 0.5f; int slot = body.SlotOf(item); item.condition = 0f;
  body.DropItem(item); body.PickUpItem(Instantiate(Resources.Load("stonefruitopen"), item.transform.position …), …)`. So the "peel"
  the user means is this delegate: it CONSUMES the closed fruit (condition 0), drops the item from the body, and REPLACES it with a
  newly instantiated `stonefruitopen` it picks up. Everything in it reads the body's own slots (`SlotOf`, `DropItem`, `PickUpItem`).
  The same read for `pancake`: `body.Eat(10f, 1.2f); body.happiness += 1.25f; body.sicknessAmount += 1.5f; item.condition -= 0.251f;
  Sound.Play("eatFlesh", …); body.talker.EatGood()` — one bite is −0.251 condition, which is exactly the 0.71 the operator's copy
  showed.
- [x] **2. Read what the cross-player path calls — DONE: it hands `Body.UseItem` a STAND-IN, so every slot step misfires.**
  `NativeSolidFoodEat.Apply` resolves the item through `StandingMaterializer.TryGetStandingItem` and calls `body.UseItem(item)` on
  that object. A standing object is by definition not the eater's carried item, so inside the delegate `body.SlotOf(item)` answers
  `-1`, `body.DropItem(item)` drops an object the eater does not carry, and the instantiated `stonefruitopen` is picked up LOCALLY —
  a client-side object the host's tables never registered. That is the whole of the observed behaviour: the closed fruit is consumed
  for the owner, no peel→open replacement reaches the authoritative tables, and the guest is left holding an unregistered fruit.
- [x] **3. Read where a give lands — DONE, and it lands in the receiver's AUTHORITATIVE inventory, not on the proxy.**
  The release reports `RemoteDragIntent{ Kind = GiveToBody, ItemInstanceId, TargetBodySteamId, TargetSlotIndex }`
  (`RemoteDragIntentDispatcher.ReportLocalReleaseOntoProxy`) and the HOST applies it as a custody transfer
  (`PlayerInventoryTakeService.HandleRemoteGiveToBody` → `HandleTakeCore`, the same core the take direction uses). So the item
  becomes the receiver's carried row in the host's tables, and the receiver's own client is supposed to hold it as ITS carried item.
  That makes the eat's failure narrower than "the give is wrong": the eater's client already has (or can have) the item as a carried
  object, yet `NativeSolidFoodEat.Apply` resolves it through `StandingMaterializer` — the path for OTHER players' items — and hands
  that stand-in to `Body.UseItem`. The fix is therefore to resolve the item from the eater's OWN carried items first
  (`body.GetItem(slot)`/`SlotOf`), and only fall back to a standing object when the body really does not carry it.
- [x] **4. The user's ruling (2026-10-25) — the shape, settled.** **"必须是对方就着我的手吃"**: the item never leaves the
  giver. The transfer-first shape (A) is REJECTED outright and without qualification — the user's reason is concrete: *"对方背包
  没有空位怎么办？这个是绝对错误的，没有任何讨价还价余地的"* — a target with a full inventory must still be feedable, so a
  shape that needs a free slot is wrong by construction. The hand case is **an EXTENSION of the native action, not a deviation
  from it** (the user's correction): the game's own `useAction` still runs and still produces its effects; only the body whose
  hand holds the item differs from the single-player case, which is what a co-op feed is.
- [x] **5. Resolution order implemented and gated.** `NativeSolidFoodEat.Apply` now resolves the EATER's own carried item FIRST
  (`CarriedItemLocator.TryFind`, the recursive carried-tree walk), and only when the eater does not carry it does it fall back to
  the standing object for the giver's item — the hand case. Both arms keep the world-row guard (the carried one asks
  `ItemWorldSync.IsWorldItem`, the hand case asks `StandingItems.Is`). Pinned by
  `SolidFoodChainGateTests.TheEat_ResolvesTheEatersOwnCarriedItemFirst_ThenTheHandCase`, which asserts the ORDER, both arms and
  the single `Body.UseItem` call.
- [ ] **6. The hand case's own slot steps (the remaining defect).** Inside the food delegates the slot calls
  (`SlotOf`, `DropItem`, `PickUpItem`) resolve against the EATER's body, which does not hold the giver's item: the closed fruit's
  peel therefore consumes for the owner with no `stonefruitopen` reaching the tables, and a multi-bite food's bite reads as the
  whole item. The fix must point those steps at the OWNER's body while the effects land on the eater — a real change to what the
  delegate sees, not a stand-in swap. Start from the two delegate bodies recorded in step 1, and check every slot call in them.

  **The plan (written before the code, so the next cycle starts here rather than re-deriving it):**
  1. The eater's client holds the giver's item as a STANDING object; the giver's BODY is a render clone there, so the real
     owner's slots are unreachable from this client. The hand shape therefore cannot be "redirect the calls to the owner's
     slots" — it has to make the stand-in occupy a HAND SLOT locally for the duration of the delegated call.
  2. Inside the `CharacterItemUse` scope, arrange the stand-in into the eater's hand slot with the game's own slot plumbing
     (`Body.PickUpItem`) while suppressing the custody reports that would otherwise claim a transfer — the CALL is
     `CallContext`, which already exists for exactly this "the scope says what is happening" reason.
  3. Intercept the three slot calls the delegates make, inside that scope only: `SlotOf` answers the arranged slot,
     `DropItem(item)` DETACHES the stand-in instead of creating a world drop, and `PickUpItem(replacement, slot)` registers the
     replacement as a CREATED item owned by the giver (the `item-creation-registration-first` family) so `stonefruitopen` reaches
     both sides' tables instead of existing only here.
  4. Restore the stand-in to its standing state on the way out, and let the existing report → host arbitration → result path carry
     `condition` back to the giver's real item, which is the half that already works.
  5. Red first: a gate/source assertion that the three slot calls are guarded by the hand-feed scope and that the replacement is
     registered, plus the existing behaviour rows below. Runtime verification (step 7) is what decides whether the arrangement is
     invisible to the player, because step 3 is exactly the kind of local rearrangement that can show up on screen.
- [ ] **7. Runtime verification with frames and both clients**: drag a pancake and a closed stone fruit onto a teammate with a
  FULL inventory, then read: the peel step happened, the opened fruit is registered, the remainder's condition agrees on both
  clients, and a frame at the eating moment shows the eater's own animation and the food's own sprite.

Failure branch: if step 2 shows the standing object IS the eater's carried item, the ticket's premise is wrong and it closes with that
reading instead of a code change; if step 3 shows the give cannot land in a carried slot for a remote body, the ticket records that
capability gap and stops at the design.

## Acceptance rows

| Row | The check |
|---|---|
| 1 | A cross-player eat produces the SAME native call in the owner's log as a local eat of the same item, captured in one batch |
| 2 | A stone fruit given to a teammate and used is refused or routed to its peel step first, exactly as a local eat would be — read on the target's client |
| 3 | The pancake is consumed the way a local eat consumes it (bites, condition, its own sound), not whole in one gesture |
| 4 | The item's remaining condition agrees between the operator's and the target's tables after the eat |

## Limits

- The consume family's target gate already refuses an unconscious target (a separate ticket questions the sleeping case); this
  ticket is about the FLOW once the use is admitted.
- The rows need the owner's client and the operator's client read in the same gesture, and a frame at the eating moment for the
  presentation row.
