# Periodic keyframe top-level state self-heal — mechanism inventory and self-check

Owner cycle: backlog "Periodic keyframe self-healing (partially implemented;
extend to remaining domains)" and the documented world-item component-state
keyframe gap in `docs/item-features.md`. Decision: the 5 s periodic snapshot
already carries the host table's full `CharacterItemMsg` (including liquid
stacks and `[Saveable]` component states); the missing piece was the
reconcile applying that top-level state to **existing** world items. It now
re-aligns condition/favourited/liquids/components whenever they diverge.
No protocol change, no new wire message.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The host's world-item table stores the full item state | `WorldItem.Item` is a `CharacterItemMsg` (`src/.../WorldItem.cs:14-15`) — condition/favourited/liquids/components/contents all ride it |
| 2 | The periodic keyframe already sends that full state | `ItemSnapshotService.SendPeriodicItemSnapshot` (`ItemSnapshotService.cs:93-110`) serializes every `WorldItem` via `ToSnapshotEntryMsg`, so components/liquids are already on the wire |
| 3 | The reconcile only aligned condition before | `ItemReconcile.OnRemoteItemSnapshot` previously wrote `item.condition` only (`ItemReconcile.cs:89-118`); components/liquids/favourited of an existing world item stayed at their last report/correction time |
| 4 | A lost/corrected state is self-healable from the same table | The host table is updated by use/slot/craft/container reports (`ItemService.UpdateWorldItemState`, `ItemArbitration.AdoptEvidence`, `ItemActionSync`), so the periodic snapshot carries the authoritative current values |
| 5 | Comparison tolerance is shared, not duplicated | `ItemStateEquality` (new, Runtime) owns condition/liquid/component field comparisons; `ItemArbitration` uses the same rules for evidence checks, and `ItemReconcile` passes the stricter 0.0005 condition tolerance to preserve the old keyframe behavior |
| 6 | The restore path already exists | `ItemStateCodec.RestoreLiquids` / `RestoreComponentStates` are the same functions used by materialization and corrections (`ItemStateCodec.cs:280-342`) |
| 7 | Position stays with the position stream | `ItemReconcile` still never places or re-positions; only missing items are materialized (`ItemReconcile.cs:125-147`) |
| 8 | Container contents stay with their own family | `ItemStateEquality.TopLevelMatches` deliberately ignores `Contents`; the reconcile does not recurse into them (nested content moves ride `ItemContainerContent`, #120) |

## 2. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Periodic keyframe carries components/liquids | Already true on the wire; now asserted by a simulation test | `PeriodicSnapshot_CarriesTopLevelComponentAndLiquidState` |
| Existing world item state alignment | `ItemReconcile` captures the item's top-level digest, asks `ItemStateEquality`, and restores condition/favourited/liquids/components on divergence | Code path + new pure `ItemStateEqualityTests` |
| Evidence-check semantics unchanged | `ItemArbitration` calls the extracted equality with its original 0.01 tolerance | Full suite; existing arbitration simulations still green |
| No protocol bump | The message shape is untouched; the reconcile is adapter-local | ProtocolVersion unchanged |
| Position ownership unchanged | `ItemReconcile` still does not place anything | Code path (`ItemReconcile.cs:125-147`) |
| Game update guard | No new Harmony patch; no game-internal type contract added | No `PatchInventory` change |

## 3. Verification design (development-period, no manual acceptance)

- `ItemStateEqualityTests` — pure unit coverage of the shared comparison
  (condition tolerance, favourited, liquids, components, field kinds).
- `ItemSnapshotSimulationTests.PeriodicSnapshot_CarriesTopLevelComponentAndLiquidState`
  — wire-level proof that the 5 s keyframe carries liquid stacks and
  component state from the host table.
- Existing `ItemArbitration` evidence tests remain the regression guard for
  the extracted equality.
- Static evidence: the keyframe sender, the restore functions and the
  reconcile path are all cited above.
- Runtime verification box for this development-period cycle: **L0
  simulation + static evidence, no manual acceptance** (user rule
  2026-08-16).

## 4. Verification results (2026-08-21)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1018 passed / 0 failed |
| `ItemStateEqualityTests` focused filter | run with the full suite |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| Static evidence | `WorldItem.cs:14-15`, `ItemSnapshotService.cs:93-110`, `ItemReconcile.cs`, `ItemStateCodec.cs:280-342` |

## 5. Structure review

- New `ItemStateEquality.cs` is a pure static helper (~80 lines), one
  top-level type per file.
- Touched classes stay under the 600-line gate: `ItemReconcile` remains
  ~170 lines, `ItemArbitration` ~430 lines after removing the duplicated
  match helpers.
- No new expression-state bool fields; no state ownership change (the
  world table still owns the authoritative item state).
- Dead mechanisms: none. The extracted equality replaces private copies in
  `ItemArbitration`, not a duplicate path.
