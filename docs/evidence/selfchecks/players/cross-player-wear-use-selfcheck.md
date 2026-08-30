# Cross-player wearable use self-check

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/evidence/selfchecks/MANIFEST.md` and
> `docs/architecture/protocol.md` before citing.

Owner cycle: backlog "Cross-player item use" wear slice. Decision: add native
wearable placement to the existing `PlayerItemUseRequest`/`PlayerItemUseResult`
operation. The host moves the acting player's inventory item onto the target's
authoritative character snapshot as a worn (negative-slot) item; the target's
local body reuses the existing character-restore wearable path. No new NetMsg
and no protocol bump (additive result field).

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native wear placement | `Body.WearWearable` (`Body.cs:1480-1517`), `GetWearableBySlotID` (`Body.cs:1590-1608`) |
| 2 | Wearable item definitions | Item.cs SetupItems `wearable` / `desiredWearLimb` / `wearSlotId` blocks (Item.cs:5887-6660) |
| 3 | Wear slot conflict / limb missing | `WearWearable` refuses an occupied slot and a dismembered limb (`Body.cs:1482-1497`) |
| 4 | Worn-item character encoding | `CharacterDataSync.CaptureCharacter` encodes worn limbs as `-(limbIndex + 2)` (`CharacterDataSync.cs:396-413`) |
| 5 | Restore wearable on a body | `CharacterDataSync.RestoreWearable` (`CharacterDataSync.cs:553-597`) |
| 6 | Guest ownership transfer table | `IItemControl.RemoveTransferredItem` / `AdoptTransferredItem` (`ItemArbitration.cs:317-351`) |
| 7 | Existing cross-player use wire | `PlayerItemUseRequestMsg` / `PlayerItemUseResultMsg` (NetMsg 116/117) — reused unchanged |

## 2. Design

- New pure Runtime catalog/application: `RemoteWearProfile`,
  `RemoteWearCatalog` (all native wearables), `RemoteWearApplication`
  (target limb exists/dismembered, no same-slot occupied, produce the worn
  negative-slot wire item).
- `PlayerItemUseService` handles wearables first in the existing branch chain:
  the acting player's inventory item is removed, the target snapshot gains the
  worn item, the guest transfer table is updated on both sides, and the result
  carries the exact worn item.
- `PlayerItemUseResultMsg.WornItem` (additive ProtoMember 8) lets the target's
  `PlayerInteractionApply` call `CharacterDataSync.RestoreWearable` inside the
  existing RemoteApply scope; the acting player's local item follows the normal
  destroyed/removed path.
- `PlayerInteractionApply.IsLocalUseItem` recognizes the wearable catalog so the
  existing Use button/per-item selectors expose wearables.
- **Scope limits** — timed/random medicine, minigame-random tools and timed
  tools remain future slices.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Wearable catalog | All native wearable ids map to slot/limb | `RemoteWearCatalog` source + Item.cs SetupItems references |
| Host placement | Worn item lands on target snapshot with negative slot | `PlayerInteractionServiceTests.Guest_WearsHelmetOnHost_MovesItemAndSendsWornResult` |
| Guest target ownership | Target guest transfer table adopts the item | `PlayerInteractionServiceTests.Host_WearsHelmetOnGuest_MovesItemAndAdoptsForGuest` |
| Slot conflict | Same wear slot already occupied is refused before consumption | `PlayerInteractionServiceTests.Wear_TargetAlreadyUsesSameWearSlot_IsRefused` |
| Dismembered limb | Missing/dismembered target limb is refused | `PlayerInteractionServiceTests.Wear_TargetLimbDismembered_IsRefused` |
| Wire | Additive `WornItem` round-trips in the existing result | `NetPacket.DecodePayload<PlayerItemUseResultMsg>` in the wear tests |
| Local apply | Target body reuses the existing RestoreWearable path | `PlayerInteractionApply.OnPlayerItemUseReceived` + `CharacterDataSync.RestoreWearable` |

## 4. Verification

- **L0 unit**: `PlayerInteractionServiceTests` +4.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` 1458 green,
  `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
