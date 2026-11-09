# The game's fluids and climate (1.0)

Cited from the 1.0 decompile (`reversing/release-1.0/Assembly-CSharp/`).

## The fluid grid

`FluidManager` owns the world's liquid cells: a simulation range around the player (`FluidManager.cs:27`, `:68`), a fill
per cell (`StartFill(pos, fill, max)`, `:130`), emptying (`:176`) and swapping two cells (`:188`). Liquids are stored as
a byte fill level per cell, not as objects; the renderer draws the cells. A fluid's effect on a body is applied where the
body stands in it (`LiquidAffect.cs`).

## The liquid registry

`LiquidType` is the data record for a liquid: a locale name, colour, value per litre, a drink callback and a health-use
callback (`LiquidType.cs:20-32`). `LiquidStack` is a volume of one liquid (`LiquidStack.cs:14-17`). `Liquids` is the
static registry and the effect functions:

- Splashing a body changes temperature (`Liquids.cs:40`, `:72`, `:119`; `LiquidAffect.cs:67` adds temperature while a
  limb is wet);
- pouring a hot or cold liquid moves the body's temperature by `amount * 2.5` (`Liquids.cs:166`, `:185`);
- drinking, filling a container and the health-use path all go through the registry (`LiquidExists`, `:147`).

## Temperature

The body has a temperature value with a movement multiplier (`Body.cs:253`, `:298`) and a base drift
(`Body.cs:437: temperature += 0.015 * dt`). Two bands matter: the game flags a body as in trouble below 29 or above 41
(`Body.cs:332`) and kills it below 27 or above 41.5 (`Body.cs:342`). `Heater` objects move a nearby body's temperature
toward a desired value within a radius (`Heater.cs:33-59`); a cooker is the same object with a different mode (`:65`).
Wetness (rain, water) pushes the temperature down through `LiquidAffect`.

## Radiation

`RadiationLine` is a world object with an active flag (`RadiationLine.cs:5-41`): while active, a body inside adds to its
`radiationSickness` at `0.03 * intensity * dt` (`:66`). The line sits at a fixed depth in the world (see
`worldgen-and-layers.md`: the radiation line is at depth 9 and the quakes at 10 in 1.0). Radiation is one of the
"experimental" hazards: the sickness value feeds the dying/death sets (`Body.cs:332`, `:342`).

## Weather

`RainyModifier` and the climate modifiers are world-side effects that change the body's wetness/temperature and the
appearance; the base game's rain is a modifier on the world, not a per-player effect.

## What the mod must respect

- The fluid grid is host-authoritative world state; a member's cells are synced from the host and never simulated
  independently.
- A liquid's health/ drink effect is a registry callback, so a replicated container carries liquid ids and volumes, not
  effects.
- Temperature and radiation act on the body's own values; the client experiencing the body judges them (decision 184).

## Not covered here

- The full liquid list and each liquid's effects: the registry's data, not read here.
- Weather state replication and the rain's rendering: not mapped yet.
