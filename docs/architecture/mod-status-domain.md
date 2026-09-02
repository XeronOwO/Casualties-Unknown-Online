# Mod Status Runtime Domain Boundary

Status: Design with phases 1–2 and phase 3's GameAdapter projection slices
landed (Runtime status table + typed API + typed status transport over
the existing mod-message channel + typed body/limb projection DTOs and a
GameAdapter vanilla overlay + circulation-target formula patch; no dedicated
NetMsg, no JObject snapshot).

This document answers the remaining CUCoreLib migration question for dynamic
statuses: when a mod wants per-player or per-limb status runtime values, where
does that state live, who owns it, and how does it travel without becoming a
generic snapshot protocol?

## 1. Problem

CUCoreLib exposes a typed status model around vanilla game objects:

- Mods define `BodyStatus` / `LimbStatus` classes and attach instances to the
  vanilla `Body` / `Limb` objects.
- `GetStatus<T>()` performs lazy creation and stores instances in per-body /
  per-limb collections.
- Save and network paths serialize those instances as JObject snapshots and
  restore them by reflection (`StatusOptionsAttribute` / type name lookup).
- `MoodleRegistry` builds presentation moodles from those status instances.

CUO cannot copy that model:

- Mods may only reference `CasualtiesUnknownOnline.Abstractions`.
- Abstractions cannot contain `Body`, `Limb`, Unity, or game-assembly types.
- CUCoreLib-style JObject network snapshots are explicitly non-goals.
- CUO's kernel is typed, dependency-free, and does not know mod schemas.

CUO already has the static half:
`ModStatusDefinition` + `ModStatusScope` + `GameAdapterStatusContentProvider`.
The dynamic half still needs a host-authoritative runtime boundary.

## 2. Constraints

| Constraint | Impact |
|---|---|
| Abstractions only for mods | The mod-facing runtime API must use primitive/BCL-safe shapes and opaque mod-owned payloads. |
| No Unity/game types in Abstractions | No `Body`, `Limb`, `Sprite`, or `MoodleManager` in the public API. |
| GameState is dependency-free | Arbitrary mod status blobs must not become a GameState kernel domain; the kernel cannot reference Abstractions or mod schemas. |
| No generic JObject snapshot | Runtime status sync must be either host-owned explicit state or dedicated discrete events, never a reflection-based full-snapshot registry. |
| Host authority / local compute | The host owns committed shared status facts; local per-player simulation remains single-player-feel, but divergence is not tolerated for gameplay-affecting values. |
| Static content off wire | Status/moodle descriptors remain static content; only runtime values may need wire. |

## 3. Decision

**Do not extend the Players kernel domain.**

The Players domain owns native terminal facts: alive/conscious, limb/body
latches, skills, carry relations, and cross-player result facts. It is typed,
closed, and already has a service table. Mod statuses are an open-ended,
mod-owned schema family; putting them there would make the kernel depend on
mod-specific payload interpretation and would violate the typed-domain rule.

**Do not put arbitrary mod status blobs into GameState.**

GameState has no project references. A generic `Dictionary<string, byte[]>`
status bag in the kernel would be a non-typed backdoor. The existing typed
kernel is not the right home for mod-defined schemas that the kernel cannot
validate.

**Do create a Runtime-level mod-status domain service.**

The host-authoritative boundary belongs in the Runtime mod layer, near
`ModService`, `ModStateStore`, and `ModDataStore`. It is a dedicated
per-mod, per-player, per-limb status table with:

- an explicit mod-facing API in Abstractions (typed by status id, player id,
  limb slot, schema version);
- mod-owned opaque payloads, with defensive copies and policy caps;
- host-authoritative writes for shared status facts;
- explicit guest request/apply paths through existing typed command/message
  surfaces;
- a GameAdapter projection seam for any vanilla body/limb effect.

This is not a generic snapshot service. The domain service does not invent a
JToken channel; it defines a small set of dedicated operations and lets mods
carry their own payload schema behind stable IDs.

## 4. Proposed model

### 4.1 State key

```text
(ModId, StatusId, PlayerSteamId, LimbSlot?)
```

- `ModId`: the owning mod, from `[CuoMod]`.
- `StatusId`: the stable id from `ModContentKind.Status`.
- `PlayerSteamId`: the player whose body/limb carries the status.
- `LimbSlot`: null/false for body-level scope; the vanilla limb index
  (or an abstraction-level stable limb slot) for limb scope.

The value is a mod-owned byte payload plus a mod-owned schema version. The
framework never interprets it.

### 4.2 Runtime table

```text
ModStatusStore
├── BodyStatusTable    (ModId, StatusId, PlayerSteamId) -> payload
└── LimbStatusTable    (ModId, StatusId, PlayerSteamId, LimbSlot) -> payload
```

Projections may be read by:

- the owning mod through `IModStatusRuntime`;
- GameAdapter through an internal read-only seam when it needs to apply a
  vanilla effect;
- future local UI through the same projection.

### 4.3 Landed Abstractions surface

The exact shape lives in `IModStatusRuntime` and `IModStatusTransport`; the
key operations are:

```csharp
public interface IModStatusRuntime
{
    bool TryDeclare(string statusId, ModStatusScope scope, ModDataScope runtimeScope, int schemaVersion = 1);
    bool TryGetBodyStatus(string statusId, ulong playerSteamId, out byte[]? value);
    bool TryGetLimbStatus(string statusId, ulong playerSteamId, int limbSlot, out byte[]? value);
    bool TrySetBodyStatus(string statusId, ulong playerSteamId, byte[] value);
    bool TrySetLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value);
    bool TryApplyBodyStatus(string statusId, ulong playerSteamId, byte[] value, ulong senderSteamId);
    bool TryApplyLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value, ulong senderSteamId);
    bool TryApplyRemoveBodyStatus(string statusId, ulong playerSteamId, ulong senderSteamId);
    bool TryApplyRemoveLimbStatus(string statusId, ulong playerSteamId, int limbSlot, ulong senderSteamId);
    bool TryRemoveBodyStatus(string statusId, ulong playerSteamId);
    bool TryRemoveLimbStatus(string statusId, ulong playerSteamId, int limbSlot);
    bool TryGetScope(string statusId, out ModStatusScope scope);
    bool TryGetRuntimeScope(string statusId, out ModDataScope runtimeScope);
    bool TryGetSchemaVersion(string statusId, out int schemaVersion);
}

public interface IModStatusTransport
{
    bool TryBroadcastBodyStatus(string statusId, ulong playerSteamId, byte[] value);
    bool TryBroadcastLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value);
    bool TryBroadcastRemoveBodyStatus(string statusId, ulong playerSteamId);
    bool TryBroadcastRemoveLimbStatus(string statusId, ulong playerSteamId, int limbSlot);
    bool TryHandleStatusPayload(ulong senderSteamId, byte[] payload);
}
```

Semantics:

- `TrySet*`: host-only for shared/gameplay-affecting statuses; local-only
  statuses may be written by any role.
- `TryApply*` / `TryApplyRemove*`: guest-only mirror apply; requires the
  sender to be the session host and the status to be shared.
- `TryBroadcast*`: host-only publish of a shared status through the existing
  `IModNetwork` mod-message frame; no dedicated NetMsg.
- The mod owns serialization; only opaque bytes cross the boundary.

### 4.4 Scope split

| Scope | Runtime behavior | Transport |
|---|---|---|
| Local-only status | Local per-process value, any role, no wire | none |
| Shared status | Host owns authoritative value; guest keeps an explicit mirror | typed `ModStatusUpdate` over existing `IModNetwork` (via `IModStatusTransport`); guest requests still use `IModCommands` |
| Host-authoritative status | Host-only table, guest has no mirror; guest requests through commands/messages | host decision + directed result or broadcast (not the status mirror seam) |

## 5. Authority and sync rules

1. **Local compute stays local.** A player's own presentation/cosmetic status
   does not need host arbitration.
2. **Gameplay-affecting statuses are host-committed.** If a status changes a
   body formula, a limb effect, or any shared simulation fact, the host owns
   the committed value; guests report/request, never silently mutate the
   authoritative table.
3. **Discrete events, not snapshots.** The landed transport uses a typed
   `ModStatusUpdate` DTO over the existing `IModNetwork` mod-message frame,
   carrying the stable key + payload + schema version. It is not a JObject full
   snapshot and it did not require a new `NetMsg`.
4. **Static content stays off wire.** The status/moodle descriptors remain
   static content; only runtime values travel (and only if needed).
5. **Persistence remains `IModState`** for durable statuses. The runtime
   status table is ephemeral; a mod that needs a status to survive restart
   writes its own versioned payload through `IModState`, with host-only access.

## 6. Relationship to existing surfaces

| Surface | Role |
|---|---|
| `ModContentKind.Status` / `ModStatusDefinition` | Static descriptor: id, scope, save metadata, moodle link. |
| `ModContentKind.Moodle` / `ModMoodleDefinition` | Static presentation descriptor. |
| `IModData` | Generic per-mod runtime value store; not a per-player/per-limb semantic status bag. |
| `IModState` | Host-persistent durable mod state. |
| `IModNetwork` / `IModCommands` | Existing transport/authority surfaces: typed status frames ride `IModNetwork`; guest change requests remain host-command semantics via `IModCommands`. |
| GameAdapter | Only layer allowed to translate a status into vanilla `Body` / `Limb` effects or a vanilla moodle row. |
| Existing Players kernel domain | Unchanged; native terminal player facts stay kernel-owned. |

## 7. Implementation phases

1. **Runtime table + typed API (no wire) — landed**
   - Added `ModStatusStore` in Runtime, `ModStatusPolicy`, and
     `IModStatusRuntime` in Abstractions.
   - Supports local-only status values and host-owned shared values with
     explicit guest apply (same shape as `IModData` but with player/limb keys).
   - Added pure-managed tests over the session stack
     (`ModStatusRuntimeTests`), selfcheck under
     `docs/evidence/selfchecks/mod-api/mod-status-runtime-selfcheck.md`.
2. **Typed status transport / command seam — landed**
   - Added `ModStatusUpdate` (typed payload DTO), `IModStatusTransport`
     (host broadcast + guest handle) and apply-remove methods on
     `IModStatusRuntime`.
   - Shared status results travel as typed frames over the existing
     `IModNetwork` mod-message channel; no dedicated `NetMsg`, no protocol
     bump, no JObject snapshot.
   - Guest-to-host change requests remain explicit `IModCommands` semantics:
     the host's command handler validates and then publishes the committed
     result with the broadcast helpers.
3. **GameAdapter vanilla projection — body/limb + circulation landed**
   - Added `ModStatusProjectionKind` plus typed `ModBodyFormulaProjection`
     / `ModLimbProjection` DTOs in Abstractions. A mod declares a runtime
     status slot as `BodyFormula` or `LimbPhysiology` and publishes the
     matching typed payload; the framework still treats the value as opaque
     bytes on the wire.
   - Added an internal `ModStatusStore` change event and a GameAdapter-facing
     projection snapshot read seam. `ModStatusVanillaProjection` (GameAdapter
     only) decodes those well-known payloads and applies additive overlays to
     the local `Body`/`Limb` through `Body.Update`/`Limb.Update` postfixes.
   - Body slice covers values that are recomputed from scratch (encumbrance,
     immunity, jump speed, average pain); limb slice covers physiology fields
     that the native limb update already modifies additively (bleed,
     skin/muscle health, infection).
   - Circulation slice adds `HeartRateOffset`, `RespiratoryRateOffset`, and
     `BloodPressureOffset` to `ModBodyFormulaProjection`.
     `ModStatusProjectionPatches` wraps `Body.HandleCirculation` with a
     prefix/postfix pair: the previous offset is removed before the native
     formula runs and the current offset is reapplied after it, so continuously
     recomputed circulation values stay at native base + mod offset without
     being erased each frame. The native readout strings are refreshed after
     the overlay.
   - The vanilla moodle row remains a future seam.
4. **Migration guide**
   - CUCoreLib `GetStatus<T>()` maps to `IModStatusRuntime.TryGet*` with a
     stable status id and opaque mod payload.
   - CUCoreLib JObject save/network snapshots map to `IModState` for durable
     state and `IModStatusRuntime`/`IModCommands` for runtime state.

## 8. Non-goals

- No reflection-based `ConditionalWeakTable<Body, ...>` status bag.
- No generic JObject status snapshot module.
- No `Body` / `Limb` / `Sprite` / `MoodleManager` in Abstractions.
- No extension of the existing Players kernel domain for mod-defined schemas.
- No automatic framework sync of status values without an explicit typed
  transport decision.
