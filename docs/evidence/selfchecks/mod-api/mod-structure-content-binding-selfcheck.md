# Mod structure content binding — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — sixth concrete typed content kind
(structure) after item, recipe, liquid, building, and tile.

Decision: expose static multi-block structure data through the same
`IModContent` + content-binder + GameAdapter-provider seam used by the earlier
kinds. Mods keep a plain Abstractions DTO; the Game Adapter validates and
compiles the authored marker grid into non-air cells. Automatic worldgen
distribution and spawn-count consumption are deliberately not part of this
initial seam; runtime placement is exposed through `IModStructurePlacement`.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed structure payload | `ModStructureDefinition` in `CUO.Abstractions` with `ToPayload()`/`FromPayload()` via DataContractSerializer. |
| 2 | Grid authoring shape | Width/height + top-to-bottom `Rows`; `'.'`/`' '` are air; single-character marker maps to vanilla block indices or custom tile content ids. |
| 3 | Future worldgen metadata | `SpawnCounts` list is carried and validated non-negative, but not consumed by a worldgen provider in this cycle. |
| 4 | Game Adapter provider | `GameAdapterStructureContentProvider` decodes `ModStructureDefinition`, validates dimensions/marker maps/row lengths, and compiles cells (bottom-based Y offsets). |
| 5 | Safety limits | Width/height caps (128 each), total cell cap (4096), no duplicate markers across vanilla/tile maps, no all-air structures. |
| 6 | Shared-content filter | Existing `ModContentBinder` applies the same network-mode filter as all other static content kinds. |
| 7 | No wire | Structure definitions are mod-local; placement writes ride the existing `BlockPlaced` relay instead of a new structure message. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModStructureDefinition` | New Abstractions DTO + serialization helpers. |
| `GameAdapterStructureContentProvider` | New GameAdapter provider (validation + compiled cell registry). |
| `GameAdapterDomains` / `GameAdapter` | Wired the structure provider into the adapter's owned domain set. |
| `PluginDependencyRegistrar` | Registered the structure provider as `IContentBindingProvider` and `ICuoService`. |
| `docs/api/mod-api.md` | Added typed structure content + current structure binding scope. |
| Tests | `ModStructureDefinitionTests` round-trip and invalid payload. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | `ModStructureDefinition.ToPayload`/`FromPayload` preserves grid/maps/spawn counts/custom data | `ModStructureDefinitionTests.RoundTrip_PreservesCoreFields` |
| Empty optional maps | A minimal air-only DTO survives the round-trip with empty maps | `ModStructureDefinitionTests.RoundTrip_PreservesEmptyOptionalMaps` |
| Invalid payload | Malformed bytes return null | `ModStructureDefinitionTests.InvalidPayload_ReturnsNull` |
| Binder routing | Structure entries can reach a provider through the generic binder | `ModContentBinderTests` (kind-routing provider test family) |
| No wire/protocol regression | Static structures remain local; placement uses existing block relay | `docs/api/mod-api.md` §4f/§4h, full suite green |

## 4. Verification design

- Pure-managed unit tests for DTO serialization and invalid payloads.
- The GameAdapter structure provider/factory stay behind the same compile
  boundary as the item/recipe/liquid/building/tile providers; DI wiring is
  verified through the full solution build and the existing generic binder
  contract tests.
- Static evidence: no game/Unity type in Abstractions; no new wire message;
  no automatic worldgen distribution added in this seam.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2019 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
