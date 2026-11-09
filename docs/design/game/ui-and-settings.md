# The game's UI and settings (1.0)

Cited from the 1.0 decompile (`reversing/release-1.0/Assembly-CSharp/`).

## Run settings

A run's settings are a dictionary of named values, seeded from a preset:

- `WorldGeneration.runSettings` is a static dictionary; when it is null the game seeds it from
  `RunSettings.GetPreset("normal").presetValues` (`WorldGeneration.cs:112-114`).
- Every rule reads it through typed getters: `GetRunSettingFloat(name)` (`:136-138`) and `GetRunSettingBool(name)`
  (`:142-144`). A missing key throws, so the preset is the schema.
- The settings UI is built from `RunSettingDisplay`, `RunSettingFloat`, `RunSettingBool`, `RunSettingDropdown` objects
  (`RunSetting*.cs`), and a `RunSettingsPreset` holds the per-preset values (`RunSettings.cs`, `RunSettingsPreset.cs`).
- The run settings the map has cited so far: `baselootdensity` and `basetrapdensity` (`WorldGeneration.cs:64`, `:74`),
  `lootmultiplier`/`trapincrease` (the per-layer accumulation, `worldgen-and-layers.md`), `infectionspeed`
  (`Body.cs:3130`), `timebetweenearthquakes` (`WorldGeneration.cs:1003`), `lockpickprecision` (`Openable.cs:30`),
  `nosleeprestrictions` (`Body.cs:39`).

## The game's windows

- `PlayerCamera` owns the game's panels: `craftingPanel` (the craft window), `WoundView` (the limb window), the
  container/backpack panel, and the trade window. Each is an `SetActive` GameObject toggled by a key bind or an
  interaction; the windows' own components hold the layout.
- The settings menu (`SettingsMenu.cs`) is the game's own options window; it plays the UI click through
  `PlayUISound("miniClick", 1f)` (`SettingsMenu.cs:192`), while adaptive buttons play `"click"`
  (`AdaptiveButton.cs:51/61/65`).
- The language is a runtime choice (`Language.cs`, `LanguageSelect.cs`, `Locale.cs`); the text catalogue is loaded per
  language at startup, and a missing language logs the game's own "Language missing!" line.

## Measurement units

`Units.cs` holds the game's unit conversions (temperature, distance, volume); a panel that shows a number uses these
converters, so a mod that prints a number should use the same units as the game's own label.

## Key binds

`Settings.cs` binds every action to a `KeyCode`; the map has cited `attack` = 323 (Mouse0, `Settings.cs:388` area),
`iteminteract` = 324 (Mouse1, `:396`), `craft` = 99 (C, `:436`). `KeyBinds.GetBind(name)` resolves a name to its current
code, and the player can rebind it, so a patch that reads input must ask the bind, never a literal code.

## What the mod must respect

- A CUO surface that shows a number uses the game's own unit conversion and the game's own label wording.
- The game's own window is reused for player-facing features when one exists; a parallel CUO window needs a recorded
  blocker and the user's direction.
- A run setting that a mod's rule depends on is read through the game's getter, never a copied constant.

## Not covered here

- The layout geometry of the game's windows (rects, anchors): not mapped yet; `docs/design/surfaces/` covers the mod's
  own surfaces.
- The full key-bind table and the settings menu's page structure.
