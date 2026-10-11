# The mod item's declaration reaches every field the game's own items carry

- Status: Done — **cut and landed 2026-10-11** out of `docs/backlog/todo/mod-content-ceiling.md` Part 3.A,
  whose only remaining entry is its "miscellaneous" list, and **accepted 2026-10-11 in batch `20261011-j`**
  (`docs/evidence/acceptance/mod-item-declaration-parity-20261011-j.md`): all seven rows pass, no residual. The
  code was complete and the independent FULL-tier review's findings were fixed in the same commit before the
  batch ran (see *Review round*). The umbrella's promotion rule puts "a defect of a surface the
  API already promises" ahead of the capability entries, and the family audit that cut this ticket found one:
  `ItemInfo.rec` decides whether the game shows the item's own name at all, and a mod item inherits a gate its
  author never declared and cannot change. The sibling
  `docs/backlog/review/mod-item-wearable-declaration.md` closes its non-goals with "each is its own entry with
  its own consumer, and none is a defect of a promised surface" — the `rec` half refutes that sentence, and
  this ticket corrects it in place.
- Priority: High
- Category: Mod platform / content binding
- Parent: `docs/backlog/todo/mod-content-ceiling.md` (Part 3.A)
- Related: `docs/backlog/review/mod-item-wearable-declaration.md` (the same build site, the same audit shape,
  and the claim this ticket corrects), `docs/backlog/done/mod-item-authored-actions.md` (the action half of
  the same surface, landed), `docs/backlog/review/mod-crafting-quality-labels.md` (Part 2 stage 1, whose
  "content surface parity" is the shape this ticket completes for the item kind),
  `docs/backlog/done/mod-declared-behaviour-with-no-function.md`,
  `docs/en/reference/mod-api.md`, `docs/en/reference/modification-policy.md`
- Source: the 2026-09-25 mod-ceiling inventory (Part 3.A's miscellaneous list), promoted 2026-10-07 by the
  user's directive that open-ended content customisation is the point of the platform; cut 2026-10-11.

## Why this ticket exists

`ModItemInfoFactory.Build` is the only place under `src/` that constructs the game's item type — one
`new ItemInfo`, `ModItemInfoFactory.cs:34` — and it writes the members a mod can declare. The game's own item
type carries **32 public instance fields**; the declaration surface reaches 23 of them (counted from the build
site's member list — the 32 and the 4 are what a test measures, the 23 is this ticket's own count). The nine
it does not are not a wish list: each is read by the game's own code, and one of them decides what the player
is told the item IS.

### The defect: a mod item is an "unknown object" whose tooltip quotes a number its author never wrote

The game's own data declares the gate almost everywhere — `Item.cs` carries 260
`rec = new Recognition(` assignments, and 201 of the 218 definitions it builds as a local assign it
(`new ItemInfo` ×165 + `new LiquidItemInfo` ×53). The framework writes it for none of them, and a mod has
no member that could, so a mod item keeps the value the type's own field initialiser already has:

```csharp
public Recognition rec = new Recognition(2);   // ItemInfo.cs:181
```

`Recognition.recognizable` is `min <= PlayerCamera.main.body.skills.INT` (<c>Recognition.cs:14-19</c>), so an
item nobody declared a gate for asks for **INT 2** — and the game's own content is not uniform about it
either: **24 of its definitions inherit the same 2** rather than declaring one. Seventeen are built as a
local and registered without ever assigning `rec` — the bags `bloodsac`, `venomgland`, `trashbag`,
`plasticbag`, `foliagebag`, `pouch`, `brokenbag`, `smallpack`, the `waterjug` container, and the clothing set
`balaclava`, `holidayhat`, `scarf`, `armwarmers`, `tornshirt`, `sneakers`, `tacticalboots`, `hoodie` — and
seven more are written as inline initialisers with no `rec` in the block. So the initialiser is not a gate
anyone chose for those either; what a mod author lacks is the ability to choose at all, and the consequence
below is what that costs. What that gate decides:

- `PlayerCamera.ItemHoverDescription` (<c>PlayerCamera.cs:1043-1055</c>) — the inventory hover replaces the
  item's name with `unknown<category>` or `unknownobject`, replaces its description with `unknownobjectdsc`,
  and appends `unknownobjectwarning` with `< >` substituted by **`item.Stats.rec.min`** — the game tells the
  player to reach INT 2 for an item whose author never asked for a gate, and no member exists that could
  change the number.
- `PlayerCamera.cs:2126` / `:2139` — the name of the item in the player's own hands, in the HUD.
- `MinigameBase.cs:223` — the item text of a minigame's own panel.
- `Body.cs:1487` / `:1496` — the two wear alerts name the item, or "unknown object".

The gate is reachable in normal play, not only on a low-INT character: `Skills.BaseSkills` hands out
`{16, 16, 0}` for character index 2 and all-zeroes for the `default` case (<c>Skills.cs:186-195</c>), and
`MindwipeScript.WipeRoutine` sets `skills.INT = 0` on a live body (<c>MindwipeScript.cs:81</c>). A mindwiped
member holding a mod item reads "unknown object" in the HUD and in the hover, while the vanilla items beside
it — all of which declare `rec.min` 0 — stay named.

### The family: nine fields, every one of them read by the game

The same audit over the remaining public fields of the game's item type, each with its reader:

| Field | Native reader | What it decides |
|---|---|---|
| `rec` | <c>PlayerCamera.cs:1043</c>, `:2126`, `:2139`, <c>MinigameBase.cs:223</c>, <c>Body.cs:1487</c>, `:1496` | whether the player is shown the item's own name and description at all, and which INT the tooltip asks for |
| `onlyHoldInHands` | <c>Body.cs:1390</c> (the slot pickup), <c>PlayerCamera.cs:1172</c>, <c>InvButton.cs:137</c> | the item is refused by every slot that is not a hand |
| `combineable` | <c>Body.cs:1297</c> (`Body.CanCombine`) | whether two of the item can be merged into one |
| `ignoreDepression` | <c>PlayerCamera.cs:745</c>, <c>InvButton.cs:85</c> | whether the medical cutoff locks the item out |
| `scaleWeightWithCondition` | <c>CarriedEncumbranceCalculator.cs:40</c> — CUO's own read of the vanilla flag | carried weight follows the item's remaining condition |
| `jumpHeightMultChange` | <c>Body.cs:2120-2122</c> | jump height, multiplied by `value + 1` |
| `slotRotation` | <c>Body.cs:1403</c> | the held item's `localEulerAngles.z` |
| `autoAttack` | <c>PlayerCamera.cs:1831</c>, `:1836` | the held attack button repeats, instead of needing a press each time |
| `decayInfo` bits 1 / 2 / 4 | <c>Item.cs:186-197</c> (`Item.HandleDecay`) and the tooltip at <c>PlayerCamera.cs:1147-1167</c> | the item decays only while it holds items / only while worn / only while its holder moves |
| `decayInfo` bit 16 | <c>Item.cs:198-201</c> | the decay drains the battery instead of the condition — **the framework already owns this bit, and it must keep owning it** (see the refusal below) |

`autoAttack` is written today, but only as a side effect of `Tool` and `Gun`; the other eight are written by
nothing, so the game's own value stands for every mod item. Eight of the nine are capabilities the API cannot
express; `rec` is the one where the game's own value is wrong for content its author described by hand, and
that is what makes this ticket a defect fix rather than a wish list.

## What the game's own items do with these fields (measured 2026-10-11)

| Question | Evidence |
|---|---|
| Does every vanilla item declare its recognition? | 260 `rec = new Recognition(` assignments in `Item.cs` against 218 item definitions (`new ItemInfo` ×165, `new LiquidItemInfo` ×53); the values span 0…18, and the low ones (0 and 1) are the mundane items |
| Is the gate player-visible? | <c>PlayerCamera.cs:1043-1055</c> builds the hover text from `rec.recognizable` and prints `rec.min` in the warning line |
| Where does the decay flag mask live? | <c>Item.cs:186-201</c>: bit 1 with `cont.itemCount == 0`, bit 2 with no parent or a container parent, bit 4 with no parent or a holder slower than 0.5, bit 16 through `this.battery.DrainCharge` |
| Which decay bits does a mod item get today? | only bit 16, and only from declaring `Battery` (`ModItemInfoFactory`: `decayInfo |= (byte)ItemInfo.DecayType.BatteryDecay`) |
| What is the range the game's own content uses for the jump multiplier? | two items declare it: `0.44f` (a booster) and `-0.1f` (a penalty) — <c>Item.cs</c> |

## Entry mapping

**No entry is added, and no cross-player chain is touched.** This ticket is a content declaration surface:
it decides which values a mod may put into the game's own static item table, on every peer, from the same
declaration — the same footing as `Weight`, `Usable` and `Qualities`, none of which crosses a wire. Nothing
here is a gesture, so nothing here mirrors a native call site, and the one field with an action behind it
(`autoAttack`) is the game's own held-button loop rather than a CUO gesture.

## Mechanism inventory

| # | Mechanism | Evidence | State |
|---|---|---|---|
| 1 | `ModItemInfoFactory.Build` writes the declaration onto the vanilla item | `ModItemInfoFactory.cs:34` is the only `new ItemInfo` under `src/` | the one place this change has to reach |
| 2 | `ItemInfo.rec` carries a field initialiser of 2 | `ItemInfo.cs:181` | defect: inherited by every mod item, undeclarable |
| 3 | The eight remaining fields | the family table above | capability no DTO member carries |
| 4 | The three decay conditions | `Item.HandleDecay` masks bits 1/2/4 | capability no DTO member carries |
| 5 | The battery decay bit | `ModItemInfoFactory` sets it from `Battery`; `Item.HandleDecay` dereferences `this.battery` behind it | correct today, and a hazard the moment the flags become declarable |
| 6 | The item provider's numeric refusals | `CustomItemBehaviorValidator` ("has an invalid Wearable numeric value — refused.") | where this ticket's two refusals belong |

## Plan

1. **The declaration**: `IModItemDefinition` / `ModItemDefinition` gain `RecognitionMinimum`, `OnlyHoldInHands`,
   `Combineable`, `IgnoreDepression`, `ScaleWeightWithCondition`, `AutoAttack`, `JumpHeightMultiplierChange`,
   `SlotRotation` and `DecayConditions`; `DecayConditions` is a `[Flags]` vocabulary
   (`ModItemDecayCondition`) whose three members ARE the game's own bits 1, 2 and 4 — the battery bit is
   deliberately not in it, because it follows `Battery` rather than a flag.
2. **The default is the author's intent, not the data class's leftover**: `RecognitionMinimum` defaults to `0`
   (always recognized), which is the defect fix. The other members default to the game's own defaults, so a
   definition that declares none of them builds the item it builds today.
3. **Materialize it**: `ModItemInfoFactory.Build` writes every member onto the vanilla item, the decay flags
   into the same `decayInfo` byte `Battery` already ORs into.
4. **Refuse what the game's own flow cannot survive**: a negative `RecognitionMinimum`; a
   `JumpHeightMultiplierChange` at or below `-1` (the game multiplies by `value + 1`, so the factor is zero or
   negative there); and any `DecayConditions` value carrying a bit outside the three declared members — bit 16
   in particular makes `Item.HandleDecay` dereference a `battery` component the declaration never asked for.
   All three are load-time refusals in the item provider's existing validator, naming the definition.
5. **Report the inert declaration**: `DecayConditions` with no positive `DecayMinutes` is written and reported
   at load — the same materialize-and-report shape the sibling ticket ruled for an unplaceable wearable.
6. **A census keeps the two lists tied**: a test drives the production provider and asserts that EVERY public
   instance field of the game's item type is reachable from a declaration, and that every `DecayType` flag is
   reachable from one — under a census floor, so a game field added later fails the suite instead of silently
   becoming undeclarable. An unreachable field is listed with a reason, and the list is empty after this change.
7. **Red first**: the recognition case is written against the frozen tree and must fail before the fix — the
   built item carries `rec.min == 2` for a definition that declared no gate.
8. **Docs**: the `mod-api` page in both blocks with its alignment record, the `Abstractions` public-surface
   baseline, the parent ticket's Part 3.A entry, the sibling ticket's corrected non-goal, the example mod
   (which implements the interface and so must declare the new members), and this ticket's own status.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| The defect (rows 2/3) | a mod item declares its recognition, and the default stops hiding the name its author wrote | red first on the frozen tree: `Item_DeclaringNoRecognitionGate_IsRecognizableByAnyPlayer` failed `Assert.Equal() Failure: Values differ / Expected: 0 / Actual: 2`, green after the factory writes `rec = new Recognition(definition.RecognitionMinimum)`; the readers are `PlayerCamera.cs:1043-1055` and the HUD lines |
| The family (row 3) | every public `ItemInfo` field is reachable from a declaration | `Item_DeclaringEveryField_ReachesEveryPublicFieldTheGameReads`: 32 of 32 public fields reached by two probe declarations, `reached.Count` printed in the failure message, under a floor of 30 |
| The decay conditions (rows 4/5) | three declarable bits, the battery bit refused by name | `TheDecayVocabulary_ReachesEveryFlagTheGameReads`: all four `ItemInfo.DecayType` values reached (bits 1/2/4 by the declaration, 16 by `Battery`), under a floor of 4; `Item_DeclaringADecayBitOutsideTheVocabulary_IsRefused` (bits 16 and 8) |
| The refusals (row 6) | negative recognition, a non-positive jump factor, an out-of-vocabulary decay bit | `Item_DeclaringANegativeRecognitionMinimum_IsRefused`, `Item_DeclaringAJumpHeightChange_IsRefusedWhenTheFactorIsNotPositive` (4 cases: -1.5 / -1 refused, -0.1 / 0.44 accepted — the game's own two values), `Item_DeclaringADecayBitOutsideTheVocabulary_IsRefused` (2 cases), each read from the recording logger by definition id and field name |
| The inert declaration (row 5) | written and reported, never fatal | `Item_DeclaringDecayConditionsWithNoDecayTime_IsRegisteredAndReported`: the built item carries the flag AND the warning names the definition, `DecayConditions` and `DecayMinutes` |
| No behaviour change for a silent definition (row 2) | every new member defaults to the game's own value | `Item_DeclaringNothing_KeepsTheGamesOwnValuesForEverythingElse`: `decayInfo` 0, `wearableVisualOffset` 5 (the game's own initialiser), every new bool false, both new floats zero — recognition excepted, which is the fix |
| The contract (row 7) | nine members and one enum on the Stable content DTO, recorded | `docs/contracts/abstractions-api-baseline.txt` +24 lines and −0 (`ApiSurfaceGateTests` green; the level follows the family — `IModItemDefinition.Wearable`, `Qualities` and `ModItemDropSource` are all `Stable`, and only new BEHAVIOUR surfaces start `Experimental`) |

## What landed

- `src/CasualtiesUnknownOnline.Abstractions/`: `ModItemDecayCondition` (a `[Flags]` vocabulary whose
  members ARE the game's own bits 1, 2 and 4, with `All` as the mask a value is checked against);
  `IModItemDefinition` and `ModItemDefinition` gain `RecognitionMinimum`, `OnlyHoldInHands`, `Combineable`,
  `IgnoreDepression`, `ScaleWeightWithCondition`, `AutoAttack`, `JumpHeightMultiplierChange`, `SlotRotation`
  and `DecayConditions`, each documented with the native reader it feeds.
- `src/CasualtiesUnknownOnline.GameAdapter/Content/`: `ModItemInfoFactory.Build` writes all nine, the decay
  flags into the same byte `Battery` already ORs into, and `rec` from the declaration rather than from the
  data class's leftover; `CustomItemBehaviorValidator` gains the three refusals; the item provider reports a
  `DecayConditions` declaration with no positive `DecayMinutes` at load.
- `tests/CasualtiesUnknownOnline.Tests/Patching/ItemDeclarationParityTests.cs`: 12 new cases — the defect,
  the 32-field census, the decay-flag census, the three refusals, the inert-declaration report and the
  unchanged-defaults case. The six other `IModItemDefinition` implementers — the example mod's dressing and
  five test doubles — declare the new members; the dressing declares `RecognitionMinimum => 0` with the reason.
- `docs/`: both `mod-api` pages (the item row and a new rule paragraph), the pair registry, the terminology
  registry (two new words), the API baseline (+24 lines), the umbrella's Part 3.A entry, the sibling
  ticket's corrected non-goal, the backlog index row, and this ticket.
- Build 0 warnings / 0 errors; `dotnet format` exit 0; full suite green at 614 normative gates and 5063
  behaviour cases (the checklist gate excluded while its boxes were still unchecked).

## Acceptance

| # | Row | Judged by | Verdict |
|---|---|---|---|
| 1 | A mod item whose definition declares no recognition gate reads as recognizable, so the game shows the name and description its author wrote instead of `unknownobject` with an unchosen INT requirement | `ItemDeclarationParityTests.Item_DeclaringNoRecognitionGate_IsRecognizableByAnyPlayer`, red first (`Expected: 0 / Actual: 2`) | verified 2026-10-11 (L1) |
| 2 | Every public field of the game's own item type is reachable from a declaration, and a game update that adds one fails the suite instead of becoming an inherited value | `...Item_DeclaringEveryField_ReachesEveryPublicFieldTheGameReads` (32/32, floor 30) | verified 2026-10-11 (L1) |
| 3 | Every flag the game's decay enum declares is reachable — the three condition bits from `DecayConditions`, the battery bit from `Battery` — and a value outside the vocabulary is refused | `...TheDecayVocabulary_ReachesEveryFlagTheGameReads`, `...Item_DeclaringADecayBitOutsideTheVocabulary_IsRefused` | verified 2026-10-11 (L1) |
| 4 | A declaration the game's own flow cannot survive is refused at load with a reason: a negative recognition gate, a non-finite slot rotation, a jump factor at or below −1 | `...Item_DeclaringANegativeRecognitionMinimum_IsRefused`, `...Item_DeclaringAJumpHeightChange_IsRefusedWhenTheFactorIsNotPositive`, `...Item_DeclaringADecayBitOutsideTheVocabulary_IsRefused` | verified 2026-10-11 (L1) |
| 5 | A live session's game table carries the declared gate on BOTH peers, and the hover text for the item names it rather than `unknownobject` | acceptance batch `20261011-j` — `Item.GlobalItems["example.dressing"].rec.min` and `PlayerCamera.ItemHoverDescription` read on both clients, with the reading body's INT forced to 0 and read back beside them, plus a vanilla-item control whose gate is 1 | **pass** 2026-10-11 |
| 6 | The declaration survives a real two-client session with no item-provider error in either log window | acceptance batch `20261011-j` — the item provider's own lines and its refusal count read from each client's run log after the session | **pass** 2026-10-11 |
| 7 | A declaration that combines `Battery` with a decay condition builds the shape the game's own content declares (`decayInfo` 17 — the battery bit AND a condition bit, as `gravbag` does) | `...Item_DeclaringBatteryAndACondition_BuildsTheShapeTheGamesOwnContentDeclares` | verified 2026-10-11 (L1) |

## Limits

- The **23 of 32** figure is counted from the build site's member list against the type, not measured by a
  test; the 32 and the 4 are measured, and a game update that adds a field fails the census rather than
  passing it.
- The census compares a list-valued field by its LENGTH, not its contents: `qualities` counts as reached when
  the built item carries an entry, and what that entry says is pinned by the crafting-quality tests instead.
- The census proves a field is WRITTEN, not that the game reads the written value the way the cited reader
  suggests: every reader in the family table comes from the decompiled source, and no test here runs the game.
- Whether the game's own 24 recognition-less definitions are deliberate or an oversight is not something the
  source says; this ticket claims only what the source shows — that they inherit the initialiser.
- A real session's own behaviour (rows 5 and 6) is the acceptance batch's to judge; nothing here substitutes
  for it.
- The battery bit's consumer is verified as a SETTER: declaring `Battery` still writes `decayInfo |= 16` and
  the validator keeps every other bit out, but whether the runtime template actually carries a `BatteryItem`
  component is the battery slice's own evidence, outside this change.

## Review round (2026-10-11)

The independent FULL-tier review (fresh context, frozen tree; report
`.agent-local/reviews/mod-item-declaration-parity-review.md`) raised ONE major and no blocker, and the major
was fixed in this same commit: the ticket claimed that every one of the game's own item definitions declares
its recognition gate, and that is false — **24 of them inherit the data class's initialiser** (17 built as a
local and registered without ever assigning `rec`: the bags, the `waterjug` container and the clothing set
`balaclava`/`holidayhat`/`scarf`/`armwarmers`/`tornshirt`/`sneakers`/`tacticalboots`/`hoodie`; plus 7 inline
initialisers). The claim had been copied into the factory's comment, both `mod-api` pages and the umbrella's
Part 3.A entry, and all five places are corrected. The defect's argument no longer rests on "nobody wants the
leftover" — which the game's own content refutes — but on what the source shows: a mod author had no member
either way, and the consequence below is player-visible.

The review's minors are fixed too: the umbrella entry was described as INCOMPLETE when it is wrong in BOTH
directions (it names `usableOnLimb`, declarable since the item-action ticket, and omits `autoAttack` and the
three condition bits); the battery-plus-condition shape the game's own `gravbag` declares (`decayInfo = 17`,
<code>Item.cs</code>) had no case, and now has one plus acceptance row 7; and the decay census proved
reachability but not that each CUO constant still MEANS the game flag it was named for, so the three are now
pinned to the game's own enum by name. Its nits are fixed as well: the implementer count (six, not five), the
"23 of 32" figure marked as this ticket's own count rather than a measurement, the census's list-length
comparison named in *Limits*, and the battery bit's consumer stated as a setter rather than as a component.

The review confirmed the parts it could not break: the premise (no entry, no cross-player chain, no wire, no
save — the change writes a static table on every peer from the same declaration), every native `file:line` in
the family table (24 of them opened), the counts (32 fields, 4 flags, 260/218/201/24 recognition
declarations, 165/53 item-info kinds), `ModItemInfoFactory.Build` as the only `new ItemInfo` under `src/`,
the suite arithmetic (614 + the checklist gate = the previous 615; 5063 − 12 = the previous 5051), and the
contract's shape under all three modification-policy questions; it also reproduced the census's own fairness
(`ItemInfo` has no constructor and no static state, so `Activator.CreateInstance` is the factory's own
`new ItemInfo{}`).

## Non-goals

- Not the remaining Part 3.C capabilities (recipe override, custom enemies, layer modifiers, settings, locale,
  assets, skills, tutorial courses, UI beyond `IModUi`, component-state replication, custom replication
  domains): each is its own entry in the umbrella's Part 3.C with its own consumer question.
- Not a second recognition vocabulary: the member is the game's own INT level, and CUO does not invent a
  "recognized / unknown" enum on top of it.
- Not a decay-rate surface: `DecayMinutes` keeps its meaning, and this ticket only makes the game's own
  condition flags expressible.
- Not a host/guest consistency change: a content definition is materialized on every peer from the same
  declaration and is never serialized, so no wire, save or protocol number moves.
