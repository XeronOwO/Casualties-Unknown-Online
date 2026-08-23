# Trader Recruit — Random Trader-Stock Items — Self-Check (2026-08-23)

Delivery fact sheet for closing the remaining "random trader items" slice of
the KrokMP-inspired Trader Recruit co-op revive. A successful host-authoritative
recruit now also grants the revived player 1–3 distinct items drawn from the
host-side trader's current stock. The revive itself remains the existing
heal-in-place slice; this increment only adds the bonus item delivery.

## Mechanism inventory

| # | Mechanism | Vanilla behaviour | CUO change | Evidence |
|---|---|---|---|---|
| 1 | Trader stock | the trader has a stock list (`TraderScript.items`, `TraderItem.cs`) used by purchase/trade UI | the host reads the same authority through `TradeExecutor.Read` and treats it as the gift pool | `TradeExecutor.Read`, `TradeStockState` |
| 2 | Gift count | n/a | 1–3 (`TraderRecruitPolicy.MinGiftItems`/`MaxGiftItems`), capped by the target's empty backpack/hand slots | `TraderRecruitPolicy`, `BuildTraderGiftItems` |
| 3 | Random selection | n/a | pure `TraderRecruitPolicy.SelectGiftItemIds` with an injected `Func<int,int>` random index, so the host uses `UnityEngine.Random.Range` and the policy stays L0-testable | `TraderRecruitPolicy.SelectGiftItemIds` |
| 4 | Item wire fact | n/a | the fresh item fact is captured from the prefab (`Resources.Load` + `ItemStateCodec.CaptureItem`) without a temporary scene instance; a host instance id is allocated via `ItemIdAllocator.AllocateId` | `CreateGiftItem` |
| 5 | Host snapshot | n/a | gift items are appended to the revived character snapshot before `SaveCharacterData` | `HandleHostRequest` |
| 6 | Guest ownership | n/a | each gift for a remote target is registered in the host's transfer table via `ItemService.AdoptTransferredItem` so later use/slot/drop reports arbitrate normally | `HandleHostRequest` |
| 7 | Wire delivery | n/a | `TraderRecruitResultMsg` gains `Items` (protobuf member 4); `ProtocolVersion` 36→37 because a v36 peer would revive but silently miss the gift | `TraderRecruitResultMsg`, `ProtocolVersion` |
| 8 | Target apply | n/a | the target's local body restores each gift under `RemoteApply` (`ItemStateCodec.RestoreItem`), preferring the host-chosen slot and falling back to `Body.FirstEmptySlot`; then `ReportInventoryChanged` refreshes host save + peer clones immediately | `ApplyTraderGiftItems` |
| 9 | Full inventory | n/a | if the target has no empty slots, no gifts are granted — the revive still succeeds | `BuildTraderGiftItems` |

## Design

- **Stock is a catalog, not a consumable inventory** in this increment: the
  trader's stock is used to pick the reward items but is not depleted. The
  existing one-recruit-per-trader guard already prevents repeat farming.
- **No temporary Unity object** is created on the host to make the gift fact:
  the wire item is captured directly from the prefab asset, so no item-domain
  spawn/destroy report can fire. The recipient binds the host-allocated
  `InstanceId` during restore.
- **Transfer-table adoption** is done for remote guests before the result is
  sent; the host's accept-with-correction layer then knows those item ids
  belong to the revived guest.
- **The revive result remains a single message**: health + limbs + bonus items
  travel together, and the target re-reports the full character snapshot at
  the end of the same RemoteApply scope.

## Verification design

1. L0 (pure policy): `TraderRecruitPolicyTests` — `FindEmptySlots` (empty,
   full, worn-only, missing `SlotCount` fallback) and `SelectGiftItemIds`
   (distinct selection, out-of-range random index, empty stock).
2. Wire (fake network): `TraderRecruitChannelTests` — `TraderRecruitResultMsg`
   round-trips the new `Items` list through the real dispatcher.
3. Full suite: 1244 green; build 0 warnings/0 errors; `dotnet format`;
   architecture / event-replay / entity-event dispatch gates pass.
4. Runtime (final acceptance only): a guest dies, another player recruits at a
   friendly trader, and the revived player receives 1–3 trader-stock items in
   its inventory; the other peers see the clone inventory update after the
   immediate re-report. No manual acceptance during the dev cycle; evidence is
   L0 + static.

## Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Gift pool | host trader stock via existing read | `TradeExecutor.Read` |
| Gift count | 1–3 capped by empty slots | `TraderRecruitPolicy.Min/MaxGiftItems`, `FindEmptySlots` |
| Random selection | pure + injected random index | `SelectGiftItemIds` |
| Item fact | prefab capture, no temp object | `CreateGiftItem`, `ItemStateCodec.CaptureItem` |
| Instance id | host bare allocation | `ItemIdAllocator.AllocateId` |
| Host snapshot | revived data persists gifts | `HandleHostRequest` |
| Guest ownership | transfer-table adopt | `ItemService.AdoptTransferredItem` |
| Wire | result `Items` + v37 | `TraderRecruitResultMsg`, `ProtocolVersion` |
| Target apply | RemoteApply + fallback slot + re-report | `ApplyTraderGiftItems`, `CharacterDataSync.ReportInventoryChanged` |
| Structure | all touched classes under 600-line gate | `tools/check-architecture.ps1` |
