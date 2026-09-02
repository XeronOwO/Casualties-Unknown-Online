# Mod status runtime domain — phase 1 self-check

Owner cycle: CUCoreLib migration support / mod-status domain.

Decision: land the phase-1 runtime status table and typed Abstractions API
(`IModStatusRuntime`) without vanilla Body/Limb integration and without a new
wire protocol. The design boundary is documented in
`docs/architecture/mod-status-domain.md`.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Runtime API | `IModStatusRuntime` in `CUO.Abstractions`: body/limb get/set/apply/remove, scope/schema queries, status id enumeration. |
| 2 | Store | `ModStatusStore` in Runtime owns the ephemeral per-mod/per-player/per-limb value table and defensive copies. |
| 3 | Policy | `ModStatusPolicy` centralizes id/value/slot/schema caps and network-mode runtime scope eligibility. |
| 4 | Scope rules | `LocalOnly` universal; `Shared` requires state-bearing mode + `SendNetworkMessage`; `HostAuthoritative` requires state-bearing or host-only mode. |
| 5 | Host/guest gates | `TrySet*`/`TryRemove*` on shared/host-authoritative are host-only; `TryApply*` is guest-only, shared-only, and requires the session host as sender. |
| 6 | No vanilla integration | The store only keeps opaque mod payloads; `Body`/`Limb`/`MoodleManager` never cross Abstractions. |
| 7 | No automatic sync | Guest mirrors are applied explicitly from a host-originated value; no generic snapshot protocol and no new NetMsg. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModStatusRuntime` | New Abstractions interface. |
| `ModStatusPolicy` | New Runtime safety/scope policy. |
| `ModStatusStore` | New Runtime ephemeral status store + per-mod adapter. |
| `IModContext` | Added `StatusRuntime` property. |
| `ModContext` / `ModLifecycle` / `ModService` | Wired the status store into the per-mod context. |
| Tests | `ModStatusRuntimeTests`. |
| `docs/architecture/mod-status-domain.md` | Updated from design proposal to phase-1 landed. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Local-only body status | Any role can read/write; copies are defensive | `ModStatusRuntimeTests.LocalOnly_BodyStatus_AnyRoleCanReadWriteRemove_AndCopiesAreDefensive` |
| Local-only independence | Host/guest values are separate | `ModStatusRuntimeTests.LocalOnly_BodyStatus_IsIndependentBetweenHostAndGuest` |
| Shared status | Host writes; guest applies only host-originated values; guest cannot write/remove | `ModStatusRuntimeTests.Shared_BodyStatus_HostWritesGuestApplies_GuestCannotWriteOrRemove` |
| Host-authoritative status | Host-only visibility; guest has no mirror and cannot apply as shared | `ModStatusRuntimeTests.HostAuthoritative_BodyStatus_IsVisibleOnlyOnHost` |
| Body/limb separation | Limb API refuses body-scoped statuses and vice versa | `ModStatusRuntimeTests.LimbStatus_RequiresLimbDeclarationAndValidSlot` |
| Declaration/caps | Undeclared/duplicate/invalid/over-cap values refused | `ModStatusRuntimeTests.Declaration_IsRequiredAndInvalidDeclarationsAreRefused` |
| ClientOnly scope rejection | ClientOnly cannot declare shared or host-authoritative statuses | `ModStatusRuntimeTests.ClientOnly_CannotDeclareSharedOrHostAuthoritativeStatus` |
| No Abstractions leak | No Unity/game type in the public status API | `docs/api/mod-api.md`, full build |
| No wire | No new NetMsg / protocol version unchanged | full build, selfcheck |

## 4. Verification design

- Pure-managed tests over the real session stack (`TestNode.CreatePair`) for
  host/guest role behavior.
- Body/limb scope separation tests, invalid slot tests, and local-only mode
  scope rejection.
- Static evidence: no new protocol message, no vanilla `Body`/`Limb`
  reference, no CUCoreLib source committed.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2046 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
