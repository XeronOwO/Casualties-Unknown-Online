# Native run fields — row 1 (multipliers across a continue) and row 3 (the report names the presentation-only field)

- Batch: `20261108-s` (host desktop, in world; artifact `0.1.0+9869d734`)
- Ticket: `docs/backlog/done/save-native-run-field-parity.md`, acceptance rows 1 and 3
- Instrument: `tools/acceptance/recipes/run-multiplier.cs` (read/set the live world's two rarity multipliers through the
  game's own public fields), `tools/acceptance/recipes/run-setting.cs` (write one float into the game's run-settings
  table), `tools/acceptance/recipes/layer-descend.cs` (the game's own `WorldGeneration.RegenerateWorld`), the native
  `/save` cut, `continue-run`, and the cut archive's `run.json`.

## Row 1 — the multipliers accumulate, are stamped, are restored, and drive the next layer

The world's chain (`reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs`): `LootDensity` =
`GetRunSettingFloat("baselootdensity") * lootRarityMultiplier` (`:64`), `TrapDensity` = `… * trapRarityMultiplier`
(`:74`); at each layer start `lootRarityMultiplier *= GetRunSettingFloat("lootmultiplier")` (`:1061`) and
`trapRarityMultiplier += GetRunSettingFloat("trapincrease")` (`:1062`). The run settings are the game's own static table
(`:112-114`, `:137-139`).

The run settings were set through the table before the descents (`lootmultiplier=2`, `trapincrease=1`):

| step | live `lootRarityMultiplier` | live `trapRarityMultiplier` |
|---|---|---|
| fresh run, layer 0 | 1 | 1 |
| after one descent | 2 | 2 |
| after the second descent | 4 | 3 |

The mid-run cut (`/save` → `Cut Command committed for world w-20261108-709f: mid-run cut taken at frame-end, revision …`)
archived `backups/mid-run-20261108-070314.cuoz`; its `run.json` run row reads `layerIndex: 3`,
`lootRarityMultiplier: 4`, `trapRarityMultiplier: 3` — the cut STAMPS the live values (it does not fall back to the run
baseline's `1/1`).

`leave-world` → `continue-run`:
- `[SaveFacts] holding the run baseline's rarity multipliers (loot 4, trap 3) until the live world exists.`
- `[SaveFacts] applied … run fields (clock …, 133 recipe row(s)) …`
- `[WorldParamsService] Applied the RESTORED run baseline at the continue click (16 RNG bytes, depth 2).`

After the Continue `run-multiplier mode=read` → `loot 4, trap 3`; one more `layer-descend` (the settings still `2/1`)
→ `loot 8, trap 4`: the next layer's generation continued the run's own accumulation from the restored values, which is
the row's expected effect (the distribution inputs of every later layer match the interrupted run).

## Row 3 — the field decided presentation-only is named, and nothing is silently defaulted

The ticket's §Decision table names every field's home and §Scope 4 requires a deliberately-left-out field to be named in
the restore report. The one field that is presentation-only in effect is `WoundView.cInfo` (the native limb-injury
carry-over window, "kept for native parity — four ints"); its home is `characters/<playerKey>.json` and the character
apply path writes it back. The display-rows batch (`20261107-…-rows`, `docs/evidence/acceptance/save-native-character-field-parity-20261107.md`)
read it back after a Continue: the wound window's `cInfo` read `180,42,777,9` — the restored four ints, not defaults.

No field in the ticket's table is left silent: the two multipliers and the run clock/recipes ride `run.json` (read back
here), the character-level fields ride the character table (read back in the character batch).

## What this settles

Row 1: a run's rarity multipliers accumulate per layer, the cut stamps them, the Continue restores them, and the next
layer generates with them — the row's "the next layer matches the interrupted run's scaling" holds on the inputs that
decide the distribution.

Row 3: the presentation-only field is named in the ticket's report and its restored value is read back after a Continue;
no field is silently defaulted.

## Limits

- The distribution COUNTS a layer rolls (loot piles, trap placements) are the game's own generation output and were not
  counted here; the readings are the generation INPUTS the game documents, which the row's expected behaviour turns on.
- One cut kind (mid-run, frame-end), one host, three clients.
- `savedRunTime`'s clock value is read in the restore lines, not asserted against the interrupted clock.

## Records

- Commands: `run-setting key=lootmultiplier value=2`, `key=trapincrease value=1`; `run-multiplier mode=read`;
  `layer-descend twice=0`; `/save`; `leave-world`; `continue-run`; `run-multiplier mode=read`; `layer-descend twice=0`.
- Archive: `w-20261108-709f/backups/mid-run-20261108-070314.cuoz`, `run.json` run row.
- Host log: the three lines quoted above.
