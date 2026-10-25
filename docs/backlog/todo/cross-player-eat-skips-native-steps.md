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

## Run sheet (cycle 2026-10-25)

- [ ] **1. Read the native local eat of a closed stone fruit.** In `reversing/Assembly-CSharp`, find what the game runs when the player
  eats `stonefruitclosed`: the peel step's own function, the state it writes, and the condition under which `Body.UseItem` refuses or
  transforms the item. Evidence: the function names quoted with their file and line (the reversing tree is stable). Done when the
  native order (peel → eat) is written down as a sequence of calls.
- [ ] **2. Read what the cross-player path calls for the same item.** `NativeSolidFoodEat.Apply` hands `Body.UseItem` the item resolved
  from `StandingMaterializer`. Evidence: whether that object is the eater's carried item or a materialised stand-in, and what
  `Body.UseItem`'s own guards read (the hand, `carried`, the item's parent). Done when the difference from step 1 is named as a call
  the native flow makes and this one does not.
- [ ] **3. Read the give side's landing place.** Where the item given into another player's backpack actually lands on the target's
  client (a carried slot, a container, a standing object), and whether the target's inventory reports it as its own carried item.
  Evidence: the container/pickup call the give maps to.
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
