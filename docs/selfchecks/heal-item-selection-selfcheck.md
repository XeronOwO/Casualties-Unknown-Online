# Heal item selection — explicit Online UI medical item picker (no protocol bump)

Owner cycle: backlog "Heal item selection — heals auto-select the first carried
medical item; the wire already supports explicit item ids, so a UI selector is
a future refinement." Decision for this cycle: add a small explicit picker to
the Online UI while keeping the existing auto Heal button. No wire change, no
ProtocolVersion bump.

Decision summary:

- `IGameAdapter` gains `GetLocalHealItems()` returning a read-only list of
  `LocalHealItem` (`InstanceId` + `ItemId`) for the local body's slot-held
  heal-profile items. Instance ids are the wire keys the host already
  understands; item ids are display text only.
- `GameAdapter` scans the same inventory slots the host's healer lookup
  accepts (`SlotIndex >= 0` only), so worn medical items are not offered as
  explicit choices because the host would refuse them.
- `OnlineUiOverlay` renders a `Heal <item>` button per local heal item beneath
  the member row. The existing auto Heal button remains and still sends
  instance id 0.
- `Plugin` forwards a chosen instance id through the existing
  `PlayerInteractionService.SendHealRequest`; the host re-validates it in
  `FindHealItemIndex` exactly as before.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Wire already supports explicit instance ids | `PlayerHealRequestMsg.ItemInstanceId` (`PlayerHealRequestMsg.cs`); `PlayerInteractionService.Heal.FindHealItemIndex` compares `item.InstanceId == itemInstanceId` before falling back to auto-select |
| 2 | Existing UI always sent auto-select | `Plugin.TryHealRemoteFromUi` called `SendHealRequest(targetSteamId, 0)` |
| 3 | Local heal presence was bool-only | `IGameAdapter.HasLocalHealItem()`; no list of usable items for a picker |
| 4 | Healable item set is host-authoritative | `RemoteHealProfiles.IsHealItem`; the GameAdapter uses the same registry only for the UI presence list |
| 5 | Host skips worn items | `FindHealItemIndex` continues on `item.SlotIndex < 0`; the selector therefore lists body slots only |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IGameAdapter` / `LocalHealItem` | New read-only list surface; no network or permission semantics |
| `GameAdapter.HealInteraction` | Scans inventory slots and projects `LocalHealItem`; existing `HasLocalHealItem` unchanged |
| `OnlineUiOverlay` | Renders explicit item buttons; retains auto Heal button |
| `Plugin` | New `TryHealWithItemFromUi` delegate and `GetLocalHealItems` wiring |
| `PlayerInteractionService` / wire | Unchanged — explicit id path already existed and is tested |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| Explicit item forwarded | UI item button calls `SendHealRequest(target, itemInstanceId)` | `TryHealWithItemFromUi`; existing host tests use explicit ids (`Host_HealsUnconsciousGuest_SendsResultToGuest`, `Heal_PartialCondition_PreservesItemAndUpdatesTransferTable`) |
| Slot-only selector | `GetLocalHealItems` skips worn/limb items and id-less items | Static code; matches host `FindHealItemIndex` `SlotIndex >= 0` rule |
| Auto path preserved | Existing Heal button still sends `0` | `TryHealRemoteFromUi` unchanged |
| No wire change | No NetMsg / message / ProtocolVersion edits | `git diff` only Runtime/GameAdapter/Plugin/test/docs files |
| Local list is read-only | `LocalHealItem` is an immutable record; no Unity object or live game state leaves the adapter | Type definition + method signature |

## 4. Verification design (development-period, no manual acceptance)

- **Existing L0 wire tests** already prove the explicit-id host path
  (`PlayerInteractionServiceTests`).
- **Full regression**: `dotnet test CasualtiesUnknownOnline.slnx --no-build` —
  **1134 passed / 0 failed**.
- **Gates**: `dotnet format`, `check-architecture.ps1`,
  `check-event-replay.ps1`, `check-entity-event-dispatch.ps1` all pass.
- **Runtime evidence**: development-period rule — L0 simulation + static
  evidence + real-game-dir deploy; **no manual acceptance** (user 2026-08-16).

## 5. Plan approval

The user instructed this session to pick one backlog item autonomously and
complete it, then write the result back into `../backlog.md`
("由你来自主挑选一个并完成，记得在完成之后回写 backlog"). That instruction is
the plan approval for this cycle; no further interactive approval is required.

## 6. Verification results (2026-08-22)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx --no-build` | 1134 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean on source |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | all passed |
| `tools/deploy.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Casualties Unknown Demo"` | deployed to the real game dir only |
| Protocol | unchanged (no bump) |

## 7. Structure review

- `LocalHealItem.cs` is a single immutable record; `GameAdapter.HealInteraction.cs`
  remains well under the 600-line gate; `OnlineUiOverlay.cs` remains under the
  gate.
- One top-level type per file; no new expression-state bools; the UI selector
  has no persistent selection state (each item is a direct action button).
- Dead mechanisms: none. The explicit path reuses the existing host heal
  request/result wire; no second heal channel was added.

## 8. Accepted boundaries

- The selector lists only slot-held items with a non-zero instance id. Worn
  items are intentionally absent because the host's heal finder refuses them.
- There is no drop-down/multi-select state; each item is a direct "Heal
  <item>" button, which is the minimal IMGUI-friendly selector for this slice.
- The auto Heal button remains, so users who do not care which item is used
  can still one-click.
