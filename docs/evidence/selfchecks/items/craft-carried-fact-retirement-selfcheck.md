# The Craft Report Retires the Carried Row — Self-Check (2026-10-20)

Delivery fact sheet for backlog item `review/craft-carried-fact-retirement.md`: a craft report's
`Destroyed` entry now retires the owner's clone fact-table row on the host and on every relay receiver,
and the in-craft character snapshot no longer leaves before the report that explains it.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | `Recipe.TryMake` consumes the materials and destroys them (`RecipeItem.UseItem` → `DropItem` + `Object.Destroy`), then spawns the product. | `reversing/Assembly-CSharp/Assembly-CSharp/Recipe.cs:172-193`, `RecipeItem.cs:125-186`, `RecipeResult.cs:9-77`. |
| 2 | A wearable product is worn through `Body.AutoPickUpItem`; a worn incumbent is NOT displaced: `DropItem(Item)` only acts on slot-held items and `WearWearable` refuses an occupied slot. | `Body.cs:494-529` (auto pickup), `:1301-1311` (`HoldingItem` slots only), `:1441-1452` (`DropItem` no-op for a limb child), `:1480-1518` (wear/refusal), `:1590-1607` (`GetWearableBySlotID`). The ticket's correction records the re-read. |
| 3 | The in-craft destroy report is suppressed (`CraftingSync.ShouldSuppressDestroy`), so the craft report is the only event carrying the consumed material. | `src/CasualtiesUnknownOnline.GameAdapter/Items/CraftingSync.cs`; `Patches/ItemPatches.cs` (the OnDestroy claim). |
| 4 | The clone fact table warned on the missing row (`WarnOnDivergence` missing-entry loop) and could only be cured by the next snapshot. | `src/CasualtiesUnknownOnline.GameAdapter/Character/CloneFactTable.cs`; `CloneFactTableDivergenceMonitorTests`. |
| 5 | The host's classification could not retire a carried row: `TransferredRemove` touched the transfer table + kernel only, the relay half called `RemoveWorldItemLocal` (a no-op for carried). | `src/CasualtiesUnknownOnline.Runtime/Session/Items/CraftSyncService.cs` (before the change), `CraftReportJudge.cs`. |
| 6 | The host's own craft left a destroyed carried item's kernel row alive: the own branch aligned world rows only, and the ordinary destroy report was claimed off. | `CraftSyncService.FireCraftReportReceived` (own branch), `ItemWorldSync.OnItemDestroyed`, `Patches/ItemPatches.cs`. |
| 7 | The ordering chain preserves report-before-snapshot on the host and on a third guest: both sends are reliable on Steam channel 0, and the host applies a member's report before it relays it. | `PacketSender`, `SteamTransport` (single channel), `CraftReportHandler`, `CharacterDataHandler` (save → apply → relay). The ticket's Limits name the one gap: a transport-refused send is dropped without retry. |

## 2. The change

- `src/CasualtiesUnknownOnline.Runtime/Session/Items/IItemControl.cs`: new `CarriedItemRetired(owner, id)`
  event; `ItemDestroyed`'s comment now says a carried row never travels through it.
- `src/CasualtiesUnknownOnline.Runtime/Session/Items/ItemService.cs`: publishes it through
  `RetireCarriedLocal(owner, itemId)`.
- `src/CasualtiesUnknownOnline.Runtime/Session/Items/CraftSyncService.cs`: the host's apply retires and
  stamps `CarriedRetire` on `TransferredRemove` and on a kernel-anchored `UnknownSkip`; world destroys are
  stamped `WorldDestroy`; an unattributable destroy keeps the old skip + warn. The relay applies the
  stamps (`WorldDestroy` → scene, `CarriedRetire` → that owner's fact row, `None` → nothing) and logs its
  routing counts.
- `src/CasualtiesUnknownOnline.Runtime/Session/Items/CraftCarriedRowRetirement.cs`: the attribution
  (`IsOwnedBy`: the kernel row must say the sender carries it) and the host's own-row retirement
  (`TryDestroy`, named at Information when there is no row).
- `src/CasualtiesUnknownOnline.GameAdapter/Character/CloneFactTable.cs` +
  `Character/CharacterDataSync.cs` + `GameAdapterSessionBinding.cs`: the targeted
  `RetireCarriedItem(owner, id)` seam (one owner's tree, top-level or nested) and its subscription.
- `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyPatches.cs`: the wear postfix computes
  `CallContext.IsWithin(CallContext.Origin.Craft)` once and exits before `OnInventoryChanged` under it.
- Tests: `CraftSyncSimulationTests` (host/relay retirement, kernel-anchored untracked entry, world
  negative, another-guest race negative, Changed negatives, host-own kernel row),
  `CloneFactTableDivergenceMonitorTests` (a retired row leaves the next snapshot quiet),
  `InventoryChangeReportOrderPinTests` (wear withhold with a guard-block matcher, craft postfix order,
  patch-bypass check, binding route, both apply paths and both stamps, attribution helper).

## 3. The independent review, and what it changed

FULL-tier adversarial review, fresh context, frozen tree. Findings and dispositions:

| Finding | Disposition |
|---|---|
| BLOCKER: the I6 matrix row grew past the 1200-character row cap (`SyncCoverageGateTests` red). | The row now states the policy in one clause ("a carried destroy retires each receiver's fact row"), 1200 characters; `SyncCoverageGateTests` 14/14 green; no new evidence entry was needed. |
| MAJOR: the `UnknownSkip` retirement was unanchored — the fact table removes an id from EVERY owner's table, so a craft report could retire the winner of a pickup race's row (and the relay repeated it). | The event is owner-addressed (`RetireCarriedItem(owner, id)`), the host retires only what its transfer table or kernel row attributes to the sender, an unattributable destroy keeps skip + warn, and the relay acts only on the host's `CarriedRetire`/`WorldDestroy` stamps. New tests: the kernel-anchored retirement and the pickup race (no retirement of the winner's row). |
| MINOR: the host-own `WorldDestroy` stamp was unpinned. | The source pin asserts both stamps in the own branch, and `HostCraft_BroadcastsToBothGuests_…` now asserts the guest's scene removal. |
| MINOR: the withhold matcher accepted any `return;` between the scope mention and the snapshot. | The matcher follows the guard local and requires the return to sit in the guard's OWN block; new samples cover a foreign exit, a snapshot before the exit, the direct-guard form and the local form. |
| MINOR: no negative pin that a Changed entry keeps the row. | `GuestCraft_ChangedEntry_…` now asserts no retirement on host or relay; `HostCraft_ChangedCarriedMaterial_…` asserts the kernel row stays carried. |
| MINOR: `ItemService` sat at 599 of the 600-line gate. | The attribution and the own-row retirement moved into `CraftCarriedRowRetirement`; the touched types are `ItemService` 598, `CraftSyncService` 596, `CloneFactTable` 599 (all under the cap, no debt entry). |
| MINOR: the ticket was still `todo/` after the code completed. | Moved to `review/` with the index row and the census row in the same change. |
| NIT: the `CraftApplyKind.None` comment read as if relay receivers adopt a Changed entry. | Reworded to "the reporting side's own carried record"; the enum now names `CarriedRetire` and defines `None` as no receiver action. |
| NIT: the acceptance row's removal-line demand held only when the receiver held the row. | The row now says "each receiver whose fact table held the row". |
| E: a transport-refused craft report has no retry, so the next snapshot can still announce the disappearance. | Named in the ticket's Limits; the matrix row claims no convergence guarantee. |

## 4. What this cycle did not verify

- The runtime half is the acceptance batch (`20261025-d`) after the commit; its record is the citation a
  reader of the done ticket opens. Until then the ticket stays in `review/`.
- The host-as-owner craft direction is covered by the simulation half only (kernel row + stamps); the
  batch drives a member as the owner.
- The real Unity patch firing order, the deferred `Object.Destroy` and a genuinely transport-refused
  report need the game running; the pre-fix red is an observed local run of the four source-shape pins,
  not something the frozen tree can reproduce on demand.
