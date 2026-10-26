# Food: eating an item

Eating food, and feeding a teammate.

## What the player does

Uses a food item on themselves, or drags it onto a teammate's use circle. The player expects one BITE per use — not the
whole item — with the food's own effect, the game's own eating sound, and the item's condition reduced by that bite.

## What the game itself does

A solid food's use action eats: it applies the food's own values to the body (hunger, happiness, sickness), reduces the
item's condition by the bite's own amount, plays the eating sound and produces the body's reaction. A food that consumes
itself and replaces itself with another item does so inside that same action, in terms of the body's slots.

## Where the family comes from

The cross-player food family is decided from the GAME'S OWN DATA, not from a list this project keeps: the accepted ticket's title is "Cross-player solid food from the game's own data", and its fifteen acceptance rows passed in batch `20261011-i` (row 5's visual half had to be re-worded because a frame cannot carry it at a parked spot). A definition the game marks usable but whose action is not an eat is not this family, and the same classification decides both the cue and the release.

**How the game's data is read, and how far.** `GameAdapter/Content/UseActionInstructions.cs` is "the one reader of an item's own `useAction` COMPILED BODY, shared by the content classifiers", and it reads "the instructions of the delegate one level down, the instructions of every GAME method it calls directly (a method merely loaded as a delegate or type token is not part of what the action does)", while "a mod's method one level down is not [read] — that would be a decompiler, not a classification". Reading is not running: "a classifier can ask what a use action WOULD do without applying it", and "a delegate whose body cannot be read (a dynamic method) answers null, and each caller decides its fail-closed verdict rather than guessing".

## Rules and states

- One use is one bite: the item survives with less condition until its own condition is exhausted.
- **The one food that REPLACES itself does it in place, and only under two conditions.** A closed stone fruit's `useAction`
  (`reversing/Assembly-CSharp/Assembly-CSharp/Item.cs:2606-2620`) runs only "`if (body.stamina > 30f && !body.disfigured)`"; inside
  it spends `stamina -= 30f`, `energy -= 5f`, `limbs[0].pain += 10f`, drops slot 2, plays `drop`, sets `eatTime = 0.5f`, reads
  `SlotOf(item)`, sets the fruit's `condition = 0f`, drops it, and picks up an instantiated `stonefruitopen` **at the closed
  fruit's own position and rotation, into the same slot**. So the player sees "the item in my hand became a peeled one" — which is
  what the user described, and what a cross-player feed has to reproduce rather than invent a transfer for.
- The eaten amount, the body's effects and the sound are the food's own data; CUO does not add values of its own.
- A food that leaves a container behind cannot be handed over: the gesture is refused rather than emptying the container.
- The eater must be conscious: eating is one of the two families where the TARGET consumes the item.
- A food that replaces itself (an opened version of itself) produces a NEW item in the eater's hands; that creation is a
  registered creation, not a local object.
- **A replacing action''s state report does not carry the condition.** The delegate zeroes the condition of the object it is about to swap, so that
  number describes the item BEFORE its replacement; applying it destroyed the owner''s object before the follow-up could make it the opened fruit
  (measured 2026-10-26: `replaced … in place` landed and was immediately overwritten by `condition set to 0.00`). For `SolidFoodVerdict.Replaces` the
  state report contributes nothing and the follow-up''s own condition is what the row takes.
- **One admitted use, one state report, and ONE follow-up for the replacement.** The grant table is keyed on (actor, item) and a grant is consumed by
  the report it authorizes; a use whose effect runs on the affected side and replaces the item reports its state first and the replacement definition
  second, because the eating client sees the created object only after its delegate returns. `ItemActionGrants` therefore remembers a settled pair for
  exactly one follow-up, which must carry a replacement — without it the second report was refused by name and the owner's row kept its old definition
  and died with its zeroed condition (measured 2026-10-26, the peel; pinned by `ItemUseTests.TheReplacementOutcome_IsTheAdmissionsOneFollowUp`).
- **The allocator asks BEFORE reading an existing id, and `Allocate` asks too.** A replacement object can arrive with an id already stamped, and
  returning that id was the last way the phantom kept the same instance id across five runs (2026-10-26). Both entry points therefore refuse first and
  then look at the component, so no reporting hook can name the replacement whatever it carries.
- **The refusal lives in the ALLOCATOR, not in each reporting hook.** Six hooks can announce a created item (instantiation, pickup, slot, use, container,
  carried-inventory) and every one of them funnels through `ItemIdAllocator.EnsureId`; guarding them one by one missed the one that fired (measured
  2026-10-26, four runs). While the replacement window is armed, `EnsureId` allocates nothing, records the object for the eat and logs the refusal — so
  no hook can turn the replacement into a new item, whatever path it takes.
- **The window is ARMED for the delegate's frame and the next, not only scoped.** The game's own pickup of the created object is not guaranteed to
  run inside `Body.UseItem`, so the hooks that report creations can fire after the scope has closed — a scope alone missed the replacement entirely
  (measured 2026-10-26: the object still got a fresh id and a world spawn while the eat's own log line never appeared). `ItemReplacementObservation`
  therefore arms for `Time.frameCount + 1` around the delegate and disarms when the eat reads the result, and both hooks ask the armed window as well
  as the scope.
- **Both hooks that see the replacement must answer the scope, and the object is published, not read back by slot.** The delegate instantiates
  the opened fruit AND picks it up (`Item.cs:2619`), so `ItemWorldSync.OnItemInstantiated` and `PickupSync.OnPickedUp` both see it; each records the
  object in `ItemReplacementObservation` and reports nothing. The eat reads that observation first and falls back to a slot read only for a delegate
  that leaves its object where the arranged item stood — reading the slot alone missed the fruit, because its own `PickUpItem` had already placed it
  in whichever slot the game found free (measured 2026-10-26: the eat reported no replacement, so the owner's row was destroyed instead of becoming
  the opened fruit).
- **The replacement the delegate creates is NOT a new item.** The object a replacing `useAction` instantiates is the OWNER's row becoming it in
  place — its identity and slot do not change, and only its definition travels back in the eat's outcome. So the creation is not registered: the
  eat runs the delegate inside `CallContext.Origin.ItemReplacementCreation`, and `ItemWorldSync.OnItemInstantiated` allocates no id and reports no
  spawn inside that scope. Before the scope existed the peel produced a phantom `stonefruitopen` at the world origin with a brand-new id while the
  owner's row became the replacement (measured 2026-10-26).
- **A use action that REPLACES the item without feeding a body is this family too** (user ruling 2026-10-26: "run the item's own
  action for any usable item"). The closed stone fruit is the case: its delegate never calls `Body.Eat`/`Body.Drink`, so a verdict
  keyed on "feeds a body" answered "not a solid food" for an item the game marks `usable = true` and the cross-player gesture refused
  the peel. `GameSolidFoodFacts` reads the replacement shape FIRST (`Instantiate`/`PickUpItem`/`Utils.Create`), answers
  `SolidFoodVerdict.Replaces` when the action replaces and does not feed, and `SolidFoodAdmission.IsFeedable` carries it; the execution
  is the existing one — the affected side runs `Body.UseItem` and reports the definition it became, so the owner's item becomes it in
  place. An action that neither feeds nor replaces is still `NotSolidFood`.

## Refusals

| Condition | The player reads |
|---|---|
| the food would leave something behind | `use.refused.food_leaves_something` |
| the target is unconscious | `use.refused.target_unconscious` |
| the target is dead | `use.refused.target_dead` |
| the item is not usable | `use.refused.not_this_way` |

## Who judges

The host validates and admits the request; the EATER's own client runs the game's eat on its own body and on the item it
carries, and reports what the item became. The host matches that report against the admission and hands the item's state
back to its owner.

## What travels

The item's identity and the target. The item's resulting condition travels back to its owner; the identity never changes
hands.

## What the player sees and hears

The eater chews, the game's eating sound plays, the body's reaction is the game's, and the item shows one bite missing —
on both clients.

## Records

Decisions 184 and 246. Accepted: `../../backlog/done/cross-player-item-use-random-determinism.md`. Open:
`../../backlog/todo/cross-player-eat-skips-native-steps.md` (the item must be the eater's CARRIED item, and a replacement
the food creates must be registered).

## Gaps

- The per-food values (how much a bite gives and costs) are the game's own table and are not tabulated here yet.
