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
| 5 | Generic snapshot sync | `MultiplayerApi.RegisterSyncModule(key, capture, apply)` + `BroadcastSnapshot`/`ApplySnapshot`; JObject full-snapshot modules for arbitrary mod state. | No generic snapshot API. CUO uses a typed deterministic kernel: discrete committed batches, high-frequency state streams, per-domain state. | **Out of scope / anti-architecture.** Do not add a generic JObject snapshot registry. Mod durable state belongs in `IModState` (host-persistent) or through `IModNetwork`/`IModCommands`; synced gameplay facts belong in kernel domains. |
| 6 | Asset / resource loading | `AssetLoader`, `FileLoader`: embedded/loose sprites, audio, text, AssetBundles, bundle registration/cache, sprite animations, sprite-sheet helpers. | No asset API in Abstractions. `IModNativeApi` is the only safe game seam and is currently read-only local player state. | **Out of scope for CUO core.** Asset loading is a local packaging concern; it should not become wire content. If a future content pipeline needs assets, they remain mod-local and are referenced by the content definition, not synced. A later local-only resource helper may be useful, but it is not required for multiplayer correctness. |
| 7 | Custom item definitions | `ItemRegistry.Register(id, ItemInfo/CustomItemInfo, icon, spawnFrequency)`; `CustomItemInfo` fields (container, battery, light, tool, gun, worn sprites, liquid mask, drop pool, world spawn, custom data); `TryGetOwnerModGuid`, `HasCustomData`, `SetCustomData`, custom item MonoBehaviours. | `IModContent` stores opaque per-mod definitions (`TryRegister(id, kind, data)`); `IModEntitySpawn` only spawns existing `BuildingEntity` prefabs; `IModGameState` reads player inventory/vitals only. | **High-value gap — implement as a CUO content seam.** Keep `IModContent` as the registration surface; add a Runtime/GameAdapter content binder that consumes `IModContentControl` entries, validates IDs/ownership, and calls vanilla item registration before world generation. Typed DTOs for well-known kinds may live in Abstractions; game/Unity types stay in GameAdapter. No new wire protocol: static content is part of the mod and the existing mod handshake is the consistency boundary. |
| 8 | Custom recipes | `RecipeRegistry.Register(Recipe)`, owner-GUID queries, invalid-recipe rejection, crafting-quality locale. | Same opaque `IModContent`; no recipe consumer in Runtime/GameAdapter. | **High-value gap — same content seam as items.** Recipes are static content; map into the content binder and reuse the existing crafting/kernel item flow once item IDs are known. |
| 9 | Custom liquids | `LiquidRegistry.Register(id, CustomLiquidInfo)`, container liquid stacks, owner-GUID query. | No custom liquid definition API. CUO already has a `Fluid` kernel domain for coarse fluid state, but it is built for vanilla fluids/regions. | **Content seam + existing Fluid domain.** Static liquid definitions belong in the content binder; runtime fluid facts remain in the Fluid domain. Do not port CUCoreLib's JObject fluid snapshots. |
| 10 | Liquid tiles / world liquids | `LiquidTileRegistry.Register/Place/FloodFill/GenerateWorldTiles`, world bytes, body touch/drink/visual helpers, snapshot helpers. | No custom liquid-tile API; CUO has a Fluid kernel domain and Tilemap/WorldEntity adapter coverage for vanilla content. | **Content seam + worldgen.** Static liquid-tile definitions are content; placement/runtime effects need a GameAdapter translator. This is larger than a simple API shim and should be scheduled after item/recipe content binding. |
| 11 | Terrain tiles and worldgen | `TileRegistry.Register`, `SetBlock`, `TryGetTile/Definition/Index`, layer masks, ore-style generation, drops, custom data. | No custom tile definition API; CUO syncs vanilla block/world facts through the World/Run and WorldEntity kernel domains. | **High-value gap — content seam + worldgen projection.** Static tile definitions must be identical on all peers and are registered by the content binder; world-generation output should remain kernel-driven. |
| 12 | Building entities / custom spawn | `BuildingEntityRegistry.Register/Spawn/PlaceOnSurface/DistributeInWorld`, prefab hooks, components, drops, worldgen density, owner queries. | `IModEntitySpawn.TrySpawn(prefabId, x, y, rotation)` supports existing game `BuildingEntity` prefabs only; no way to register a new prefab definition from a mod. | **High-value gap — extend the entity-spawn/content seam.** Add custom building definitions to the content binder and route `IModEntitySpawn` through the existing `EntitySpawned` runtime channel. No new NetMsg; no mod access to Unity prefab types. |
| 13 | Multi-block structures | `StructureRegistry.RegisterFromJson/EmbeddedJson/File`, spawn counts, `Place`, JSON payload from the structure editor. | No structure-registration API; no static structure content consumer. | **Content seam.** Structure definitions are static authored worldgen content; a binder can consume the same JSON and hand it to GameAdapter worldgen. Not a wire feature. |
| 14 | Statuses / per-body per-limb custom state | `StatusRegistry` + `BodyStatus`/`LimbStatus` inheritance, `[StatusOptions]`, `GetStatus<T>()`, save providers, network snapshots. | No per-player per-limb status extension exposed to mods. `IModState` gives host-persistent opaque mod state; `IModGameState` gives read-only projected vitals/inventory. | **Out of scope until a real per-player status domain is designed.** CUO already owns terminal player/limb facts in the `Players` kernel domain; arbitrary reflection-free status bags may be useful but need authority and sync boundaries. For now, mods should persist opaque data in `IModState` and coordinate through `IModNetwork`/`IModCommands`. |
| 15 | Moodles / player status UI | `MoodleRegistry.AddMoodle/AddAnimatedMoodle`, `RegisterBody/RegisterLimb`; custom status icons in the vanilla moodle row. | `IModUi` offers local immediate-mode windows only; no vanilla moodle/status indicator integration. | **Out of scope / future UI seam.** Moodles are presentation state; if a concrete CUO mod needs it, the safe path is a local UI projection and a typed status source, not direct vanilla moodle mutation from Abstractions. |
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
