# The mod API contract

[Documentation](../README.md) > [Reference](README.md) > The mod API contract

---

**After this page** you can look up what a mod may declare, what CUO enforces, which surfaces exist and
what each one promises. This is the contract a mod author codes against; the reasoning behind it is in
[The life of a mod](../internals/mod-loading-lifecycle.md) and
[Permissions and what they enforce](../internals/permissions-and-security.md). Read
[Your first mod](../start/your-first-mod.md) first if you have not built one.

## What a mod may reference

`CUO.Abstractions` is the only assembly a mod references, and that is a statement about the contract,
not a fence around what a mod may do: patching CUO's own implementation by name is allowed and carries
no promise, and a mod that needs the game's own code binds it through the declared
[native binding](glossary.md) tier. Which surface is a promise, which is only an implementation, what a
mod may patch and what diagnostics an author can expect are
`docs/api/advanced-modification-policy.md`; its §1.1 is the tier table.

## How a mod is loaded

BepInEx 5 loads plugins one by one, load-then-`Awake`, in a single loop. Two rules follow:

1. **Discovery runs on the framework's first update frame**, not in its `Awake` — a scan in `Awake`
   would miss every plugin loaded after it. `ModService` scans `AppDomain.GetAssemblies()` once, finds
   every `[CuoMod]`-declared `ICuoMod` type, validates it, orders the accepted set topologically by
   dependencies and binds it. The discovery frame runs `Bind` → `Initialize` → `Start` → `Update`;
   `Update` runs every frame after that, and `Stop`/`Dispose` run in reverse load order.
2. **A mod's BepInEx shell `Awake` stays empty** — it may run before or after the CUO plugin's `Awake`
   and must not touch any CUO API.

Two traps the layout has to respect:

- **The double-instance trap.** BepInEx instantiates the shell (the `BaseUnityPlugin`), CUO
  instantiates the `[CuoMod]` class. A single type playing both roles yields two instances with
  independent state, so the shell and the mod class are always separate types;
  `src/CasualtiesUnknownOnline.ModExample/` is the canonical layout.
- **The BepInEx reference reconciliation.** BepInEx.Core is referenced only as the loading mechanism
  (the shell derives `BaseUnityPlugin` and nothing else); every line of business logic references
  `CUO.Abstractions` only.

## The manifest

`[CuoMod]` is the single manifest source: id, display name, version, `NetworkMode`, `Permissions`,
`Dependencies`, description, `Namespace` and `NativeBinding`.

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

**`NetworkMode`** defaults to `Unspecified` and is rejected at discovery. Other rejection causes:
a duplicated id, an abstract or non-public type, a missing public parameterless constructor, a
non-SemVer version, unknown permission bits, host/state permissions on `ClientOnly`/`Cosmetic`, and
malformed or unsatisfiable dependencies (missing target, self, duplicate, cycle — a transitive failure
propagates to the dependent). A declared `Namespace` adds three more: an invalid grammar, the reserved
built-in namespace `cu`, or a namespace another loaded mod already declared. One rejected mod never
blocks the scan: per-mod fail-closed.

**`Permissions`** are never implicit — the default is `ModPermission.None`. The eight declared flags
and their live enforcement points:

| Flag | What it gates |
|---|---|
| `SendNetworkMessage` | `IModNetwork` send **and** receive |
| `RegisterCommand` | command registration |
| `ExecuteHostAction` | additionally gates `ModCommand.IsHostAction` |
| `WriteGameState` | host-persistent mod-state writes (`IModState`) |
| `RegisterContent` | mod content registration (`IModContent`) |
| `ReadGameState` | the read-only game-state projection (`IModGameState`) |
| `SpawnEntity` | world entity spawn (`IModEntitySpawn`), world item spawn (`IModItemSpawn`), and the tile/block, structure and liquid placement surfaces (`IModTilePlacement`, `IModStructurePlacement`, `IModLiquidPlacement`) |
| `AccessNativeApi` | the curated native/game-private operation registry (`IModNativeApi`) |

**`Dependencies`** are mod ids loaded before the dependent. **`NativeBinding`** names the game's own
code a mod binds — the declared Tier 2 of `docs/api/advanced-modification-policy.md` §1.1. It is a
declared FACT, not a permission and not a rejection cause: `ModPermission` is what CUO enforces, and
CUO enforces nothing here, so an undeclared binding is undetectable and the declaration is opt-in
honesty whose only force is another peer's parity policy. Discovery normalizes a blank value (empty or
whitespace-only) to "no declaration" and reports it in the `[Mods] discovered …` line, so a host's log
answers "which mod binds the game's own code" without reading any mod's source. It buys visibility,
never stability: what it names belongs to the game, and a game update may break it with no CUO
decision. The declaration also rides the handshake (each `ModInfoMsg` carries it), where the host's
`NativeBindingParity` rule judges it per mod id — see [Handshake consistency](#handshake-consistency).

## Lifecycle and the context

`ICuoMod : ICuoService` — the standard lifecycle driven by the framework's pump on the Unity main
thread, every stage exception-isolated. `IModContext` is what `Bind` receives:

| Member | Semantics |
|---|---|
| `Session` | a **snapshot at bind time**, not a live view — the host never fires `SessionActivated` (it activated at lobby creation) and events fired before discovery are lost. The snapshot is the only reliable "current state"; `MemberSteamIds` is the peer member set and the local peer is `LocalSteamId`. |
| `Logger` | mod-scoped logger; its lines carry the mod id (`[Mod:<id>]`). |
| `Network` | the mod message channel. |
| `Commands` | host-authoritative commands. |
| `ConsoleCommands` | local in-game console commands with no wire relay; the host-command surface is `Commands`. |
| `State` | host-persistent per-mod state. |
| `Data` | runtime scope-declared per-mod data. |
| `StatusRuntime` | runtime per-player/per-limb mod status values. |
| `MoodleRuntime` | local per-status moodle-presentation resolvers. |
| `BuildingRuntime` | per-mod building prefab/instance hooks. |
| `Ui` | local immediate-mode mod UI windows. |
| `Content` | mod content registration. |
| `ContentOwners` | framework-wide content-ownership lookup. |
| `ResourceCompletion` | per-mod resource-completion stages. |
| `GameState` | read-only player-state projection. |
| `EntitySpawn` / `ItemSpawn` | world entity spawn / world item spawn. |
| `TilePlacement` / `StructurePlacement` / `LiquidPlacement` | world tile-block / multi-block structure / liquid-tile placement. |
| `NativeApi` | curated native/game-private operation registry. |
| `SessionActivated` | the first member handshake completed. **Host side: never** — read the snapshot. |
| `PlayerJoined` / `PlayerLeft` | a member's handshake completed / a member was removed (host side). Not the in-world entity join. Each member exactly once, including yourself. |
| `SessionEnded` | the session tore down. A guest's `PlayerLeft` for the host is not fired on host exit — only `SessionEnded`. |

## Mod messages

`IModNetwork` — report/directed semantics, star topology, **no auto-relay**:

| Call | Host | Guest | Notes |
|---|---|---|---|
| `SendToHost(payload)` | no-op | reports to the host's copy of the mod | outside a session: no-op |
| `SendToPeer(steamId, payload)` | sends to one member's copy | no-op | |
| `Broadcast(payload)` | every member **including** the host's own copy (local fire with its own SteamId) | no-op | the "all sides run this" call |
| `MessageReceived` | `(senderSteamId, payload)` — a report (guest) or the host's own broadcast | a directed or broadcast frame | |

The mod must declare `SendNetworkMessage`: undeclared sends are refused at the sender (no-op plus a
log) and undeclared receives are dropped. The [payload](glossary.md) is **opaque**; unknown mod ids
are dropped with a log. Frames are reliable at the transport; the per-sender rate limit is 20/s
sustained with a 40-frame burst (`ModRateLimitPolicy`).

**A drop is accepted loss.** An over-burst frame is dropped with a log and never queued, and nothing is
re-sent: `ModMessage` and the mod-status transport (`ModStatusTransport`) are loss-tolerant by design —
they carry opaque payloads with no backfill, so the next message, never a retry, is the recovery. Only
the command request/result pair has a settlement, and it is the requester's own deadline.

**64 KiB payload cap** — framework policy (`ModChannel.MaxPayloadBytes`), not a line limit: refused at
the sender and re-checked at the receiver.

## Host commands

```csharp
context.Commands.Register(new ModCommand("heal", ctx =>
    $"healed {string.Join(" ", ctx.Arguments)} for {ctx.RequesterSteamId}",
    description: "Heal a member", isHostAction: true));
context.Commands.TryExecute("heal", new[] { "alice" }, result => { /* ... */ });
```

- **Host-authoritative execution**: the handler runs only on the host's copy of the mod. A host call
  completes synchronously (callback before return); a guest call sends `ModCommandRequest`
  (`NetMsg` 86), the host validates and executes its own copy, and answers with a directed
  `ModCommandResult` (`NetMsg` 87) that settles the guest's pending callback by request id.
- Framework checks before execution: request shape caps (name ≤64, ≤16 arguments, each ≤256
  characters, total ≤4 KiB), sender is a handshaken member, mod id/command registered, permission
  flags, per-guest request rate limit (4/s, burst 8). The mod's handler remains the semantic validator
  and can authorize per-guest behaviour from `ctx.RequesterSteamId`.
- Handler exceptions become `Success=false` results carrying the exception message; output is capped at
  32 KiB (error 4 KiB). Pending guest callbacks are settled with a failure when the session ends or the
  framework shuts down; the deadline below settles the rest earlier.
- **Request deadline and a bounded pending map**: a guest request is settled by the result frame, by
  the requester's deadline (`ModCommandPolicy.CommandRequestTimeoutMs`, 10 s, swept once per frame), or
  by the session-end/shutdown failure. The deadline is the answer to every silent loss: a request the
  host dropped (rate limit, shape caps, not a handshaken member) and one whose result was lost both
  fail the caller with `Success=false` and a `timed out` error instead of hanging until the session
  ends. The host never answers an over-burst frame — answering would let a flooding peer drive the
  host's outbound past the token bucket that bounds one member — so a dropped frame and a lost result
  reach the caller as one observable reason. Per mod, at most `ModCommandPolicy.MaxPendingRequests`
  (32) requests may be outstanding; a call over the cap is refused at the sender (`false`, no callback,
  the same contract as the other sender-side refusals).
- Guest-side result callbacks are directed to the requester only. A result for an unknown request id
  (a request that already timed out, or one this copy never sent) is dropped with a log, and a failure
  result names its request id and command name.

## Mod state

```csharp
context.State.TrySet("loadout", bytes);       // host-only + WriteGameState
context.State.TryGet("loadout", out var bytes);
context.State.TrySetSchemaVersion(2);
```

- **Scope**: `IModState` is scoped to the mod id — a mod can only read and write its own entry. Values
  are opaque `byte[]`; the framework never interprets or serializes the mod payload, so the mod owns
  its schema and migration.
- **Host-only save authority**: `TrySet` / `TrySetSchemaVersion` / `TryRemove` / `TryClear` require the
  host role **and** `ModPermission.WriteGameState`. A guest copy sees `CanWrite = false` and cannot
  read the host's table; a synchronized mod that needs host state coordinates through `IModNetwork` /
  `IModCommands`.
- **Persistence**: the host writes a versioned protobuf file under
  `BepInEx/config/CasualtiesUnknownOnline.mod-state.bin` (atomic temp plus replace). Each write
  persists the full table; the in-memory table is process-scoped and loaded once before
  discovery/`Bind`. A missing file is empty; a corrupt or unknown-version file degrades to empty with a
  warning — never a startup crash, never a guessed migration.
- **Metadata**: the file carries the mod id, the mod version (last writer) and the mod-declared schema
  version. `SchemaVersion` defaults to 1; the framework stores it verbatim and does not migrate.
- **Missing-mod policy**: an entry for a mod that is not currently loaded is preserved untouched, so
  the data is still there if the mod returns.
- **Safety rails**: key ≤128 characters, ≤1024 keys per mod, value ≤64 KiB. Errors are refused with a
  log, never silently truncated.

## Mod UI

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

- **Scope**: `IModUi` is a per-mod, local-only immediate-mode window registry. A mod registers an id,
  a title and a draw callback in `Bind`; CUO invokes the callback every frame and the Unity plugin owns
  all IMGUI/Unity details.
- **No permission**: a UI window cannot touch network, session or game-authoritative state by itself,
  so every network mode may use it. Shared UI state still flows through `IModNetwork` /
  `IModCommands`; the window only projects that state locally.
- **Control alphabet**: `Label`, `Button` (returns true on click), `TextField` (returns the edited
  value; the mod owns persistence) and `Separator`. The set is deliberately tiny — no Unity types leak
  into `CUO.Abstractions`.
- **Rules**: an empty id/title or a null draw callback is refused; a duplicate id within the same mod
  is refused; `Unregister` removes the window.
- **Failure isolation**: a draw callback that throws shows an inline error in the window and is logged
  by the plugin; it never breaks the UI frame.
- **No wire change**: local presentation only.

## Content registration

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

- **Scope**: `IModContent` is a per-mod registry of opaque content definitions (item defs, weapon
  stats, NPC types, recipes, skills, map entries). A mod registers an id, a kind and an opaque payload
  in `Bind`; it may also declare a positive `schemaVersion` (default 1). The framework never
  interprets, serializes or migrates the payload, so the mod owns its content schema and versioning;
  the stored version is carried verbatim on every definition read.
- **Namespaced [content ids](glossary.md)**: every CUO content id is a canonical `namespace:path`.
  Declare the mod's [namespace](glossary.md) in the manifest (`[CuoMod(..., Namespace = "mymod")]`);
  its content is then addressable as `mymod:wooden.sword`, while built-in game content is
  `cu:<item id>` (for example `cu:fentanyl`). The grammar is `[a-z][a-z0-9_]{0,31}` for the namespace
  and `[a-z0-9][a-z0-9_.-]{0,94}` for the path; `ContentId` in Abstractions parses and formats it and
  normalises input case. A mod without a declared namespace keeps the bare id (mod-scoped; cross-mod
  duplicates are still reported as conflicts). The bare id is also the game-table key that content
  providers materialise, so two mods in different namespaces registering the same bare id for the same
  kind are still a conflict — the canonical addresses resolve, but only one game entry can exist. The
  console's `ResourceLocation` completion accepts a canonical id prefix, the bare id or the localized
  display name, and always inserts the canonical id.
- **Permission**: registration requires `ModPermission.RegisterContent`. `CanRegister` reflects
  whether this mod copy declared the flag; every `TryRegister` call also enforces it. The permission
  policy already refuses that flag on `ClientOnly`/`Cosmetic`, so only state-bearing mods may register
  content.
- **Process-local**: content bytes do not travel over the wire. Content is part of the mod itself, so
  the handshake (mod id / SemVer / permissions / network mode) is the consistency boundary; a mod that
  needs client-specific dynamic content coordinates through `IModNetwork` / `IModCommands` instead.
- **Rules**: an empty id or kind, a null or over-cap payload, a non-positive schema version, or a
  duplicate id within the same mod is refused; `TryUnregister` removes a definition. A content id must
  already be a canonical lower-case path segment (`ContentId.IsValidPath`): upper case, whitespace, the
  `:` separator and ids longer than 95 characters are refused. Rails: kind ≤64 characters, schema
  version positive, payload ≤64 KiB, ≤1024 definitions per mod. Errors are refused with a log, never
  silently truncated.
- **Framework read view**: `IModContentControl.Entries` exposes every mod's registered definitions to
  other CUO layers (the plugin, future native-content consumers) as a read-only snapshot. The runtime
  content catalog (`IModContentCatalog`) adds kind filtering, unique kind+id resolution and cross-mod
  duplicate/schema-version conflict diagnostics without interpreting payloads; it is the intended base
  for a future native-content binder.
- **Content ownership query**: `context.ContentOwners.TryGetOwner(kind, id, out owner)` resolves the
  owning mod id for any framework-wide content registration. It is the migration replacement for
  CUCoreLib's per-kind `TryGetOwnerModGuid`; the query is read-only, needs no permission, and follows
  the same ordinal matching and ambiguity policy as the runtime catalog — a duplicate kind+id returns
  false.
- **Shared-content binding boundary**: the runtime content binder only routes content from mods whose
  network mode guarantees a matching copy on every player that can receive the content instances
  (`Synchronized`, `Authoritative`, `RequiresAllPlayers`). `HostOnly`, `ClientOnly` and `Cosmetic`
  content is never bound into shared world state: a guest without the same mod cannot safely
  materialise a host-only item. This is the first concrete implementation of the local-only versus
  public/shared mod-data distinction for static content.

**Typed definitions.** Abstractions ships one DTO per well-known kind; a mod fills it and calls
`ToPayload()`, and the framework still stores bytes opaquely. The Game Adapter is what decodes them.

| DTO (kind) | What it carries, and how the adapter binds it |
|---|---|
| `ModItemDefinition` (`Item`, first) | display name, description, category, weight, value, usability/wear/destruction flags, tags, spawn frequency, an optional vanilla `TemplateId` (the runtime prefab base), optional `SpawnComponents` (component type names attached at template build time), optional `WorldSpawnPerChunk` (loose world-gen distribution), optional `DropSources` (explicit fixed corpse/crate/trader loot pools), optional `DecayMinutes`, optional `Container` / `Battery` / `Light` / `Tool` / `Gun` behaviour DTOs, optional `Visual` (worn sprite, liquid-mask resource paths, frame animations for the base, worn and liquid-fill sprites) and an extensible `CustomData` dictionary. The provider registers the item into `Item.GlobalItems` and, when `TemplateId` is set, builds an inactive runtime template from that prefab, renames it to the custom id, attaches the requested components and serves it through CUO's item prefab resolution seam — so CUO's restore/spawn paths, a narrow `Utils.Create` prefix and targeted transpilers for `BuildingEntity.Update` and `SaveSystem.TryLoadGame` resource loads can materialise it. `DropSources` opts the item into explicit vanilla loot containers instead of (or beside) the generic category pool, registered under stable synthetic `ItemLootPool` categories for corpse, built-in medical/food/container crates, drop capsules, capsule containers and trader 1-3 stock; narrow `CorpseScript.Start`, `BuildingEntity.Start` and host-side `TraderScript.GenerateSingleItemList` patches add those categories to the vanilla loot flow, and the item is removed from its generic pool — "fixed source" means the authored sources are authoritative. `Container` / `Battery` / `Light` / `Tool` / `Gun` / `Visual` author the minimal vanilla behaviour and visual slice: numeric values are validated, tool/gun static use defaults and battery decay flags are applied to `ItemInfo`, components are configured on the runtime template, `Light` uses the GameAdapter reflection convention for the URP `Light2D` type (URP is not in the reference graph), and `Visual` resolves sprites into a per-instance visual state component whose worn-sprite hooks apply and restore the sprite on wear/drop and on remote clone/restore paths while liquid-mask hooks reapply the fill sprite after `WaterContainerItem.Start`. `Visual.MultiWornSprites` authors additive sprites on named vanilla limbs through `Wearable.CreateSprites`, with missing limbs filtered at wear time and per-limb offsets/sorting applied after creation; `Visual.BaseSpriteAnimation`, `WornSpriteAnimation` and `LiquidMaskAnimation` accept ordered frame paths plus frames-per-second and loop settings into a local sprite animator. |
| `ModRecipeDefinition` (`Recipe`, second) | result item/liquid, result amount and condition, intelligence requirement, recipe category, repair flag and an ordered list of `ModRecipeIngredient` entries (specific item id or crafting quality, liquid flag, minimum condition, destroy flag). The provider waits for `Recipes.recipes`, deduplicates against the existing table and injects each accepted recipe with its game-category mapping. |
| `ModLiquidDefinition` (`Liquid`, third) | display and description text, RGBA tint, value per liter, health/injection flags, injection sickness, locale-from-item flag, crafting qualities. The provider waits for `Liquids.Registry`, refuses to overwrite a known liquid, and applies the static fields plus local locale entries. |
| `ModLiquidTileDefinition` (`LiquidTile`, the static world-liquid half of the liquid family) | logical `LiquidId`, fill liquid, buoyancy and drag, per-second body-touch rates, visual base byte and tint, spawn amount and layers, flood-fill cap, consume-on-drink. The provider allocates deterministic custom world-fluid bytes starting at 7 in stable id order, maps them through `FluidManager.WorldFluidToLiquidID`, and supplies local projection surfaces (water info, display colour and name, body touch, drink, render) plus host-authoritative runtime placement and flood fill (`IModLiquidPlacement`). `LiquidTileWorldGenDistribution` runs from the same vanilla `GenerateOres` postfix as tile ore, inside CUO's isolated generation stream, and the grid changes ride the existing `FluidRegion`/`FluidInteraction` sync. |
| `ModBuildingDefinition` (`Building`, fourth) | display and description text, a vanilla `TemplateId` base prefab, optional `BuildingEntity` field overrides, optional `SpawnComponents`, authored `DropOnDestroy`/`AlwaysDrop`/`ItemCategoriesToAdd` drop rules, optional worldgen density (`SpawnMinPerChunk`, `SpawnMaxPerChunk`, `SpawnLayers`, `GenerationStyle`, `Placement`, `SpawnInGround`, `SurfaceOffset`, `RandomFlip`) and an extensible `CustomData` dictionary. The provider builds an inactive runtime template from the base prefab, applies the drop tables to the `BuildingEntity` and serves it through the existing `Utils.Create` / `EntitySpawned` materialisation path; enabled worldgen definitions are distributed deterministically from the `PlaceCrystals` generation stream, with generation-time starts suppressed so no wire message is needed. |
| `ModTileDefinition` (`Tile`, fifth) | display and description text, an optional sprite resource path, an optional vanilla tile index used as the visual base, `BlockInfo`-style static fields (health, hit/step sounds, sleep quality, metallic/toxicity/slippery flags, variation flag, RGBA tint, collider type) and an extensible `CustomData` dictionary. The provider allocates a deterministic custom block index starting at 36, injects a Unity `Tile` into the current `WorldGeneration.tiles` palette and supplies the matching `BlockInfo` through a narrow `GetBlockInfo` prefix. `SpawnAmount`, `SpawnLayers` and `GenerationStyle` are consumed by `TileWorldGenDistribution` from the vanilla `GenerateOres` postfix inside the isolated generation stream, so both peers generate the same custom ore deposits; `Drops` are spawned when a local custom tile breaks and ride the existing block-break/drop report. Mods choose where static tiles appear — no random world generation and no wire message are involved. |
| `ModStructureDefinition` (`Structure`, sixth) | display and description text, a width/height grid of rows, marker maps from single-character markers to vanilla block indices or custom tile content ids, per-depth spawn counts and an extensible `CustomData` dictionary. The provider validates and compiles the grid; non-empty per-depth spawn counts are consumed during world generation by `StructureWorldGenDistribution`, which runs inside `WorldGenRandomIsolation`, writes through the vanilla `SetBlock` path while `generatingWorld` is true (so the existing block relay treats it as baseline) and refuses tutorial worlds. Multi-block runtime placement is `IModStructurePlacement`. |
| `ModStatusDefinition` (`Status`, seventh) | display and description text, a body/limb scope, save-enabled metadata, an optional moodle id, optional per-limb moodle routing (`ShowPerLimbMoodles` plus `LimbMoodles`) and an extensible `CustomData` dictionary. The provider validates the scope/id/save fields and stores the static descriptor as migration base; it does not create a per-player or per-limb status bag — dynamic runtime values belong to the status runtime seam. |
| `ModMoodleDefinition` (`Moodle`, eighth) | display and description text, a vanilla moodle intensity, a stable icon/resource id key, critical/chipped/important presentation flags, hold seconds, an optional `ModMoodleAnimation` frame-path icon animation, optional per-limb display/description templates (`LimbDisplayNameFormat` / `LimbDescriptionFormat`) and an extensible `CustomData` dictionary. The provider stores the static descriptor; `ModStatusMoodleProjection` feeds active status-linked moodles into the vanilla moodle manager, and a `Moodle.Start` patch drives the vanilla moodle UI image from the authored frames. Moodle content is still never a wire feature. |

**Building runtime hooks.** `context.BuildingRuntime` lets a mod register one prefab hook and/or one
instance hook per custom building id. A prefab hook receives a plain `ModBuildingPrefabRequest`
(building/template id) and returns component type names; the Game Adapter attaches them to the inactive
runtime template before it is cached. An instance hook receives a `ModBuildingInstanceRequest`
(building/template id plus world X/Y/rotation) and returns component type names; the Game Adapter
attaches them to each custom building clone before it becomes active. Only the owning mod's hooks are
consulted. No live `GameObject`, game type or Unity type crosses Abstractions, no wire message is added,
and a mod-authored component owns its own initialization.

No content bytes and no new `NetMsg` travel for any of these: the Game Adapter binds them locally on
every side that has the mod.

## Read game state

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

- **Scope**: a read-only projection of the latest framework-held **player character state** already
  arriving on the 1 Hz character stream — the same data source the built-in Online UI uses. A mod never
  sees Unity objects or game-assembly types.
- **Permission**: reading requires `ModPermission.ReadGameState`. `CanRead` reflects whether this mod
  copy declared the flag, and every `TryGetPlayer` call also enforces it (false with a log otherwise).
- **Exposed shape**: `IModPlayerState` carries `SteamId`, `InWorld`, `Vitals` (`BrainHealth`, `Hunger`,
  `Thirst`, `Stamina`, `Energy`, `Temperature`, `Alive`, `Conscious`) and `Inventory` (a recursive
  `IModInventoryEntry` tree: instance id, item id, slot/wear index, condition, favourite flag,
  container contents). A missing half is null until its snapshot arrives.
- **Live read, immutable snapshot**: each call returns the latest cached facts at that moment; the
  returned objects are copies and can be held safely. A remote leaving the world or the session ending
  clears the cache.
- **Not in this slice**: the local player's own character state is not exposed here; it is available
  through the native API's read-only local-player projection. World/item/block/entity global state is
  not exposed yet.
- **No wire change**: this surface only projects data that already arrives.

## Spawn and placement

| Surface | Permission | Preconditions and replication |
|---|---|---|
| `IModEntitySpawn` (`context.EntitySpawn`) | `SpawnEntity` | Creates a runtime world entity by the game's prefab id (`BuildingEntity.id`, the same id `Utils.Create` accepts) — `CanSpawn` and `TrySpawn(prefabId, worldX, worldY, rotation)` are the whole public surface. Requires an active session and the local player in-world. The adapter creates the local `BuildingEntity` through `Utils.Create` and the normal `BuildingEntity.Start` report path sends the existing `EntitySpawned` message, so every side creates the same prefab at the same position and rotation with the same creation-time data handling (geyser liquid type, keypad code, crystal tint). Vanilla prefabs and custom building definitions registered as static content are supported; it is a spawn/replication surface, not a generic component- or state-injection mechanism. |
| `IModItemSpawn` (`context.ItemSpawn`) | `SpawnEntity` | Creates one world-item prefab by the game's item id or a custom id registered through `ModContentKind.Item` — `CanSpawn` and `TrySpawn(itemId, worldX, worldY, rotation)`. The adapter creates the local `Item` through `Utils.Create` and the normal `Item.Start` report path sends the existing `ItemSpawned` channel. Vanilla and custom item prefabs are supported; mod state still belongs in `IModState` or explicit `IModNetwork`/`IModCommands` coordination. |
| `IModTilePlacement` (`context.TilePlacement`) | `SpawnEntity` | Places one custom terrain tile at integer block coordinates, addressed by the stable content id registered as `ModContentKind.Tile` — `CanPlace` and `TryPlaceBlock(tileId, blockX, blockY)` are the surface; the adapter resolves it to the deterministic custom block index and calls the vanilla `WorldGeneration.SetBlock` path, which the CUO `BlockPlaced` relay already monitors (guest → host report plus host arbitration/broadcast). Requires an active in-world session and the target block to be air; it writes one block cell — not a structure and not a worldgen distribution. Vanilla block indices are not addressable. |
| `IModStructurePlacement` (`context.StructurePlacement`) | `SpawnEntity` | Places one static structure at integer block coordinates (`originX`, `originY` is the bottom-left block) by its `ModContentKind.Structure` id — `CanPlace` and `TryPlaceStructure(structureId, originX, originY)` are the surface; every non-air cell resolves to a vanilla block index or a custom tile content id and goes through the same `SetBlock` path. The adapter preflights the whole structure before the first write — every non-air cell must be inside the current world and on air — so a failed request never leaves a partial structure. This runtime surface does not apply worldgen distribution or spawn counts: automatic placement is a separate generation-time seam. |
| `IModLiquidPlacement` (`context.LiquidPlacement`) | `SpawnEntity` | Places one custom world-liquid cell (`TryPlaceLiquid`) or starts a flood fill (`TryFloodFill`) by its `ModContentKind.LiquidTile` id, through the vanilla `FluidManager.SetLiquid` / `StartFill` path. `CanPlace` reports the declared flag and every call enforces it again. Requires an active in-world session: `TryPlaceLiquid` needs an in-world air cell and `TryFloodFill` an in-world seed cell (a non-positive `maxFill` uses the definition's authored `MaxFloodFill` cap), and the adapter refuses an unknown or unmapped liquid tile before any write. CUO's fluid grid is host-authoritative, so this surface writes only on the host/solo copy — a guest call is refused with a log and guest-initiated placement belongs in a host-authoritative `IModCommands` call. Vanilla fluid bytes and asset-backed visual modes are not addressable, and automatic worldgen distribution remains a separate generation-time seam. |

Every surface here reuses the flag named in the table: `CanSpawn` and `CanPlace` report whether this mod
copy declared it, and every call enforces it again (false with a log otherwise). The policy already
refuses those flags on `ClientOnly`/`Cosmetic`, so only state-bearing mods may spawn or place.

Invalid prefab ids, non-finite positions, an out-of-world session, a missing permission or an adapter
rejection (unknown or non-`BuildingEntity` prefab) return false and are logged; a non-entity prefab
created by the adapter is destroyed, never left as a local-only ghost. No new `NetMsg` is used by any
of these surfaces.

## Native operations

```csharp
if (context.NativeApi.CanAccess)
{
    // The generic operation registry (Game Adapter-curated, not arbitrary reflection)
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

- **Scope**: `IModNativeApi` is a permission-gated registry of named native operations. The Runtime
  never exposes arbitrary reflection or direct access to game assemblies; only the Game Adapter
  registers operations, and only those operation ids are invokable.
- **Permission**: invoking requires `ModPermission.AccessNativeApi`. `CanAccess` reflects the declared
  flag; every invoke method also enforces it (false with a log otherwise).
- **Safe value surface**: arguments and results are restricted to `null`, strings, numeric primitives,
  capped `byte[]` and primitive arrays, and framework DTO types (currently
  `IModNativeLocalPlayerState`). Unity objects, game-assembly objects and arbitrary object graphs are
  refused before and after the Game Adapter seam — they never cross to a mod.
- **Registered operation in this slice**: `local.player.state`
  (`ModNativeApiOperations.LocalPlayerState`) returns the local player body's position, vitals,
  consciousness and derived alive/conscious flags as `IModNativeLocalPlayerState`. It is read-only and
  local-only: no wire message, no authority change.
- **Policy boundary**: the first slice is deliberately read-only. Write and native-mutation operations
  are not registered until a concrete consumer exists and its sync/authority boundary is designed —
  the explicit escape-hatch policy decision: a curated allowlist, never open reflection.

## Runtime mod data

```csharp
// Local-only presentation/config/debug state: never leaves this process.
if (context.Data.TryDeclare("settings", ModDataScope.LocalOnly))
{
    context.Data.TrySet("settings", myBytes);
    context.Data.TryGet("settings", out var current);
}

// Shared state: the host owns the value; a guest keeps a mirror only after
// applying a host-originated value received over context.Network.
if (context.Data.TryDeclare("score", ModDataScope.Shared))
{
    context.Data.TrySet("score", scoreBytes);                  // host only
    context.Network.Broadcast(scoreBytes);                     // mod-owned payload/serialization
    context.Data.TryApplyShared("score", payload, senderSteamId); // guest, in MessageReceived
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

- **Scope**: `IModData` is a per-mod, process-local, **ephemeral** runtime store. It is not `IModState`
  and not a generic snapshot service. The mod declares each slot's scope once and then reads/writes
  opaque `byte[]` values with the same caps as the durable state store (key ≤128, value ≤64 KiB,
  ≤1024 slots per mod).
- **No persistence and no automatic sync**: values exist only for the current process. Durable values
  belong in `IModState`; cooperative gameplay facts belong in CUO's typed kernel domains. The framework
  never sends a runtime data value — shared mirrors are applied explicitly by the mod from a value it
  received over `IModNetwork`, so there is no hidden JToken/JObject snapshot protocol.
- **Scopes**: `LocalOnly` — every network mode; any role may set/get/remove. `Shared` — only
  state-bearing modes (`Synchronized`, `Authoritative`, `RequiresAllPlayers`) and only when the mod
  declares `SendNetworkMessage` (the transport that makes a mirror meaningful); the host is the only
  writer and guests call `TryApplyShared` with the session host's SteamId to store a local mirror.
  `HostAuthoritative` — state-bearing modes plus `HostOnly`; the host is the only writer and reader in
  the framework store, guests get no mirror and must coordinate through `IModCommands` / `IModNetwork`.
- **Role gates**: `TrySet` and `TryRemove` on `Shared`/`HostAuthoritative` slots require the host role.
  `TryApplyShared` requires a guest copy, a `Shared` slot and a sender that equals the session host.
  These checks are logged and return false; nothing is silently ignored.
- **Migration mapping**: CUCoreLib's ad-hoc custom data and snapshot modules map to this seam by
  declaring a scope and then using the existing typed `IModNetwork` / `IModCommands` surfaces for
  transport. Do not port a generic JObject snapshot registry.

## Runtime mod status

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
    context.StatusTransport.TryBroadcastLimbStatus("bleeding", playerSteamId, limbSlot, payload); // host only
    context.Network.MessageReceived += (sender, payload) =>
    {
        if (context.StatusTransport.TryHandleStatusPayload(sender, payload))
        {
            return; // other mod-message traffic continues here
        }
    };
}
```

- **Scope**: `IModStatusRuntime` is the per-mod runtime counterpart to static `ModStatusDefinition`
  content. Values are ephemeral, process-local and keyed by `(status id, player SteamId, optional limb
  slot)`. The mod owns the byte payload schema and version.
- **Scopes**: the same `ModDataScope` rules as `IModData` — `LocalOnly` for any role, `Shared` for
  host-write plus explicit guest apply, `HostAuthoritative` for host-only with no guest mirror.
- **Typed transport**: `IModStatusTransport` publishes committed shared values as versioned
  `ModStatusUpdate` frames over the existing `IModNetwork` channel. The host calls
  `TryBroadcastBodyStatus` / `TryBroadcastLimbStatus` (and the remove overloads); every side calls
  `TryHandleStatusPayload` from its mod-message handler so guest mirrors are applied and removed
  automatically from a host-originated frame. The host consumes its own broadcast echo without
  re-applying.
- **Guest request path**: this seam adds no framework command. A guest that needs the host to change a
  shared or host-authoritative status still uses `IModCommands`; the host command handler is the
  semantic validator and then calls a `TryBroadcast*` helper to publish the committed result.
- **Typed projection**: `TryDeclare` accepts an optional `ModStatusProjectionKind` (`BodyFormula` or
  `LimbPhysiology`). When set, the mod's opaque status value should be the matching typed DTO
  (`ModBodyFormulaProjection` / `ModLimbProjection`); the GameAdapter decodes only those well-known
  payloads and applies additive overlays to the local vanilla `Body`/`Limb` after their native
  updates. The mod still owns the payload bytes and serialization, and no game or Unity type crosses
  Abstractions.
- **Projection scope**: body fields are `MaxEncumbrance`, `TotalEncumbrance`, `Immunity`, `JumpSpeed`,
  `AveragePain`, plus `HeartRateOffset`, `RespiratoryRateOffset` and `BloodPressureOffset`; limb fields
  are `BleedAmount`, `SkinHealth`, `MuscleHealth` and `InfectionAmount`. The circulation offsets are
  applied through a dedicated `Body.HandleCirculation` prefix/postfix seam: the previous offset is
  removed before the native formula and the current offset is reapplied after it, so those continuously
  recomputed values stay at native base plus mod offset instead of being erased every frame. The
  vanilla moodle row is fed by `ModStatusMoodleProjection` through `MoodleManager.AddAllMoodles`
  prefix/postfix patches, not an additive body overlay. Limb-scoped statuses can opt into one row per
  affected limb with `ModStatusDefinition.ShowPerLimbMoodles`, route limbs to distinct moodle
  descriptors with `LimbMoodles`, and use the moodle-level `LimbDisplayNameFormat` /
  `LimbDescriptionFormat` templates for limb-aware tooltip text.
- **Local moodle resolver**: `IModMoodleRuntime` lets a mod register one resolver per runtime status id.
  The resolver receives a plain `ModStatusMoodleRequest` (status/player/limb identity plus the mod-owned
  payload) and returns a static moodle id. The GameAdapter's local moodle-row projection calls it for
  each active body/limb presence and falls back to the static status/moodle routing when the resolver is
  absent or returns null. This is the CUO-safe replacement for CUCoreLib's `RegisterBody` /
  `RegisterLimb` callbacks: no `Body`/`Limb`/game delegate crosses Abstractions, and it is local-only
  presentation with no wire message.
- **Boundary**: opaque `None` statuses are never interpreted by the GameAdapter — only body/limb
  projection statuses reach the vanilla layer, and the store change event is internal. No new `NetMsg`
  and no protocol bump: the typed frames ride the existing `NetMsg.ModMessage` channel, and no generic
  JObject snapshot is introduced.

## Resource completion

`IModResourceCompletion` (`context.ResourceCompletion`) lets a mod register its own completion stages
for the console's resource-location argument (`CommandArgumentKind.ResourceLocation`). It is the
console's completion extension point promoted to the mod surface: a stage receives a
`ResourceLocationEntry` (canonical id, content kind and the owning source's display name) and answers
exactly one question — does this entry match this prefix?

```csharp
[CuoMod("cuo.pinyinsearch", "Pinyin Search", "0.1.0", NetworkMode = NetworkMode.ClientOnly,
    NativeBinding = "PlayerCamera.RefreshRecipeList + Recipe.simpleName getter")]
public sealed class PinyinSearchMod : ICuoMod
{
    public void Bind(IModContext context) =>
        context.ResourceCompletion.TryRegisterMatchStage("pinyin", new PinyinSearchStage());

    public void Initialize() { } public void Start() { } public void Update() { }
    public void Stop() { } public void Dispose() { }
}

internal sealed class PinyinSearchStage : IResourceLocationMatchStage
{
    public bool Matches(ResourceLocationEntry entry, string prefix) =>
        entry.DisplayName.Length > 0 && PinyinMatcher.Contains(entry.DisplayName, prefix);
}
```

- **Additive by construction**: the catalog consults a registered stage only after its four built-in
  ranks (exact canonical id, id prefix, bare path prefix, display-name prefix) and ranks the stages
  behind them in registration order, so a stage can only add candidates — it never displaces what the
  framework already completes. `ResourceLocationCatalog.MaxSuggestions` (20) stays the catalog's own
  cap, and an accepted suggestion is always the canonical id.
- **Per-mod and local**: the stage table belongs to the registering mod (the same id in two mods is two
  registrations), lives in the local process and is never sent anywhere — the console completes on the
  client the player is typing on, so a stage never needs to exist on the host.
- **No permission flag**: a stage can only widen what the local player's own console offers, so
  registration is open to every mod. `ModPermission` gates the surfaces that change shared or other
  players' state, not this one.
- **Lifetime and query snapshot**: the table lives as long as the process (a mod is discovered once per
  process, like its content registrations) and `TryUnregisterMatchStage` is the only way a stage leaves
  it. A completion query ranks the stages that were registered when it started, so a stage that
  registers or unregisters while it is being asked changes the next query, not the running one. (A
  stage that calls back into the catalog's own query from inside `Matches` is the mod's own recursion —
  the catalog promises nothing there.)
- **Rails**: a mod may hold at most 8 stages. A null stage, a blank id, an id longer than 128 characters,
  a duplicate id and the cap all return `false` and are logged (`[ContentId]`);
  `TryUnregisterMatchStage` only ever removes the calling mod's own stage.
- **Failure isolation**: the catalog contains a stage that throws — that entry counts as "no match",
  the remaining stages still run, and the failure is logged at debug, because this path runs per
  keystroke per entry. A mod's exception never breaks the console.
- **Stability**: `IModResourceCompletion`, `IResourceLocationMatchStage` and `ResourceLocationEntry` are
  `Experimental` (`docs/api/advanced-modification-policy.md`); the shipped pinyin search mod is the
  first consumer and the worked example
  (`src/CasualtiesUnknownOnline.PinyinSearch.Core/PinyinSearchMod.cs`).

## Handshake consistency

The guest's declared mod list rides the handshake (`HandshakeMsg.Mods`). The host validates before the
member is created:

| Host has | Guest has | Verdict |
|---|---|---|
| `RequiresAllPlayers` / `Synchronized` / `Authoritative` | missing, SemVer-precedence-unequal, or permission-unequal | **reject** |
| any mode | same id but a different `NetworkMode` while either side is state-bearing | **reject** |
| `HostOnly` | missing | pass (host-side logic) |
| `ClientOnly` / `Cosmetic` | missing or a different version | pass (local surfaces) |
| — | claims `RequiresAllPlayers` / `Synchronized` / `Authoritative` the host lacks | **reject** |
| — | malformed list (empty or duplicated id, invalid mode/permissions, unparseable state-bearing version) | **reject** |
| discovery not yet run | anything | **"pending" refusal** — the guest's 1 s retry re-runs the check |
| any mode | same mod id, a **different declared native binding** (a declaration against none included) | **allow / warn / reject** by the host's `NativeBindingParity` rule: allow is silent, warn (the default) admits the member and records the mismatch, require rejects — naming the mod and both declarations |

**Native-binding parity** is judged apart from those rows because it is a declared fact, not a network
contract. Each `ModInfoMsg` carries the mod's `NativeBinding`; the host compares it with its own
declaration **per mod id**, and only for a mod both sides list — a mod only one side lists has no
counterpart to compare, and the rows above already decide who may lack what. A blank declaration is
"none" on both sides (the same normalization discovery applies), so a blank and an absent declaration
are the same answer, while a declaration against none is a difference. The rule is the host's
`HostRules` → `NativeBindingParity` entry (`allow` / `warn` / `require`, default `warn`), editable on
the Online UI's admin page and through the console's host-rule command. Warn is the default because the
declaration is new: a host that refused by default would lock out every session whose host updated
first, and an undeclared binding is invisible anyway. The comparison is exact after trimming (ordinal,
case-sensitive): two spellings that differ only in surrounding whitespace match, two casings do not —
a third-party author must spell the binding identically to pass a `require` host.

What parity proves: for a mod both sides list, the two sides' declarations agree, so a host that
requires it knows every admitted member either declared the same binding or was refused. What it does
**not** prove: an undeclared binding is undetectable (CUO takes no anti-cheat stance,
`docs/api/advanced-modification-policy.md` §4), so a mod that binds the game without declaring it
passes every check; and an equal declaration does not prove equal behaviour — the same name may cover
different patches. A host that chooses `allow` carries the risk knowingly, and a `warn` mismatch leaves
the log line as its record.

Versions are strict SemVer, and for state-bearing modes the comparison is **precedence equality**
(build metadata ignored). Compatibility ranges are not inferred; the surface they would have to be
checked against is the stability levels in `docs/api/advanced-modification-policy.md` and the reviewed
public surface in `docs/api/abstractions-api-baseline.txt`, enforced by `ApiSurfaceGateTests`.

## The layout of a mod

```text
BepInEx/plugins/MyMod/MyMod.dll        <- BepInEx loads this (the shell)
```

The shell (`[BepInPlugin]` plus an empty `BaseUnityPlugin`), the `[CuoMod]` class and the manifest
metadata travel in ONE assembly. Copy the example: `src/CasualtiesUnknownOnline.ModExample/` registers
`echo`/`whoami` commands and remains the two-process verification target.

## Versioning discipline

- The current wire version is whatever `ProtocolVersion.Current` declares
  (`src/CasualtiesUnknownOnline.Runtime/Protocol/ProtocolVersion.cs`). That constant's own comment is
  the wire-change log — this page deliberately names no number, and a gate fails a live governance
  document that restates the value.
- Behavioral wire changes bump `ProtocolVersion.Current`; local-only or read-only mod surfaces that add
  no wire change do not.
- Mod versions are strict SemVer strings, validated at discovery and compared by precedence for
  state-bearing modes.
- The 64 KiB cap is a policy constant (`ModChannel.MaxPayloadBytes`); raising it is a protocol-adjacent
  decision, not a wire format change.

## How you know it works

Every mod behaviour above is covered by pure-managed tests over the production stack
(`tests/.../Mods/`): discovery and dependency ordering, permission policy, SemVer, lifecycle, message
routing and the permission/rate gates, host commands, mod-state saves, the local mod UI, content
registration and the content catalog, runtime mod data and status, the building runtime hooks, read game state, entity/item spawn,
tile placement, the native API and its GameAdapter contract, the handshake matrix, the rate limiter,
direction rows and wire round-trips.

Green tests are not a session. The example mod doubles as the two-process verification target: deploy
it to both machines and join — the logs show `[Mods] discovered …`, the handshake admitting the pair,
the guest → host command results and the echo round-trip. Two real clients are the user's acceptance
run.

## Related reading

- [The life of a mod](../internals/mod-loading-lifecycle.md) — the load order, the pump and the failure isolation
- [Permissions and what they enforce](../internals/permissions-and-security.md) — what a declaration buys, and what CUO refuses to defend
- [Your first mod](../start/your-first-mod.md) — the smallest working mod, built step by step
- [Send a message to the other players](../how-to/send-a-network-message.md) — the network surface in use
- [Register content](../how-to/register-content.md) — the content seam in use
- [Glossary](glossary.md) — permission, network mode, native binding, content id, schema version

---

[Documentation](../README.md) > [Reference](README.md) > The mod API contract
