# CUO Mod API — Phase 4 (Core Skeleton + Host Commands & Permissions)

Status: first round **landed 2026-08-13**; second round (4b) **landed
2026-08-16**. This document is the binding contract for mod authors AND for
the framework's future rounds — the semantics below are locked by tests
(`tests/.../Mods/`, 761 total green at landing) and the two-process runtime
verification (host + sandbox guest; the pre-release ProtocolVersion sequence was
later reset to `ProtocolVersion.Current = 1` — see tech-decisions #137).

## 1. Scope

**First round**: discovery, lifecycle, manifest, mod network messages,
session events, handshake consistency.

**Second round (4b)**: the full permission model (declaration + enforcement
for the live surfaces), host-authoritative commands, dependency ordering,
SemVer versions, and per-sender rate limits.

**UI is landed (see §4e), content registration is landed (see §4f),
ReadGameState is landed (see §4g), entity and item spawn are landed (see §4h),
AccessNativeApi is landed (see §4i), the runtime mod-data scope seam is
landed (see §4j), and the runtime status table + typed status transport +
GameAdapter projection slices and the vanilla moodle-row seam are landed (see §4k).**
The mod surface lives in **`CUO.Abstractions`** — the ONLY
assembly mods may reference (architecture.md §5.5). A mod never touches
BepInEx, Steamworks, the game assemblies, or CUO.Runtime. **Mod-state saves
are landed (see §4d), the local mod UI surface is landed (see §4e), and
content registration is landed (see §4f).**

## 2. How a mod is loaded (read this — the timing is deliberate)

BepInEx 5 loads plugins **one by one, load-then-Awake, in a single loop** —
verified by IL (`Chainloader.Start`: `Assembly.LoadFile → GetType →
instantiate → Awake → next`) and by the game's own log (the CUO plugin's Awake
lines appear BEFORE `Loading [HotRepl]`). Two hard rules follow:

1. **Discovery runs on the framework's FIRST UPDATE FRAME**, not in its Awake
   — a scan in Awake would miss every plugin loaded after it. The
   `ModService` scans `AppDomain.GetAssemblies()` once, finds every
   `[CuoMod]`-declared `ICuoMod` type, validates it (see §3), orders the
   accepted set topologically by dependencies, and binds it.
   The discovery frame runs Bind → Initialize → Start → Update in that same
   frame; Update per frame from then on. Stop/Dispose run in reverse load order.
2. **A mod's BepInEx shell Awake must stay EMPTY** — it may run before or
   after the CUO plugin's Awake and must not touch any CUO API.

**The double-instance trap**: BepInEx instantiates the shell (the
`BaseUnityPlugin`), CUO instantiates the `[CuoMod]` class — a single type
playing both roles yields TWO instances with independent state. The shell and
the mod class are always separate types (`src/CasualtiesUnknownOnline.ModExample/`
is the canonical layout).

**The BepInEx-reference reconciliation**: §1 says "never reference BepInEx",
the shell is a BepInEx plugin. The reconciliation: BepInEx.Core is referenced
ONLY as the loading mechanism (the shell derives `BaseUnityPlugin` and nothing
else); every line of business logic references CUO.Abstractions only.

## 3. The mod surface

```csharp
[CuoMod("com.example.mymod", "My Mod", "1.0.0", NetworkMode = NetworkMode.Synchronized,
        Permissions = ModPermission.SendNetworkMessage | ModPermission.RegisterCommand,
        Dependencies = new[] { "com.example.dependency" }, Description = "...")]
public sealed class MyMod : ICuoMod   // ICuoService lifecycle + Bind
{
    public void Bind(IModContext context) { ... }   // once, before Initialize
    // ICuoService: Initialize / Start / Update / Stop / Dispose
}
```

- **`[CuoMod]`** is the single manifest source (id / displayName / version /
  `NetworkMode` / `Permissions` / `Dependencies` / description). `NetworkMode`
  defaults to `Unspecified` and is **rejected at discovery**. Other rejection
  causes: duplicated id, abstract/non-public type, missing public parameterless
  constructor, non-SemVer version, unknown permission bits or host/state
  permissions on `ClientOnly`/`Cosmetic`, and malformed/unsatisfiable
  dependencies (missing target, self, duplicate, cycle). One rejected mod never
  blocks the scan (per-mod fail-closed).
- **Permissions** are never implicit: the default is `ModPermission.None`.
  The eight declared flags are `ReadGameState, WriteGameState, SpawnEntity,
  SendNetworkMessage, RegisterContent, RegisterCommand, ExecuteHostAction,
  AccessNativeApi`. Live enforcement today: `SendNetworkMessage` gates
  `IModNetwork` send AND receive; `RegisterCommand` gates command
  registration; `ExecuteHostAction` additionally gates `ModCommand.IsHostAction`;
  `WriteGameState` gates host-persistent mod-state writes (`IModState`);
  `RegisterContent` gates mod content registration (`IModContent`);
  `ReadGameState` gates the read-only game-state projection (`IModGameState`);
  `SpawnEntity` gates the world entity-spawn surface (`IModEntitySpawn`),
  the world item-spawn surface (`IModItemSpawn`) and the world tile/block,
  structure and liquid placement surfaces (`IModTilePlacement`,
  `IModStructurePlacement`, `IModLiquidPlacement`);
  `AccessNativeApi` gates the curated native/game-private operation registry
  (`IModNativeApi`).
- **`Dependencies`** are mod ids loaded before the dependent; missing or
  cyclic dependencies reject the dependent (transitive failures propagate).
- **`ICuoMod : ICuoService`** — the standard lifecycle, driven by the
  framework's pump on the Unity main thread. Every stage is exception-isolated.
- **`IModContext`** — `Logger`, `Network`, `Commands`, `Session`, `State`,
  `Ui`, `Content` and events:

| Member | Semantics |
|---|---|
| `Session` | a **SNAPSHOT at bind time**, not a live view — the host never fires `SessionActivated` (it activated at lobby creation), and events fired before discovery are lost. The snapshot is the only reliable "current state". `MemberSteamIds` is the peer member set (the local peer is `LocalSteamId`). |
| `Commands` | host-authoritative commands — see §4b. |
| `State` | host-persistent per-mod state — see §4d. |
| `Data` | runtime scope-declared per-mod data — see §4j. |
| `StatusRuntime` | runtime per-player/per-limb mod status values — see §4k. |
| `Ui` | local immediate-mode mod UI windows — see §4e. |
| `Content` | mod content registration — see §4f. |
| `GameState` | read-only player-state projection — see §4g. |
| `EntitySpawn` | world entity spawn — see §4h. |
| `ItemSpawn` | world item spawn — see §4h. |
| `TilePlacement` | world tile/block placement — see §4h. |
| `StructurePlacement` | world multi-block structure placement — see §4h. |
| `LiquidPlacement` | world liquid-tile placement/flood fill — see §4h. |
| `NativeApi` | curated native/game-private operation registry — see §4i. |
| `SessionActivated` | the first member handshake completed. **Host side: never** — read the snapshot. |
| `PlayerJoined` / `PlayerLeft` | a member's handshake completed / a member was removed (host side). NOT the in-world entity join. Each member exactly once, including yourself. |
| `SessionEnded` | the session tore down. A guest's `PlayerLeft` for the host is NOT fired on host exit — only `SessionEnded`. |

## 4. Mod messages

`IModNetwork` — report/定向 semantics, star topology, **NO auto-relay**:

| Call | Host | Guest | Notes |
|---|---|---|---|
| `SendToHost(payload)` | no-op | reports to the host's copy of the mod | outside a session: no-op |
| `SendToPeer(steamId, payload)` | sends to one member's copy | no-op | |
| `Broadcast(payload)` | every member INCLUDING the host's own copy (local fire with its own SteamId) | no-op | the "all sides run this" call |
| `MessageReceived` | `(senderSteamId, payload)` — a report (guest) or the host's own broadcast | a directed/broadcast frame | |

The mod must declare `SendNetworkMessage`; undeclared sends are refused at the
sender (no-op + log) and undeclared receives are dropped. The payload is
**opaque**; unknown ids are dropped with a log. Frames are reliable; per-sender
rate limit: 20/s sustained with a 40-frame burst (`ModRateLimitPolicy`).

**64 KiB payload cap** — framework policy, NOT a line limit: refused at the
sender and re-checked at the receiver.

## 4b. Host commands

```csharp
context.Commands.Register(new ModCommand("heal", ctx =>
    $"healed {string.Join(" ", ctx.Arguments)} for {ctx.RequesterSteamId}",
    description: "Heal a member", isHostAction: true));
context.Commands.TryExecute("heal", new[] { "alice" }, result => { /* ... */ });
```

- **Host-authoritative execution**: the handler runs ONLY on the host's copy
  of the mod. A host call completes synchronously (callback before return); a
  guest call sends `ModCommandRequest` (NetMsg 86), the host validates and
  executes its own copy, and answers with a directed `ModCommandResult`
  (NetMsg 87) that settles the guest's pending callback by request id.
- Framework checks before execution: request shape caps (name ≤64, ≤16 args,
  each ≤256, total ≤4 KiB), sender is a handshaken member, mod id/command
  registered, permission flags, per-guest request rate limit (4/s, burst 8).
  The mod's handler remains the semantic validator and can authorize per-guest
  behavior from `ctx.RequesterSteamId`.
- Handler exceptions become `Success=false` results with the exception
  message; output is capped at 32 KiB (error 4 KiB). Pending guest callbacks
  are settled with a failure when the session ends or the framework shuts down.
- Guest-side result callbacks are reliable and directed to the requester only;
  unknown request ids are dropped with a log.

## 4d. Mod state (host-persistent saves)

```csharp
context.State.TrySet("loadout", bytes);       // host-only + WriteGameState
context.State.TryGet("loadout", out var bytes);
context.State.TrySetSchemaVersion(2);
```

- **Scope**: `IModState` is scoped to the mod id — a mod can only read/write
  its own entry. Values are opaque `byte[]`; the framework never interprets or
  serializes the mod payload, so the mod owns its schema/migration.
- **Host-only save authority**: `TrySet` / `TrySetSchemaVersion` / `TryRemove` /
  `TryClear` require the host role AND `ModPermission.WriteGameState`. A guest
  copy sees `CanWrite = false` and cannot read the host's table; a synchronized
  mod that needs host state coordinates through `IModNetwork` / `IModCommands`.
- **Persistence**: the host writes a versioned protobuf file under
  `BepInEx/config/CasualtiesUnknownOnline.mod-state.bin` (atomic temp+replace).
  Each write persists the full table; the in-memory table is process-scoped
  and loaded once before discovery/Bind. A missing file is empty; a corrupt or
  unknown-version file degrades to empty with a warning (never a startup
  crash, never a guessed migration).
- **Metadata**: the file carries mod id, mod version (last writer) and the
  mod-declared schema version. `SchemaVersion` defaults to 1; the framework
  stores it verbatim and does not migrate — migration policy belongs to the mod.
- **Missing-mod policy**: an entry for a mod that is not currently loaded is
  preserved untouched, so the data is still there if the mod returns.
- **Safety rails**: key length ≤128, ≤1024 keys per mod, value ≤64 KiB.
  Errors are refused with a log, never silently truncated.

## 4e. Mod UI (local windows)

```csharp
context.Ui.Register("status", "My Mod Status", window =>
{
    window.Label($"session active: {context.Session.SessionActive}");
    if (window.Button("ping"))
    {
        context.Network.Broadcast(Encoding.UTF8.GetBytes("ping"));
    }
    var text = window.TextField(_lastText);
    _lastText = text; // the mod owns persistent UI state
});
context.Ui.Unregister("status");
```

- **Scope**: `IModUi` is a per-mod, local-only immediate-mode window registry.
  A mod registers an id + title + draw callback in `Bind`; CUO invokes the
  callback every frame and the Unity plugin owns all IMGUI/Unity details.
- **No permission**: a UI window cannot touch network, session, or
  game-authoritative state by itself, so every network mode may use it. Shared
  UI state still flows through `IModNetwork` / `IModCommands`; the window only
  projects that state locally.
- **Control alphabet**: `Label`, `Button` (returns true on click),
  `TextField` (returns the edited value; the mod owns persistence), and
  `Separator`. The set is deliberately tiny — no Unity types leak into
  `CUO.Abstractions`.
- **Rules**: empty id/title or a null draw callback is refused; a duplicate id
  within the same mod is refused; `Unregister` removes the window.
- **Failure isolation**: a mod draw callback that throws shows an inline error
  in the window and is logged by the plugin; it never breaks the UI frame.
- **No wire change**: local presentation only.

## 4f. Mod content (registration)

```csharp
if (context.Content.CanRegister)
{
    var itemBytes = new ModItemDefinition
    {
        DisplayName = "Wooden Sword",
        Description = "A simple wooden sword.",
        Weight = 1f,
        Value = 5,
        Usable = true,
        Tags = "weapon",
        TemplateId = "stone",
        SpawnComponents = ["Example.WoodenSwordBehaviour, ExampleMod"]
    }.ToPayload();

    context.Content.TryRegister("wooden.sword", ModContentKind.Item, itemBytes);
    context.Content.TryRegister("healing.recipe", ModContentKind.Recipe, myRecipeDefinitionBytes, schemaVersion: 2);
}
var itemDefs = context.Content.Definitions; // snapshot; payloads are copied on read
context.Content.TryUnregister("wooden.sword");
```

- **Scope**: `IModContent` is a per-mod registry of opaque content
  definitions (item defs, weapon stats, NPC types, recipes, skills, map
  entries, etc.). A mod registers an id + kind + opaque payload in `Bind`;
  it may also declare a positive `schemaVersion` (defaults to 1). The
  framework never interprets, serializes, or migrates the payload, so the mod
  owns its own content schema/versioning; the stored version is carried
  verbatim on every definition read.
- **Permission**: registration requires `ModPermission.RegisterContent`.
  `CanRegister` reflects whether this mod copy declared the flag; every
  `TryRegister` call also enforces it. The permission policy already refuses
  that flag on `ClientOnly`/`Cosmetic` modes, so only state-bearing mods may
  register content.
- **Process-local**: content bytes do not travel over the wire. Content is
  part of the mod itself, so the existing Mod API handshake (mod id /
  SemVer / permissions / network mode) is the consistency boundary; a mod
  that needs client-specific dynamic content must coordinate through
  `IModNetwork` / `IModCommands` instead.
- **Rules**: empty id/kind, a null or over-cap payload, a non-positive
  schema version, or a duplicate id within the same mod is refused;
  `TryUnregister` removes a definition. Safety rails: id ≤128 chars,
  kind ≤64 chars, schema version must be positive, payload ≤64 KiB, ≤1024
  definitions per mod. Errors are refused with a log, never silently truncated.
- **Framework read view**: `IModContentControl.Entries` exposes every mod's
  registered definitions to other CUO layers (plugin / future native-content
  consumers) as a read-only snapshot. The runtime content catalog
  (`IModContentCatalog`) adds kind filtering, unique kind+id resolution, and
  cross-mod duplicate/schema-version conflict diagnostics without
  interpreting payloads; it is the intended base for a future native-content
  binder.
- **Content ownership query**: `context.ContentOwners.TryGetOwner(kind, id, out owner)`
  resolves the owning mod id for any framework-wide content registration.
  It is the migration replacement for CUCoreLib's per-kind
  `TryGetOwnerModGuid`; the query is read-only, needs no permission, and
  follows the same ordinal matching / ambiguity policy as the runtime
  catalog — a duplicate kind+id returns false.
- **Typed item content (first well-known kind)**: `ModItemDefinition` is a
  plain Abstractions DTO for the `ModContentKind.Item` payload. It carries
  display name, description, category, weight, value, usability flags, wear
  flag, destruction flag, tags, spawn frequency, an optional vanilla
  `TemplateId` (the runtime prefab base), optional `SpawnComponents` (component
  type names attached by the Game Adapter at template build time), optional
  `WorldSpawnPerChunk` (loose world-gen distribution), optional
  `DropSources` (explicit fixed corpse/crate/trader loot pools), and an
  extensible `CustomData` dictionary. A mod serializes it with `ToPayload()`
  and registers the bytes through `IModContent`; the framework still stores
  bytes opaquely.
- **Typed recipe content**: `ModRecipeDefinition` is the second well-known
  Abstractions DTO. It carries the result item/liquid, result amount and
  condition, intelligence requirement, recipe category, repair flag, and an
  ordered list of `ModRecipeIngredient` entries (specific item id or crafting
  quality, liquid flag, minimum condition, destroy flag). The Game Adapter
  recipe provider builds the vanilla recipe object and injects it into
  `Recipes.recipes` once the game table is ready.
- **Typed liquid content**: `ModLiquidDefinition` is the third well-known
  Abstractions DTO. It carries display/description text, RGBA tint, value per
  liter, health/injection flags, injection sickness, locale-from-item flag,
  and crafting qualities. The Game Adapter liquid provider maps the static
  fields into `Liquids.Registry` and applies local UI locale entries.
- **Typed building content**: `ModBuildingDefinition` is the fourth well-known
  Abstractions DTO. It carries display/description text, a vanilla
  `TemplateId` base prefab, optional `BuildingEntity` field overrides, optional
  `SpawnComponents`, authored `DropOnDestroy`/`AlwaysDrop`/`ItemCategoriesToAdd`
  drop rules, optional worldgen density (`SpawnMinPerChunk`,
  `SpawnMaxPerChunk`, `SpawnLayers`, `GenerationStyle`, `Placement`,
  `SpawnInGround`, `SurfaceOffset`, `RandomFlip`), and an extensible
  `CustomData` dictionary. The Game Adapter building provider builds an inactive
  runtime building template from the base prefab, applies the drop tables to the
  `BuildingEntity`, and serves it through the existing `Utils.Create` /
  `EntitySpawned` materialization path; enabled worldgen definitions are later
  distributed deterministically from the `PlaceCrystals` generation stream.
- **Typed tile content**: `ModTileDefinition` is the fifth well-known
  Abstractions DTO. It carries display/description text, an optional sprite
  resource path, an optional vanilla tile index used as the visual base,
  BlockInfo-style static behavior fields (health, hit/step sounds, sleep
  quality, metallic/toxicity/slippery flags, variation flag, RGBA tint,
  collider type), and an extensible `CustomData` dictionary. The Game Adapter
  tile provider allocates a deterministic custom block index, injects a Unity
  `Tile` into the current `WorldGeneration.tiles` palette, and supplies the
  matching `BlockInfo` through a narrow `GetBlockInfo` prefix. No random
  world generation and no wire message are involved — mods choose where
  static tiles appear.
- **Typed structure content**: `ModStructureDefinition` is the sixth well-known
  Abstractions DTO. It carries display/description text, a width/height grid of
  rows, marker maps from single-character markers to either vanilla block
  indices or custom tile content ids, per-depth spawn counts, and an extensible
  `CustomData` dictionary. The Game Adapter structure provider validates and
  compiles the grid; multi-block runtime placement is exposed through
  `IModStructurePlacement` (§4h). Non-empty per-depth spawn counts are consumed
  automatically during world generation: the Game Adapter distributes the
  static block grid after vanilla worldgen, inside CUO's isolated generation
  stream, and before the collider/UpdateWorld pass. No entity/loot/background
  layer is distributed and no new wire message is used.
- **Typed status content**: `ModStatusDefinition` is the seventh well-known
  Abstractions DTO. It carries display/description text, a body/limb scope,
  save-enabled metadata, an optional moodle id, and an extensible `CustomData`
  dictionary. The Game Adapter status provider validates and stores the static
  descriptor; per-player/per-limb runtime values are deliberately not part of
  this content seam.
- **Typed moodle content**: `ModMoodleDefinition` is the eighth well-known
  Abstractions DTO. It carries display/description text, a vanilla moodle
  intensity, a stable icon/resource id key, critical/chipped/important
  presentation flags, hold seconds, and an extensible `CustomData` dictionary.
  The Game Adapter moodle provider validates and stores the static descriptor;
  `ModStatusMoodleProjection` later feeds active status-linked moodles into
  the vanilla moodle manager. Moodle content is still never a wire feature.
- **Shared-content binding boundary**: the runtime content binder only routes
  content from mods whose network mode guarantees a matching copy on every
  player that can receive the content instances
  (`Synchronized`, `Authoritative`, `RequiresAllPlayers`). `HostOnly`,
  `ClientOnly`, and `Cosmetic` content is never bound into shared world state:
  a guest without the same mod cannot safely materialize a host-only item.
  This is the first concrete implementation of the local-only vs
  public/shared mod-data distinction for static content.
- **Current item binding scope**: the Game Adapter provider decodes
  `ModItemDefinition`, owns the registration into the vanilla item table
  (`Item.GlobalItems`), and — when `TemplateId` is set — builds an inactive
  runtime template from that vanilla prefab, renames it to the custom item id,
  attaches the requested `SpawnComponents`, and serves it through CUO's item
  prefab resolution seam. CUO's own restore/spawn paths, a narrow
  `Utils.Create` prefix, and targeted transpilers for the native
  `BuildingEntity.Update` and `SaveSystem.TryLoadGame` resource loads can
  therefore materialize a custom item; no game type is exposed to mods and no
  new wire message is used.
- **Current item fixed drop-source scope**: `ModItemDefinition.DropSources`
  lets a mod opt a custom item into explicit vanilla loot containers instead of
  (or in addition to) the generic category pool. The Game Adapter provider
  registers the item under stable synthetic `ItemLootPool` categories for
  corpse, built-in medical/food/container crates, drop capsules, capsule
  containers, and trader 1-3 stock; narrow `CorpseScript.Start`,
  `BuildingEntity.Start`, and host-side `TraderScript.GenerateSingleItemList`
  patches add those source categories to the existing vanilla loot flow. The
  item is removed from its generic category pool, so "fixed source" means the
  authored sources are authoritative. No game/Unity type crosses Abstractions
  and no new wire message is used.
- **Current recipe binding scope**: `GameAdapterRecipeContentProvider` decodes
  `ModRecipeDefinition`, waits for `Recipes.recipes`, deduplicates against the
  existing recipe table, and injects each accepted recipe with its game-category
  mapping. No game type is exposed to mods and no new wire message is used.
- **Current liquid binding scope**: `GameAdapterLiquidContentProvider` decodes
  `ModLiquidDefinition`, waits for `Liquids.Registry`, refuses to overwrite a
  known liquid, and applies the static fields plus local locale entries. No
  game delegate is passed from mods; no new wire message is used.
- **Current liquid-tile binding scope**: `ModLiquidTileDefinition` carries the
  static world-liquid fields (logical `LiquidId`, fill liquid, buoyancy/drag,
  per-second body-touch rates, visual base byte/tint, spawn amount/layers,
  flood-fill cap, consume-on-drink). `GameAdapterLiquidTileContentProvider`
  allocates deterministic custom world-fluid bytes starting at 7 in stable id
  order, maps them through `FluidManager.WorldFluidToLiquidID`, and supplies
  local projection surfaces (water info, display colour/name, body touch,
  drink, render) plus host-authoritative runtime placement/flood fill
  (`IModLiquidPlacement`). `LiquidTileWorldGenDistribution` runs from the
  same vanilla
  `GenerateOres` postfix as tile ore, inside CUO's isolated generation stream;
  the grid changes ride the existing FluidRegion/FluidInteraction sync. No
  JObject snapshot, no new wire message, and no game/Unity type or delegate
  crosses Abstractions.
- **Current building binding scope**: `GameAdapterBuildingContentProvider`
  decodes `ModBuildingDefinition`, builds an inactive runtime template from
  the mod's vanilla `TemplateId`, applies optional `BuildingEntity` overrides,
  attaches `SpawnComponents`, applies authored chance/always drop tables and
  guaranteed-drop categories to the runtime `BuildingEntity`, and exposes the
  template to `Utils.Create` so the existing `EntitySpawned` channel can
  replicate a mod spawn. When worldgen density/style is enabled,
  `BuildingWorldGenDistribution` distributes custom buildings from the
  `WorldGeneration.PlaceCrystals` postfix inside the sealed generation stream;
  generation-time starts are suppressed, so no wire message is needed. No
  game/Unity type is exposed to mods; no new wire message is used.
- **Current tile binding scope**: `GameAdapterTileContentProvider` decodes
  `ModTileDefinition`, allocates a stable custom block index starting at 36,
  injects a built `Tile` into every fresh `WorldGeneration.tiles` palette,
  answers `WorldGeneration.GetBlockInfo` for custom indices through the
  provider, and stores optional ore/worldgen/drop fields. `SpawnAmount`,
  `SpawnLayers`, and `GenerationStyle` are consumed by
  `TileWorldGenDistribution` from the vanilla `GenerateOres` postfix inside
  CUO's isolated generation stream, so both peers generate the same custom ore
  deposits without a wire message. `Drops` are spawned by the Game Adapter when
  a local custom tile breaks and ride the existing block-break/drop report.
  Single-cell runtime placement is exposed through `IModTilePlacement` (§4h).
  No game/Unity type is exposed to mods and no new wire message is used.
- **Current structure binding scope**: `GameAdapterStructureContentProvider`
  decodes `ModStructureDefinition`, validates the authored grid/marker maps,
  and compiles it into non-air cells. Multi-cell runtime placement is exposed
  through `IModStructurePlacement` (§4h). When `SpawnCounts` is present for the
  current biome depth, `StructureWorldGenDistribution` also places the compiled
  static block grid during generation; it runs inside `WorldGenRandomIsolation`
  (same stream on every side), writes through the vanilla `SetBlock` path while
  `generatingWorld` is true (the existing block relay/difference table therefore
  treats it as baseline), and refuses tutorial worlds. No game/Unity type is
  exposed to mods and no new wire message is used.
- **Current status binding scope**: `GameAdapterStatusContentProvider` decodes
  `ModStatusDefinition`, validates the scope/id/save fields, and stores the
  static descriptor as migration base. It does not create a per-player or
  per-limb status bag; dynamic runtime values belong to the future typed
  mod-data domain. No game/Unity type is exposed to mods and no new wire
  message is used.
- **Current moodle binding scope**: `GameAdapterMoodleContentProvider` decodes
  `ModMoodleDefinition`, validates the icon/intensity/hold fields, and stores
  the static descriptor as migration base. ModStatusMoodleProjection feeds
  the vanilla moodle row for active status-linked moodles. No game/Unity type
  is exposed to mods and no new wire message is used.
- **No wire change**: no content bytes or new NetMsg.

## 4g. Read game state (read-only projection)

```csharp
if (context.GameState.CanRead)
{
    if (context.GameState.TryGetPlayer(steamId, out var player))
    {
        var hp = player.Vitals?.BrainHealth;
        var items = player.Inventory?.Items;
    }
}
```

- **Scope**: a read-only projection of the latest framework-held **player
  character state** already arriving on the 1 Hz character stream. It is the
  same data source the built-in Online UI uses; a mod never sees Unity objects
  or game-assembly types.
- **Permission**: reading requires `ModPermission.ReadGameState`.
  `CanRead` reflects whether this mod copy declared the flag, and every
  `TryGetPlayer` call also enforces it (returns false with a log otherwise).
- **Exposed shape**: `IModPlayerState` carries `SteamId`, `InWorld`,
  `Vitals` (`BrainHealth`, `Hunger`, `Thirst`, `Stamina`, `Energy`,
  `Temperature`, `Alive`, `Conscious`) and `Inventory` (recursive
  `IModInventoryEntry` tree: instance id, item id, slot/wear index, condition,
  favourite flag, container contents). A missing half is null until its
  snapshot arrives.
- **Live read, immutable snapshot**: each `TryGetPlayer` call returns the
  latest cached facts at that moment; the returned objects are copies and can
  be held safely. A remote leaving the world or the session ending clears the
  cache.
- **Not in this slice**: the local player's own character state is not exposed
  through `IModGameState`; it is available through the native API's read-only
  local-player projection (see §4i). World/item/block/entity global state is
  not exposed yet. The same projection pattern is the forward path for those
  slices.
- **No wire change**: this surface only projects data that already arrives.

## 4h. Entity and item spawn

```csharp
if (context.EntitySpawn.CanSpawn)
{
    context.EntitySpawn.TrySpawn("landmine", 10f, 20f, 45f);
}

if (context.ItemSpawn.CanSpawn)
{
    context.ItemSpawn.TrySpawn("wooden.sword", 10f, 20f, 45f);
}
```

- **Entity scope**: `IModEntitySpawn` lets a synchronized/authoritative mod
  create a runtime world entity by the game's prefab id (`BuildingEntity.id`,
  the same id `Utils.Create` accepts). The method signature is the full public
  surface for this slice: prefab id + world X/Y + z rotation. No Unity or
  game-assembly type crosses the boundary.
- **Permission**: spawning requires `ModPermission.SpawnEntity`.
  `CanSpawn` reflects the declared flag; every `TrySpawn` call also enforces it
  (false + log otherwise). The permission policy already refuses that flag on
  `ClientOnly`/`Cosmetic`, so only state-bearing mods may spawn entities.
- **Runtime gates**: `TrySpawn` additionally requires an active session and the
  local player to be in-world (`LocalInWorld`); a spawn outside a live world is
  refused, not silently ignored.
- **Replication reuses the runtime-entity channel**: the Game Adapter creates
  the local `BuildingEntity` copy via `Utils.Create`; the normal
  `BuildingEntity.Start` report path then sends the existing
  `EntitySpawned` message (guest → host or host → broadcast), so every side
  creates the same prefab at the same position/rotation with the same
  creation-time data handling (geyser liquid type, keypad code, crystal tint)
  as any native runtime spawn. No new `NetMsg`; the existing `EntitySpawned` path is used.
- **Failure isolation**: invalid prefab ids, non-finite positions, an out-of-
  world session, a missing permission, or an adapter rejection (unknown prefab
  / non-`BuildingEntity` prefab) return false and are logged; a non-entity
  prefab created by the adapter is destroyed, never left as a local-only ghost.
- **Boundary (this slice)**: existing game `BuildingEntity` prefabs and
  custom building definitions registered as static content
  (`ModContentKind.Building`) are supported. This is a spawn/replication
  surface, not a generic custom-component/state-injection mechanism — a mod
  that needs per-entity custom data still coordinates through `IModNetwork` /
  `IModCommands` or registers it as static content (`IModContent`).
- **Item scope**: `IModItemSpawn` lets a synchronized/authoritative mod create
  one world-item prefab by the game's item id (or a custom item id registered
  through `ModContentKind.Item`). The signature takes item id + world X/Y + z
  rotation and returns false when the request cannot be fulfilled. No Unity or
  game-assembly type crosses the boundary.
- **Item replication reuses the item-domain channel**: the Game Adapter
  creates the local `Item` copy via `Utils.Create`; the normal `Item.Start`
  report path sends the existing `ItemSpawned` channel (guest → host or host →
  broadcast), so every side creates the same item at the same place. No new
  `NetMsg`.
- **Item boundary (this slice)**: vanilla item prefabs and custom item
  definitions registered as static content are supported. It is a spawn
  surface, not a generic item-state/custom-data injection mechanism — mod
  state still belongs in `IModState` or explicit `IModNetwork` /
  `IModCommands` coordination.
- **Tile/block placement scope**: `IModTilePlacement` lets a synchronized/
  authoritative mod place one custom terrain tile at integer block
  coordinates. The tile is addressed by the stable content id registered
  through `ModContentKind.Tile`; the Game Adapter resolves that id to its
  deterministic custom block index and calls the vanilla
  `WorldGeneration.SetBlock` path. No Unity or game-assembly type crosses
  the boundary.
- **Tile placement permission**: the surface reuses
  `ModPermission.SpawnEntity`. `CanPlace` reflects the declared flag and every
  `TryPlaceBlock` call also enforces it (false + log otherwise). It also
  requires an active in-world session.
- **Tile placement replication reuses the existing block channel**: the write
  goes through the vanilla `SetBlock` path already monitored by the CUO
  `BlockPlaced` relay, so guest → host report + host arbitration/broadcast
  works exactly like native block placement. No new `NetMsg`.
- **Tile placement precondition**: the target block must currently be air;
  the Game Adapter refuses a placement over an occupied cell before calling
  `SetBlock`, matching the existing `BlockPlaced` arbitration rule.
- **Tile placement boundary (this slice)**: only custom tiles registered as
  static content are addressable by stable id; vanilla block indices are not
  exposed through this seam. It writes one block cell, not a structure or a
  worldgen distribution.
- **Structure placement scope**: `IModStructurePlacement` lets a synchronized/
  authoritative mod place one static structure at integer block coordinates
  (`originX`, `originY` is the structure's bottom-left block). The structure is
  addressed by the stable content id registered through
  `ModContentKind.Structure`; the Game Adapter resolves every non-air cell to
  either a vanilla block index or a custom tile content id and calls the
  vanilla `WorldGeneration.SetBlock` path for each cell. No Unity or
  game-assembly type crosses the boundary.
- **Structure placement precondition**: all non-air cells must be inside the
  current world and on air. The Game Adapter preflights the entire structure
  before the first write, so a failed request never leaves a partial structure.
- **Structure placement replication**: each write goes through the vanilla
  `SetBlock` path already monitored by the CUO `BlockPlaced` relay, so
  guest → host report + host arbitration/broadcast works exactly like native
  block placement. No new `NetMsg`.
- **Structure placement boundary (this slice)**: only structures registered as
  static content are addressable; this runtime placement surface does not
  apply worldgen distribution or spawn counts. Automatic worldgen placement is
  a separate generation-time seam described in the typed structure content
  section above.
- **Liquid-tile placement scope**: `IModLiquidPlacement` lets a synchronized/
  authoritative mod place one custom world-liquid cell (`TryPlaceLiquid`) or
  start a flood fill (`TryFloodFill`) at integer block coordinates. The tile is
  addressed by the stable content id registered through
  `ModContentKind.LiquidTile`; the Game Adapter resolves it to its
  deterministic custom world-fluid byte and calls the vanilla
  `FluidManager.SetLiquid` / `StartFill` path. No Unity or game-assembly type
  crosses the boundary.
- **Liquid placement permission**: the surface reuses
  `ModPermission.SpawnEntity`. `CanPlace` reflects the declared flag and every
  call also enforces it (false + log otherwise). It also requires an active
  in-world session.
- **Liquid placement authority**: CUO's world fluid grid is host-authoritative
  (the host simulates alone and streams each guest's viewport through the
  existing `FluidRegion` channel). This surface writes only on the host/solo
  copy; a guest call is refused with a log and guest-initiated placement should
  be requested through `IModCommands`' host-authoritative execution. No new
  `NetMsg`; the host fluid stream replicates the grid write.
- **Liquid placement preconditions**: `TryPlaceLiquid` requires the target cell
  to be inside the world and on air. `TryFloodFill` requires the seed cell to
  be inside the world; a non-positive `maxFill` uses the definition's authored
  `MaxFloodFill` cap. The Game Adapter refuses an unknown/mapped-failed liquid
  tile before any write.
- **Liquid placement boundary (this slice)**: only custom liquid tiles
  registered as static content are addressable; vanilla fluid bytes and
  asset-backed visual modes are not exposed through this seam. Automatic
  worldgen distribution remains a separate generation-time seam.
- **No wire change**: no new `NetMsg`.

## 4i. AccessNativeApi (curated native operation registry)

```csharp
if (context.NativeApi.CanAccess)
{
    // Generic operation registry (Game Adapter-curated; not arbitrary reflection)
    if (context.NativeApi.TryInvoke("local.player.state", [], out var raw))
    {
        var state = (IModNativeLocalPlayerState)raw;
        var hp = state.BrainHealth;
    }

    // Typed convenience for the same registered operation
    if (context.NativeApi.TryGetLocalPlayerState(out var local))
    {
        var x = local.X;
        var y = local.Y;
    }
}
```

- **Scope**: `IModNativeApi` is a permission-gated registry of named native
  operations. The Runtime never exposes arbitrary reflection or direct access
  to game assemblies; only the Game Adapter registers operations, and only
  those operation ids are invokable.
- **Permission**: invoking requires `ModPermission.AccessNativeApi`.
  `CanAccess` reflects the declared flag; every invoke method also enforces it
  (false + log otherwise).
- **Safe value surface**: arguments and results are restricted to `null`,
  strings, numeric primitives, capped `byte[]` / primitive arrays, and
  framework DTO types (currently `IModNativeLocalPlayerState`). Unity objects,
  game-assembly objects, and arbitrary object graphs are refused before and
  after the Game Adapter seam — they never cross to a mod.
- **Registered operation (this slice)**: `local.player.state`
  (`ModNativeApiOperations.LocalPlayerState`) returns the local player body's
  position, vitals, consciousness, and derived alive/conscious flags as
  `IModNativeLocalPlayerState`. It is read-only and local-only; no wire
  message, no authority change.
- **Policy boundary**: the first slice is deliberately read-only. Write /
  native-mutation operations are not registered until a concrete consumer
  exists and its sync/authority boundary is designed. This is the explicit
  escape-hatch policy decision: a curated allowlist, never open reflection.
- **No wire change**: this surface only reads local game state.

## 4j. Runtime mod data (scope-declared ephemeral values)

```csharp
// Local-only presentation/config/debug state: never leaves this process.
if (context.Data.TryDeclare("settings", ModDataScope.LocalOnly))
{
    context.Data.TrySet("settings", myBytes);
    context.Data.TryGet("settings", out var current);
}

// Shared state: host owns the value; a guest keeps a mirror only after
// applying a host-originated value received over context.Network.
if (context.Data.TryDeclare("score", ModDataScope.Shared))
{
    // Host only:
    context.Data.TrySet("score", scoreBytes);
    context.Network.Broadcast(scoreBytes); // mod-owned payload/serialization

    // Guest, in MessageReceived from the host:
    context.Data.TryApplyShared("score", payload, senderSteamId);
}

// Host-authoritative state: the framework keeps no guest mirror.
if (context.Data.TryDeclare("hostSecret", ModDataScope.HostAuthoritative))
{
    if (context.Session.IsHost)
    {
        context.Data.TrySet("hostSecret", secretBytes);
    }
}
```

- **Scope**: `IModData` is a per-mod, process-local, **ephemeral** runtime store.
  It is not `IModState` and it is not a generic snapshot service. The mod
  declares each slot's scope once (`LocalOnly`, `Shared`, or
  `HostAuthoritative`), then reads/writes opaque `byte[]` values with the same
  key/value caps as the durable state store (key ≤128, value ≤64 KiB, ≤1024
  slots per mod).
- **No persistence and no automatic sync**: values exist only for the current
  process. Durable values belong in `IModState`; cooperative gameplay facts
  belong in CUO's typed kernel domains. The framework never sends a runtime
  data value. Shared mirrors are applied explicitly by the mod from a value it
  received over `IModNetwork`, so there is no hidden JToken/JObject snapshot
  protocol.
- **Scopes**:
  - `LocalOnly` — every network mode may declare. Any role may set/get/remove.
  - `Shared` — only state-bearing modes (`Synchronized`, `Authoritative`,
    `RequiresAllPlayers`) and only when the mod declares
    `SendNetworkMessage` (the transport that makes a mirror meaningful). The
    host is the only writer; guests call `TryApplyShared` with the session
    host's SteamId to store a local mirror.
  - `HostAuthoritative` — state-bearing modes plus `HostOnly`. The host is the
    only writer/reader in the framework store; guests get no mirror and must
    coordinate through `IModCommands` / `IModNetwork` if they need the value.
- **Role gates**: `TrySet` and `TryRemove` on `Shared`/`HostAuthoritative`
  slots require the host role. `TryApplyShared` requires a guest copy, a
  `Shared` slot, and a sender that equals the session host. These checks are
  logged and return false; nothing is silently ignored.
- **Migration mapping**: CUCoreLib's ad-hoc custom data / snapshot modules map
  to this seam by declaring a scope and then using the existing typed
  `IModNetwork` / `IModCommands` surfaces for transport. Do not port a generic
  JObject snapshot registry.
- **No wire change**: no new NetMsg is introduced by this surface.

## 4k. Runtime mod status (phases 1–3)

```csharp
// Declare a typed body-formula status the GameAdapter knows how to project:
if (context.StatusRuntime.TryDeclare(
        "strength.potion",
        ModStatusScope.Body,
        ModDataScope.Shared,
        projectionKind: ModStatusProjectionKind.BodyFormula))
{
    var projection = new ModBodyFormulaProjection { MaxEncumbrance = 2f, Immunity = 5f, HeartRateOffset = 12f };
    context.StatusTransport.TryBroadcastBodyStatus(
        "strength.potion", playerSteamId, projection.ToPayload());
}

if (context.StatusRuntime.TryDeclare("bleeding", ModStatusScope.Limb, ModDataScope.Shared))
{
    // Host only after the mod's own validation/commit:
    context.StatusTransport.TryBroadcastLimbStatus(
        "bleeding", playerSteamId, limbSlot, payload);

    // Route mod-message frames through the typed status handle first:
    context.Network.MessageReceived += (sender, payload) =>
    {
        if (context.StatusTransport.TryHandleStatusPayload(sender, payload))
        {
            return;
        }

        // Other mod-message traffic continues here.
    };
}
```

- **Scope**: `IModStatusRuntime` is the per-mod runtime counterpart to static
  `ModStatusDefinition` content. Values are ephemeral, process-local, and keyed
  by `(status id, player SteamId, optional limb slot)`. The mod owns the byte
  payload schema/version.
- **Scopes**: the same `ModDataScope` rules as `IModData`: `LocalOnly` any
  role; `Shared` host-write + explicit guest apply; `HostAuthoritative` host
  only with no guest mirror.
- **Typed transport (phase 2)**: `IModStatusTransport` publishes committed
  shared values as versioned `ModStatusUpdate` frames over the existing
  `IModNetwork` channel. The host calls `TryBroadcastBodyStatus` /
  `TryBroadcastLimbStatus` (and the remove overloads); every side calls
  `TryHandleStatusPayload` from its mod-message handler so guest mirrors are
  applied/removed automatically from a host-originated frame. The host
  consumes its own broadcast echo without re-applying.
- **Guest request path**: this seam does not add a framework command. A guest
  that needs the host to change a shared/host-authoritative status still uses
  `IModCommands`; the host command handler is the semantic validator and then
  calls a `TryBroadcast*` helper to publish the committed result.
- **Typed projection (phase 3)**: `TryDeclare` accepts an optional
  `ModStatusProjectionKind` (`BodyFormula` or `LimbPhysiology`). When set, the
  mod's opaque status value should be the matching typed DTO
  (`ModBodyFormulaProjection` / `ModLimbProjection`). The GameAdapter decodes
  only those well-known payloads and applies additive overlays to the local
  vanilla `Body`/`Limb` after their native updates. The mod still owns the
  payload bytes and serialization; no game/Unity type crosses Abstractions.
- **Projection scope**: body fields are MaxEncumbrance,
  TotalEncumbrance, Immunity, JumpSpeed, AveragePain, plus HeartRateOffset,
  RespiratoryRateOffset, and BloodPressureOffset; limb fields are BleedAmount,
  SkinHealth, MuscleHealth, and InfectionAmount. The circulation offsets are
  applied through a dedicated `Body.HandleCirculation` prefix/postfix seam:
  the previous offset is removed before the native formula and the current
  offset is reapplied after it, so these continuously recomputed values remain
  at native base + mod offset rather than being erased every frame. The
  vanilla moodle row is fed by `ModStatusMoodleProjection` through
  `MoodleManager.AddAllMoodles` prefix/postfix patches, not an additive
  body overlay.
- **Boundary**: opaque `None` statuses are never interpreted by the
  GameAdapter. Only body/limb projection statuses reach the vanilla layer; the
  store change event is internal and does not add a wire message.
- **No dedicated wire change**: no new NetMsg and no protocol bump; the typed
  frames ride the existing `NetMsg.ModMessage` channel. No generic JObject
  snapshot is introduced.

## 5. Handshake consistency (how sessions stay coherent)

The guest's declared mod list rides the handshake (`HandshakeMsg.Mods`). The
host validates BEFORE the member is created:

| Host has | Guest has | Verdict |
|---|---|---|
| RequiresAllPlayers / Synchronized / Authoritative | missing, SemVer-precedence-unequal, or permission-unequal | **reject** |
| any mode | same id but a different NetworkMode while either side is state-bearing | **reject** |
| HostOnly | missing | pass (host-side logic) |
| ClientOnly / Cosmetic | missing / different version | pass (local surfaces) |
| — | claims RequiresAllPlayers / Synchronized / Authoritative the host lacks | **reject** |
| — | malformed list (empty/duplicated id, invalid mode/permissions, unparseable state-bearing version) | **reject** |
| discovery not yet run | anything | **"pending" refusal** — the guest's 1 s retry re-runs the check |

Versions are strict SemVer. For state-bearing modes the comparison is
**precedence equality** (build metadata ignored); compatibility ranges are
deliberately not inferred until a formal API-compatibility contract exists.

## 6. Reference layout of a mod

```
BepInEx/plugins/MyMod/MyMod.dll        ← BepInEx loads this (the shell)
```

The shell (`[BepInPlugin]` + empty `BaseUnityPlugin`), the `[CuoMod]` class,
and the manifest metadata travel in ONE assembly. Copy the example:
`src/CasualtiesUnknownOnline.ModExample/` (it registers `echo`/`whoami`
commands and remains the two-process verification target).

## 7. Versioning and protocol discipline

- `ProtocolVersion.Current` is `1`. The pre-release protocol-version sequence
  was deliberately reset before first release (tech-decisions #137); earlier
  numbers such as 10/29/34 in this document are historical and must not be used
  as current wire versions.
- Behavioral wire changes after the first release will bump
  `ProtocolVersion.Current`; local-only/read-only mod surfaces that add no wire
  change do not bump it.
- Mod versions are strict SemVer strings, validated at discovery and compared
  by precedence for state-bearing modes.
- The 64 KiB cap is a policy constant (`ModChannel.MaxPayloadBytes`); raising
  it is a protocol-adjacent decision, not a wire format change.

## 8. Tests and verification

All mod behavior is covered by pure-managed tests over the production stack
(`tests/.../Mods/`): discovery + dependency ordering (`ModDiscoveryTests`),
permission policy (`ModPermissionPolicyTests`), SemVer (`SemanticVersionTests`),
lifecycle (`ModLifecycleTests`), message routing + permission/rate gates
(`ModMessageTests`), host commands (`ModCommandTests`), mod-state saves
(`ModStateTests`), local mod UI (`ModUiTests`), mod content registration
(`ModContentTests`, `ModContentCatalogTests`), runtime mod data
(`ModDataTests`, `ModStatusRuntimeTests`, `ModStatusWireTests`, `ModStatusUpdateTests`,
`ModStatusProjectionStoreTests`, `ModBodyFormulaProjectionTests`,
`ModLimbProjectionTests`, `ModStatusProjectionContractTests`), read game state (`ModGameStateTests`), entity spawn
(`ModEntitySpawnTests`), item spawn (`ModItemSpawnTests`), tile placement
(`ModTilePlacementTests`), native API
(`ModNativeApiTests` + `GameAdapterNativeApiContractTests`), handshake matrix
(`ModHandshakeTests`), rate limiter (`ModRateLimiterTests`), direction rows
(`DirectionTests`) and wire round-trips (`ModHandshakeProtocolTests`).

The example mod doubles as the **two-process runtime verification target**:
deploy it to both machines, join, and the logs show `[Mods] discovered
cuo.example ...`, the handshake admitting the pair (Synchronized, equal
version/permissions), guest→host command results (`ModCommandRequestHandler`/
`ModCommandResultHandler ... success True`), and the echo round-trip
(`[Example] echo from <steamId>`).
