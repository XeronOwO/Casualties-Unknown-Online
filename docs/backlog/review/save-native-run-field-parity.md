# Native run fields are not covered by the world archive

- Status: Review — **S3.4a landed 2026-09-11** (the run-level fields: both rarity multipliers, the run
  clock base and the recipe unlock table), **S3.4b landed 2026-09-11** as its own ticket
  (`review/save-native-character-field-parity.md`): the character-level fields `lastHappiness`,
  `caloriesConsumed` and `WoundView.cInfo` now ride `CharacterDataMsg.NativeFields` and are written
  back on the local restore path, with the missing-field case named in the restore report. The
  hardening cycle of 2026-09-17 closed the two recorded ENGINEERING gaps (the missing multiplier guard
  and the concrete adapter dependency) and split the two that are each their own cycle into
  `todo/save-run-clock-not-sent.md` and `todo/save-layer-time-not-carried.md`. Awaiting the final
  unified acceptance pass; the per-field decided homes below are kept as the frozen record.
- Priority: Medium-High
- Category: Persistence / save system
- Source: found by the S2 independent adversarial review (2026-09-10)
- Related: `docs/architecture/save-archive-format.md`, `review/save-layer-end-save-and-restore.md` (S2), `docs/decisions/active.md` 166, 169, 171

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

## Disposition of the recorded gaps (2026-09-17)

| gap | why it matters | disposition |
|---|---|---|
| The run clock base is archived but NOT sent | a guest joining a run mid-way has `SaveSystem.savedRunTime == 0`, so `WorldGeneration.TotalRunTime()` (the pause/tooltip/death-stat clock) shows only the time since it joined. Pre-existing, but the field is now formally a run-level value | SPLIT — `todo/save-run-clock-not-sent.md`: a wire carrier is its own cycle (additive member + `ProtocolVersion` bump), and the generation group must not become a general side channel |
| `layerTimeSpent` / `maxTimePerLayer` are not carried | continuing into the SAME layer restarts the radiation-line timer and hands the player a fresh `timelimit`. The native save does not carry it either | SPLIT — `todo/save-layer-time-not-carried.md`: whether a restore should RESUME the timer is a gameplay decision (native restarts it), so it is asked before it is built |
| No value-range guard on the multipliers | a malformed or hostile wire value (NaN/Inf) reaches `WorldGeneration.lootRarityMultiplier` unchanged. Low priority (accept-first, no anti-cheat in MVP) but the kernel already asserts its other invariants | CLOSED — one rule, four seams: `RunRarityMultipliers.IsWellFormed` is the rule; the kernel refuses a non-finite run baseline on the command path, on the applied wire batch and at the checkpoint restore; and the adapter refuses to write one into the live world |
| `WorldParamsService` injects the concrete adapter type | the new capture/apply branches cannot be covered by `FakeNativeWorldFacts`, so they are only reachable through the adapter's own tests | CLOSED — the dependency is `INativeWorldFacts`, the port every call it makes already belongs to |

### Hardening landed (2026-09-17)

The rarity multipliers are world-generation INPUTS — a layer's loot and trap distribution are scaled by
them — so a value the game could not have produced must never become the run baseline.
`RunRarityMultipliers.IsWellFormed(float?)` answers that question in one place (the game starts a run at
`Neutral` and only ever scales the pair, so a NaN or an infinity is a malformed producer; an ABSENT
value stays well formed, because that is a field a sender never carried rather than a malformed one).
`WorldDomainModule.AssertInvariants` refuses a run whose committed baseline carries one on the two paths
that go through a domain module — the host's own capture (the command path rejects it with
`InvariantViolation`) and the guest's wire batch (it fails to apply, so the guest never projects it into
the params the adapter generates from). The RESTORE family does not go through `Execute` at all — the
archive decode and the wire checkpoint both hand their whole checkpoint to `GameStateKernel.Restore` —
so it is refused THERE, before the store is replaced. That seam was found by this cycle's independent
adversarial pass by probe (`Restore` of a NaN run returned success while `StartRun` of one was rejected),
which is exactly why the claim below is a list of seams rather than "one gate every producer passes".
`WorldParamsService.Apply` carries the same rule as its LAST line before the live-world write, because
the params object the adapter reads is the one the publisher stored rather than the kernel's projection:
a non-finite pair is refused exactly like the half-pair the method already refused, so the layer keeps
the game's own values instead of being scaled by a number that has no meaning.

Machine-verified by `RarityMultiplierGuardTests` (the rule's answers, the rejected start, the failed
wire apply, the refused restore, and the finite-but-non-neutral case that must still be accepted). The
adapter site is game-typed and is read-only reviewed, like every other live-world write in that layer.
The second closed gap is the dependency narrowing: `WorldParamsService` now takes `INativeWorldFacts`, so it is no longer
welded to the adapter's own implementation; the proof is the compile (the port exposes every member the
service calls) plus the composition/DI tests and the deployed build.

Accepted as-is: log lines format floats with the current culture (the codebase does this
everywhere); `WorldGeneration.cs:257`'s Start-time trap term is reproduced exactly as the native path
does it (the generation boundary reads the live world, so both sides agree).

## Landed (S3.4b)

The three character-level fields landed as their own ticket
(`review/save-native-character-field-parity.md`): they ride `CharacterDataMsg.NativeFields` (read off
the live scene at the cut and at each 1 Hz report, all-or-nothing), are written back by the local
restore path's second pass, and a snapshot that carries none of them — or a malformed one — is named as
damage in the restore report (decision 171). They hook the seam described above rather than opening a
second apply path: a continue hands the archive's character for the local player back with
`WorldContinueOutcome.LocalCharacter` and the adapter queues it on `CharacterDataSync`'s local restore
path (decision 170).

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
