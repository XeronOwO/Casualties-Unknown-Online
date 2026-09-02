# CUCoreLib migration support

- Status: Todo
- Priority: Medium
- Category: Mod ecosystem / migration
- Source: External project — <https://github.com/jimmyking9999999/CUCoreLib> (based on KrokMP)

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

The largest genuine gap is **actual semantic content support**. CUO's
`IModContent` stores opaque bytes and currently has no consumer that turns
those bytes into game content (items, recipes, tiles, buildings, liquids).
The KrokMP channel/snapshot layer, by contrast, should not be ported: CUO's
typed kernel, discrete events, state streams, and `IModNetwork`/`IModCommands`
already cover the same needs with explicit authority semantics.

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

## Implemented minimal migration base (2026-09-01)

This round intentionally does **not** port full CUCoreLib content
functionality. It lands the foundation that CUCoreLib-style mods and future
content binders can build on:

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
- `ModTileDefinition` + `GameAdapterTileContentProvider` — the fifth typed
  content kind. The plain DTO carries a sprite resource path or a vanilla tile
  index as the visual source, static BlockInfo behavior fields, RGBA tint, and
  collider type. The Game Adapter allocates a deterministic custom block index
  (never a vanilla index), injects a built `Tile` into every fresh
  `WorldGeneration.tiles` palette, and answers `WorldGeneration.GetBlockInfo`
  for custom indices through a narrow Harmony prefix. No random world
  generation and no wire change are part of this seam.
- `IModItemSpawn` + `IModItemSpawner` + the Game Adapter implementation — the
  mod-facing world-item spawn seam. It uses the same `SpawnEntity` permission
  and policy rails as entity spawn; the Game Adapter creates the local `Item`
  via `Utils.Create`, and the existing `ItemSpawned` item-domain path
  replicates it. No new wire message and no game/Unity type crosses the mod
  boundary.
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
  stores the descriptor. Feeding the vanilla moodle manager remains a future
  local UI/GameAdapter seam, not a static content wire feature.
- `ModDataScope` + `IModData` + `ModDataStore`/`ModDataPolicy` — the runtime
  mod-data scope seam. It gives mods an ephemeral, per-process slot store with
  explicit `LocalOnly` / `Shared` / `HostAuthoritative` declarations. Shared
  mirrors are applied explicitly from a host-originated value received over
  `IModNetwork`; the framework does not auto-replicate and does not add a
  generic snapshot protocol. `IModState` remains the host-persistent special
  case.
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
  `docs/evidence/selfchecks/mod-api/mod-structure-content-binding-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-structure-placement-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-structure-worldgen-distribution-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-status-moodle-content-binding-selfcheck.md`,
  `docs/evidence/selfchecks/mod-api/mod-runtime-data-selfcheck.md`,
  and `docs/evidence/selfchecks/mod-api/mod-item-spawn-selfcheck.md`.

Still explicitly **not** implemented:
- dynamic per-player / per-limb status runtime values and the
  host-authoritative mod-status domain boundary (static status descriptors are
  landed; the runtime instance model still needs the mod-data sync design);
- actual vanilla-moodle presentation binding (static moodle descriptors are
  landed; feeding the vanilla moodle row remains a future UI/GameAdapter seam);
- an **automatic runtime mod-data sync engine** (the scope seam is landed:
  `IModData` declares/keeps local-only, shared-mirror, and host-authoritative
  values; actual transport remains explicit through `IModNetwork` /
  `IModCommands`, and the dedicated design ticket has moved to review).

Those remain the future content-binding path described below.

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
| 7 | Custom item definitions | `ItemRegistry.Register(id, ItemInfo/CustomItemInfo, icon, spawnFrequency)`; `CustomItemInfo` fields (container, battery, light, tool, gun, worn sprites, liquid mask, drop pool, world spawn, custom data); `TryGetOwnerModGuid`, `HasCustomData`, `SetCustomData`, custom item MonoBehaviours. | `ModItemDefinition` + `GameAdapterItemContentProvider` + `IModItemSpawn`: typed DTO, static `ItemInfo`, runtime templates, custom component attach, and a mod-facing world-item spawn surface. | **Landed at the content + spawn seam.** Remaining: full drop-pool/worldgen integration if mod demand appears. |
| 8 | Custom recipes | `RecipeRegistry.Register(Recipe)`, owner-GUID queries, invalid-recipe rejection, crafting-quality locale. | `ModRecipeDefinition` + `GameAdapterRecipeContentProvider`: typed DTO, recipe table injection, category mapping, duplicate/rebuild protection. | **Landed at the content seam.** Recipes are static content injected through the same binder; crafting runtime flow remains CUO's existing item/crafting kernel. |
| 9 | Custom liquids | `LiquidRegistry.Register(id, CustomLiquidInfo)`, container liquid stacks, owner-GUID query. | `ModLiquidDefinition` + `GameAdapterLiquidContentProvider`: typed DTO, static `LiquidType` fields, locale entries. | **Landed at the static content seam.** Runtime fluid facts remain in CUO's Fluid domain; CUCoreLib's JObject fluid snapshots are not ported. |
| 10 | Liquid tiles / world liquids | `LiquidTileRegistry.Register/Place/FloodFill/GenerateWorldTiles`, world bytes, body touch/drink/visual helpers, snapshot helpers. | No custom liquid-tile API; CUO has a Fluid kernel domain and Tilemap/WorldEntity adapter coverage for vanilla content. | **Content seam + worldgen.** Static liquid-tile definitions are content; placement/runtime effects need a GameAdapter translator. This is larger than a simple API shim and should be scheduled after item/recipe content binding. |
| 11 | Terrain tiles and worldgen | `TileRegistry.Register`, `SetBlock`, `TryGetTile/Definition/Index`, layer masks, ore-style generation, drops, custom data. | `ModTileDefinition` + `GameAdapterTileContentProvider` + `IModTilePlacement`: typed DTO, deterministic custom block indices, `WorldGeneration.tiles` injection, a `GetBlockInfo` prefix, and a single-cell placement surface by stable tile id. | **Landed at the static content + single-cell placement seam.** Remaining: optional ore/drop projection, worldgen distribution, and layer masks; all future work; world-generation output stays kernel-driven. |
| 12 | Building entities / custom spawn | `BuildingEntityRegistry.Register/Spawn/PlaceOnSurface/DistributeInWorld`, prefab hooks, components, drops, worldgen density, owner queries. | `ModBuildingDefinition` + `GameAdapterBuildingContentProvider` + `IModEntitySpawn`: typed DTO, vanilla `TemplateId` base prefab, optional `BuildingEntity` overrides and `SpawnComponents`, runtime template creation, and the existing `EntitySpawned` channel for replication. | **Landed at the content + entity spawn seam.** Custom buildings use `IModEntitySpawn`; no new NetMsg and no mod access to Unity prefab types. Remaining: prefab hooks/drop/worldgen-density options if real demand appears. |
| 13 | Multi-block structures | `StructureRegistry.RegisterFromJson/EmbeddedJson/File`, spawn counts, `Place`, JSON payload from the structure editor. | `ModStructureDefinition` + `GameAdapterStructureContentProvider` + `IModStructurePlacement`: typed marker grid, validated GameAdapter compile, runtime placement through existing `SetBlock`/`BlockPlaced`, per-depth spawn counts consumed by deterministic worldgen distribution. | **Landed at the static content + runtime placement + automatic worldgen seam.** Distribution runs inside the isolated generation stream, static block grid only, no new wire. |
| 14 | Statuses / per-body per-limb custom state | `StatusRegistry` + `BodyStatus`/`LimbStatus` inheritance, `[StatusOptions]`, `GetStatus<T>()`, save providers, network snapshots. | `ModStatusDefinition` + `ModStatusScope` + `GameAdapterStatusContentProvider`: typed static status descriptors, body/limb scope, save metadata, optional moodle link. `IModState` remains host-persistent opaque mod state; no per-player runtime status bag is exposed yet. | **Landed at the static status-descriptor seam.** Dynamic per-player/per-limb runtime values still require the host-authoritative mod-status domain design and belong with the mod-data sync model; arbitrary reflection-free status bags are not ported. |
| 15 | Moodles / player status UI | `MoodleRegistry.AddMoodle/AddAnimatedMoodle`, `RegisterBody/RegisterLimb`; custom status icons in the vanilla moodle row. | `ModMoodleDefinition` + `GameAdapterMoodleContentProvider`: typed static moodle descriptors with icon key, intensity, presentation flags and hold seconds. `IModUi` remains the local immediate-mode surface. | **Landed at the static moodle-descriptor seam.** Feeding the vanilla moodle row is a future local UI/GameAdapter seam; it is not a wire feature and does not expose Unity types through Abstractions. |
| 16 | Native settings menu / mod options | `ModOptionsRegistry.Register`, `ModOptionDefinition`, category/locale handling, optional BepInEx config mirroring. | No native settings API. `IModUi` is a local window surface; `IModState` can persist mod settings. | **Out of scope for CUO core.** Settings are local UX; a future `IModSettings` or settings-menu seam is acceptable only if demand appears. It is not multiplayer/wire work. |
| 17 | Localization | `LocaleRegistry`, `LocaleLoader`, generated locale files, locale categories, crafting-quality labels. | No locale API in Abstractions. | **Out of scope / mod-local.** Localization is a normal mod packaging concern. CUO should not own locale unless a future content pipeline needs display-name resolution; keep it out of the wire. |
| 18 | Save providers | `SaveRegistry.RegisterGlobal/Item/Body/Limb/WorldProvider`, `ICustomSaveProvider`, `IItemSaveProvider`, `IBodySaveProvider`, `ILimbSaveProvider`, `IWorldSaveProvider`, hooking into vanilla `save.sv`. | `IModState` is host-persistent, per-mod, versioned, opaque key/value bytes. Writes are host-only and require `WriteGameState`. | **Already superseded — no port.** Migration: move CUCoreLib save-provider payloads into `IModState` under a mod-owned schema/version. If per-item/body/world granularity is genuinely needed, that belongs in a future typed kernel domain, not a vanilla-save provider registry. |
| 19 | Console commands | `ConsoleCommandRegistry.Register(name, desc, Command.Action, autofill, argDescriptions)`, built-in commands, bug-report command. | `IModConsoleCommands.Register(ModConsoleCommand)` with `CommandPermission`, `CommandArgumentKind`, usage/description, local-only execution, unregister. | **Already native.** Migration: port registration to `IModConsoleCommands`; autofill maps to `CommandArgumentKind` / resource/selector suggestions. No new code unless the command console needs additional suggestion kinds. |
| 20 | Hot reload / debug / bug reporting / update checker | `ContentReloadManager`, `DebugWatchService`, `BugReportCollector/Service`, `UpdateChecker`, launch-override helpers. | No equivalent; `docs/backlog/README.md` already tracks Phase 5 tooling/ecosystem. | **Out of scope for this ticket; future Phase 5 tooling.** These are developer-experience tools, not mod API. Do not port until the stable mod surface has real ecosystem demand. |
| 21 | Player/utility helpers | `CUCoreUtils` (readiness, coroutines, PlayerPrefs, give item, worn sprite, alerts, talk, console bridging, keybinds), `CCLBody` (blood pressure, heart rate, encumbrance, jump-speed contributions), `CustomInstantiate`, `BodyAnimationPlayer`, minigame helpers. | `IModNativeApi` exposes only `local.player.state` (read-only local body); `IModGameState` exposes read-only projected player state. No mutating/game-private helpers. | **Out of scope / future curated native API increments.** Direct writes to vanilla gameplay state from mods violate CUO's local-compute/authority rules. Provide read-only projections now; any mutation needs an authority design and should appear as a command/event or a GameAdapter operation. |
| 22 | KrokMP custom player data | `MultiplayerApi.GetCustomPlayerData`, `GetCustomPlayerLimbData`, `RequestCustomPlayerData/LimbData`, reflection into KrokMP `NetPlayer`. | `IModGameState.TryGetPlayer` gives a read-only projected player/vitals/inventory for all members; no KrokMP client-id concept. | **Do not port.** Migration: use `IModGameState` for read-only player projections and `IModNetwork`/`IModCommands` for any additional per-player data. KrokMP client IDs are not part of CUO's SteamId-based session model. |
| 23 | KrokMP compatibility adapter | `MultiplayerBridge.TryConfigureLocalIdentity`, quick-test host/client, `MultiplayerApi.IsAvailable/IsRunning/IsHost/IsClient/IsServer`. | CUO has Steam and IP-direct session modes; no KrokMP dependency. | **Out of scope.** `docs/backlog/future/krokmp-compatibility-adapter.md` reserves this only if real binary compatibility becomes necessary; it is not needed for CUO-native mods. |
| 24 | Ownership/discovery of registered content | `TryGetOwnerModGuid` on item/liquid/building/tile/recipe registries. | `IModContentControl` can enumerate every mod's opaque definitions with mod id; there is no per-vanilla-content owner table. | **Follow from the content binder.** The content binder should retain owner-mod metadata and expose it to Runtime layers; mods do not need direct access unless a future read-only query is requested. |

## What to implement first (if the ticket becomes code)

The matrix points to one coherent feature family as the real migration path:
**static game-content binding**.

1. Keep `IModContent` as the mod-facing registration surface (opaque id/kind/
   payload); do not replace it or expose Runtime internals.
2. Add a Runtime content-binder service that collects `IModContentControl`
   entries after mod discovery, before a lobby/world needs them. It should:
   - validate per-kind schemas, duplicate IDs, cross-mod ID collisions, and
     owner attribution;
   - call narrow GameAdapter content-registration interfaces
     (item, recipe, liquid, tile, building, structure);
   - not send content bytes over the wire;
   - rely on the existing Mod API handshake/mode/permission rules for
     consistency.
3. If real mod authors need a typed surface instead of opaque bytes, add plain
   DTOs to Abstractions for the supported content kinds. No game/Unity type may
   appear in Abstractions.
4. Extend `IModEntitySpawn` only after custom building definitions can be turned
   into real prefabs by the GameAdapter; keep using the existing
   `EntitySpawned` runtime channel so no new wire message is created.
5. For any custom item state that must persist/sync (CUCoreLib-style custom
   data), map it into CUO's existing typed `ItemComponentState` / item
   capability pipeline rather than inventing a new generic bag.

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
