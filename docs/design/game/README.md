# The game's own systems (1.0)

This folder maps the game as the mod must understand it. It is not the mod's design (that lives in
`docs/design/mechanics/`, `surfaces/`, `items/`); it is the ground the mod stands on: what the game runs, what its
rules are, and where the code says so.

Source: the 1.0 release assemblies, decompiled once into `reversing/release-1.0/Assembly-CSharp/` (not in git — read it
from disk). A page cites `File.cs:line` from that tree; a claim without a line is a note, not a rule.

Method: read the system's own entry points and data in the decompiled source, write down the rule the player feels, and
name the line that enforces it. A page is done when every player-visible rule in its area has a line, and the areas it
does not cover are named.

| page | covers | main sources |
|---|---|---|
| `worldgen-and-layers.md` | layers 0-10, depth, descent, the radiation line, the layer-10 exit, endless loops, endings (Biopod/Escape/Merc), dune mode | `WorldGeneration.cs`, `EndLabScript.cs`, `BlueHeart.cs`, `DuneMode.cs` |
| `items.md` | items, condition, liquids, containers, wearables, prosthetics, customisation | `Item.cs`, `LiquidItemInfo.cs`, `WaterContainerItem.cs`, `Wearable.cs`, `ProsthesisRepairMinigame.cs` |
| `crafting.md` | the crafting table, recipes, conditions, recursive steps | `GameCraftingTable.cs`, `Recipe.cs` |
| `medical.md` | limbs, wounds, infection, surgery, amputation, prosthetics, the health panel's opiate/venom | `Body.cs`, `Limb.cs`, `WoundView.cs`, `*Minigame.cs` |
| `combat.md` | weapons, bullets, enemies (incl. the Elder), the wider hitbox | `GunScript.cs`, `EnemyScript.cs`, `ElderThornbackBehaviour.cs` |
| `traps-and-events.md` | traps, crystals, earthquakes, sound cannons, hazard rolls | `BlockDamage.cs`, `CrystalBehaviour.cs`, `WorldGeneration.cs` |
| `world-objects.md` | buildings, openables, containers, traders, scavenging | `BuildingScript.cs`, `Openable.cs`, `TraderScript.cs` |
| `fluids-and-climate.md` | liquids, temperature, radiation, weather | `FluidManager.cs`, `Liquids.cs`, `Body.cs` |
| `ui-and-settings.md` | the game's windows, run settings, measurement units, language | `PlayerCamera.cs`, `RunSetting*.cs`, `Settings.cs` |
| `console-and-save.md` | console commands, the save file, run settings persistence | `ConsoleScript.cs`, `SaveSystem.cs` |
| `principles.md` | the gameplay principles the systems imply (why the rules are shaped this way) | derived from the pages above |

Order of work: the pages the mod touches first (worldgen, items, medical, crafting), then combat/traps, then the rest.
`principles.md` is written last, from the finished pages — never before them.

Written so far: `worldgen-and-layers.md`, `items.md`, `medical.md`, `crafting.md`, `combat.md`, `traps-and-events.md`, `world-objects.md`, `fluids-and-climate.md`, `ui-and-settings.md`.
