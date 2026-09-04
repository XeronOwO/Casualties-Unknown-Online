# CUCoreLib migration support

- Status: Review
- Priority: Medium
- Category: Mod ecosystem / migration
- Source: External project — <https://github.com/jimmyking9999999/CUCoreLib> (based on KrokMP)
- Evidence: `docs/evidence/selfchecks/mod-api/*` (content, status, moodle, item, building, tile, structure, runtime, worldgen, visual, and migration-base selfchecks)

This ticket is now code-complete on the CUO-side migration seams. The remaining
items in the matrix are future/non-goal, not open implementation work inside
this ticket; it waits in Review for the unified acceptance pass.

Evaluate the external CUCoreLib project and either:

1. Implement its feature set directly in CUO when the feature is within CUO's
   architecture boundaries, or
2. Provide/adjust CUO functional interface seams so CUCoreLib (or the KrokMP
   patterns it builds on) can migrate to CUO with minimal adapter work.

Constraints:

- Do not commit external source code, assets, or reverse-engineered material
  from CUCoreLib/KrokMP; only understand its public feature/API surface.
- Respect existing architecture rules: no new wire protocol unless the feature
  genuinely requires it; `Abstractions` remains the only package mods reference.
- Prefer interface support/migration guidance over porting code verbatim.
- Any landed functionality must pass the normal build/test/architecture gates.

## Executive summary

CUCoreLib is best understood as two different layers:

1. A **single-player content/helper library**: asset loading, item/recipe/liquid/
   tile/building/structure registration, statuses, moodles, locale, settings,
   save providers, console commands, debug tools, and direct player/Unity
   helpers. Most of this API is static and exposes game-private or Unity types.
2. A **thin KrokMP compatibility layer**: dynamic JToken channels, request/
   response handlers, and generic JObject snapshot modules. This layer exists
   only to backfill KrokMP v4; it is explicitly experimental and is not a
   model CUO should copy.

CUO already has a stricter and safer mod framework: discovery, manifest,
lifecycle, permissions, host commands, local console commands, an opaque
content registry, host-persistent mod state, local UI, read-only game state,
entity spawn, curated native API, and a typed mod message channel.

The original evaluation identified **actual semantic content support** as the
largest genuine gap. That gap is now closed on the CUO side: typed
content DTOs and Runtime binding providers cover items, recipes, liquids,
liquid tiles, tiles, buildings, structures, statuses, and moodles, plus
spawn/placement, worldgen density/drops, advanced item behavior/visuals, and
the local status/moodle runtime projections. The KrokMP channel/snapshot
layer remains deliberately not ported: CUO's typed kernel, discrete events,
state streams, and `IModNetwork`/`IModCommands` already cover the same needs
with explicit authority semantics.

Recommended direction:

- Do **not** port CUCoreLib source or its JToken snapshot/channel protocol.
- Treat `IModContent` as the seed for a future **content binding pipeline**:
  mods declare static content (opaque today, or typed DTOs later);
  Runtime/GameAdapter consumes the framework-wide content view and converts it
  into vanilla registries before world generation. No wire change; the existing
  mod handshake is the consistency boundary.
- For everything already covered by CUO Abstractions, document a source-level
  migration mapping only.
- Game/Unity-facing helper APIs (CCLBody, CustomInstantiate, minigames,
  animations, direct body mutation) are outside the safe mod boundary. Only
  read-only projections are exposed today through `IModNativeApi` /
  `IModGameState`; write operations need a real authority design first.

## Implemented migration base (2026-09-01)

This is the landed CUO-side migration foundation. It intentionally does not
port full CUCoreLib functionality (the non-goal rows below remain out of
scope), but it now covers the content-binding family that CUCoreLib-style
mods need:

- `ModContentKind` in Abstractions — a stable base vocabulary for common
  content kinds (item, recipe, liquid, tile, building, structure, status,
  moodle, setting, locale).
- `ModContentDefinition.SchemaVersion` and the
  `IModContent.TryRegister(id, kind, data, schemaVersion)` overload — a mod
  can version its opaque content schema; the framework stores the version
  verbatim and never migrates it.
- `ModItemDefinition` in Abstractions — the first well-known typed item
  payload, with `ToPayload()` / `FromPayload()` using the BCL
  DataContractSerializer (no new package, no game/Unity type).
- `IModContentCatalog` / `ModContentCatalog` in Runtime — a read-only,
  payload-agnostic catalog over every mod's registered content. It supports
  kind filtering, unique kind+id resolution, and cross-mod duplicate /
  schema-version conflict diagnostics. It deliberately makes no ownership
  choice and does not interpret bytes.
- `IModContentOwnerQuery` + `IModContext.ContentOwners` + the GameAdapter-safe
  Runtime adapter — the mod-facing content ownership lookup. A mod can ask
  who registered a given content kind + id; the lookup follows the same
  ordinal matching and ambiguity policy as the runtime catalog. It is
  read-only, payload-agnostic, and adds no wire/protocol surface.
- `IContentBindingProvider` + `ModContentBinder` in Runtime — the generic
  content-binding skeleton. It runs after first-frame mod discovery and
  routes each content entry to the provider registered for its kind. It only
  binds content from **shared-content network modes** (`Synchronized`,
  `Authoritative`, `RequiresAllPlayers`); local-only and HostOnly content is
  skipped to avoid remote materialization desync.
- `GameAdapterItemContentProvider` — the first concrete provider. It accepts
  `ModItemDefinition`, waits for the vanilla item table, injects the
  static `ItemInfo` into `Item.GlobalItems`, and — when the DTO supplies a
  `TemplateId` — builds an inactive runtime template from that vanilla prefab,
  attaches the requested `SpawnComponents`, and serves it to CUO's item
  prefab resolution seam. `Utils.Create`, CUO's restore/spawn paths, and
  targeted transpilers for the game's native building-drop/save-restore
  resource loads can materialize those custom items; no game/Unity type is
  exposed to mods.
- `ModRecipeDefinition` + `GameAdapterRecipeContentProvider` — the second
  typed content kind. The plain DTO carries result/ingredients/category and
  the Game Adapter provider injects built `Recipe` objects into
  `Recipes.recipes` once the table exists, with recipe-table-rebuild
  re-injection and duplicate protection.
- `ModLiquidDefinition` + `GameAdapterLiquidContentProvider` — the third
  typed content kind. Static liquid fields (tint, value, health/injection
  flags, qualities) are mapped into `Liquids.Registry`; locale display text is
  applied locally. Game delegates are not part of the DTO.
- `ModBuildingDefinition` + `GameAdapterBuildingContentProvider` — the fourth
  typed content kind. A plain DTO carries a vanilla `TemplateId`, optional
  `BuildingEntity` field overrides, and `SpawnComponents`; the Game Adapter
  builds an inactive runtime building template and serves it through the
  existing `Utils.Create`/`EntitySpawned` materialization path, so
  `IModEntitySpawn.TrySpawn` can create custom buildings without exposing game
  or Unity types.
- `ModBuildingDrop` + `ModBuildingGenerationStyle`/`ModBuildingPlacement` +
  `BuildingWorldGenDistribution` — the building drop/worldgen-density seam
  on top of the typed building content. A plain DTO carries chance-based and
  always drops (`ModBuildingDrop` lists), extra guaranteed-drop categories,
  per-chunk spawn density, layer masks, generation style, placement surface,
  and surface/random-flip controls. The Game Adapter provider validates the
  authored values and exposes a stable id-ordered worldgen snapshot; the
  factory applies vanilla `ItemDrop` arrays to the runtime `BuildingEntity`;
  `BuildingWorldGenDistribution` distributes custom buildings from the
  `WorldGeneration.PlaceCrystals` postfix inside the sealed generation stream.
  No wire message, no JObject snapshot, and no game/Unity type crosses
  Abstractions.
- `ModTileDefinition` + `GameAdapterTileContentProvider` — the fifth typed
  content kind. The plain DTO carries a sprite resource path or a vanilla tile
  index as the visual source, static BlockInfo behavior fields, RGBA tint, and
  collider type. The Game Adapter allocates a deterministic custom block index
  (never a vanilla index), injects a built `Tile` into every fresh
  `WorldGeneration.tiles` palette, and answers `WorldGeneration.GetBlockInfo`
  for custom indices through a narrow Harmony prefix. It also carries the
  typed ore/drop projection fields: `SpawnAmount`, `SpawnLayers`,
  `ModTileGenerationStyle`, and `ModTileDrop` entries. The Game Adapter
  distributes accepted tiles inside the sealed generation stream through a
  `WorldGeneration.GenerateOres` postfix, and spawns authored drops on local
  custom-tile breaks through the existing block-break report. No arbitrary
  world snapshot and no wire change are part of this seam.
- `IModItemSpawn` + `IModItemSpawner` + the Game Adapter implementation — the
  mod-facing world-item spawn seam. It uses the same `SpawnEntity` permission
  and policy rails as entity spawn; the Game Adapter creates the local `Item`
  via `Utils.Create`, and the existing `ItemSpawned` item-domain path
  replicates it. No new wire message and no game/Unity type crosses the mod
  boundary.
- `ModItemDefinition.WorldSpawnPerChunk` + category loot-pool injection +
  `ItemWorldGenDistribution` — the custom item world-spawn and vanilla loot
  seam. Bound items are injected into `ItemLootPool.pool` under their authored
  `Category` (weighted by `SpawnFrequency`) so corpses, building guaranteed
  drops, traders and dev-console spawners see them exactly like vanilla items;
  a positive `WorldSpawnPerChunk` opts the item out of the generic category
  pool and scatters loose ground items from a
  `WorldGeneration.PlaceCrystals` postfix inside the sealed generation stream.
  `NativeItemResourcePatches` also covers `CorpseScript.Start`, so direct
  `Resources.Load` corpse loot can materialize CUO custom item templates. The
  existing generation-item snapshot synchronizes world-spawned items — no new
  wire message and no game/Unity type crosses the mod boundary.
- `ModItemDropSource` + `ModItemDefinition.DropSources` +
  `ModItemDropSourcePatches` — the explicit fixed drop-source pool seam. The
  Abstractions flags enum mirrors CUCoreLib's `DropPool` source set (corpse,
  built-in medical/food/container crates, trader 1-3, drop capsule, capsule
  container); the Game Adapter provider registers each selected source as a
  stable synthetic `ItemLootPool` category and suppresses the generic category
  fallback, while narrow `CorpseScript.Start`, `BuildingEntity.Start`, and
  host-side `TraderScript.GenerateSingleItemList` patches add the source
  category to the existing vanilla loot flow. No new wire message, no JObject
  snapshot, and no game/Unity type crosses Abstractions.
- `ModItemContainer` / `ModItemBattery` / `ModItemLight` / `ModItemTool` /
  `ModItemGun` + `CustomItemBehaviorValidator` +
  `CustomItemBehaviorApplier` — the advanced item behavior seam. Plain
  Abstractions DTOs carry the vanilla-compatible container capacity,
  battery preset/charge, light shape/color/offset, melee AttackInfo fields and
  gun nullable overrides; the Game Adapter provider validates them and maps
  tool/gun/battery onto the static `ItemInfo` surface (use action, auto attack,
  gun tag, battery decay flag). The runtime item template factory additionally
  configures vanilla `Container`/`BatteryItem`/`GunScript` components and
  creates `Light2D` through the existing reflection convention for URP types.
  Game delegates/Unity types never cross Abstractions and no new wire message
  is used.
- `ModItemVisual` + `ModItemLimbWornSprite` +
  `CustomItemVisualState` + `CustomItemVisualPatches` — the custom item
  visual seam. The plain Abstractions DTO carries a worn-sprite resource
  path, local worn offsets, an optional worn sorting-order override, a
  liquid-mask resource path for water containers, additive multi-limb
  worn sprite entries (limb name + resource path + per-limb offsets), and
  ordered frame-animation definitions for the base, worn, and liquid-fill
  sprites (frame paths + fps + loop); the Game Adapter resolves the resources
  on the inactive runtime template, stores the base/worn/liquid/multi-limb
  sprites on a per-instance component, drives the primary/worn/liquid
  renderers with a local GameAdapter sprite animator, applies/restores the
  worn sprite on `Body.WearWearable` / `Body.DropWearable` and the remote
  restore/clone paths, configures the vanilla `Wearable` secondary-sprite
  arrays at `Wearable.CreateSprites`, and re-applies the liquid mask after
  `WaterContainerItem.Start`.
- `IModTilePlacement` + `IModTilePlacer` + the Game Adapter implementation —
  the mod-facing single-cell tile placement seam. It uses the same
  `SpawnEntity` permission and policy rails as spawn; the Game Adapter
  resolves a custom tile content id to its deterministic block index and calls
  the vanilla `WorldGeneration.SetBlock` path, and the existing `BlockPlaced`
  relay replicates the write. No new wire message and no game/Unity type
  crosses the mod boundary.
- `ModStructureDefinition` + `GameAdapterStructureContentProvider` +
  `IModStructurePlacement` + `IModStructurePlacer` + the Game Adapter
  implementation — the mod-facing typed multi-block structure seam. The plain
  DTO carries a validated marker grid (vanilla block indices and/or custom tile
  content ids) plus per-depth spawn counts; the Game Adapter provider compiles
  it into non-air cells. The placement surface uses the same
  `SpawnEntity` permission and policy rails as spawn, preflights the entire
  structure on air/in-world cells, then calls the vanilla
  `WorldGeneration.SetBlock` path per cell; the existing `BlockPlaced` relay
  replicates every write. Automatic worldgen distribution consumes
  `SpawnCounts` during the isolated generation stream, after vanilla worldgen
  but before the collider/UpdateWorld pass, and places only the static block
  grid — no entity/loot/background layer. No new wire message and no game/Unity
  type crosses the mod boundary.
- `ModStatusDefinition` + `ModStatusScope` +
  `GameAdapterStatusContentProvider` — the typed static status-descriptor
  seam. The plain DTO carries body/limb scope, player-facing text,
  save-enabled metadata and an optional moodle id; the Game Adapter provider
  validates and stores the descriptor as migration base. Dynamic per-player /
  per-limb runtime status values are deliberately not part of this seam and
  belong to the future typed mod-data domain.
- `ModMoodleDefinition` + `GameAdapterMoodleContentProvider` — the typed
  static moodle/presentation-descriptor seam. The plain DTO carries intensity,
  a stable icon id/resource key, display text, critical/chipped/important
  presentation flags and hold seconds; the Game Adapter provider validates and
  stores the descriptor. The local vanilla moodle-row feed is landed through
  `ModStatusMoodleProjection`; static content still never travels over the wire.
- `ModDataScope` + `IModData` + `ModDataStore`/`ModDataPolicy` — the runtime
  mod-data scope seam. It gives mods an ephemeral, per-process slot store with
  explicit `LocalOnly` / `Shared` / `HostAuthoritative` declarations. Shared
  mirrors are applied explicitly from a host-originated value received over
  `IModNetwork`; the framework does not auto-replicate and does not add a
  generic snapshot protocol. `IModState` remains the host-persistent special
  case.
- `IModStatusRuntime` + `ModStatusStore`/`ModStatusPolicy` — the phase-1
  runtime status table. It adds an ephemeral per-mod status store keyed by
  status id, player SteamId, and optional limb slot, with the same
  local/shared/host-authoritative scope rules as `IModData`. It does not touch
  vanilla `Body`/`Limb` or feed the vanilla moodle row yet; the domain design
  is in `docs/architecture/mod-status-domain.md`.
- `ModStatusUpdate` + `IModStatusTransport` + `ModStatusTransport` — the
  phase-2 typed status transport seam. It publishes committed shared status
  values as versioned typed frames over the existing `IModNetwork` mod-message
  channel and applies/removes guest mirrors with `TryApplyRemove*`. It adds no
  new NetMsg, no protocol bump, and no generic JObject snapshot; guest-to-host
  change requests remain explicit `IModCommands` semantics.
- `ModStatusProjectionKind` + `ModBodyFormulaProjection` +
  `ModLimbProjection` + `ModStatusVanillaProjection` — the phase-3
  GameAdapter projection slice. A mod can declare a runtime status slot as
  `BodyFormula` or `LimbPhysiology` and publish the matching typed payload;
  the GameAdapter decodes only those well-known payloads and applies additive
  body/limb overlays through `Body.Update`/`Limb.Update` postfixes. The body
  slice covers encumbrance/immunity/jump/average-pain contributions plus
  circulation `HeartRateOffset`/`RespiratoryRateOffset`/`BloodPressureOffset`
  through a `Body.HandleCirculation` prefix/postfix seam; the limb slice covers
  bleed/skin/muscle/infection fields. Vanilla moodle-row feeding is landed by
  ModStatusMoodleProjection. No game/Unity type crosses
  Abstractions and no new wire message is added.
- `ModStatusMoodleProjection` + `ModStatusStore.GetStatusPresences` +
  `ModStatusMoodlePatches` + `MoodleAnimationRegistry` + `CustomImageAnimator`
  — the phase-3 local vanilla moodle-row seam. `ModStatusMoodleProjection`
  reads the local player's active runtime status presences, resolves linked
  static `ModMoodleDefinition`s, and feeds them into `MoodleManager.AddMoodle`
  through prefixes/postfixes around `MoodleManager.AddAllMoodles`. Important
  moodles go to the main row and non-important moodles to the side row.
  Authored `ModMoodleAnimation` frame lists are resolved into a synthetic
  canonical icon key and driven by a `Moodle.Start` postfix on the vanilla
  moodle UI image. Limb-scoped statuses can opt into one moodle row per
  affected limb through `ShowPerLimbMoodles`, route individual limbs to
  distinct moodle descriptors through `LimbMoodles`, and format per-limb
  tooltip text with `LimbDisplayNameFormat` / `LimbDescriptionFormat`. No
  wire message, no reflection-based moodle registry, and no game/Unity type
  in Abstractions.
- `IModMoodleRuntime` + `ModStatusMoodleRequest` + `ModStatusMoodleRuntimeAdapter`
  — the local abstraction-safe moodle resolver seam. A mod registers one
  resolver per runtime status id; the resolver receives the mod-owned opaque
  payload plus stable player/limb identity and returns a static moodle id.
  `ModStatusMoodleProjection` invokes it for active body/limb presences before
  static routing and falls back on absence/exception. It is the CUO-safe local
  replacement for CUCoreLib's `RegisterBody` / `RegisterLimb` callbacks: no
  game/Unity delegate crosses Abstractions and no wire message is added.
- `IModBuildingRuntime` + `ModBuildingPrefabRequest` +
  `ModBuildingInstanceRequest` + `ModBuildingRuntimeStore` +
  `ModBuildingRuntimeAdapter` — the local abstraction-safe building
  prefab/instance hook seam. A mod registers one prefab hook and/or one
  instance hook per custom building id; each hook receives a plain request
  (building/template identity, and instance world transform for the instance
  hook) and returns component type names for the Game Adapter to attach. The
  prefab hook runs when the inactive runtime template is created; the
  instance hook runs on every custom building clone before it becomes active.
  It is the CUO-safe replacement for CUCoreLib's `ConfigurePrefab` /
  `ConfigureInstance` GameObject callbacks: no Unity/game type crosses
  Abstractions and no wire message is added.
- Tests for version storage/validation, catalog enumeration/filtering,
  unique resolution, duplicate/version conflicts, empty-catalog behavior,
  null-argument edges, DTO round-trip, binder routing/shared-mode
  filtering/error isolation, and DI integration.
- Evidence: `docs/evidence/selfchecks/mod-api/mod-content-migration-base-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-item-content-binding-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-recipe-content-binding-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-liquid-content-binding-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-tile-content-binding-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-tile-placement-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-liquid-placement-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-structure-content-binding-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-structure-placement-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-structure-worldgen-distribution-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-tile-ore-worldgen-projection-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-status-moodle-content-binding-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-runtime-data-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-status-runtime-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-status-wire-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-status-projection-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-status-moodle-row-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-item-spawn-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-item-worldgen-loot-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-item-fixed-drop-sources-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-item-advanced-behavior-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-item-visual-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-building-drop-worldgen-selfcheck.md`,
  and `docs/evidence/selfchecks/mod-api/mod-building-runtime-hooks-selfcheck.md`.

Remaining non-goals / future (not open implementation work in this ticket):
- full vanilla integration for every dynamic per-player / per-limb status
  shape (static descriptors are landed; runtime table + typed API phase 1,
  typed status transport phase 2, and the GameAdapter body/limb +
  circulation projection slice phase 3 are landed; the domain design is in
  `docs/architecture/mod-status-domain.md`; arbitrary mod-defined status
  classes remain future);
- advanced moodle presentation details: frame-animated moodle icons are now
  landed through `ModMoodleAnimation`, and richer per-limb row behavior is
  now landed through static `ShowPerLimbMoodles` / `LimbMoodles` routing plus
  per-limb display/description templates. The static-descriptor-driven
  vanilla moodle-row seam is landed. A mod can also register a per-status
  `IModMoodleRuntime` resolver that receives a plain `ModStatusMoodleRequest`
  (status/player/limb identity + opaque payload) and returns a static moodle
  id; this is the CUO-safe local replacement for CUCoreLib's body/limb
  callbacks. Only the exact CUCoreLib-style runtime callbacks that pass a
  live `Limb`/game delegate remain non-goal (Abstractions cannot expose a
  game type);
- CUCoreLib's frame-based animated sprite modes are now landed for the
  primary/worn and liquid-fill sprites through `ModItemSpriteAnimation`
  resource-path frame lists; asset-backed visual modes (Material / Sprite /
  HighResImage) remain future. The primary worn-sprite, additive multi-limb
  worn-sprite and liquid-mask resource-path visuals are landed on top of the
  typed item DTO seam. The container/battery/light/tool/gun minimal behavior
  slice is landed, the explicit fixed drop-source pools is landed on top of
  the category loot-pool + world-spawn seam and the mod-facing content owner
  query;
- CUCoreLib's raw building prefab configure hooks (runtime callbacks that
  receive a live `GameObject` and edit arbitrary prefab/instance state) remain
  non-goal; authored drop rules and worldgen density are landed through typed
  DTO fields, and the component-returning `IModBuildingRuntime`
  prefab/instance hook seam is the CUO-safe replacement. Direct arbitrary
  GameObject mutation stays outside Abstractions;
- an **automatic runtime mod-data sync engine** (the scope seam is landed:
  `IModData` declares/keeps local-only, shared-mirror, and host-authoritative
  values; actual transport remains explicit through `IModNetwork` /
  `IModCommands`, and the dedicated design ticket has moved to review).

Those remain future/non-goal after this ticket. The ticket itself is
code-complete on the migrated seams and is in Review for the unified
acceptance pass.

## Migration function matrix

Source basis: CUCoreLib README/CHANGELOG, `CUCoreLibWebapp` machine docs
(`public/api/topics/*.json`), and the public API signatures in `Registries/`,
`Helpers/`, `Saving/`, and `Networking/`. CUO basis: `docs/api/mod-api.md`,
`docs/architecture/current.md`, and `src/CasualtiesUnknownOnline.Abstractions/`.

| # | CUCoreLib public area | What it gives mod authors | CUO today (Abstractions) | Proposed CUO action |
|---|---|---|---|---|
| 1 | Mod lifecycle / manifest / dependencies | CUCoreLib itself is a BepInEx dependency library; there is no first-class mod manifest or dependency graph. | `[CuoMod]`, `ICuoMod`, `ICuoService`, `IModContext`, `ModManifest`, SemVer dependencies, handshake consistency. | **Already native.** No new seam. Migration: convert a BepInEx plugin into the CUO shell + `[CuoMod]` class; metadata moves to the attribute. |
| 2 | Session / player membership events | No stable equivalent; CUCoreLib only adds a few gameplay callbacks (`OnHeal`, `OnLastStand`). | `IModContext.SessionActivated`, `PlayerJoined`, `PlayerLeft`, `SessionEnded`; `ISessionInfo` snapshot. | **Already native.** No new seam. Migration: subscribe to CUO session events instead of custom/informal MP hooks. |
| 3 | Host-authoritative commands / remote actions | Not a first-class API; mods used KrokMP channels (`RequestServer`, `SendToServer`, `Broadcast`) to ask the server for decisions. | `IModCommands` (`Register`, `TryExecute`), `ModCommand`, `IModCommandContext/Result`, permissions, rate limits, directed reliable result. | **Already native and safer.** No new seam. Migration: replace request/response JToken channels with `IModCommands`; the host copy is the only handler. |
| 4 | Generic mod messaging / channels | `MultiplayerApi` / `MultiplayerBridge`: `RegisterServerHandler`, `RegisterClientHandler`, `SendToServer`, `SendToClient`, `Broadcast`, `RequestServer`, arbitrary string channels, JToken payloads. | `IModNetwork`: `SendToHost`, `SendToPeer`, `Broadcast`, `MessageReceived`, opaque `byte[]`, mod-id routed, 64 KiB cap, star topology. | **Do not port.** Only provide migration mapping. CUO already has a typed, permission-gated message surface; arbitrary public channels would duplicate/mod-add a new protocol. |
| 5 | Generic snapshot sync | `MultiplayerApi.RegisterSyncModule(key, capture, apply)` + `BroadcastSnapshot`/`ApplySnapshot`; JObject full-snapshot modules for arbitrary mod state. | No generic snapshot API. `IModData` provides scope-declared runtime slots (`LocalOnly`, `Shared`, `HostAuthoritative`) with explicit apply, no automatic replication. CUO uses a typed deterministic kernel: discrete committed batches, high-frequency state streams, per-domain state. | **Out of scope / anti-architecture.** Do not add a generic JObject snapshot registry. Mod durable state belongs in `IModState` (host-persistent), runtime scoped values in `IModData`, and transport through `IModNetwork`/`IModCommands`; synced gameplay facts belong in kernel domains. |
| 6 | Asset / resource loading | `AssetLoader`, `FileLoader`: embedded/loose sprites, audio, text, AssetBundles, bundle registration/cache, sprite animations, sprite-sheet helpers. | No asset API in Abstractions. `IModNativeApi` is the only safe game seam and is currently read-only local player state. | **Out of scope for CUO core.** Asset loading is a local packaging concern; it should not become wire content. If a future content pipeline needs assets, they remain mod-local and are referenced by the content definition, not synced. A later local-only resource helper may be useful, but it is not required for multiplayer correctness. |
| 7 | Custom item definitions | `ItemRegistry.Register(id, ItemInfo/CustomItemInfo, icon, spawnFrequency)`; `CustomItemInfo` fields (container, battery, light, tool, gun, worn sprites, liquid mask, drop pool, world spawn, custom data); `TryGetOwnerModGuid`, `HasCustomData`, `SetCustomData`, custom item MonoBehaviours. | `ModItemDefinition` + `GameAdapterItemContentProvider` + `IModItemSpawn`: typed DTO, static `ItemInfo`, runtime templates, custom component attach, a mod-facing world-item spawn surface, category loot-pool injection (`Category` + `SpawnFrequency`), `WorldSpawnPerChunk` loose-worldgen distribution, `DropSources` explicit fixed corpse/crate/trader pools, typed container/battery/light/tool/gun behavior DTOs mapped by `CustomItemBehaviorApplier`, and `ModItemVisual` worn-sprite/multi-limb/liquid-mask resource-path visuals plus `ModItemSpriteAnimation` frame-path base/worn/liquid animations. | **Landed at the content + spawn + category loot-pool/world-spawn + explicit fixed drop-source + advanced behavior/visual seam.** Frame-based animated sprite modes are landed; asset-backed visual modes are non-core and only if real demand appears. |
| 8 | Custom recipes | `RecipeRegistry.Register(Recipe)`, owner-GUID queries, invalid-recipe rejection, crafting-quality locale. | `ModRecipeDefinition` + `GameAdapterRecipeContentProvider`: typed DTO, recipe table injection, category mapping, duplicate/rebuild protection. | **Landed at the content seam.** Recipes are static content injected through the same binder; crafting runtime flow remains CUO's existing item/crafting kernel. |
| 9 | Custom liquids | `LiquidRegistry.Register(id, CustomLiquidInfo)`, container liquid stacks, owner-GUID query. | `ModLiquidDefinition` + `GameAdapterLiquidContentProvider`: typed DTO, static `LiquidType` fields, locale entries. | **Landed at the static content seam.** Runtime fluid facts remain in CUO's Fluid domain; CUCoreLib's JObject fluid snapshots are not ported. |
| 10 | Liquid tiles / world liquids | `LiquidTileRegistry.Register/Place/FloodFill/GenerateWorldTiles`, world bytes, body touch/drink/visual helpers, snapshot helpers. | `ModLiquidTileDefinition` + `GameAdapterLiquidTileContentProvider` + `IModLiquidPlacement`: typed DTO, deterministic custom world bytes, `FluidManager.WorldFluidToLiquidID` mapping, worldgen through the sealed generation stream, local body-touch/drink/colour/name rendering projection, host-authoritative runtime placement/flood fill through the existing FluidRegion stream, and existing FluidInteraction sync. | **Landed at the typed content + worldgen + local projection + host-authoritative runtime placement/flood-fill seam.** Mod-authored liquid tiles ride CUO's existing fluid domain; no CUCoreLib JObject snapshot, no new NetMsg, and no game delegates in Abstractions. Snapshot helpers and the CUCoreLib asset-backed visual modes are not ported. |
| 11 | Terrain tiles and worldgen | `TileRegistry.Register`, `SetBlock`, `TryGetTile/Definition/Index`, layer masks, ore-style generation, drops, custom data. | `ModTileDefinition` + `GameAdapterTileContentProvider` + `IModTilePlacement`: typed DTO, deterministic custom block indices, `WorldGeneration.tiles` injection, a `GetBlockInfo` prefix, single-cell placement, typed `SpawnAmount`/`SpawnLayers`/`ModTileGenerationStyle`/`ModTileDrop`, `TileWorldGenDistribution` inside the sealed generation stream, and local custom-tile drop spawning through the existing block-break report. | **Landed at the static content + single-cell placement + ore/drop/worldgen projection seam.** World-generation output stays kernel-driven; no new wire message or JObject snapshot. |
| 12 | Building entities / custom spawn | `BuildingEntityRegistry.Register/Spawn/PlaceOnSurface/DistributeInWorld`, prefab hooks, components, drops, worldgen density, owner queries. | `ModBuildingDefinition` + `GameAdapterBuildingContentProvider` + `IModEntitySpawn`: typed DTO, vanilla `TemplateId` base prefab, optional `BuildingEntity` overrides and `SpawnComponents`, runtime template creation, authored drop rules + item categories, worldgen density/layers/placement, the existing `EntitySpawned` channel for replication, and `IModBuildingRuntime` prefab/instance hooks that return component type names. | **Landed at the content + entity spawn + authored drop/worldgen-density + abstraction-safe prefab/instance hook seam.** Custom buildings use `IModEntitySpawn`; no new NetMsg and no mod access to Unity prefab types. Raw `ConfigurePrefab`/`ConfigureInstance` callbacks that receive a live `GameObject` remain non-goal; component-returning hooks are the CUO-safe replacement. |
| 13 | Multi-block structures | `StructureRegistry.RegisterFromJson/EmbeddedJson/File`, spawn counts, `Place`, JSON payload from the structure editor. | `ModStructureDefinition` + `GameAdapterStructureContentProvider` + `IModStructurePlacement`: typed marker grid, validated GameAdapter compile, runtime placement through existing `SetBlock`/`BlockPlaced`, per-depth spawn counts consumed by deterministic worldgen distribution. | **Landed at the static content + runtime placement + automatic worldgen seam.** Distribution runs inside the isolated generation stream, static block grid only, no new wire. |
| 14 | Statuses / per-body per-limb custom state | `StatusRegistry` + `BodyStatus`/`LimbStatus` inheritance, `[StatusOptions]`, `GetStatus<T>()`, save providers, network snapshots. | `ModStatusDefinition` + `ModStatusScope` + `GameAdapterStatusContentProvider` for static descriptors; `IModStatusRuntime` + `IModStatusTransport` + `ModStatusUpdate` for per-player/per-limb runtime values and typed shared-status transport; `ModStatusProjectionKind` + `ModBodyFormulaProjection`/`ModLimbProjection` + GameAdapter `ModStatusVanillaProjection` for typed body/limb and circulation overlays. `IModState` remains host-persistent opaque mod state. | **Landed at static descriptor + runtime table + typed status transport + typed GameAdapter body/limb + circulation projection seam.** Guest mirror set/remove rides the existing `IModNetwork` mod-message channel; no new NetMsg and no JObject snapshot. Arbitrary reflection-free status bags are not ported; the vanilla moodle-row local seam is landed. |
| 15 | Moodles / player status UI | `MoodleRegistry.AddMoodle/AddAnimatedMoodle`, `RegisterBody/RegisterLimb`; custom status icons in the vanilla moodle row. | `ModMoodleDefinition` + `ModMoodleAnimation` + `GameAdapterMoodleContentProvider`: typed static moodle descriptors with icon key, intensity, presentation flags, hold seconds, optional frame animation and per-limb display templates; `ModStatusDefinition.ShowPerLimbMoodles` + `LimbMoodles` route limb-scoped statuses to per-limb rows and distinct moodle descriptors; `ModStatusMoodleProjection` feeds active status-linked moodles into `MoodleManager.AddMoodle`, and the `Moodle.Start` patch drives animated moodle icons; `IModMoodleRuntime` lets a mod route active status presences from its own opaque payload to a static moodle id. `IModUi` remains the local immediate-mode surface. | **Landed at the static moodle-descriptor + local vanilla moodle-row + animated/per-limb moodle row + abstraction-safe runtime moodle resolver seam.** Active status-linked descriptors are fed to `MoodleManager.AddMoodle`; it is not a wire feature and does not expose Unity types through Abstractions. |
| 16 | Native settings menu / mod options | `ModOptionsRegistry.Register`, `ModOptionDefinition`, category/locale handling, optional BepInEx config mirroring. | No native settings API. `IModUi` is a local window surface; `IModState` can persist mod settings. | **Out of scope for CUO core.** Settings are local UX; a future `IModSettings` or settings-menu seam is acceptable only if demand appears. It is not multiplayer/wire work. |
| 17 | Localization | `LocaleRegistry`, `LocaleLoader`, generated locale files, locale categories, crafting-quality labels. | No locale API in Abstractions. | **Out of scope / mod-local.** Localization is a normal mod packaging concern. CUO should not own locale unless a future content pipeline needs display-name resolution; keep it out of the wire. |
| 18 | Save providers | `SaveRegistry.RegisterGlobal/Item/Body/Limb/WorldProvider`, `ICustomSaveProvider`, `IItemSaveProvider`, `IBodySaveProvider`, `ILimbSaveProvider`, `IWorldSaveProvider`, hooking into vanilla `save.sv`. | `IModState` is host-persistent, per-mod, versioned, opaque key/value bytes. Writes are host-only and require `WriteGameState`. | **Already superseded — no port.** Migration: move CUCoreLib save-provider payloads into `IModState` under a mod-owned schema/version. If per-item/body/world granularity is genuinely needed, that belongs in a future typed kernel domain, not a vanilla-save provider registry. |
| 19 | Console commands | `ConsoleCommandRegistry.Register(name, desc, Command.Action, autofill, argDescriptions)`, built-in commands, bug-report command. | `IModConsoleCommands.Register(ModConsoleCommand)` with `CommandPermission`, `CommandArgumentKind`, usage/description, local-only execution, unregister. | **Already native.** Migration: port registration to `IModConsoleCommands`; autofill maps to `CommandArgumentKind` / resource/selector suggestions. No new code unless the command console needs additional suggestion kinds. |
| 20 | Hot reload / debug / bug reporting / update checker | `ContentReloadManager`, `DebugWatchService`, `BugReportCollector/Service`, `UpdateChecker`, launch-override helpers. | No equivalent; `docs/backlog/README.md` already tracks Phase 5 tooling/ecosystem. | **Out of scope for this ticket; future Phase 5 tooling.** These are developer-experience tools, not mod API. Do not port until the stable mod surface has real ecosystem demand. |
| 21 | Player/utility helpers | `CUCoreUtils` (readiness, coroutines, PlayerPrefs, give item, worn sprite, alerts, talk, console bridging, keybinds), `CCLBody` (blood pressure, heart rate, encumbrance, jump-speed contributions), `CustomInstantiate`, `BodyAnimationPlayer`, minigame helpers. | `IModNativeApi` exposes only `local.player.state` (read-only local body); `IModGameState` exposes read-only projected player state. No mutating/game-private helpers. | **Out of scope / future curated native API increments.** Direct writes to vanilla gameplay state from mods violate CUO's local-compute/authority rules. Provide read-only projections now; any mutation needs an authority design and should appear as a command/event or a GameAdapter operation. |
| 22 | KrokMP custom player data | `MultiplayerApi.GetCustomPlayerData`, `GetCustomPlayerLimbData`, `RequestCustomPlayerData/LimbData`, reflection into KrokMP `NetPlayer`. | `IModGameState.TryGetPlayer` gives a read-only projected player/vitals/inventory for all members; no KrokMP client-id concept. | **Do not port.** Migration: use `IModGameState` for read-only player projections and `IModNetwork`/`IModCommands` for any additional per-player data. KrokMP client IDs are not part of CUO's SteamId-based session model. |
| 23 | KrokMP compatibility adapter | `MultiplayerBridge.TryConfigureLocalIdentity`, quick-test host/client, `MultiplayerApi.IsAvailable/IsRunning/IsHost/IsClient/IsServer`. | CUO has Steam and IP-direct session modes; no KrokMP dependency. | **Out of scope.** `docs/backlog/future/krokmp-compatibility-adapter.md` reserves this only if real binary compatibility becomes necessary; it is not needed for CUO-native mods. |
| 24 | Ownership/discovery of registered content | `TryGetOwnerModGuid` on item/liquid/building/tile/recipe registries. | `IModContentControl` enumerates every mod's opaque definitions with mod id; `IModContentOwnerQuery` exposes the mod-facing read-only kind+id → owner lookup and follows the catalog's ambiguity policy. | **Landed at the content catalog/owner-query seam.** No new wire, no Runtime internals, and no payload interpretation; the query is the generic migration replacement for the per-kind `TryGetOwnerModGuid` methods. |

## What landed (CUO-side migration support)

The matrix's coherent feature family — **static game-content binding** — is
implemented across the seams this ticket scoped:

- `IModContent` remains the mod-facing registration surface; the Runtime
  content-binder service routes registered content to kind-specific
  GameAdapter providers and does not send content bytes over the wire.
- Plain typed DTOs were added to Abstractions for item, recipe, liquid,
  liquid tile, tile, building, structure, status, and moodle content; no
  game/Unity type appears in Abstractions.
- `IModEntitySpawn`, tile/structure/liquid placement, building/structure
  worldgen distribution, item worldgen/loot/fixed drops, advanced item
  behavior/visuals, status/moodle runtime projections, and the
  abstraction-safe building runtime hooks are all covered by narrow typed
  seams.
- Custom item runtime state maps into CUO's typed item capability pipeline;
  no generic JObject/JToken bag was added.
- The detailed inventory lives in the "Implemented minimal migration base"
  section above; the selfchecks are listed in the evidence block near the
  end of that section.

## Migration guide for CUCoreLib mod authors

This is source-level migration guidance. CUO cannot provide a binary
CUCoreLib compatibility layer because CUCoreLib's public API exposes
game-private and Unity types; CUO's mod boundary deliberately forbids mods
from referencing those assemblies.

1. **Change the project identity.** Keep the thin BepInEx shell (`BaseUnityPlugin`
   with an empty Awake) and add a separate `[CuoMod]`-annotated `ICuoMod` class.
   Move all logic out of the shell into that class.
2. **Replace metadata/dependencies.** CUCoreLib GUID references and manual
   dependency checks become `[CuoMod(...)]` id/version/network mode/permissions/
   dependencies. Choose `NetworkMode` carefully. State-bearing modes require all
   peers to run matching versions; `ClientOnly`/`Cosmetic` cannot use
   content/state permissions.
3. **Replace static registries with `IModContent` when the content is not yet
   natively supported by CUO.** This keeps the mod compiling and lets CUO's
   future content binder discover it. Use `kind` values and schema versions that
   match the planned binder; do not expect the game to see the content until the
   binder exists.
4. **Replace console commands with `IModConsoleCommands`.**
   `ConsoleCommandRegistry.Register(...)` maps to
   `new ModConsoleCommand(name, description, usage, permission, argumentKinds, handler)`.
   Autofill/argument hints map to `CommandArgumentKind`.
5. **Replace host/remote calls with `IModCommands` or `IModNetwork`.**
   - KrokMP `RequestServer` → `context.Commands.TryExecute` when the host has to
     decide and return a result.
   - KrokMP `SendToServer` → `context.Network.SendToHost`.
   - KrokMP `Broadcast` → `context.Network.Broadcast`.
   - KrokMP server→client directed messages → `context.Network.SendToPeer`
     (host side) plus `MessageReceived` on the target.
6. **Replace save-provider code with `IModState`.** Use one namespace-style key
   prefix, a mod-owned schema version, and versioned byte payloads. Guest copies
   should not write state; they must request changes through commands/messages if
   host persistence is needed.
7. **Keep asset loading and localization in the mod/plugin layer.** Unless CUO
   releases a resource API, these are not part of the CUO mod boundary. Do not
   put asset bytes into `IModNetwork` or `IModState`.
8. **Do not call game/Unity helper APIs from business code.** If a CUCoreLib mod
   used `CCLBody`, `CustomInstantiate`, `GiveItem`, body mutation, animations, or
   minigame helpers, each write path must be redesigned as a CUO command/event or
   a future GameAdapter native operation. The current allowed read-only
   projections are `IModGameState` and `IModNativeApi.TryGetLocalPlayerState`.
9. **Do not use KrokMP-specific JToken channels or snapshot modules.** They do not
   exist in CUO and will not be added. Re-express durable mod state through
   `IModState` and real-time coordination through `IModNetwork`/`IModCommands`;
   synced gameplay facts belong in CUO kernel domains.
10. **Keep all content/state schemas versioned and documented by the mod.** CUO
    stores opaque bytes but does not migrate mod schemas; the mod owns
    compatibility, exactly like CUCoreLib's versioned save providers.

## Explicit non-goals from this evaluation

- No CUCoreLib/KrokMP source or reverse-engineered material will be committed.
- No arbitrary JToken channel protocol or generic JObject snapshot protocol will
  be added to CUO.
- No Abstractions type will expose a game assembly type or Unity type.
- No direct mod access to vanilla registries/Harmony patches; that remains
  GameAdapter territory.
- No new wire protocol for static content; static content is part of the mod and
  is covered by the existing handshake consistency boundary.
