# Native run fields are not covered by the world archive

- Status: Todo — **S3.4a landed 2026-09-11** (the run-level fields: both rarity multipliers, the run
  clock base and the recipe unlock table). S3.4b (the character-level fields: `lastHappiness`,
  `caloriesConsumed`, `WoundView.cInfo`) is NOT implemented yet and stays open here.
- Priority: Medium-High
- Category: Persistence / save system
- Source: found by the S2 independent adversarial review (2026-09-10)
- Related: `docs/architecture/save-archive-format.md`, `review/save-layer-end-save-and-restore.md` (S2), `docs/decisions/active.md` 166, 169

## Landed (S3.4a, 2026-09-11)

| field | where it lives now | restore seam |
|---|---|---|
| `lootRarityMultiplier` | `run.json`'s `run` row (`WireRunState.LootRarityMultiplier`), STAMPED with the cut instant's value | the kernel baseline, applied at the Continue click (`WorldParamsService.TryApplyRestoredNow`), written into the live world through the adapter's pending handover |
| `trapRarityMultiplier` | same | same |
| `savedRunTime` | `run.json`'s `native-run-fields` row | the adapter writes it at the native `SaveSystem.TryLoadGame` slot, before `WorldGeneration.cs:252-262` derives from it |
| `savedRecipeData` | `run.json`'s `native-run-fields` row (one row per recipe, keyed by INDEX) | the WORLD-ENTRY seam (`RestoredWorldFactReplay`), not the save slot: the game rebuilds `Recipes.recipes` in `WorldGeneration.Awake` and CUO's mod-content provider appends the custom recipes on a later Update frame, so an early write would refuse every custom recipe. A row whose index the finished table lacks is refused by name into the restore account |

The recipe table's seam was found by the S3.4a independent adversarial pass: the first implementation
wrote it at the save slot together with the clock, which would have dropped every mod recipe's unlock
(the table there still holds vanilla recipes only) and reported it in the log alone.

Both multipliers also travel the WIRE (`WireRunState` → `WorldStartParams`), which closes the second
half of the same defect: a guest joining a run at layer 3 generated with the game's fresh `1f` while
the host used the run's accumulated value, so the two sides built different layers. The capture is the
generation boundary (`WorldParamsService.CaptureAtBoundary`), the same instant the RNG baseline is
taken. Evidence: `WorldRunFieldTests` (cut rows, layer-end cut, unreadable reader refuses the cut,
restore handover, named absence), `WorldRunStateProjectionTests` (wire round trip + old-sender
degradation), `WorldSnapshotCodecTests` (malformed native row skipped by itself, sound row round-trips).

Reading is all-or-nothing for the same reason the damage table is: a reader that met no live world (or
no recipe table) reports a failure and the cut REFUSES, because a snapshot whose clock reads back as 0
and whose recipe table reads back as empty is worse than no snapshot — the player continues believing
the world was recorded. Recipe rows are written back by INDEX, not by position, because
`GameAdapterRecipeContentProvider` appends custom recipes to `Recipes.recipes`.

## Recorded gaps from the S3.4a adversarial pass (not fixed here)

| gap | why it matters | where it would land |
|---|---|---|
| The run clock base is archived but NOT sent | a guest joining a run mid-way has `SaveSystem.savedRunTime == 0`, so `WorldGeneration.TotalRunTime()` (the pause/tooltip/death-stat clock) shows only the time since it joined. Pre-existing, but the field is now formally a run-level value | the run baseline (it is not a generation input, so it does not belong in `WorldStartParams`' generation group) or a small absolute message at the world-entry fan-out |
| `layerTimeSpent` / `maxTimePerLayer` are not carried | continuing into the SAME layer restarts the radiation-line timer and hands the player a fresh `timelimit`. The native save does not carry it either | `run.json`'s native row (the field is game state no CUO domain owns) — decide with S3.5 |
| No value-range guard on the multipliers | a malformed or hostile wire value (NaN/Inf) reaches `WorldGeneration.lootRarityMultiplier` unchanged. Low priority (accept-first, no anti-cheat in MVP) but the kernel already asserts its other invariants | `WorldDomainModule.AssertInvariants` |
| `WorldParamsService` injects the concrete adapter type | the new capture/apply branches cannot be covered by `FakeNativeWorldFacts`, so they are only reachable through the adapter's own tests | change the dependency to `INativeWorldFacts` (the port it already uses for everything else) |

Accepted as-is: log lines format floats with the current culture (the codebase does this
everywhere); `WorldGeneration.cs:257`'s Start-time trap term is reproduced exactly as the native path
does it (the generation boundary reads the live world, so both sides agree).

## Still open (S3.4b)

| field | decided home | why it is not done |
|---|---|---|
| `lastHappiness` | `characters/<playerKey>.json` | needs a per-player capture (the host can read its own body at the cut; a guest's value can only come from that guest's own report) and an apply through the character-restore path; `CharacterDataMsg` has no field for it yet |
| `caloriesConsumed` | same | same |
| `WoundView.cInfo` | same | same; it is a WINDOW value (four ints) with no reader besides the save system |

Until S3.4b lands a continued run keeps the LIVE values for these three fields, and no report names
them yet (the native-run-field row names the run-level values only). Closing that gap means either
implementing the fields or naming them explicitly in the restore report — a silent default is what §6
forbids.

## Problem

The frozen v1 scope (decision 166) is the kernel checkpoint + run baseline + character data + world
diff. Several live native fields that shape a layer are in none of them, because no CUO domain owns
them. Native `SaveSystem.TryLoadGame` used to restore them (`reversing/Assembly-CSharp/Assembly-CSharp/SaveSystem.cs:433-445`);
CUO deliberately does not read that file any more (decision 165), and S2 blocks it, so a continued
world starts each of them from a default:

| field | where it is produced | why it matters |
|---|---|---|
| `lootRarityMultiplier` | `WorldGeneration.cs:1061-1062`, accumulated per layer | loot distribution of every later layer differs from the run being continued |
| `trapRarityMultiplier` | same | trap/entity distribution differs |
| `caloriesConsumed` | `PlayerCamera.caloriesConsumed` | a per-run counter the game carries |
| `lastHappiness` | `Body.lastHappiness` | character history |
| `savedRecipeData` | `Recipes.recipes[].hasMadeBefore/INT` | recipe unlock state (also tracked by the kernel backlog item `recipe-unlock-fallback.md`) |
| `savedRunTime` | run clock base | the layer's time-limit accounting |
| `WoundView.cInfo` | native limb-injury carry-over window | presentation of old injuries |

The run baseline's `RunSettings` DO get restored (S2 applies them before `WorldGeneration.Start`
consumes them), so `trapRarityMultiplier`'s per-run setting input is right; what is missing is the
accumulated value itself and the fields with no setting input at all.

Effect: acceptance row 1's "identical character/run state" holds for the kernel/character half and
for the layer's generated content on the FIRST layer of a run, but a restore deeper into a run is
not byte-identical to the interrupted run.

## Decision (frozen with the user, 2026-09-10)

The table is the frozen INTENT. Where landing refined the mechanism it is recorded in *Landed
(S3.4a)* above — in particular the two multipliers ride the kernel run baseline (so the wire and the
restore use one source) instead of being written only from a private cut block, and the clock base
and recipe table are one typed `run.json` row rather than a third blob.

| field | decided home | restore seam |
|---|---|---|
| `lootRarityMultiplier` | `run.json` (world-generation block) | written back where the native `SaveSystem.TryLoadGame` used to run — the arm that currently only skips it — before `WorldGeneration` derives anything from it (`WorldGeneration.cs:253-262`) |
| `trapRarityMultiplier` | `run.json` (world-generation block) | same seam |
| `savedRunTime` | `run.json` | same seam |
| `savedRecipeData` | `run.json` (recipes table) | same seam; a future kernel recipe domain would take it over |
| `lastHappiness` | `characters/<playerKey>.json` | the character-apply path (body-level) |
| `caloriesConsumed` | `characters/<playerKey>.json` | the character-apply path (body-level) |
| `WoundView.cInfo` | `characters/<playerKey>.json` | the character-apply path; kept for native parity — four ints — although no reader besides the save system itself was found |

Evidence for the decision:

- The native write/read pair is `SaveSystem.cs:151-180` (write) and `SaveSystem.cs:433-446` (read).
- Restore timing decides the seam: `WorldParamsService.TryApplyRestoredNow` runs at the Continue
  click, before `WorldGeneration.world` exists, so the multiplier/time fields cannot be written
  there; the patch that skips `SaveSystem.TryLoadGame` runs after the world object exists and before
  its values are consumed, which is the only correct point.
- `savedRecipeData` consumers: `Recipe.cs:28`, `Recipe.cs:183-185`, `PlayerCamera.cs:432`; it is
  reset by `MindwipeScript.cs:79`.
- `WoundView.cInfo` is declared at `WoundView.cs:826` and written by `SetCharDetails` (`:54-65`);
  the only other references are the save system's own write/read.

## Scope

1. Decide per field whether it is (a) kernel-owned state that should move into an existing domain
   (recipe unlocks already have a candidate), (b) a run-baseline field that belongs in `RunState`, or
   (c) genuinely presentation-only and acceptably reset with an explicit in-game note.
2. Persist what is decided into the domain files (§3.4) — no new blob, one file per domain table.
3. Restore it before `WorldGeneration.Start` reads it (the S2 click-time apply is the seam).
4. A field deliberately left out must be named in the restore report, never silently defaulted.

## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | Continue a run at layer 3 whose `trapincrease`/`lootmultiplier` settings were non-default | The next layer's trap/loot distribution matches the interrupted run's |
| 2 | Continue after crafting a recipe in an earlier layer | The recipe stays unlocked |
| 3 | A field decided as presentation-only | The report names it; nothing else changes silently |

## Verification limits

Row 1 needs the user's in-game run: distribution counts are produced by the game's generation and
cannot be asserted from this test host. Rows 2–3 can be machine-verified once the field has a
persisted home.
