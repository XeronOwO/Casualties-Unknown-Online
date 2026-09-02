# Mod status GameAdapter projection — phase 3 first slice self-check

Owner cycle: CUCoreLib migration support / mod-status domain.

Decision: land the first typed GameAdapter body/limb projection slice. The
runtime status table and typed status transport (phases 1–2) already give mods
per-player/per-limb opaque runtime values; this round adds a typed projection
contract so the GameAdapter can decode a small, well-known set of those values
into vanilla body/limb behavior without copying CUCoreLib's reflection-based
status classes or JObject snapshots.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Projection kind | `ModStatusProjectionKind` in Abstractions: `None`, `BodyFormula`, `LimbPhysiology`. |
| 2 | Body DTO | `ModBodyFormulaProjection` in Abstractions: MaxEncumbrance, TotalEncumbrance, Immunity, JumpSpeed, AveragePain. No game/Unity type. |
| 3 | Limb DTO | `ModLimbProjection` in Abstractions: optional BleedAmount, SkinHealth, MuscleHealth, InfectionAmount. No game/Unity type. |
| 4 | Declare surface | `IModStatusRuntime.TryDeclare` accepts an optional projection kind; store validates kind/scope combination. |
| 5 | Store event | `ModStatusStore.StatusChanged` is raised after every value write/removal; it is internal (not a wire event). |
| 6 | Store read seam | `ModStatusStore.GetProjectionSnapshots(player)` returns defensive, typed snapshot objects containing projection metadata and payload for the GameAdapter. |
| 7 | GameAdapter projection | `ModStatusVanillaProjection` decodes only well-known projection kinds and applies additive overlays to the local `Body`/`Limb` after native updates. |
| 8 | Harmony bridge | `ModStatusProjectionPatches` add `Body.Update` / `Limb.Update` postfixes; the bridge verifies the body is the local body before applying. |
| 9 | No wire change | No new NetMsg, no protocol bump, no JObject snapshot. Projection is a local decode of already-stored opaque status bytes. |
| 10 | No CUCoreLib source | No CUCoreLib status classes or reflection-based ConditionalWeakTable bag are ported; only typed DTOs and a narrow GameAdapter overlay. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModStatusProjectionKind` | New Abstractions enum. |
| `ModBodyFormulaProjection` / `ModLimbProjection` | New Abstractions DataContract DTOs + payload helpers. |
| `IModStatusRuntime.TryDeclare` | Optional projection-kind parameter. |
| `ModStatusPolicy` | Projection-kind/scope validation. |
| `ModStatusStore` | Stores projection kind, raises change event, exposes projection snapshots. |
| `ModStatusProjectionSnapshot` | New internal Runtime read DTO. |
| `ModService` | Internal `StatusStore` seam for GameAdapter. |
| `ModStatusVanillaProjection` | New GameAdapter projection applier. |
| `ModStatusProjectionPatches` | New Harmony postfixes for `Body.Update` / `Limb.Update`. |
| `IPatchBridge` / `GameAdapterBridge` | Two narrow projection forwarding methods. |
| Tests | DTO round-trip, store snapshots/events/scope validation, reflective GameAdapter contract. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Body DTO round-trip | `ModBodyFormulaProjection.ToPayload`/`FromPayload` preserves contributions | `ModBodyFormulaProjectionTests.RoundTrip_*` |
| Limb DTO round-trip | `ModLimbProjection.ToPayload`/`FromPayload` preserves optional fields | `ModLimbProjectionTests.RoundTrip_*` |
| Invalid projection payloads | Malformed bytes return null | `ModBodyFormulaProjectionTests.InvalidPayload_ReturnsNull`, `ModLimbProjectionTests.InvalidPayload_ReturnsNull` |
| Projection kind/scope validation | BodyFormula only on Body, LimbPhysiology only on Limb | `ModStatusProjectionStoreTests.ProjectionKind_MustMatchBodyLimbScope` |
| Store snapshot body | Body snapshot carries kind/scope/player and decodable value | `ModStatusProjectionStoreTests.BodyFormula_DeclareAndSet_ProducesBodySnapshot` |
| Store snapshot limb | Limb snapshot carries slot and decodable value | `ModStatusProjectionStoreTests.LimbPhysiology_DeclareAndSet_ProducesLimbSnapshotWithSlot` |
| Change event | Set/removal raise the internal refresh event; remove clears snapshots | `ModStatusProjectionStoreTests.StatusChanged_FiresOnSetAndRemoval_AndRemovalClearsSnapshot` |
| Opaque statuses ignored | `None` statuses are not projected | `ModStatusProjectionStoreTests.OpaqueStatuses_AreExcludedFromProjectionSnapshots` |
| GameAdapter contract | Applier methods and Harmony postfixes exist with Body/Limb shapes | `ModStatusProjectionContractTests.*` |
| Patch verification | New `Body.Update` / `Limb.Update` postfixes are auto-verified | full `PatchInventory` contract tests pass |

## 4. Scope / non-goals

- Only local player body/limb overlays are applied by this slice; remote
  render clones are not gameplay-projected.
- Continuous circulation targets (heart rate, respiratory rate, blood
  pressure) are not in the body DTO yet; a post-update additive overlay cannot
  express a target offset without a dedicated native formula patch.
- Vanilla moodle row feeding is still a future local UI/GameAdapter seam.
- No generic snapshot, no reflection-based status registry, no game/Unity type
  in Abstractions.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2068 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
