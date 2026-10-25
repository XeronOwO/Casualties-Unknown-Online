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

**(3) How the remote case reaches that same chain — and the honest limit.** The release must put the item into the consumer's body
slot at that moment and then run the game's own use on it; when the slot is unavailable the game's own pickup rule refuses and the
operator reads why. There is **no native "use an item directly out of a backpack"**: locally a player takes it out first. So "drag
straight from a teammate's displayed backpack into their mouth" has no native counterpart at all — that expectation is the user's
decision, not a chain to invent. What this ticket holds as the corrected shape is therefore: transfer into the consumer's own slot as
part of the release, then the native use, with a refusal rather than a silent consume when the slot is unavailable.

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
- [ ] **4. Decide the corrected shape and write it in the ticket** — the give must land in the eater's own inventory through the game's
  own container path, and the eat must run `Body.UseItem` on the carried item.
- [ ] **5. Implement + red test.** A test that pins the corrected shape (the eat of a closed fruit is refused or peels first; the item's
  condition agrees on both clients), red before the change.
- [ ] **6. Runtime verification with frames and both clients**: give, then eat, reading the owner's and the operator's item state, the
  peel step's presence, and a frame at the eating moment.

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
