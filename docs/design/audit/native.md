# Native audit: the game's own gameplay design

**Status: parked by the user's instruction (2026-10-25).** The CUO-side sweep (`code.md`, `sweep.md`) is finished first; this
ledger keeps the method, the areas and the four rows already found, and the walk resumes when the CUO sweep is complete.

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
| Body and wounds | `Body`, `Limb`, `WoundView`, `BodyPart`, `Eye`, `FacialExpression`, `CorpseScript`, `GroundBlood` | parked (four rows below) |
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
| … | the remaining areas | | | | |
