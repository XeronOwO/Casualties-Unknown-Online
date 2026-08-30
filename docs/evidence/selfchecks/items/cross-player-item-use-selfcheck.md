# Cross-player consumable use self-check

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/evidence/selfchecks/MANIFEST.md` and
> `docs/architecture/protocol.md` before citing.

Owner cycle: backlog "Cross-player item use (give/feed/drink/wear/use an item on
another player)" first slice. Decision: implement drink/food consumable use only;
leave wear, injectables and generic tool use as future extensions. The operation
is host-authoritative and uses the same request/result pattern as the existing
cross-player heal slice.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | `Body.Drink` / `WaterContainerItem.Drink` | `WaterContainerItem.cs:198-214`, `Body.Drink` (`Body.cs:3680-3685`) |
| 2 | Liquid effects | `LiquidType.onDrink` delegates in `Liquids.cs` (clean water, milk, juices, coffee, energy drink, soda, etc.) |
| 3 | Solid food effects | `Item.cs` `useAction` tables for food items (bread, burger, steak, nutrientbar, etc.) |
| 4 | Cross-player heal pattern | `PlayerHealService`/`PlayerHealResultMsg` — the established host-authoritative direct-interaction shape |
| 5 | Character snapshots | `ICharacterDataControl` / `PlayerCharacterAccess` — host saves and restores guest character data |
| 6 | Guest transfer table | `IItemControl.UpdateTransferredItem` / `RemoveTransferredItem` — consumed guest-owned items must not be resurrected by reconnect restore |
| 7 | Local body apply | `PlayerInteractionApply` + `CharacterDataSync.ApplyHealState` — the RemoteApply body mutation path |

## 2. Design

- New dedicated wire messages: `PlayerItemUseRequestMsg` (guest → host,
  `NetMsg.PlayerItemUseRequest`, 116) and `PlayerItemUseResultMsg`
  (host → participants, `NetMsg.PlayerItemUseResult`, 117).
  `ProtocolVersion` 47 → 48 because new peers and old peers cannot interop on
  this new operation.
- New `RemoteConsumeCatalog` (Runtime): a curated host-authoritative registry of
  drinkable liquids and solid food items. Unknown liquids/foods are refused as
  a whole, never approximated.
- New `RemoteConsumeApplication` (Runtime): pure “draw a drink plan” +
  “apply liquid/food body effects” helpers, used by the host and by L0 tests.
- New `PlayerItemUseService` (Runtime): host validates user/target in-world,
  alive and conscious, finds/auto-selects the item, drains a liquid container
  (100 ml or the whole remaining stack) or consumes a food item, applies the
  curated effect to the target’s authoritative character snapshot, saves both
  players, updates the guest transfer table, and sends one result to both
  participants.
- `PlayerInteractionApply` (Game Adapter): on a result, the local participant
  consumes/updates/destroys its item (including liquid stack + condition) or
  applies the target post-use health/limbs inside `RemoteApply`, then
  re-reports the character snapshot immediately.
- Online UI entry: KrokMP-style drag/overlap release — when the local player
  drags a usable inventory item and releases it over an in-world remote player's
  authoritative body position, `CrossPlayerDragUse` routes the existing
  cross-player use request and the native drop is skipped. The static “Use” /
  “Use with” buttons were removed from the Players page and right-click context
  menu because they did not match the intended interaction.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Drink draw | `RemoteConsumeApplication.TryCreateDrinkPlan` drains exactly `min(100, total)` ml proportionally | `RemoteConsumeApplicationTests` (full/small/unknown liquid) |
| Liquid body effect | Curated per-100 ml effects applied to `CharacterHealthMsg` | `RemoteConsumeApplicationTests` water thirst/temperature |
| Food body effect | `RemoteFoodEffect` applies hunger/thirst/weight/etc. | `RemoteConsumeApplicationTests` bread effect |
| Host use operation | `PlayerItemUseService` validates and commits both snapshots + transfer table | `PlayerInteractionServiceTests` guest-water/host-bread/refused cases |
| Local apply | `PlayerInteractionApply` updates item or body and re-reports | adapter code path + existing RemoteApply tests |
| UI eligibility | `OnlineUiMemberProjection` shows use actions only on alive+conscious remotes | `OnlineUiMemberProjectionTests` new use-item case |
| Wire direction | Two new messages classified in the explicit direction contract | `DirectionTests` updated |

## 4. Verification

- **L0 unit**: `RemoteConsumeApplicationTests` (7), `PlayerInteractionServiceTests`
  +4 use cases, `OnlineUiMemberProjectionTests` +1 use-item case.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` 1386 green,
  `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
