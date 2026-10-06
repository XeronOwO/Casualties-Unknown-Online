# Container-Move Pair Classification — Self-Check (2026-10-06)

Delivery fact sheet for two tickets that land together: `done/container-move-snapshot-only-sync.md` (row A1g,
rejected by batch `20261006-b`) and `review/drop-pending-single-slot-overwrite.md` (whose single pending slot the
container-move pair made untenable). On the deployed artifact of batch `20261006-b` the gesture worked — the
operator captured the intent, the owner's loop ran and its scene showed the child in the target bag — but the
child's two container calls reached the peers as "the child left the world" plus "the child was picked up" while
the TARGET container's contents changed with no event.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | `Container.UnloadItem` is the game's "detach this item into the world" primitive: it re-parents the item to nothing, re-enables its rigidbody and lifts it 1.5 units. | `reversing/Assembly-CSharp/Assembly-CSharp/Container.cs:154-169` |
| 2 | The container EXPANSION is exactly that detach followed by a load into the target, in ONE bracket, once per child the target's guard admits. | `reversing/…/PlayerCamera.cs:1585-1593` (`dragItem.container.UnloadItem(item3, null)` then `container.LoadItem(item3)`), mirrored on the owner by `RemoteIntentApplier.ApplyMoveContainerChildren` |
| 3 | The load hook classified the move by the scene AT the post-mutation instant, so the pair's own detach read as "it came from the world". | `Patches/ContainerItemPatches.cs` load prefix `__state = ItemWorldSync.IsWorldItem(item)`; `ContainerItemSync.OnLoadedIntoContainer`'s `wasWorldItem` branch reported a PICKUP |
| 4 | The unload hook reported a drop the instant it saw a parentless item, so the pair sent TWO messages and the drop one materialized a world copy on every peer. | old `ContainerItemSync.OnUnloadedFromContainer` → `_items.SendItemDropped(…)`; batch `20261006-b` guest-owner direction: `[ItemDrop] dogfood (id …) not present — requesting materialization at (0.8,483.6)` and `[CarriedSync] removed … from …'s snapshot contents` |
| 5 | ONE target-root contents fact covers the source container too: the peers' fact table prunes a moved descendant from wherever its tree held it. | `CloneFactTable.RemoveMovedDescendantDuplicates` (`CollectDescendantIds` + `RemoveDuplicateEntries`, run after the nested replace) |
| 6 | The pending machine held ONE slot, and the same-frame flush the caller attempted could not settle it — so a second departure in one frame lost the first report. | old `DropPendingState` (`private (ulong, int, long)? _pending`) + `ItemWorldSync.OnItemDropped`'s "flush the first first" call against `TryFlush`'s `currentFrame <= pending.Frame` refusal |
| 7 | Every owner's release shape funnels into the same two hooks, so the pair's classification is one seam — and `UnloadItem` also has callers with NO load after them, which is the departure's other half. | `PlayerCamera.TryPerformInventoryAction` (expansion + drop-onto-container), `PlayerCamera.TryPerformWorldActions` (take-out, then a container under the pointer), `PlayerCamera.TryPerformSpecialUIAction` (the `ContainerBack` drop), `RemoteIntentApplier.ApplyMoveContainerChildren` / `ApplyMoveIntoContainer` / `ApplyTakeOutOfContainer`; the unload-only callers are `Body.PickUpItem` (resolved by the pickup hook), `Body.WearWearable` (resolved by the slot carrier's own report, the edge this cycle's review found missing), `Body.CombineItems`/`CombineLiquids` (the item really is in the world, so the flush reports it) and `SelfHarmer.Suicide` (a pickup into the hand slot) |

## 2. The change

- The unload half registers a DEPARTURE with where the item came from, and sends nothing itself:
  `ContainerItemSync.OnUnloadedFromContainer(Item, bool wasWorldItem)` calls
  `_dropState.EnterDrop(itemId, item, position, op, source)` and logs `[ContainerUnload] … from the
  {CarriedInventory|World} side`. The pre-unload fact is captured in `ContainerItemPatches`' unload PREFIX
  (`new UnloadState(item.transform.parent == __instance.transform, ItemWorldSync.IsWorldItem(item))`), because
  the postfix runs after `SetParent(null)` — explicit state between hooks, never scene inference.
- The load half consumes it and classifies through the pure Runtime rule
  `ContainerLoadClassifier.Classify(bool landsInWorldContainer, DropPendingState.Source? departure, bool wasWorldItem)`:
  a world-container target takes `Kind.WorldContainerDrop` whatever the item came from; otherwise the departure —
  when there is one — decides between `Kind.Pickup` (it came from the world) and `Kind.CarriedContent` (a move
  inside the carried inventory); when no departure was registered, the pre-load scene capture decides.
- The three carriers stay the ones decision 235 established: the pickup report, the carried ROOT's full contents
  fact (`SendItemCarriedSync` on a host, `SendItemContainerContent` on a guest), and the bound drop report. No
  wire member, no protocol number.
- The departure machine now holds ONE ENTRY PER ITEM (`DropPendingState` is a `Dictionary<ulong, Pending>`,
  `ItemDropState` carries the game-side place per item, `ItemWorldSync.FlushPendingDrop` reports every due entry).
  The pair needed it: an expansion unloads one child per REFUSED load, so a second child's unload would have
  overwritten the first child's still-pending report. The per-item throw merge (`TryConsumeByThrow`), the cancel
  edges (re-pick, container load, destruction, replacing unload, world-left reset) and `TrySettle`'s
  frame/alive/standalone rule are unchanged, and `ItemWorldSync.OnItemDropped` no longer forces a same-frame
  flush of another item's entry.
- The flush takes its due set OUT of the machine before the first report goes out: a report RE-ENTERS the flush
  (`ItemMessageFlowService.SendItemDropped` → `PendingItemCreations.SettleBeforeOperation` →
  `PendingItemCreationReports.SettlePendingCreations` → `ItemWorldSync.FlushPendingDrop`), and a shared buffer
  cleared by that re-entry would have corrupted the outer iteration.
- Registering a departure for EVERY landed unload widened the machine's producer set, so its resolution edges had
  to grow with it: a wear straight out of a carried container (`Body.WearWearable` detaches the item and parents
  it to a limb — no container load, no pickup) left an entry that could never settle, and the entry held the
  owner's immediate snapshots back for the rest of the session. The slot carrier now resolves a departure for the
  item it reports (`ItemSlotSync.ReportCarried` → `_dropState.TryCancel`, traced `Cancelled`/`ReHomed`), and
  `ContainerMovePairGateTests.EveryCarrierThatReHomesAnItem_ResolvesThePendingDeparture` pins the pairing across
  the four carriers that can re-home an item.
- Decision 236's query now reads whether a report is still OWED — `HasPendingDropReport =>
  _dropState.HasReportOwed`, i.e. some entry's item is a standalone world item right now — rather than whether an
  entry merely exists. A drop that is really coming always qualifies (every body-drop carrier runs after the
  native call left the item parentless), and an entry that can never settle can no longer hold the snapshots
  back: the blast radius of a producer this change cannot enumerate is one report, never the session.

## 3. Verification

| Mechanism × change × evidence | Evidence |
|---|---|
| The carrier rule's truth table, including batch `20261006-b`'s own reading (a departure outvotes a world scene capture) | `tests/CasualtiesUnknownOnline.Tests/Items/ContainerLoadClassifierTests.cs` — 9 table cases + `Classify_ADepartureOutvotesAWorldSceneCapture` |
| Two departures in one frame are two reports, not one overwritten report | `DropPendingStateTests.ASecondDepartureInTheSameFrame_DoesNotSwallowTheFirst` and `…TwoDeparturesInOneFrame_BothSettleAndReportAfterTheFrame`. The behaviour was read RED against HEAD with the then-current three-argument `EnterDrop` (the first assertion — the first departure survives a second one in the same frame — is false on the single-slot machine); the committed body passes the `Source` this fix adds, so it no longer compiles against HEAD, and the observable red is the committed assertion's failure on the pre-fix behaviour rather than against the pre-fix source text |
| One entry per item still folds a repeated departure of the SAME item, and the reset hands every op back | `DropPendingStateTests.EnterDrop_SameItemTwice_ReplacesThatItemsOwnEntry`, `…ResetAll_ReturnsEveryOpAndClears`, `…CopyItemIds_IsASnapshotTheSettleCanRemoveFrom` |
| Every carrier that can re-home an item resolves a pending departure — the edge a wear out of a carried container needs | `ContainerMovePairGateTests.EveryCarrierThatReHomesAnItem_ResolvesThePendingDeparture` (census floor 6 across `ContainerItemSync` 2, `PickupSync` 1, `ItemSlotSync` 1, `ItemWorldSync` 2; matcher pinned by seven samples including a comment mention and a renamed receiver). RED on HEAD for the slot carrier — it declared no resolution call at all, and the wear path is what this cycle's independent review found |
| The pair's wiring: the unload registers with a source and sends no report itself, the load consumes and classifies, the patch captures the pre-unload fact, the bridge carries it, the machine declares the source | `tests/CasualtiesUnknownOnline.NormativeGates.Tests/ContainerMovePairGateTests.cs` — five rule tests RED on the pre-fix tree (the unload half still sent `SendItemDropped`, the load half never asked a departure, the prefix captured only "was inside", the bridge had one parameter, the machine had no `Source`), each matcher pinned with positive and negative samples |
| The item-fact carriers still ask the replay query and never the bare attribution (the pair's hooks are carriers) | `ItemFactReportScopeGateTests` — unchanged and green |
| Decision 236's re-report ordering still asks the pending state, now the owed half of the per-item set | `RemoteIntentReportOrderGateTests.ThePendingDropQuery_ReadsTheDropMachinesUnsentReport` (its pinned state text moved from the single slot's phase to `_dropState.HasReportOwed` in the same change, with the reason in the constant's doc) |
| The sync-coverage audit's own quote of the pending machine still resolves to a real source line | `docs/evidence/sync-coverage-evidence.json` (I5's `DropPendingState.cs` entry repointed to `TrySettle`'s condition) — gate `SyncCoverageGateTests.SyncCoverageEvidence_EveryQuoteMatchesItsSourceLine` |
| The runtime reading — PLANNED, not evidence yet: the expansion's child load announces the TARGET's fact and both viewers' monitor stays at zero, in both owner directions and in the local control, plus the regression kinds | the next three-client batch (`20261006-c`), whose records will be `docs/evidence/acceptance/container-move-snapshot-only-sync-20261006-c.md` and `…-drop-pending-single-slot-overwrite-20261006-c.md` |

Behavioural suite 4,637/4,637 and gate project 382/382 on the frozen tree, `dotnet format` clean.

## 4. Limits

- **No session ran in the writing of this change.** Everything above is the unit and source-shape half; row A1g's
  verdict belongs to batch `20261006-c` against the deployed artifact, and the ticket stays in `review/` until
  that record exists. The classification's *runtime* half is exactly what no test in this repository can reach:
  the Unity scene is not available to the suite, which is why the gate pins the wiring and the classifier's table
  is a pure unit rule.
- **A multi-child expansion reports one message per child**, each carrying the TARGET root's fact as it stands at
  that moment (the last one is the whole truth). The native loop has no bracket boundary a hook could announce
  once, and every child's own move is a fact the peers must apply; the alternative — suppressing the per-child
  reports and emitting one — would need a patch inside `PlayerCamera`'s own loop, which no layer here can place
  between two statements. Nothing reads this as a divergence: each report is the truth at its own moment, and the
  peer prunes the moved child from wherever its tree held it.
- **The container-broke spill (`Container.UnloadAllItems`) keeps its immediate per-child drop report.** It is the
  same detach primitive, but no move follows it in its bracket (the container is gone), so it has no pair to
  resolve — a departure would only delay a report whose landing is not in question. Named here rather than
  "aligned" away later.
- **An item dragged out of a carried container and dropped on the ground now reports one frame later**, because
  the departure waits out its bracket. That is the same deferral a body drop has always had, it is what
  `HasPendingDropReport` holds the immediate re-report for (decision 236), and the reported position is still the
  detach position.
- **The departure machine's producer set is wider than before, and that is the one thing this change cannot
  enumerate.** Before it, `EnterDrop` had exactly one caller (`ItemWorldSync.OnItemDropped`, a short-lived entry
  every throw/flush/cancel resolves); the unload half now registers for EVERY landed `Container.UnloadItem`,
  including unload-only callers. The known ones are resolved (the pickup hook, the slot carrier's report, the
  flush for the two callers whose item really is in the world, and the hand-slot pickup), and the pairing is gated
  — but a native re-home path with no carrier at all would still leave a frozen entry: its own report never goes
  out, the item trace keeps a begin-without-end (the observer that assert watches), and it costs two list
  allocations in the frame-end flush for as long as it lives. Two further ways into that state are named rather
  than implied: a destroy while destroy reports are suppressed (a scene teardown), and a REMOTE deletion, which
  zeroes the item's instance id before the destroy runs — so the destroy's `TryCancel` looks up id 0 and misses.
  None of them can hold an owner's snapshots back any more (the hold asks whether a report is still OWED, not
  whether an entry exists), and the world-left reset clears whatever is left. Reachability of the wear case itself
  is argued from the native call sites, not observed: no session exists in this cycle.
- **A wear now releases an immediate snapshot whose ordering the next batch should read.** With the stuck entry
  gone, a wear out of a carried container lets `GameAdapterBridge.OnInventoryChanged` run its immediate re-report
  BEFORE the slot carrier's own wear report reaches the wire, and the peers' clone fact table is the observer that
  would say so ("slot N → M — a carried move without an event sync"). The same order already existed for a plain
  wear (slot → limb), so this is a pre-existing ordering property rather than something this change introduced —
  but the path it applies to is one this change released, so the next batch should drive a wear (a carried
  wearable, the radial centre) and read both peers' `[CharSync] divergence` lines. The fix, if the reading is
  real, is a hold rule for the carried-fact carriers, which is a decision of its own and not a patch here.
- **The battery family was not re-staged** by this change: `A1f` (battery unload) was read `pass` by batch
  `20261005-e`, this change touches the container pair rather than the battery branch, and the batch's fixture set
  decides whether that row can be re-driven.
- **The `reversing/sync-audit/` snapshot still carries the previous quote text and line numbers** for the machine
  it audited. That tree is a point-in-time audit input (gitignored); the tracked
  `docs/evidence/sync-coverage-evidence.json` is what the gate verifies and it is repointed.
