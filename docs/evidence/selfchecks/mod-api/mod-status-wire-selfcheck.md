# Mod status runtime domain — phase 2 typed status transport self-check

Owner cycle: CUCoreLib migration support / mod-status domain.

Decision: after the phase-1 runtime status table, add a typed status transport
seam that carries committed shared status values over the existing
`IModNetwork` mod-message channel. It does not introduce a new NetMsg, a
protocol bump, or a generic JObject snapshot. Guest-to-host change requests
remain explicit `IModCommands` semantics, not a new framework request protocol.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed wire DTO | `ModStatusUpdate` in `CUO.Abstractions`: status id, body/limb scope, player id, limb slot, schema version, opaque value, remove flag, `ToPayload`/`FromPayload`. |
| 2 | Transport interface | `IModStatusTransport` in `CUO.Abstractions`: host broadcast set/remove for body/limb + `TryHandleStatusPayload` for guest mirror handling. |
| 3 | Runtime implementation | `ModStatusTransport` in Runtime wraps `IModStatusRuntime` + `IModNetwork`, builds typed frames and routes them; no new message id. |
| 4 | Guest removal path | `TryApplyRemoveBodyStatus` / `TryApplyRemoveLimbStatus` added to `IModStatusRuntime` so a host removal can clear a guest mirror while leaving the declaration. |
| 5 | Authority split | Host writes + broadcasts; guest applies only host-originated frames; host consumes its own broadcast echo without re-applying; host-authoritative/local-only scopes are refused by the broadcast helpers. |
| 6 | Existing transport | The frames ride `NetMsg.ModMessage` through `IModNetwork`, so the existing permission, role, session, rate-limit, 64 KiB, and star-topology rails apply unchanged. |
| 7 | No vanilla integration | The transport only moves opaque mod payloads; no `Body`/`Limb`/Unity type crosses Abstractions. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModStatusRuntime` | Added host-removal apply methods for guest mirrors. |
| `IModStatusTransport` | New Abstractions interface. |
| `ModStatusUpdate` | New Abstractions typed wire DTO. |
| `ModStatusTransport` | New Runtime per-mod transport implementation. |
| `IModContext` / `ModContext` | Added `StatusTransport` property and wired the transport into each mod context. |
| Tests | `ModStatusUpdateTests`, `ModStatusWireTests`, and new removal paths in `ModStatusRuntimeTests`. |
| `docs/architecture/mod-status-domain.md` | Updated from phase 1 to phases 1–2 landed. |
| `docs/api/mod-api.md` | Updated §4k with typed transport example and guest-request command semantics. |
| Protocol version | Unchanged (no new NetMsg). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DTO body round-trip | `ModStatusUpdate.ForBody` preserves status id, scope, player, schema, value | `ModStatusUpdateTests.BodySetRoundTrip_PreservesKeyScopeSchemaAndValue` |
| DTO limb removal round-trip | `ModStatusUpdate.RemoveLimb` preserves limb slot and remove flag | `ModStatusUpdateTests.LimbRemoveRoundTrip_PreservesLimbSlotAndRemoveFlag` |
| Invalid payload discrimination | Non-status bytes return null and are not consumed by the transport | `ModStatusUpdateTests.InvalidPayload_ReturnsNull`, `ModStatusWireTests.TryHandleStatusPayload_ReturnsFalseForNonStatusPayload` |
| Host broadcast body status | Host commit + guest mirror apply through the real mod-message channel | `ModStatusWireTests.SharedBodyStatus_HostBroadcast_WritesAuthorityAndAppliesGuestMirror` |
| Host broadcast removal | Host removal clears guest mirror and leaves the declaration | `ModStatusWireTests.SharedBodyStatus_HostBroadcastRemove_ClearsGuestMirrorButKeepsDeclaration` |
| Host broadcast limb status | Limb set/remove routes to the limb table | `ModStatusWireTests.SharedLimbStatus_HostBroadcastSetAndRemove_AppliesToGuestLimbMirror` |
| Scope refusal | Local-only and host-authoritative statuses are not published by the shared-status seam | `ModStatusWireTests.Broadcast_RefusesNonSharedScopes` |
| Guest broadcast refusal | A guest cannot publish shared status frames | `ModStatusWireTests.GuestCannotBroadcastSharedStatus` |
| Guest apply-remove rules | Guest can remove only with host sender; host does not apply mirrors | `ModStatusRuntimeTests.Shared_BodyStatus_GuestCanApplyHostRemoval_AndRefusesNonHostRemoval` |
| Host self-echo | Host consumes its own broadcast echo without treating it as a guest apply | `ModStatusWireTests.SharedBodyStatus_HostBroadcast_WritesAuthorityAndAppliesGuestMirror` (host `Received` consumed entry) |
| No Abstractions leak | DTO/interface contain no Unity/game type | full build |
| No wire change | Protocol version still `1`, no new NetMsg entry | full build |

## 4. Verification design

- Pure-managed DTO round-trip tests.
- Pure-managed two-node tests over the real session stack
  (`TestNode.CreatePair`) with a test mod that routes inbound mod-message
  frames to `TryHandleStatusPayload`, exercising host→guest broadcast,
  guest mirror apply/remove, scope rejection, and non-status payload routing.
- Static evidence: no new `NetMsg`, no protocol version bump, no JObject
  snapshot, no CUCoreLib source committed.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2056 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
