# Cross-player limb-tool use self-check

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/evidence/selfchecks/MANIFEST.md` and
> `docs/architecture/protocol.md` before citing.

Owner cycle: backlog "Cross-player item use" tools first slice. Decision: add
curated non-liquid limb tools to the existing `PlayerItemUseRequest`/
`PlayerItemUseResult` operation. No new wire message and no protocol bump.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Tool `useLimbAction` tables | `Item.cs:683-705` (boneweldingtool), `1573-1591` (clottingmush), `1597-1615` (chestdrain), `604-625` (musharm) |
| 2 | Existing cross-player use wire | `PlayerItemUseRequestMsg` / `PlayerItemUseResultMsg` (NetMsg 116/117) — reused unchanged |
| 3 | Character snapshot surfaces | `CharacterHealthMsg` + `CharacterLimbMsg` — all changed values already ride the existing result |
| 4 | Local body apply | `PlayerInteractionApply.OnPlayerItemUseReceived` + `CharacterDataSync.ApplyHealState` — unchanged |
| 5 | Guest transfer table | `IItemControl.UpdateTransferredItem` / `RemoveTransferredItem` — same consume path as other slices |
| 6 | Host authority | `PlayerItemUseService` validates both players, consumes condition and publishes one result |

## 2. Design

- New `RemoteLimbToolProfile` (Runtime): pure per-tool condition cost and
  body/limb deltas, including the `boneHealTimer` and `bleedAmount`
  multiplicative factors.
- New `RemoteLimbToolCatalog` (Runtime): maps the four curated tool ids to
  their profiles; unknown tools are refused as a whole.
- New `RemoteLimbToolApplication` (Runtime): applies to the most-injured limb
  or to the profile's required limb; returns false when a required limb is
  missing so the host refuses before consuming.
- `PlayerItemUseService` tries limb tools after the liquid/topical branches;
  `IsActuallyUsable` and `PlayerInteractionApply.IsLocalUseItem` include the
  tool registry so the Online UI and in-world menu expose them.
- **Scope limits** — supported tools: `boneweldingtool`, `clottingmush`,
  `chestdrain`, `musharm`. Component-bearing tools (splint/tourniquet/
  icepack), minigame-random tools (tweezers) and timed tools (medicalsuture)
  remain future slices.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Tool catalog | known ids accepted, unknown refused | `RemoteLimbToolApplicationTests.Catalog_ExposesKnownLimbToolsAndRefusesUnknown` |
| Bone welding | skin/muscle/pain/bleed + bone-heal multiplier + viscosity | `ApplyBoneweldingtool_AppliesLimbAndBodyEffects` |
| Clotting mush | bleed multiplier + body viscosity | `ApplyClottingmush_ReducesBleedAndRaisesViscosity` |
| Chest drain | required chest limb + hemothorax reduction | `ApplyChestdrain_ReducesHemothoraxOnChestLimb` |
| Missing required limb | refused before consuming | `ApplyChestdrain_MissingChestLimb_ReturnsFalse` |
| Mush arm | skin heals + bandage slow | `ApplyMusharm_AddsSkinHealAndBandageSlow` |
| Host operation | guest uses boneweldingtool on host, item condition and target state follow | `PlayerInteractionServiceTests.Guest_UsesBoneWeldingToolOnHost_AppliesToolAndSendsResult` |

## 4. Verification

- **L0 unit**: `RemoteLimbToolApplicationTests` (6) +
  `PlayerInteractionServiceTests` +1 tool case.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` full suite
  green, `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
