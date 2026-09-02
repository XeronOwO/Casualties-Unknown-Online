# Runtime mod data scope seam — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support / mod-data sync model first seam.

Decision: land a scope-declared, process-local runtime data surface
(`IModData` / `ModDataScope`) that makes the local-only vs shared vs
host-authoritative boundary explicit. It deliberately does **not** add a
generic JObject snapshot protocol or a new wire message: shared mirrors are
applied explicitly from a value the mod already received over the existing
`IModNetwork` / `IModCommands` surfaces.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Scope enum | `ModDataScope` in `CUO.Abstractions` (`LocalOnly`, `Shared`, `HostAuthoritative`). |
| 2 | Runtime API | `IModData` in `CUO.Abstractions`: declare, read, write, apply-shared, remove, scope/schema inspection, keys, count. |
| 3 | Store | `ModDataStore` in Runtime owns the ephemeral per-mod slot table and defensive copies; no persistence, no wire. |
| 4 | Policy | `ModDataPolicy` centralizes key/value/slot caps, schema version validation, and network-mode scope eligibility. |
| 5 | Scope rules | `LocalOnly` is universal; `Shared` requires a state-bearing mode + `SendNetworkMessage`; `HostAuthoritative` requires a state-bearing or host-only mode. |
| 6 | Host/guest gates | `TrySet`/`TryRemove` on shared/host-authoritative slots are host-only; `TryApplyShared` is guest-only, requires `Shared` scope, and requires the session host as sender. |
| 7 | Transport | No automatic replay. The mod still owns sending/receiving values over `IModNetwork` / `IModCommands`; the runtime store only mirrors what the mod explicitly applies. |
| 8 | No wire | No new NetMsg, no protocol version change, no generic snapshot channel. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModDataScope` | New Abstractions enum. |
| `IModData` | New Abstractions interface. |
| `ModDataPolicy` | New Runtime policy/safety rails. |
| `ModDataStore` | New Runtime ephemeral store + per-mod adapter. |
| `IModContext` | Added `Data` property. |
| `ModContext` / `ModLifecycle` / `ModService` | Wired the runtime data store into the per-mod context. |
| Tests | `ModDataTests`, `TestDataMod`, `TestClientOnlyDataMod`. |
| `docs/api/mod-api.md` | Added §4j runtime mod data scope seam. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Local-only independence | Host and guest local values are separate and do not cross the wire | `ModDataTests.LocalOnly_IsIndependentBetweenHostAndGuest` |
| Defensive copies | Writes/reads clone byte arrays | `ModDataTests.LocalOnly_AnyRoleCanReadWriteRemove_AndCopiesAreDefensive` |
| Shared host-write / guest-apply | Host writes; guest applies only a host-originated value; guest cannot write/remove | `ModDataTests.Shared_HostWritesGuestApplies_GuestCannotWriteOrRemove` |
| Host-authoritative visibility | Only host can read/remove; guest has no mirror and cannot apply as shared | `ModDataTests.HostAuthoritative_IsVisibleOnlyOnHostAndCannotBeAppliedByGuests` |
| Declaration required | Set/get/scope on undeclared keys are refused; duplicate/invalid declarations refused | `ModDataTests.Declaration_IsRequiredAndDuplicateOrInvalidDeclarationsAreRefused` |
| Value caps | Over-cap values refused without silent truncation | `ModDataTests.ValueCaps_AreEnforcedWithoutSilentTruncation` |
| ClientOnly scope rejection | ClientOnly cannot declare `Shared` or `HostAuthoritative` | `ModDataTests.ClientOnly_CannotDeclareSharedOrHostAuthoritative` |
| No Abstractions leak | API is plain enum/interface + `byte[]`; no Unity/game type | `docs/api/mod-api.md`, full build |
| No new wire | No new NetMsg / protocol version unchanged | `git diff`, full build |

## 4. Verification design

- Pure-managed tests over the real session stack (`TestNode.CreatePair`) for
  host/guest role behavior.
- Policy tests for local-only mode scope rejection.
- Static evidence: no new protocol message, no automatic syncing code path,
  no CUCoreLib source committed.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2039 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
