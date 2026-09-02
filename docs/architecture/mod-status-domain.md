# Mod Status Runtime Domain Boundary

Status: Design proposal with phase 1 landed (Runtime status table + typed API,
no vanilla integration, no wire).

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

### 4.3 Proposed Abstractions surface (not yet implemented)

```csharp
public interface IModStatusRuntime
{
    bool TryGetBodyStatus(string statusId, ulong playerSteamId, out byte[]? value);
    bool TryGetLimbStatus(string statusId, ulong playerSteamId, int limbSlot, out byte[]? value);

    bool TrySetBodyStatus(string statusId, ulong playerSteamId, byte[] value);
    bool TrySetLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value);

    bool TryApplyBodyStatus(string statusId, ulong playerSteamId, byte[] value, ulong senderSteamId);
    bool TryApplyLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value, ulong senderSteamId);

    bool TryGetSchemaVersion(string statusId, out int schemaVersion);
    IReadOnlyCollection<string> StatusIds { get; }
}
```

Semantics:

- `TrySet*`: host-only for shared/gameplay-affecting statuses; local-only
  statuses may be written by any role.
- `TryApply*`: guest-only mirror apply; requires the sender to be the session
  host and the status to be shared/authoritative.
- The mod owns serialization; only opaque bytes cross the boundary.

### 4.4 Scope split

| Scope | Runtime behavior | Transport |
|---|---|---|
| Local-only status | Local per-process value, any role, no wire | none |
| Shared status | Host owns authoritative value; guest keeps an explicit mirror | existing `IModNetwork` / `IModCommands`, or future dedicated `ModStatusUpdated` event |
| Host-authoritative status | Host-only table, guest has no mirror; guest requests through commands/messages | host decision + directed result or broadcast |

## 5. Authority and sync rules

1. **Local compute stays local.** A player's own presentation/cosmetic status
   does not need host arbitration.
2. **Gameplay-affecting statuses are host-committed.** If a status changes a
   body formula, a limb effect, or any shared simulation fact, the host owns
   the committed value; guests report/request, never silently mutate the
   authoritative table.
3. **Discrete events, not snapshots.** If a dedicated wire path is added, it is
   a typed `ModStatusUpdated` message carrying the stable key + payload +
   schema version, not a JObject full snapshot.
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
| `IModNetwork` / `IModCommands` | Transport/authority for status updates after the status service is added. |
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
2. **Host commands / dedicated update message**
   - If real mods need network status updates, add a dedicated
     `ModStatusUpdatedMsg` or route through `IModCommands`.
   - Keep payload opaque; bump protocol only if a new message is required.
3. **GameAdapter vanilla projection**
   - Add a narrow GameAdapter seam to apply status values to body/limb
     behavior.
   - Add a local/UI moodle feed using the existing static moodle descriptors.
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
