# Heal another player — direct player interaction slice (ProtocolVersion 28)

Owner cycle: backlog "Direct player interaction (view/take items, carry, view
vitals, heal)". Decision for this cycle: close the **heal another player**
slice by making the host the cross-player authority for the healing operation.
The Online UI gets a Heal button on in-world remote members when the local body
carries a known medical item. The host validates both participants against its
authoritative character snapshots, consumes the healer's item, applies the
medical item's effect to the target's most injured limb, and sends the two
participants one authoritative `PlayerHealResultMsg`. This is the remaining
slice of the direct player-interaction family.

Decision summary:

- The host is the only cross-player authority: it rejects non-in-world,
  missing-snapshot, unable-healer, dead-target and non-medical-item cases;
  it never trusts the Online UI or the request payload.
- The Online UI sends `PlayerHealRequestMsg` with `ItemInstanceId = 0`, so the
  host auto-selects the first slot-held item in the heal profile set. Exact
  item selection remains a future UI refinement; the wire already supports a
  concrete instance id.
- The heuristic selects the most injured limb (lowest skin + muscle health,
  dismembered limbs skipped) and applies the item's dressing/medicine effect
  (`RemoteHealProfile` + `RemoteHealApplication`) to that limb.
- The healer's item is consumed by condition; when condition reaches zero it is
  destroyed and removed from the character snapshot and guest transfer table.
- The target receives the post-heal full health + limb state and applies it to
  the local body inside a `RemoteApply` scope, then re-reports the character
  snapshot immediately — the host save and every peer clone converge in the
  same run.
- This slice does not implement CPR or resurrect dead players; the target must
  be alive. No distance/line-of-sight validation, consistent with the existing
  take/carry slices.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Online UI surface | `OnlineUiOverlay` shows a Heal button next to an in-world remote member when the remote is alive and `IGameAdapter.HasLocalHealItem()` reports a heal-profile item on the local body |
| 2 | Wire request | `PlayerHealRequestMsg` (NetMsg 102, guest → host) carries target SteamId + optional item instance id; `PacketReceiver` locks it guest→host |
| 3 | Host authority | `PlayerInteractionService.HandleHealRequest` validates in-world, snapshots, healer conscious/alive, target alive, known heal item and limb data |
| 4 | Heal profiles | `RemoteHealProfiles` + `RemoteHealProfile` + `RemoteHealApplication` define the supported medical-item effects as pure data/logic (no game assembly in Runtime) |
| 5 | Authority update | The host clones both snapshots, consumes the healer item, applies the profile to the target's most injured limb, saves both, and updates the guest transfer table where a guest healer's item survives partial consumption |
| 6 | Wire result | `PlayerHealResultMsg` (NetMsg 103, host → participant) carries item consumption state + target post-heal health/limbs; `PacketReceiver` locks it host→guest |
| 7 | Local apply | `GameAdapter.HealInteraction.cs`: healer side finds the item by instance id and sets condition/destroys it; target side maps post-heal body/limb state; both inside `RemoteApply` |
| 8 | Immediate re-report | After the local mutation `CharacterDataSync.ReportInventoryChanged(body)` re-sends the full character snapshot |
| 9 | Protocol bump | New wire messages require ProtocolVersion 28 (`PlayerHealRequest` 102, `PlayerHealResult` 103) |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `OnlineUiOverlay` / `Plugin` | Heal button + local heal-item availability check; forwards to `SendHealRequest` |
| `PlayerInteractionService` (+ heal partial) | Host-authoritative request handler, item auto-select, snapshot mutation, transfer-table update, result publish |
| `RemoteHealProfile` / `RemoteHealProfiles` / `RemoteHealApplication` | Pure heal effect data/logic, L0-testable |
| `PlayerHealRequestMsg` / `PlayerHealResultMsg` / `NetMsg` / `ProtocolVersion` | New wire IDs + 28 |
| `PacketReceiver` / `DirectionTests` | 102 guest→host, 103 host→guest |
| `ItemArbitration` / `IItemControl` / `ItemService.PlayerInteraction` | `UpdateTransferredItem` so a consumed condition survives reconnect restore |
| `CharacterDataSync` | `ApplyHealState` maps post-heal health/limbs onto the local body |
| `GameAdapter.HealInteraction` | Consumes local healer item and/or applies target state inside `RemoteApply`, then re-reports |
| Existing item/character streams | Unchanged — the target re-report is the convergence path, no second channel |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Host validates healer | Conscious + alive + in-world | `Heal_UnableHealer_IsRefused`; `HandleHealRequest` |
| Host validates target | Alive (no CPR in this slice) + in-world + limb data | `Heal_DeadTarget_IsRefused`; empty-limb guard |
| Host validates item | Known heal profile + slot item, or 0 = auto-select first | `Heal_NoHealItem_IsRefused`; `FindHealItemIndex` |
| Host computes target limb | Most injured + skips dismembered | `RemoteHealApplication.PickMostInjuredLimb` tests |
| Host consumes item | Condition minus profile cost; destroy at zero | `Guest_HealsUnconsciousHost_ConsumesItemAndSendsResult`, `Heal_PartialCondition_PreservesItemAndUpdatesTransferTable` |
| Transfer table follows | Guest healer item updated/removed so reconnect restore cannot resurrect consumed condition | `ItemArbitration.UpdateTransferredItem`, partial-condition test |
| Both participants apply | Healer consumes; target applies health/limbs | `GameAdapter.HealInteraction.cs` (static) + L0 service tests |
| Direction table | 102 g2h, 103 h2g | `DirectionTests` |
| No parallel channel | Target re-reports through the existing 1 Hz stream | Existing CharacterDataSync report path; no new movement/render channel |

## 4. Verification design (development-period, no manual acceptance)

- **L0 service tests**: guest→host/heal, host→guest/heal, no-item refusal,
  unable-healer refusal, dead-target refusal, item destruction, partial
  condition + transfer-table update.
- **Pure profile tests**: most-injured limb selection, empty limbs, effect
  application and non-negative clamping.
- **Direction tests**: both new NetMsg ids explicitly classified.
- **Full regression**: `dotnet test CasualtiesUnknownOnline.slnx` —
  **1066 passed / 0 failed**.
- **Static evidence**: host-authority path + local apply in RemoteApply +
  immediate re-report; no manual acceptance (user rule 2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `docs/backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## 6. Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1066 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean on source |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game dir only |
| Protocol | 28 (new NetMsg 102-103) |

## 7. Structure review

- `PlayerInteractionService.cs` stays under the 600-line gate (heal half in
  `PlayerInteractionService.Heal.cs` partial, pure logic in
  `RemoteHealApplication` / `RemoteHealProfiles`).
- One top-level type per file; no new expression-state bool fields in touched
  runtime classes.
- The heal authority state belongs to `PlayerInteractionService`, the item
  transfer table to `ItemArbitration`, and the local apply to `GameAdapter` —
  no shared mutable service added.
- Dead mechanisms: none. The target result rides the existing character-data
  report path; no second item or health channel was introduced.

## 8. Accepted boundaries

- Auto-select first medical item from the Online UI; exact item picking remains
  a possible future UI refinement (the wire supports explicit instance ids).
- No CPR / dead-target healing.
- Supported item set is the dressing/medicine limb-usable profile set; liquid,
  drug, container and tool items are not included.
- No distance/line-of-sight validation, matching the take/carry slices.
