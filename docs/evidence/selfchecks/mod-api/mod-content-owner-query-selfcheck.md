# Mod content owner query — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — the missing mod-facing
`TryGetOwnerModGuid` equivalent for the content registry.

Decision: add a small read-only `IModContentOwnerQuery` surface on
`IModContext`. It resolves a content kind + id to the owning mod id from the
same framework-wide content view the runtime catalog reads, so there is no
second owner table, no new wire/protocol, and no payload interpretation. This
completes the generic replacement for CUCoreLib's per-kind owner queries
without granting mods access to Runtime internals.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Abstractions surface | `IModContentOwnerQuery.TryGetOwner(kind, id, out modId)` — plain read-only method; no game/Unity type and no Runtime reference. |
| 2 | Mod context seam | `IModContext.ContentOwners` exposes the query to every mod alongside the existing `Content` registration surface. |
| 3 | Runtime implementation | `ModContentOwnerQueryAdapter` reads `IModContentControl.Entries`, matches kind+id with ordinal equality, and returns false for absent or ambiguous id — the same policy as `IModContentCatalog.TryResolve`. |
| 4 | Wiring | `ModService` passes its `IModContentControl` implementation into `ModLifecycle`, which hands it to each `ModContext`; no DI cycle is introduced because the control already belongs to the mod-domain facade. |
| 5 | No protocol | Static content and ownership remain process-local; no NetMsg, no JObject snapshot, no protocol bump. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModContentOwnerQuery` | New Abstractions read-only interface. |
| `IModContext` | Added `ContentOwners` property. |
| `ModContext` | Added constructor wiring and `ContentOwners` implementation. |
| `ModLifecycle` | Added `IModContentControl` pass-through to per-mod contexts. |
| `ModService` | Passes its own content-control implementation to the lifecycle. |
| `ModContentOwnerQueryAdapter` | New Runtime adapter; correct ambiguity/unknown behavior. |
| Tests | `ModContentOwnerQueryTests` — six cases. |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Unique owner | A unique kind+id resolves to the owning mod | `OwnerQuery_ReturnsSingleOwner` |
| Unknown | Missing content returns false and an empty owner | `OwnerQuery_ReturnsFalseForUnknown` |
| Ambiguity | Duplicate kind+id returns false, matching catalog behavior | `OwnerQuery_ReturnsFalseForAmbiguous` |
| Same id across kinds | Different kinds resolve independently | `OwnerQuery_DistinguishesSameIdAcrossKinds` |
| Empty catalog | No false-positive resolution | `OwnerQuery_EmptyCatalog_ReturnsFalse` |
| Null arguments | Null kind/id are refused loudly, matching catalog semantics | `OwnerQuery_NullArguments_AreRefused` |
| Real mod wiring | A discovered mod sees its own content through `ContentOwners` | `ModContentTests.ContentOwnerQuery_ReadsRealModStack` |
| No wire/protocol regression | No NetMsg or JObject surface added; full suite green | `docs/api/mod-api.md` §content, full test run |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2097 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | pass |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass (33 events) |
| `tools/check-entity-event-dispatch.ps1` | pass (33 kinds × 3 tables) |
| `tools/check-delivery.ps1` | pass (7 boxes checked) |
