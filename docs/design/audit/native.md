# Native audit: the game's own gameplay design

**Status: active (2026-10-25).** The CUO-side sweep is complete — `sweep.md` reads all 76 blocks and 2,289 files, `code.md`
holds its 177 findings — so the walk of the game's own tree starts here, area by area, in the order the table below lists.

The other two ledgers audit CUO against its tickets (`README.md`) and against its own code (`code.md`, `sweep.md`). This
one audits the thing both are judged against: **the game's own gameplay**, read from the decompiled tree.

Why it exists: a mechanic that mirrors a native action has to mirror the game's RULES, not just its call site. `Body.Eat` is
not "play a crunch and raise hunger" — it drops the eaten item out of slot 2, sets a 0.5 s eat time, and scales the meal by
0.75 when the head is dislocated or the body disfigured. A CUO design written without that paragraph is a guess that happens
to call the right function.

## How a game fact is recorded

1. Read the type that owns the behaviour (`reversing/Assembly-CSharp/Assembly-CSharp/<Type>.cs`).
2. State its design in one line: the rule, the number, the refusal, the state it writes.
3. Point at the corpus page that carries it, and judge: `page-states-it`, `page-missing` (the page is corrected) or
   `conflict` (CUO deliberately differs — the difference is named on the page with the ruling that allows it).
4. Cite `reversing/…:line`. That tree is never edited, so its line numbers are stable, and every citation is verified by
   `NativeCitationGateTests` (which skips itself on a machine that has no `reversing/` tree, and fails on a citation that
   does not resolve where the tree exists).

## The areas

`reversing/` is gitignored, so this table is a plan rather than a gate-checked census: it is walked by area, and the areas
below are the whole of `Assembly-CSharp` (265 types). The CUO-side census (`sweep.md`) stays the mechanically enforced one.

| Area | Types it holds | Walks |
|---|---|---|
| Body and wounds | `Body`, `Limb`, `WoundView`, `BodyPart`, `Eye`, `FacialExpression`, `CorpseScript`, `GroundBlood` | in progress (rows 5-9) |
| Medical minigames | `AEDMinigame`, `BandageMinigame`, `AmputationMinigame`, `DislocationMinigame`, `HandCrankMinigame`, `KeypadMinigame` | not started |
| Items and use | `Item`, `ItemInfo`, `AmmoScript`, `BatteryItem`, `BatteryRecharger`, `Heater`, `WaterContainerItem`, `CraftingQuality` | parked (four rows below) |
| Liquids and consumables | `WaterContainerItem`, `FluidManager`, `AutoPump`, `Combine` paths on `Body` | not started |
| World and layers | `WorldGeneration`, `ChunkScript`, `DrillPod`, `GeyserScript`, `FluidManager`, the layer modifiers | not started |
| Enemies | `CrystalEnemy` and the `Crystal*` family, `GrabberPlant`, `ElderThornbackBehaviour`, `CaveTicks`, `DroneScript` | not started |
| Traps and mechanisms | `BearTrap`, `GunmineScript`, `BarbedFence`, `MineScript`, `BounceShroom`, `JumpPadScript` | not started |
| Run and save | `PreRunScript`, `BasicCourse`, `EscapeCourse`, `FirstAidCourse`, `CraftingCourse` | not started |
| UI the game owns | `PlayerCamera`, `Command`, `ConsoleScript`, `WoundView`, `InvButton`, `ItemLabel` | not started |

## The ledger

| # | Game fact (quoted) | Source | Corpus page | Verdict | Action |
|---|---|---|---|---|---|
| 1 | `public void UseItem(Item item)` dispatches to the item's own delegate: `if (item.Stats.usable) { item.Stats.useAction(this, item); }` — the game's item use is per-definition data, not a switch on kind | `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:2475-2481` | `mechanics/item-use.md` | `page-states-it` | none: the page already says the item's own data decides the action |
| 2 | `public void Eat(float hungerAmount, float weightGain)` drops the eaten item out of slot 2 (`this.DropItem(2)`), sets `this.eatTime = 0.5f`, and when the head is dislocated or the body disfigured adds `hungerAmount * 0.5f` pain and scales BOTH the hunger and the weight gain by `0.75` | `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:2259-2275` | `mechanics/item-use.md`, `items/food.md` | `page-missing` | both pages now state the 0.5 s eat time, the slot-2 drop and the 0.75 injury factor |
| 3 | `public void Drink(float amt)` drops the container out of slot 2, adds `amt` to thirst and sets the same `0.5f` eat time — a drink and a meal share one body timer | `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:3680-3685` | `items/drink.md` | `page-missing` | the page now states the shared timer and the slot-2 drop |
| 4 | `public void WearWearable(Item item)` refuses when the slot is already occupied and names the occupant: `PlayerCamera.main.DoAlert(Locale.GetOther("alertalreadywearing").Replace("<1>", item.Stats.rec.r…` then `return;` — the refusal is a player-facing line, not a silent no-op | `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:1480-1496` | `items/wear.md` | `page-missing` | the page now states the occupied-slot refusal and the key it reads |
| 5 | The body's own starting values are `bloodVolume = 100f`, `hunger = 100f`, `thirst = 100f`, `energy = 100f`, `consciousness = 100f` — all five on one 0-100 scale, plus `happiness`, `sicknessAmount`, `radiationSickness`, `painShock` and `temperature` | `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:3872-4161` | `mechanics/medical-view.md` | `page-missing` | the page now states the scale the readouts share, which is what makes a threshold like "blood oxygen below 50" legible |
| 6 | `isCriticallyDying` is one predicate: `(totalBleedSpeed > 0.02f && bloodVolume < 30f) || bloodOxygen < 50f || (hunger < 0f && limbs[0].muscleHealth < 25f) || septicShock > 82.5f || temperature < 27f \|\| temperature > 41.5f \|\| fibrillationProgress > 60f \|\| inCardiacArrest \|\| bloodPressure < 70f` | `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:330-340` | `mechanics/medical-view.md`, `surfaces/status-line.md` | `page-missing` | both pages now list the gates, because "how badly off is this body" is the game's rule and not a CUO invention |
| 7 | `isDying` is a separate, earlier predicate: `totalBleedSpeed > 0.134f \|\| (totalBleedSpeed > 0.02f && bloodVolume < 40f) \|\| !breathing \|\| …` — bleeding out is judged by RATE first and by volume second, with a strictly higher bleed threshold than `isCriticallyDying` | `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:320-330` | `mechanics/medical-view.md` | `page-missing` | the page now states both predicates and that the game answers "dying" before "critically dying" |
| 8 | Thirst drains on the game's own curve: `BaseThirstRate` = `Time.deltaTime / (17f - tempDiff * 0.5f)` × `2f` when thirst is above 100, × `2.5f` above 175, × the run setting `metabolismrate` — so a thirst above the scale drains faster than a full one | `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:272-278` | `items/drink.md` | `page-missing` | the page now states the rate, the two accelerations and the run setting it reads |
| 9 | Sleep quality scales the regen: `SleepQualityToRegen` returns `0.7f` (bad), `0.85f` (mediocre), `1f` (okay), `1.25f` (good) | `reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:1030-1045` | `mechanics/sleep.md` | `page-missing` | the page now states the four multipliers, because a CUO sleep rule that invents its own would be a different game |
| … | the remaining areas | | | | |
