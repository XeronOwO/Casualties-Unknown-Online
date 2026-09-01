# Mod content migration base — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support (minimal base only; no full content
binding yet).

Decision: do not port CUCoreLib content systems. Instead land a small,
payload-agnostic foundation that CUCoreLib-style mods and a future
Runtime/GameAdapter content binder can build on. The mod-facing surface stays
opaque and versioned; the runtime gets a read-only catalog for enumeration,
unique resolution, and conflict diagnostics.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Public content kind vocabulary | `ModContentKind` in `CUO.Abstractions` — stable tags for item, recipe, liquid, tile, building, structure, status, moodle, setting, locale. |
| 2 | Versioned opaque content | `ModContentDefinition.SchemaVersion` and `IModContent.TryRegister(id, kind, data, schemaVersion)`; default overload continues to mean schema version 1. |
| 3 | Schema version validation | `ModContentPolicy.IsValidSchemaVersion` — positive versions only; zero/negative refused with log. |
| 4 | Runtime read-only catalog | `IModContentCatalog` / `ModContentCatalog` — enumerates every mod's content, filters by kind, resolves a unique kind+id, reports conflicts. It never interprets bytes. |
| 5 | Conflict diagnostics | `ModContentConflict` / `ModContentConflictKind` — duplicate id across mods and schema-version mismatch on the same kind+id. |
| 6 | DI wiring | `CuoBootstrap` registers `ModContentCatalog` and `IModContentCatalog` after `IModContentControl`. |
| 7 | No wire / protocol | Static content remains local; no new NetMsg, no content bytes cross the wire, ProtocolVersion unchanged. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModContent` | Added versioned registration overload; existing 3-arg method remains. |
| `ModContentDefinition` | Added `SchemaVersion`, defaulting to 1. |
| `ModContentKind` | New Abstractions constant class for common content kinds. |
| `ModContentPolicy` | Added `IsValidSchemaVersion`. |
| `ModContentCatalog` / `IModContentCatalog` | New Runtime read-only catalog with kind filter, unique resolve, conflict view. |
| `ModContentConflict` / `ModContentConflictKind` | New Runtime diagnostic records. |
| `CuoBootstrap` | Registered the new catalog in DI. |
| Tests | Extended `ModContentTests`, added `ModContentCatalogTests`. |
| Protocol version | Unchanged. |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Version storage | `TryRegister(..., schemaVersion)` stores and returns the version | `ModContentTests.SchemaVersion_IsStoredAndRead` |
| Invalid schema version | Non-positive versions refused, count unchanged | `ModContentTests.InvalidSchemaVersion_IsRefused` |
| DI integration | Catalog resolves real entries from the production mod stack | `ModContentTests.ContentCatalog_ReadsRealModStack` |
| Kind filtering | `OfKind` returns only exact-kind entries | `ModContentCatalogTests.Catalog_EnumeratesAndFiltersByKind` |
| Unique resolution | `TryResolve` returns one owner only | `ModContentCatalogTests.Catalog_TryResolve_ReturnsSingleMatch` |
| Ambiguity | Multiple owners cannot be resolved; `HasConflicts` true | `ModContentCatalogTests.Catalog_TryResolve_RefusesUnknownAndAmbiguous` |
| Cross-mod duplicate | Same kind+id across mods yields `DuplicateId` conflict | `ModContentCatalogTests.Catalog_ReportsDuplicateIdsAcrossMods` |
| Schema mismatch | Same kind+id with different schema versions yields `VersionMismatch` | `ModContentCatalogTests.Catalog_ReportsSchemaVersionMismatchForSameContent` |
| Kind namespacing | Same id in different kinds is not a conflict | `ModContentCatalogTests.Catalog_AllowsSameIdInDifferentKinds` |
| Empty catalog | No entries, no conflicts, no resolution | `ModContentCatalogTests.Catalog_EmptyCatalog_HasNoConflicts` |
| Null arguments | Catalog methods throw `ArgumentNullException` | `ModContentCatalogTests.Catalog_NullArguments_AreRefused` |
| No wire/protocol regression | No NetMsg; content is local and versioned | `docs/api/mod-api.md` §4f, full suite green |

## 4. Verification design

- L0 simulation over the real mod stack for schema version registration and
  control-surface aggregation.
- Pure-managed unit tests for the Runtime catalog with a fake
  `IModContentControl` (no game, no Unity, no network).
- Static evidence: catalog stays in Runtime, not Abstractions; mods still see
  only `IModContent` / `ModContentDefinition`; no wire/protocol change.
- Runtime verification box: L0 simulation + static evidence, no manual
  acceptance per development-period rule.

## 5. Verification results (2026-09-01)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1983 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean for tracked source |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass (33 events) |
| `tools/check-entity-event-dispatch.ps1` | pass (33 kinds x 3 tables) |
| `tools/check-delivery.ps1` | pass (7 boxes) |
