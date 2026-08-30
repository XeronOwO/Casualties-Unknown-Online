# World-Generation Determinism Audit (2026-08-16)

> **Historical audit.** Point-in-time evidence for world-gen determinism. The
> current run/epoch and random-stream handling is documented in
> [`architecture-evolution/domains.md`](architecture-evolution/domains.md) and
> [`architecture-evolution/protocol.md`](architecture-evolution/protocol.md).

Follow-up to the backlog item inherited from the Claude memory
(`layer-modifier-sync`): drill holes (block 35) were reported to diverge after
segment 19 with no visible symptom, and "post-generation `Random` consumers
were never audited". This document records the audit result so the claim does
not have to be re-investigated.

## 1. The block-35 claim has no current code path

- `GenerateWorld` (WorldGeneration.cs:1534-1547) drives
  `WorldGenerateTerrain` and `FinishWorldGeneration` as nested coroutines.
- `GenerateOres` — the only writer of block 35
  (`worldBlocks[num3, num4] = 35`, WorldGeneration.cs:3718) — is called from
  three places, all inside `WorldGenerateTerrain`:
  WorldGeneration.cs:2734, 2939, 3067.
- `WorldGenerationGenerateWorldPatch` wraps `GenerateWorld` in
  `WorldGenRandomIsolation` (patch lines 25-31), which saves and restores
  `UnityEngine.Random.state` around every plain yield
  (WorldGenRandomIsolation.cs:87-112). `GenerateOres` runs synchronously
  inside an isolated segment; no yield exists inside it.
- The existing `[GenStream]` segment fingerprints
  (WorldGenRandomIsolation.cs:49-62) and the whole-block-table
  `[WorldFingerprint]` (RunCoordinator.cs:334-370, includes block 35) are the
  runtime comparison points.

Conclusion: as the tree stands, the block-35 path is sealed by the generation
isolation. The memory claim is stale or predates the current wrapper; there is
no static path left to fix. Runtime confirmation remains pending and is cheap:
compare `[GenStream]` segment fingerprints and `[WorldFingerprint]` between
host and guest during the next dual-side pass.

## 2. Consumers after the last restore

The last generation suspension is `FinishWorldGeneration`'s darken wait
(WorldGeneration.cs:3625). After it:

- `ApplyLayerModifiers` was the one consumer that leaked (frame-level draws
  between restore and decision). `LayerModifierApplyPatch` rewinds the
  decision to `WorldGenRandomIsolation.LastSegmentStart` on both sides and the
  guest replays the same draws; `LayerModifierSync` defers `Initialize` and
  restores the post-draw state (LayerModifierApplyPatch.cs:43-79,
  LayerModifierSync.cs:126-169).
- The only `Random` consumer after that in the same coroutine is
  `SetFog(Random.Range(...))` for depth 5 (WorldGeneration.cs:3679); it runs
  with no intervening yield and therefore continues the deterministic stream.

Runtime `WorldGeneration.Update` consumers are OUTSIDE the isolated coroutine,
but every world-state effect is domain-covered:

| Consumer | World-state effect | Coverage |
|---|---|---|
| Earthquake trigger/timer/duration (WorldGeneration.cs:866-871) | quake start | `WorldGenerationUpdatePatch` freezes the guest timer; host broadcasts `EarthquakeStart`; guests re-align (patch lines 32-58, WorldEventSync.cs:346-384) |
| Earthquake shove / breaks (WorldGeneration.cs:876-895) | block air writes, body velocity | local body physics is local-compute; the `SetBlock(0)` air writes ride the existing block relay (WorldGenerationSetBlockPatch, WorldEventSync) |
| Fungus rain droplets / wetness / dirtiness (WorldGeneration.cs:901-920) | local body + visual only | wetness/dirtiness are local body fields and ride the 1 Hz character snapshot; droplets are visual |
| Loading-screen jitter (WorldGeneration.cs:943-947) | UI only | no world state |

`WorldGeneration.Start` rolls `skyColor` and shader `_RainIntensity`
(WorldGeneration.cs:241-244) on the public stream before the baseline capture;
these are visual only and are accepted per-side divergences (recorded below).

## 3. Start/Awake random fields on generated objects

`Object.Instantiate` runs `Awake` synchronously inside the isolated segment,
but `Start` runs later on the public stream; per-side `Start` rolls can differ
even when the segment fingerprints match. Each generated-object roll was
audited:

| Component | Random field | World-state effect | Coverage |
|---|---|---|---|
| `CorpseScript.Start` | loot categories, item condition | loot items | `GeneratedItemAuthority` publishes the host's list; `GeneratedItemApplication` binds/replaces guest copies and destroys host-unknown locals |
| `GeyserScript.Start` | `liquidType` | eruption liquid | `GeyserStateSync` full set on world entry / periodic, plus creation-data on runtime spawn |
| `NonDescriptCan.Start` | food/liquid/happiness etc. | consume effects | `[Saveable]` component fields captured by `ItemStateCodec.CaptureSaveableComponents` and applied by `ItemApplication.ApplyAuthoritativeState` |
| `EPdaScript.Start` | `savedIndex` note choice | PDA content | `[Saveable]` capture/apply, same path |
| `PlushScript.Start` | sprite/sound index | cosmetic item state | `[Saveable]` capture/apply, same path |
| `BlueprintScript.Awake` | `recipeIndex` | unlock content | `[Saveable]` capture/apply; the use event additionally reports the index (`RecipeUnlock`) |
| `SpiderHandler.Start` / `CrystalEnemy.Start` | movement/attack timing | enemy AI | host-authoritative enemy sim; guest copies frozen by `EnemyPatches` |
| `OilPipeScript.Start` | `coolTime` | oil production | guest `OilPipePatch` skips the guest pipe; fluid host simulation streams the grid |
| `LifepodPump.Awake` | destroy 20% of pumps | pump existence | `Awake` runs synchronously inside `Instantiate` during an isolated segment — deterministic |
| `GrabberPlant.Start` | tendril phase/scale | visual phase only | grab terminal state rides `EnemyEffectMsg` (`GrabberGrabbed`) |
| `SpikeStabberScript.Start` | `randOffset` light phase | visual phase only | activated/stab state rides `SpikeStabbed` event replay |
| `StalactiteDropper.Start` | `countTime` initial delay | local trigger timing | drop outcome rides `StalactiteDropped` event + one-shot replay |
| `SurvivorNote.Start` | note choice | local UI + time scale | accepted/excluded in `entity-features` matrix |
| `Body.Start` | initial hunger/thirst/weight/happiness/energy | own body stats | own body is local-compute; peers see it through `CharacterData` snapshots |
| `PlayerCamera.Start`, `Limb.Awake`, `FacialExpression`, `PantSound`, `MenuParallax` | local presentation only | none | accepted |

## 4. Decision

- No production code change is warranted for the block-35 backlog item: the
  writer is inside the isolated coroutine, and the original observation cannot
  be reproduced by static analysis. The item is downgraded to a one-time
  runtime confirmation (fingerprint comparison) at the next dual-side pass.
- The per-side `Start` divergences that remain are either already covered by
  the item/enemy/fluid/entity-event domains or are visual/timing-only and are
  recorded as accepted gaps in `docs/backlog.md`.
- No new protocol, patch, or pure-machine change is introduced by this audit.

## 5. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Block-35 generation | none | WorldGeneration.cs:1534-1547, 2734/2939/3067, 3718; WorldGenerationGenerateWorldPatch.cs:25-31; WorldGenRandomIsolation.cs:87-112 |
| Post-restore layer decision | already landed (`32fd0eb`) | LayerModifierApplyPatch.cs:43-79; LayerModifierSync.cs:126-169 |
| Post-restore depth-5 fog | none needed | WorldGeneration.cs:3679 (no yield after) |
| Runtime quake | already landed | WorldGenerationUpdatePatch.cs:32-58 |
| Runtime block/air writes | already landed | WorldGenerationSetBlockPatch; WorldEventSync |
| Start-callback item fields | already landed | ItemStateCodec.cs:167-237, 296-353; ItemApplication.cs:160-188, 476-485 |
| Start-callback enemy fields | already landed | EnemyPatches.cs:27-60 |
| Start-callback fluid fields | already landed | OilPipePatch.cs:14-18; FluidSimulationPatch.cs |
| Accepted visual/timing gaps | backlog record only | `docs/backlog.md` presentation gaps + accepted exclusions |
| Runtime confirmation | pending at next dual-side pass | `[GenStream]` and `[WorldFingerprint]` log comparison |
