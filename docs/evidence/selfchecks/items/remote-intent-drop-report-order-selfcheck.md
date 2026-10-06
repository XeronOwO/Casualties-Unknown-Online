# Remote Intent Drop Report Order — Self-Check (2026-10-05)

Delivery fact sheet for backlog item `todo/container-move-snapshot-only-sync.md`: the drop half of row A1
that batch `20261005-e` rejected. On the deployed `0.1.0+cdd93044…` the container kinds, the take-out, the
slot release and the battery unload all read the operator's and the third peer's clone-fact monitor at zero;
the drop kind warned on both viewers, in both owner directions, with
`left the inventory without an event sync`.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The drop is carried by the owner's OWN item report, and that carrier is DEFERRED one frame on purpose. | `src/CasualtiesUnknownOnline.GameAdapter/Items/ItemWorldSync.cs` `OnItemDropped` → `ItemDropState.EnterDrop`; `src/CasualtiesUnknownOnline.Runtime/Session/Items/DropPendingState.cs` `TrySettle` refuses `currentFrame <= pending.Frame` so the game's `DropItem` → `ThrowItem` pair can set the final velocity ("a zero-velocity report materialized a ghost on the host") |
| 2 | Until that report is committed the peers' clone fact table still holds the item, and only the report removes it. | `CloneFactTable.RemoveCarriedItem`, subscribed at `GameAdapterSessionBinding` (`domains.Items.ItemDropped += OnCarriedItemDropped`); live logs — third peer `[CarriedSync] removed 1181436987331 …` 2 ms after `[ItemDrop] lantern …` |
| 3 | `RemoteIntentApplier` ended every applied discrete intent with an immediate character re-report; for a `DropItem` that snapshot already omitted the item and was sent BEFORE the deferred report. | `RemoteIntentApplier.Apply` (`domains.CharacterDataSync.ReportInventoryChanged(body)`); batch `20261005-e` owner log 22:50:37.159 (`[CloneRender] inventory changed — immediate re-report.`) against 22:50:37.188/.189 (`origin=FlushPendingDrop result=Committed(1) events=[Drop, Flush]`) |
| 4 | The peers read that pair exactly as the monitor documents: a snapshot that lost an item whose event is still in flight warns. | `CloneFactTable.WarnOnDivergence` ("A change whose event is still in flight trips the warning too"); operator 22:50:37.189, third peer 22:50:37.182 |
| 5 | Both drop kinds share ONE carrier, so both share the order. | `Body.DropItem(Item)` (Body.cs:1441) and `Body.DropWearable(Item)` (Body.cs:1521), reported by `BodyItemPatches.DropItemPatch` / `DropWearablePatch` into `ItemWorldSync.OnItemDropped` |
| 6 | The owner has a SECOND re-report path, in the patch layer, and it is unconditional. | `Patches/BodyPatches.cs` `DropWearablePatch` — `private static void Postfix(Item item) => PatchBridge.Impl?.OnInventoryChanged();` → `GameAdapterBridge.OnInventoryChanged` → `CharacterDataSync.ReportInventoryChanged`. It runs INSIDE the applier's `body.DropWearable(item)` call, before the applier can decide anything, and no call-context check stands in it: a kind-shaped rule is inert for this kind. This cycle's independent adversarial review found it, and it is why the rule is asked of the PENDING STATE at BOTH entry points |

Whole-family audit: every other discrete kind's carrier sends INSIDE the apply scope, so its re-report lands
after the event that explains the change and only speeds convergence — `ContainerItemSync`
(`OnLoadedIntoContainer`, `OnItemUnloadedFromContainer`), `PickupSync.OnPickedUp`, `ItemSlotSync.OnItemRehomed`
and `ItemUseSync.OnItemUsed` all call their `_items.Send…` synchronously in the same frame. Two controls in the
rejected batch's own run separate the carrier from the order: a LOCAL drop of the owner's item produced no
immediate re-report and no warning at all, and a world-driven burst that dropped three of the host's items
inside 2 ms warned about exactly the two whose reports were still pending. The kinds whose carrier is
deliberately suppressed keep the re-report and are named here so nobody "aligns" them away later: a
remote-driven `CombineItems` has its craft report suppressed by `CraftingPatches.BodyCombinePatch` (decision
234), and `GiveToTrader`'s consumed item reaches the peers on that re-report alone.

## 2. The change

- The rule is asked of the item domain's PENDING STATE, not of the intent kind:
  `ItemWorldSync.HasPendingDropReport` (`_dropState.HasReportOwed` — a report registered, not yet sent, and still
  owed, because the item is in the world) is the fact that a
  report is registered and NOT yet sent, and while it holds, no inventory snapshot goes out.
- Both entry points ask it: `RemoteIntentApplier.Apply` before its re-report, and
  `GameAdapterBridge.OnInventoryChanged` — the patch layer's one entry point, which is what covers §1's row 6.
- Asking the state rather than the kind also covers a kind whose native call left a drop behind on a path that
  did not land: R9's slot release drops the two slot occupants before its own pickup may refuse, and that
  refusal keeps the pending report alive.
- The two drop branches gained the landed check every other kind already has
  (`body.HoldingItem(item)` / `body.GetWearable(item.id) != null` read back after the native call), so a stale
  or refused drop intent is logged as a refusal instead of as a write.
- Both rejected alternatives are recorded rather than left implicit: a same-frame flush would report a drop
  without its throw velocity (the ghost the pending state exists to prevent), and a deferred re-report would
  put two messages on one fact. One operation = one message; the drop report already removes the clone entry.
- New gate `RemoteIntentReportOrderGateTests` pins both entry points' guards, their polarity and the query's
  own state; the superseded Runtime kind-classification experiment was deleted rather than kept beside it.
- No wire member, no protocol change.

## 3. Verification

| Mechanism × change × evidence | Evidence |
|---|---|
| Both re-report entry points ask the pending-drop query, with the polarity that holds the state back | `RemoteIntentReportOrderGateTests.EveryReReportEntryPoint_AsksThePendingDropQueryFirst` — RED on the pre-fix tree ("never asks `HasPendingDropReport`" for the applier, and the gate did not exist for the bridge), green after; the matcher is pinned by nine positive/negative samples including the reversed polarity and a condition that merely names the query |
| The query reads the drop machine's unsent report, in the file that owns it | `RemoteIntentReportOrderGateTests.ThePendingDropQuery_ReadsTheDropMachinesUnsentReport` |
| The deferral the rule depends on is the machine's own: a same-frame settle is refused, a later one succeeds | `tests/CasualtiesUnknownOnline.Tests/Items/DropPendingStateTests` (`TrySettle_SameFrame_Rejected`, `TrySettle_NextFrame_AliveStandalone_Consumed`) |
| The peer-side half the order depends on — a drop report removes the item from the clone fact table before the next snapshot — is unchanged and still pinned | `CloneFactTableDivergenceMonitorTests` (the monitor still warns when only the snapshot carries a change) |

## 4. Limits

- No session ran in this cycle. The fix's own reading is the ticket's row A1d, driven by the next three-client
  batch in BOTH owner directions over the operator's and the third peer's monitor at zero for at least one
  full periodic cycle — and it must now include a remote-driven `DropWearable`, the kind §1's row 6 was found
  on, which no batch has driven yet.
- The claim is about the kinds this batch drove plus the drop carriers' mechanism; the remote-driven
  `CombineItems`, `GiveToTrader`, `SwapSlots`, `UseItem`/`WearItem` and battery-LOAD kinds were not driven in
  `20261005-e`, so their carrier timing is read from the code (in the apply scope) and not from a run.
- The container-expansion kind (`MoveContainerChildren`) is still blocked on a driver capability — the native
  branch needs `Input.GetKey(KeyBinds.GetBind("expanddesc"))`, which the in-process driver cannot hold — so
  row A1g stays unjudged and the ticket stays open beside this fix.
- The drop machine had ONE pending slot when this fact sheet was written, so two drops in one frame overwrote the
  first report (the pin that asserted the overwrite as intended is gone; the machine now holds one entry PER ITEM —
  decision 238). This rule made the consequence louder — the peers then get no event AND no immediate snapshot — so
  it was filed with its evidence as `review/drop-pending-single-slot-overwrite.md` (re-scoped there on 2026-10-07:
  the occupied-destination release the next clause names registers no departure, see the ticket's
  `## The row's producer`); `RemoteIntentSlotRelease.Plan`'s
  occupied-destination slot release reaches it.
- The other `ReportInventoryChanged` call sites (`PlayerInteractionApply`'s cross-player applies, the trader
  recruit coordinator, the push and medical appliers) are NOT re-ordered by this change: none of them is an
  intent replay on the owner's own items, and the batch did not drive them.
