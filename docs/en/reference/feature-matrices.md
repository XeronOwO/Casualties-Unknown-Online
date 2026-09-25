# Feature matrices

[Documentation](../README.md) > [Reference](README.md) > Feature matrices

---

**After this page** you can look up whether a game feature is synced, deliberately not synced, or still
open, and you know which file is the machine copy of that answer. The game-side detail behind the item
rows is in [The game behind the adapter](../internals/game-internals.md); why reads never arbitrate is
in [Who decides what happens to a player](../internals/judgment-ownership.md).

## The two matrices

| Matrix | File | Shape |
|---|---|---|
| Items | `docs/contracts/item-features-matrix.csv` | one row per item (192 rows), 12 feature columns |
| Entities | `docs/contracts/entity-features-matrix.csv` | one row per entity (67 rows), 9 feature columns |

Read them with the tool, never by hand: a misaligned row is detected before any output is trusted.

```text
tools/item-features.ps1   validate | list | get <item> [feature] | set <item> <feature> <value> | add-item | remove-item | add-feature
tools/entity-features.ps1 validate | list | get <entity> [feature] | set <entity> <feature> <value> | add-entity | remove-entity | add-feature
```

- The CSV is UTF-8 without BOM; a cell may not contain a comma (use `/` to separate values).
- Every read validates first and every write validates after — a misaligned row aborts with exit 1,
  never silently.
- The narrative lives on this page: the item section below for the item rows, and the
  [Entities by family](#entities-by-family) tables for the entity rows, with the enemy design itself in
  [How enemies stay in step](../internals/enemy-sync.md). The entity tables and the entity CSV are
  cross-checked by `EntityFeaturesDocConsistencyTests`, so an entity added, dropped or re-verdicted in
  one has to be refreshed in the other in the same change.

## What `sync` means

| Value | Meaning |
|---|---|
| `covered` | synced; the `path` column names the covering mechanism (an entity-event kind, a domain class, a message) |
| `excluded` | deliberately not synced; the `path` column carries the reason |
| `missing` | not synced yet; the `path` column carries a priority — a `missing` row is an open TODO |

The entity matrix carries no `missing` row today: 48 rows are `covered` and 19 are `excluded` by design.
An item row instead marks each feature column with `Y` or leaves it blank, and the sync verdict per
feature is the table below.

## Item matrix: the feature columns

| Column | What it covers |
|---|---|
| `battery` | battery compartment and charge drains, including the battery items' own charge |
| `liquid` | liquid container (`stack`) |
| `consumable` | eat/drink/inject: `condition` or `stack` per use, zero means despawn |
| `durability` | per-hit or per-time `condition` decrement |
| `modeswitch` | `CustomItemBehaviour.state` modes |
| `payload` | `CustomItemBehaviour.data` — an `object[]` payload |
| `gun` | the `GunScript` state machine |
| `ammo` | `AmmoScript.rounds` |
| `geiger` | `GeigerCounterAudio.active` |
| `randomroll` | values rolled once at spawn and then fixed |
| `randomaction` | per-action randomness — diverges by design |
| `bodycomponent` | a component attached to a body or limb |

## Item sync status, feature by feature

| Feature | Status | What travels |
|---|---|---|
| `battery` | covered | The charge (`Item.condition`) is synced on every path. The `decayMultiplier` that drives the drain is derived from mode state, so the two sides' decay *rates* could disagree before the state whitelist landed; the charge itself always followed the host's mirror. |
| `liquid` | covered | `WaterContainerItem.stack` round-trips as `Liquids` on every path. Restore rebuilds the stack directly: the prefab `Awake` already filled the defaults, so an additive restore would read "full" again. |
| `consumable` | covered | `condition` and `stack` are synced; the use itself is a guest-local fact — use reports are adopted unconditionally by the host, because comparing use evidence would bounce every use back. |
| `durability` | covered | `condition`, on every path. |
| `modeswitch` | covered | The state whitelist admits `CustomItemBehaviour` and only the public `int state` travels — the private `Item` reference and the `object[] data` array stay out. Restore matches by component simple name, and all ten carrying paths share one capture/restore pair. |
| `payload` | partial | `object[]` is still an unsupported generic saveable field kind, but both persistent gameplay states inside it have explicit wire faces now: the liquidcentrifuge cooldown is captured as a synthetic `cooldown` component field (with a one-frame reapply marker, because `CustomItemBehaviour.Start` re-initializes the array), and the dynamite lit-fuse latch as a synthetic `fuse` component field, with the detonation itself as `DynamiteExplosion` (`NetMsg` 105). Only the frame-level jetpack throttle stays local-only. |
| `gun` | covered | The nine `[Saveable]` `GunScript` fields are captured and restored on every path. The persistent transitions (fire, rack/unrack, safety, load, unload) additionally report through the existing item-use fact path (`GunStateSync`), so the host's record and the peer clones update at the action edge; the 1 Hz character snapshot remains the fallback. The one-shot shot presentation rides the `CharacterSoundKind.GunFire` event and `MuzzleFlashReplay`. |
| `ammo` | covered | `AmmoScript.rounds`. |
| `geiger` | covered | `GeigerCounterAudio.active`; the use action also mirrors `decayMultiplier`. |
| `randomroll` | covered | Values rolled once at spawn (`EPdaScript.savedIndex`, `PlushScript.index`, `BlueprintScript.recipeIndex`, `NonDescriptCan` contents). Inside the isolated generation stream the roll is deterministic per side; outside generation each side rolls its own and the capture after `Start` overwrites it with the host's value on apply. |
| `randomaction` | by design | Per-action random consumption is not state divergence: the same action lands different results on each side by game design (food effects, gun jams and spread, jetpack wet-throttle and flame alpha, the exposed-core death animation). "Both sides eat the same funguschunk and get different effects" is correct behaviour, not a bug. |
| `bodycomponent` | covered | Components applied to a limb or body (tourniquet, splint, icepack, the injectables) travel as `CharacterLimbMsg.Components` — the same component state shape as item components — on the 1 Hz character snapshot, cross-player item-use results and reconnect restore; `LimbComponentStateCodec` captures and applies the real game components. |

**Passive-effect items** sit outside the matrix: they continuously modify the body but hold no state of
their own (the crystalshard set, the autozoom goggles zoom assist, the scuba gear wetness drain, the
blindfold, the roselight intensity). Nothing has to travel for them.

## Crafting: operations, not per-entry writes

The crafting family syncs as **operations**: one operation is one `CraftReport` (`NetMsg` 76) carrying
its complete terminal state — the consumed or changed materials plus the products. The host classifies
each entry against its world and transfer tables, applies the whole report and relays it with the
source excluded; it is never decomposed into per-entry broadcasts.

| Surface | How it reports, and what commits |
|---|---|
| `Recipe.TryMake` (the crafting menu) | Materials are the inventory plus floor items within 10 m. The material disposition comes from the recipe data; a liquid **result** merges into an existing container with no new item, and the coordinator's liquid-fingerprint diff turns that container into a changed entry. The deny path (no materials) reports nothing, and a destroyable material that still carries contents is refused before the native consume path (`CraftingContentsGuard`), so crafting cannot lose those contents — the player empties the item first. The first-craft bonus and the failure-branch injuries ride the 1 Hz character snapshot (accepted latency). |
| `Body.CombineItems` (drag-combine) | Gun/mag and mag/round loads destroy the dragged item, whose end-of-frame `OnDestroy` rides the destroy-claim set; the condition merge changes both items; a refused load or a full-condition no-op commits nothing. The water branch opens the interactive liquid-transfer UI instead, and reports nothing until the transfer finishes (cancel changes nothing). |
| Blueprint use | The blueprint's own destruction rides the existing use digest; the unlock rides `RecipeUnlock` (`NetMsg` 77), applied to the per-process static table on every side. The native "learned recipe" popup also replays on the other sides for a new unlock, and is suppressed on the acting side. The one-shot report has an absolute set beside it: `RecipeUnlockSnapshot` (`NetMsg` 139) sends the host's live unlocked indices on world entry and the 60 s repair group, and a guest re-reports its own set until the host's set carries it — a backfill applies silently, because a catch-up is not a learn. |
| Enum component fields (codec kind 6) | `GunScript.roundInChamber` and the ammo/firing-mode enums ride the component digest as the underlying int. The gun's live state was already covered through the public bool/int fields; the enum was silently dropped before this round, and `CraftCodecContractTests` now guards the kind table. |

Recorded gaps that stay where they are: non-craft container-material spills (container break, unload-all
children) deliberately ride the container-item domain because they create real new world items;
no-autopickup products ride the item domain's spawn path; mindwipe's recipe-static reset rides the entity
domain; the save-restore recipe divergence cannot happen because multiplayer has no save-load path. The
heater cooker is resolved as one `CookItemCommand` in a single kernel batch. Gun firing and racking is
resolved through `GunStateSync`.

## Known state gaps

| Item | Status |
|---|---|
| `CustomItemBehaviour.data` | Only the frame-level jetpack throttle remains local; the cooldown and the lit-fuse latch are synced as synthetic component fields and the detonation rides its own message. |
| Grappling hook `fired` / `hookLatched` / `pulling` | Synced: the codec's multiplayer-state table carries the three private bools on every item state path, and the clone renderer presents the fired sprite while disabling the owner-local script. The rope and hook projectile stay local presentation. |
| `WatchScript` timers | Excluded by design: they only drive the owning player's UI and body speech, and the render clone's `WatchScript` is disabled. |
| `AutoPump.worn` | Excluded by design: it only drives the owning player's blood-pressure effect, and the render clone's `AutoPump` is disabled. |
| Peer-view rendering | The clone renderer instantiates by prefab and applies the snapshot's component state, so a remote player's held flashlight renders in its real mode and a fired grappling hook renders with the fired sprite. Display path only. |
| World-item component state on keyframes | Resolved: the periodic snapshot (5 s base, which the adaptive governor may stretch to 10 s under pressure) re-aligns an existing world item's top-level state whenever it diverges from the host table. Position stays on the position stream and container contents stay on the container message family. |

## Entity matrix: the columns

| Column | Meaning |
|---|---|
| `type` | the family: `trap`, `lifepod`, `unlock`, `trade`, `crystal`, `fluid`, `environment`, `building`, `creature` |
| `trigger` | the interaction face that drives the state: `collide`, `trigger`, `click`, `detect`, `field`, `story`, `use` |
| `state` | the fields that can diverge between sides; `none` = stateless |
| `one-shot` | `yes` = the entity consumes itself and cannot re-trigger, so the consumption must be shared once |
| `damages` | `yes` = the entity deals damage (writes limb/body stats or destroys items) |
| `random` | `yes` = the entity consumes the random stream: generation-time rolls are deterministic, runtime rolls stay local and their results travel as state |
| `replay` | what the receiving side plays for the event — the "does everyone see and hear it" column |
| `sync` | `covered`, `excluded` or `missing` |
| `path` | the covering mechanism, or the exclusion reason, or the priority of a `missing` row |

## Entities by family

**Traps** — the entity-event channel: the trigger side computes the full original effect, reports, the
host applies, relays, and each receiver replays.

| Entity | Sync | Path |
|---|---|---|
| MineScript | covered | `MinePressed` + `MineExploded` |
| SpikeStabberScript | covered | `SpikeStabbed` |
| BearTrap | covered | `BearTrapClamped` / `BearTrapReleased` |
| BarbedFence | covered | `BarbedFenceHit` |
| CoilScript | covered | `CoilShocked` |
| CactusScript | covered | `CactusHit` plus a silent `BuildingEntityDamaged` relay |
| JumpPadScript | covered | `JumpPadLaunched` |
| StalactiteDropper | covered | `StalactiteDropped` |
| GeyserScript | covered | `GeyserActivated`; the liquid type rides `GeyserStateSnapshot` |
| SoundCannon | covered | `SoundCannonFired` |
| TurretScript | covered | `TurretFired` / `TurretSelfDestructed` |
| CrystalElectric | covered | `CrystalElectricShocked` |
| CrystalFragile | covered | `CrystalFragileBroken` |
| CaveTickSpawner | covered | `CaveTicksSpawned`; the spiders ride `EntitySpawned` + the runtime-spawn binding |
| BananaPlantSlip | covered | `BananaPlantSlip` |
| GrabberPlant | covered | layout key plus `EnemyEffectMsg` terminal state |

**Lifepod interior**

| Entity | Sync | Path |
|---|---|---|
| ShuttleStartOpen | covered | `ShuttleDoorOpened` |
| LifepodController (heat button) | covered | `LifepodHeatChanged`, with `heatState` in the extra data |
| LifepodShower | covered | `LifepodShowerActivated` (one consumption shared) |
| Heater (cooker branch) | covered | `CookItemCommand` kernel batch; the temperature field is excluded as a local body effect |

**Unlocks** — one-shot progression, where divergence would be a hard gameplay difference.

| Entity | Sync | Path |
|---|---|---|
| BioTerminalScript (blood unlock) | covered | `BioTerminalUnlocked` |
| ScrapEaterScript | covered | `ScrapEaterProgress`, with progress in the extra data |
| MedStationScript | covered | `MedStationHealed` |
| BatteryRecharger | covered | `BatteryInserted`; the charge itself rides the item domain's condition |

**Trade**

| Entity | Sync | Path |
|---|---|---|
| TraderScript | covered | the trade domain (`TradeStateSync` / `TradeExecutor`) plus `TraderSwing`; the host's computed state overwrites on every interaction and on a 5 s base fallback, and the acting side runs the game method in full and reports `TraderAction` |
| Talker | covered | `SpeechMsg` (`NetMsg` 74) — entity key plus text id, replayed as a clone-side bubble |
| LampScript | covered | the trade domain — the flat reputation loss runs on both sides from the broadcast base |

**Crystals** — the effect assignment happens inside the isolated generation stream, so both sides get
the same crystal type and only the runtime behaviour needs sync.

| Entity | Sync | Path |
|---|---|---|
| CrystalUnstable | covered | `CrystalUnstableExploded` + `CrystalUnstableTicked` (the pre-explosion ticking is transient, the explosion is the durable consumption) |
| CrystalMetamorphic | covered | `CrystalMetamorphicTriggered` — death and drops ride the event |
| CrystalMimic | covered | `CrystalMimicTriggered`; the enemies ride `EntitySpawned` + the runtime-spawn binding |
| CrystalShy | covered | `CrystalShySwapped` |
| CrystalTeleport | covered | `CrystalTeleportTriggered` plus the 20 Hz player stream for the body teleport |
| CrystalEMP | covered | `CrystalEMPActivated` |
| CrystalDripping | covered | the drip writes ride the fluid domain |
| CrystalBurning, CrystalTemperature, CrystalSeptic, CrystalHealing, CrystalBlinding, CrystalIrradiated | excluded | local body effect |
| CrystalGravity, CrystalKinetic | excluded | local physics |

**Fluid world grid**

| Entity | Sync | Path |
|---|---|---|
| FluidManager | covered | `FluidRegion` / `FluidWorldSync` plus `FluidPresentation` (`NetMsg` 96); the host simulates every member's viewport |
| OilPipeScript | covered | oil production rides the host fluid stream |
| LifepodPump | covered | pump writes ride the host fluid stream |

**Environment**

| Entity | Sync | Path |
|---|---|---|
| CorpseScript | covered | building-entity / generated-item authority — corpse health and loot |
| TutorialHandler | covered | `TutorialClawState` — the host's 20 Hz claw stream; per-side course and props stay by design |
| GrapplingHook | covered | item component state plus `RemoteItemPresentation` |
| WaterPusher | covered | `FluidPresentation` |
| XalorisScript | covered | `EnemyEffectMsg` septic tick, a 0.5 s edge terminal state |
| SurvivorNote | excluded | local UI and local time-scale (accepted divergence) |
| Climbable, GeigeFruitScript, LeadbushScript, RadioactiveObject | excluded | local body |
| BounceShroom | excluded | local physics |
| CampfireAnimation | excluded | pure visual |
| ItemLock | excluded | marker only |

**Buildings**

| Entity | Sync | Path |
|---|---|---|
| Openable (locks and crates) | covered | `BuildingEntityOpened` |
| BuildingEntity (attack damage) | covered | `BuildingEntityDamaged`, with the hit flash replayed on non-attacker views |
| DrillPod | excluded | world-join granularity: repair and world reset |
| GunmineScript, SawbladeScript | excluded | hand-placed, outside the generation stream |

The serialized `Openable` components are all in `resources.assets` (17 instances, 11 root prefabs):
`isKeypad` is true only on the `dropcapsule` prefab and the two nested `dropcapsule` props inside
`Structures/BrickLoot`, and `instantOpen` only on `foodbox` — the root prefab plus the nested copy inside
`BioContainer`. Every other `Openable` is lockpick.

**Creatures** — the host-authoritative enemy domain, whose own page is
[How enemies stay in step](../internals/enemy-sync.md).

| Entity | Sync | Path |
|---|---|---|
| SpiderHandler | covered | enemy state stream, `EnemyAttack` announcement judged locally, bite events, claw-animation replay |
| CaveTicks | covered | same family as SpiderHandler |
| ElderThornbackBehaviour | covered | enemy state stream plus horror events |
| CrystalEnemy | covered | enemy state stream (wind-up telegraph), `EnemyAttack` announcement judged locally, and the kernel lunge-result event; the runtime tint rides `EntitySpawned` / `EnemySnapshot` |

Enemy movement uses Unity physics and local random numbers, so two sides simulating independently would
diverge: guests do not simulate enemy physics at all, they render a frozen copy driven by host state.
The `Heater` temperature field on `xaloris` is excluded by design — it writes only the local player's
body temperature.

## Related reading

- [The game behind the adapter](../internals/game-internals.md) — the game mechanisms these rows rest on
- [Who decides what happens to a player](../internals/judgment-ownership.md) — why a reading never becomes a verdict
- [The adapter and game updates](../internals/adapter-and-updates.md) — what a game update may move under these rows
- [Protocol messages](protocol-messages.md) — the ids and channels named in the `path` column
- [Configuration keys](configuration.md) — the settings that shape the run these matrices describe
- [Glossary](glossary.md) — feature matrix, projection, remote clone, deterministic, seed

---

[Documentation](../README.md) > [Reference](README.md) > Feature matrices
