# World generation and layers (1.0)

The world is a single column of square layers, generated one at a time; the player moves between them by walking to the
bottom edge (down) or, at the last layer, to the top edge (up). Every rule below is from
`reversing/release-1.0/Assembly-CSharp/WorldGeneration.cs:1138` unless named otherwise.

## The layers

- `amountOfLayers = temperatureCurves.Length` (`WorldGeneration.cs:128`) — the layer count is a designer array on the
  component, not a constant; 1.0 ships eleven layers (0-10) and the page treats the top index as data, not 10.
- `biomeDepth` is the current layer index, 0 at the surface (`:247`); `debugStartDepth` can start deeper (`:247`).
- Each layer's art is picked by index: `layerGenerationImages[biomeDepth]` / `layerGenerationCharColors[biomeDepth]`
  (`:1095`, `:1104`) — a layer without an entry there is an out-of-range, not a fallback.
- `biomeDepth == 6` forces rain (`:1042`); `biomeDepth == 5` starts the cold warning when body temperature is below
  32.5 °C (`:1119`).
- Depth in metres and units: `PlayerLayerDepthMeters()`, `PlayerTotalDepthMeters()` add `totalTraveled`
  (`:182-220`) — the same travelled distance is what the radiation line reads.

## Descent (the bottom edge)

- The trigger: a non-tutorial, non-regenerating world, no save panel open, alive, `biomeDepth != 9`,
  `biomeDepth != 10`, and `position.y < -halfHeight + 3.1f` (`:1138`).
- It does not teleport. It sets `savePanel.SetActive(true)` and `body.forceWalk = true` (`:1158-1159`): the player walks
  into the layer-end save panel, and the panel's Continue is what calls `RegenerateWorld(false)`.
- The first frame of a new layer is protected by the game's own generation gate; the mod's `WorldStartGate` exists because
  a member's scene starts before the host's generation finishes.

## The exit (the top edge)

- At `biomeDepth == 10` the trigger flips to `position.y > halfHeight - 5f` (`:1138`).
- With `mercscarf` in the body (`FindByIdThorough("mercscarf")`) the run ends in `MercEnding()` (`:1147`); otherwise the
  `SurfaceBreachCutscene()` plays and then `RegenerateWorld(false)` (`:1205`) — the lab, not the credits.
- `biomeDepth == 9` is excluded from the bottom trigger (`:1138`): layer 9 is the lab's floor, not a descent.
- The `WRONGWAY` achievement fires on jumping at the top of any layer 0-9 (`:1165`).

## Endings and endless

- `EndLabScript` owns the lab's two ends: `BiopodEnding()` (`EndLabScript.cs:134`) and `EscapeEnding()` (`:212`).
- `canStartMercRun` requires a previous ending recorded in `PlayerPrefs` (`didbiopod`/`didescape`), depth 0, not
  unchipped, not endless, no override (`WorldGeneration.cs:331`) — the merc run is a second-run mode, not a setting.
- Endless: `RunSettingBool("endless")` (`BlueHeart.cs:70`); the Blue Heart counts loops in `endlessLoops`
  (`:83-108`) and speaks a loop line per pass (`endlessloop0..3`, `endlessloopdefault`). Endless disables the merc run
  (`:331`) but not the endings themselves.
- `biomeOverride` (tutorial, etc.) bypasses the depth triggers (`:1138`, `:986`, `:991`).

## Dangers that ride the layer clock

- The radiation line: the line activates when `layerTimeSpent > maxTimePerLayer` and `biomeDepth < 9` and not in a
  tutorial/override and not unchipped (`:986`) — the line is a per-layer timer, not a position.
- Earthquakes: `earthquakeDelay < 0f && biomeDepth < 10 && !body.sleeping` (`:991`) — the layer's own clock starts a
  quake every delay; 1.0 adds "small chance for instant earthquakes" (patchnotes), so both the delay and the roll matter.

## Dune mode

- `DuneMode` is a scene-level effect (`DuneMode.cs:12-30`): it swaps objects' materials to `Special/DuneMaterial` and
  shuffles them every 20 s (`ShuffleObjects`). It is presentation only — no gameplay rule lives there.

## What this page does not cover

- The generator's tile rules (biomes, caves, ores) — they live in the same file's `Generate*` methods and the scene's
  tile assets; a later page can map them if the mod ever needs to reason about generation beyond "it is the host's".
- Enemy/trap placement: `traps-and-events.md`.
