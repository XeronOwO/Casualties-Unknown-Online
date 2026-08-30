# Cross-player opiate use self-check

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/evidence/selfchecks/MANIFEST.md` and
> `docs/architecture/protocol.md` before citing.

Owner cycle: backlog "Cross-player item use" opiate slice. Decision: add
curated injectable opiate/opiate-antagonist liquids to the existing
`PlayerItemUseRequest`/`PlayerItemUseResult` operation and carry the
`Painkillers` component state on `CharacterHealthMsg`. No new wire message and
no protocol bump.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Opiate item containers | `Item.cs:711-735` (morphine), `860-887` (opium), `889-917` (heroin), `1035-1063` (fentanyl), `919-945` (naloxone) — all `WaterContainerItem.Inject` on-limb use |
| 2 | Opiate liquid `onHealthUse` formulas | `Liquids.cs:229-233` (morphine 90/100ml), `284-288` (opium 40/100ml), `320-325` (heroin 130 + 50 sickness), `407-411` (fentanyl 420/10ml), `340-348` (naloxone antagonist 50/100ml) |
| 3 | Painkiller component | `Painkillers.cs` — `[Saveable]` fields `opiateAmount`, `opiateTolerance`, `opiateReception`, `antagonistAmount`, `actualOpiateReception`; the component drives limb pain reduction, opiate happiness and withdrawal |
| 4 | Existing cross-player use wire | `PlayerItemUseRequestMsg` / `PlayerItemUseResultMsg` (NetMsg 116/117) — reused unchanged |
| 5 | Character snapshot | `CharacterHealthMsg` now carries the five painkiller component fields (ProtoMember 68-72) |
| 6 | Local apply | `PainkillersSync` (Game Adapter) captures from / applies to the local `Painkillers` component; `CharacterDataSync.ApplyHealState` and `ApplyRestoredStatsAndWipe` call it |
| 7 | Host authority | `PlayerItemUseService` already routes known injectable containers through `RemoteMedicineCatalog`/`RemoteMedicineApplication` |

## 2. Design

- `RemoteMedicineCatalog` adds the five opiate container ids and their per-ml
  liquid effects; `RemoteMedicineLiquidEffect` gains `OpiateAmountPerMl` and
  `AntagonistAmountPerMl`.
- `RemoteMedicineApplication.Apply` writes `OpiateAmount`/`AntagonistAmount`
  onto the target's `CharacterHealthMsg`.
- `CharacterHealthMsg` gains five additive proto fields (existing numbers
  remain unchanged); no new NetMsg / ProtocolVersion bump.
- New `PainkillersSync` adapter helper: `Capture` reads the component into the
  health message; `Apply` creates or updates the local body's `Painkillers`
  component from a host-authoritative health result/restore.
- Supported items: `morphine`, `opium`, `heroin`, `fentanyl`, `naloxone`.
  Drinkable pill items and timed/random opiate branches stay future slices.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Opiate catalog | five item ids accepted; known liquids accepted | `RemoteMedicineApplicationTests.Catalog_ExposesOpiateItemsAndLiquids` |
| Opiate draw | full/small morphine plan | `Plan_DrawsOpiateItemAmountOrEntireSmallStack` |
| Morphine effect | 100 ml inject adds 90 opiate amount | `ApplyMorphine_AddsOpiateAmount` |
| Heroin effect | adds opiate + sickness | `ApplyHeroin_AddsOpiateAndSickness` |
| Naloxone effect | adds antagonist amount | `ApplyNaloxone_AddsAntagonistAmount` |
| Host operation | guest injects morphine on host, both snapshots/transfer table follow | `PlayerInteractionServiceTests.Guest_InjectMorphineOnHost_AppliesOpiateAndSendsResult` |
| Adapter capture/apply surface | static helper with Body + CharacterHealthMsg | `PainkillersSyncTests` |

## 4. Verification

- **L0 unit**: `RemoteMedicineApplicationTests` +4 opiate cases,
  `PlayerInteractionServiceTests` +1 service case, `PainkillersSyncTests` +2.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` full suite
  green, `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
