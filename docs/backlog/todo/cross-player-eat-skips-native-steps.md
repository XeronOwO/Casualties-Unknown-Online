# A cross-player eat skips the game's own eating steps and swallows the whole item

- Status: Todo — opened 2026-10-25 from the user's report (translated): "I dragged a pancake and a stone fruit into the guest's backpack, then
  dragged them onto the guest to use, and it ate the WHOLE pancake in one go! Then it 'ate' the stone fruit! The stone fruit's eating has a
  peeling step!".
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
only as the record of what was rejected and why (translated): *"I explicitly rejected give-first then eat — what if the other player's
backpack has no free slot? That is absolutely wrong, there is no room for negotiation. The other player has to eat it out of my hand"*.
So the shape is: **the item stays in the giver's hand and the target consumes it out of that
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
- [x] **4. The user's ruling (2026-10-25) — the shape, settled.** **"The other player eats it out of my hand"** (translated): the item
  never leaves the giver. The transfer-first shape (A) is REJECTED outright and without qualification — the user's reason is concrete
  (translated): *"what if the other player's backpack has no free slot? That is absolutely wrong, there is no room for negotiation"* — a
  target with a full inventory must still be feedable, so a
  shape that needs a free slot is wrong by construction. The hand case is **an EXTENSION of the native action, not a deviation
  from it** (the user's correction): the game's own `useAction` still runs and still produces its effects; only the body whose
  hand holds the item differs from the single-player case, which is what a co-op feed is.
- [x] **5. Resolution order implemented and gated.** `NativeSolidFoodEat.Apply` now resolves the EATER's own carried item FIRST
  (`CarriedItemLocator.TryFind`, the recursive carried-tree walk), and only when the eater does not carry it does it fall back to
  the standing object for the giver's item — the hand case. Both arms keep the world-row guard (the carried one asks
  `ItemWorldSync.IsWorldItem`, the hand case asks `StandingItems.Is`). Pinned by
  `SolidFoodChainGateTests.TheEat_ResolvesTheEatersOwnCarriedItemFirst_ThenTheHandCase`, which asserts the ORDER, both arms and
  the single `Body.UseItem` call.
- [x] **6. The hand case's slot steps — the ARRANGEMENT half landed.** `NativeSolidFoodEat.Apply` now parents the giver's
  standing item into the EATER's first EMPTY hand slot (`slots[i].transform`, the game's own slot shape), runs the delegate, and
  takes it back out in a `finally` — only when it is still the same live object in the slot it arranged, so a consumed or
  re-parented item is not yanked out of where the game's own step put it. Two hands full means no arrangement and the previous
  behaviour, deliberately: nothing here may need a free slot, per the ruling. The arrangement runs under a new
  `CallContext.Origin.HandFeed` scope, and the pickup/drop patches honour it — the parented item is never reported as a local
  pickup, and the delegate's `DropItem` is never reported as this body dropping the giver's item. This is what makes
  `Body.SlotOf` answer the real slot (instead of its own `return 0` fallback) and `Body.GetItem` read the arranged child, so the
  peel's replacement lands in the slot the closed fruit occupied rather than by luck.
- [x] **7. The replacement's ownership — RESOLVED and IMPLEMENTED the way the user described it.** The ruling (translated): *"keep the
  original position; the player just sees the item become the peeled one, no other visible behaviour — that is how the original
  works as I remember it"* — keep the original position, the player sees the item
  become the peeled one, no other visible behaviour. Verified against the decompiled delegate (`Item.cs:2606-2620`: the
  opened fruit is instantiated at the closed one's position and rotation and put back into the SAME slot index), so the
  ruling and the game agree. Implemented as three links: the eating client watches the object the delegate leaves in the
  arranged hand slot and DESTROYS it there (it must never enter the eater's inventory), reporting its definition and
  condition through `PlayerItemActionOutcomeMsg.ReplacementItemId`/`ReplacementCondition`; the host commits the owner's row
  with that definition and condition, same instance id and same slot; the owner's own client swaps its object in place
  (`ItemDefinitionSwap.InPlace`), so the item on screen simply becomes the peeled one. Admission stopped refusing the shape
  (`SolidFoodAdmission.IsFeedable` carries `EatsAndReplaces`, the named refusal in `PlayerItemUseExecution` is gone, and the
  two family tests were rewritten to the new rule), pinned by
  `ItemUseTests.Use_OfAFoodThatHandsTheEaterAReplacement_CarriesTheNewDefinitionBackToItsOwner`.
- [ ] **8. Runtime verification with frames and both clients**: drag a pancake and a closed stone fruit onto a teammate whose
  inventory is FULL, then read: the peel step happened, the peeled fruit is registered on both sides, the remainder's condition
  agrees on both clients, and a frame at the eating moment shows the eater's own animation and the food's own sprite.

  **The plan, and what of it landed (2026-10-25):**
  1. ✅ The eater's client holds the giver's item as a STANDING object; the giver's BODY is a render clone there, so the owner's
     real slots are unreachable from this client — confirmed, and it is why the shape makes the stand-in occupy a hand slot.
  2. ✅ Arrangement done with the game's own slot shape (`slots[i].transform`) rather than `Body.PickUpItem`, because `PickUpItem`
     runs the capacity/distance checks and would itself be a custody claim; the `HandFeed` scope suppresses the pickup/drop
     reports either way.
  3. ◑ `SlotOf`/`GetItem` now answer truthfully through the arrangement (no patch needed — the item really is the slot's child).
     `DropItem` is suppressed as a REPORT but still runs, so a consumed item is detached by the game itself. **Not done:** the
     replacement's registration and ownership — see step 7, which is a design question rather than a mechanical one.
  4. ✅ The stand-in is restored in a `finally`, and the report → host arbitration → result path still carries `condition` back to
     the giver's real item.
  5. ✅ Red-first gate extended: the arrangement's existence, its position BEFORE the delegate call, and the `HandFeed` scope in
     both the eat and the patches.

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

## Step 8 read in process (2026-10-26 21:22): the pancake half is judged, the peel still needs a hand

Driven with `use-request` (the request `IPlayerInteractionControl.SendUseRequest` sends) from the giver's client at a teammate, on the deployed
build — the eater ran the game's own eat out of the giver's hand:

```
21:22:42.351 (eater) [ItemUsed] pancake (id 1331760842691) is a standing item object — the eat's outcome reported to the host for its owner
21:22:42.351 (eater) [ItemUse] ran the game's own eat of pancake (id 1331760842691) on the local body out of the giver's hand (arranged int…)
```

and the numbers are the game's: the pancake's condition on its OWNER's copy fell to **0.7474** — the native pancake delegate's one bite is
`item.condition -= 0.251f`, so this is one bite and not a swallowed item — the item is still in the owner's `slot0`, and the eater's hunger read
116.35. That is the ticket's first complaint ("it ate the WHOLE pancake in one go") answered on the machine.

**The peel half is not judgeable through this driver**: a bare use request for `stonefruitclosed` is refused by the entry classifier ("is empty or is
not a family this [gesture] carries"), so the closed fruit's delegate only runs through the real drag — the row stays owed, together with the frame.

## The closed fruit is a PEEL, and the classifier says so (2026-10-26, read at the source)

The refusal the bare request met (`stonefruitclosed … is not a family this [gesture] carries`) is the classifier being RIGHT by its own definition, not
a gap in it: `GameSolidFoodFacts` decides "is this a solid food" by reading the item's own `useAction` instructions for a `Body.Eat`/`Body.Drink` call,
and the closed stone fruit's action calls `SlotOf`/`DropItem`/`Instantiate(stonefruitopen)`/`PickUpItem` — it PEELS, and the eating happens on the opened
fruit afterwards, as a second gesture. `use-eligibility mode=one type=stonefruitclosed` answers `isUseItem false` while `type=pancake` answers `true`,
which matches the natives.

So today the gesture is REFUSED BY NAME rather than run, and the ticket''s remaining question is a product one, not a code one: should a cross-player
gesture be able to PEEL a closed fruit into the eater''s hand (a new family whose native action runs on the eater''s client and whose replacement the owner
reports), or is "the fruit cannot be used on somebody" the accepted answer?

## The peel's refusal was the verdict's, and the user's ruling fixed the family (2026-10-26)

Read on the live client: `use-request target=<guest> item=<closed stone fruit>` was refused with "stonefruitclosed … is empty or is not a
family this gesture carries". The game's own data says the opposite — `usable = true` and a `useAction` (`Item.cs:2600-2621`) — so the refusal
was `GameSolidFoodFacts`' feeding requirement: the delegate never calls `Body.Eat`/`Body.Drink`, and the verdict was keyed on that alone.

User ruling the same evening: "run the item's own action for any usable item". Implemented as the minimal true shape: the replacement is read
FIRST, a replacing-but-not-feeding action answers `SolidFoodVerdict.Replaces`, `SolidFoodAdmission.IsFeedable` carries it, and the existing
execution runs the affected side's `Body.UseItem` and reports the definition it became (so the owner's item becomes it in place — the same
arrangement `EatsAndReplaces` already used). An action that neither feeds nor replaces stays `NotSolidFood`.

Pinned by `SolidFoodAdmissionTests.AUseActionThatReplacesTheItemWithoutFeeding_IsThisFamily` (with a control: `scrapmetal` is still refused).
**Owed**: the live row — the same `use-request` now peeling the fruit on the target, read from both clients' rows.

## The live peel run: the step runs, and TWO defects follow it (2026-10-26 22:26, deployed `83b4b506`)

The peel now runs end to end — the closed stone fruit is consumed and a `stonefruitopen` exists:

```
22:26:52.170 [ItemUse] 76561198281246659 offers stonefruitclosed (id 1280221235139) to 76561198863287957 — …
22:26:52.277 [ItemUse] local item 1280221235139 condition set to 0.00 by cross-player use.
22:26:52.291 [ItemTrace] op=7 item=1280221235139 origin=OnItemDestroyed result=Committed(1) events=[Destroyed]
22:26:52.325 [ItemSpawn] materializing stonefruitopen (id 22377858709) at (0.0,0.0), vel (0.0,0.0), container 0.
```

Two defects follow it, both read from the clients' own rows and censuses:

1. **The replacement lands in the WORLD at (0,0), not in the eater's hand.** The rows say the closed fruit is `Terminal` and the open fruit is
   `World`/owner 0 at the origin — while the design (`items/food.md`) says the player sees "the item in my hand became a peeled one", and the
   delegate itself does `PickUpItem(Instantiate("stonefruitopen"), …)` on the affected side. So the creation's report loses its PLACEMENT: what
   travels is only the definition, and the owner materializes it as a loose world object at 0,0.
2. **The new item TOOK AN ID THAT WAS IN USE THIS SESSION.** `22377858709` was the guest's own waterbottle in slot 0 minutes earlier (census at
   22:12: `slot0=waterbottle#22377858709`), and after the peel that slot is empty while the id now names the `stonefruitopen` row (`World`,
   revision 1). That is an id collision in the creation-registration path — the replacement must be registered under a freshly allocated
   authoritative id, never one this session already used.

**Row 5 verdict**: the peel STEP now runs, but its custody does not agree after the replacement — rows 1-2 above are the disagreement, so the row
stays FAILING until the placement and the fresh id are fixed.