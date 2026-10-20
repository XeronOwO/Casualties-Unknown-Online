# The character snapshot is posted before the item reports, so the peers' fact table warns on every wear and swap

- Status: Done (batch `20261025-c`, 2026-10-20: the member's bag-child wear, hand swap and `SwapSlots`
  swap over occupied slots each reported the item fact before the immediate snapshot; both viewers applied
  every moved item's fact and logged **zero** `[CharSync] divergence` across 4 s windows. Rows 1–4 pass;
  record `docs/evidence/acceptance/snapshot-overtakes-item-reports-20261025-c.md`).
- Priority: Medium
- Category: Item sync / clone fact table (carried-fact event ordering)
- Source: batch `20261025-b` (2026-10-20) — while accepting the container-child tickets, the run drove a
  radial wear of a bag child and a `SwitchHands` slot move on the owner; both viewers logged the clone-fact
  monitor's divergence pair in the same millisecond window in which the carrying fact was applied.
- Related: `done/container-content-event-gap-on-repick.md` (the missing-event half the same batch closed;
  its own rows are clean), `done/pickup-early-return-kernel-relocation.md` (the wear row this run drove),
  `done/remote-inventory-native-parity-rework.md` (batch `20261006-g` read the slot-item swap's warnings as
  "the native path's"), `done/drop-pending-single-slot-overwrite.md`,
  `src/CasualtiesUnknownOnline.GameAdapter/GameAdapterBridge.cs` (`OnInventoryChanged`, the order rule),
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyPatches.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyItemPatches.cs`

## Symptom (read from batch `20261025-b`, three clients, one lobby)

The owner wore an `autopump` out of its carried `trashbag` in process (`Body.WearWearable`). Both viewers
logged two warning lines each, 1 ms before the carrying fact was applied:

```text
[WRN] […CloneFactTable] [CharSync] divergence for <guest>'s trashbag (id 9492956821): nested container contents changed without an event sync (the 1 Hz snapshot carried it).
[WRN] […CloneFactTable] [CharSync] divergence for <guest>'s autopump (id 22377858709): a new carried item the fact table never saw — a pickup without an event sync (the 1 Hz snapshot carried it).
```

```text
[INF] […CloneFactTable] [CarriedSync] applied autopump (id 22377858709) to <guest>'s snapshot — re-rendering the clone.
```

The host applied the fact at 21:52:22.764 / .765; the warnings are at .763 and the third client's at .768 /
.771. The same run's `SwitchHands` slot move (`a1c-*`) logs `dogfood slot 1 → 0` and `trashbag slot 0 → 1`
at 21:51:29.267 and the two `[CarriedSync] applied …` lines at .269 / .270. The owner logs no divergence
(it never compares its own snapshot).

## Mechanism

`BodyPatches.WearWearablePatch.Postfix` calls `OnInventoryChanged()` — the immediate character snapshot —
**before** `OnItemWorn` / `OnItemPickedUp`, and `BodyItemPatches.SwapSlotsPatch` / `SwitchHandsPatch` do the
same before their `OnSlotMoved` calls. The snapshot is therefore posted first and the item report follows;
the peers' fact table compares each arriving snapshot against the facts it has, so the snapshot that
legitimately repeats the change arrives before the event that explains it and the divergence check fires.
The entry point's own docstring already states the intended order (the pending-drop case defers the
snapshot so it cannot overtake the event), so these call sites defeat the rule they go through rather than
lacking one. Batch `20261006-g` read the slot-move shape as "the native path's" and concluded the warnings
were a different carrier's; this run shows the event does arrive — 1–3 ms later — and only the post order
is wrong.

## What a fix has to decide

- Which postfixes: `SwapSlots`, `SwitchHands`, `WearWearable` at least; `DropWearablePatch` only re-reports
  and has no item report to order against.
- The order or the deferral: report the item fact first and the snapshot second (the simplest reading of
  the rule), or keep the call site and defer the snapshot one frame the way the pending-drop case does.
  The second survives a report that is itself deferred (a drop) but costs a frame of clone latency; the
  first is exact when every report is same-frame, which these three gestures are.
- Whether the other `OnInventoryChanged` call sites have the same inversion: `RemoteIntentApplier` already
  reports the item facts inside the native call and re-reports after it (the model), `PlayerInteractionApply`,
  `TraderRecruitCoordinator` and `LocalBodyReset` need their own reading.
- A source-shape pin for the order, since the Unity adapter has no unit harness (the shape
  `ContainerArrivalPolicyTests.TheTwoDepartureSites_SendThePolicysReport` uses), plus the acceptance row
  below.

## The fix (this cycle)

- **Order, not deferral.** `WearWearablePatch`, `SwapSlotsPatch` and `SwitchHandsPatch` now send their
  item fact first and call `OnInventoryChanged` last. These three gestures report in the same frame
  (`OnItemWorn` / `OnItemPickedUp` / `OnSlotMoved` send immediately), so the order is exact and costs no
  clone latency. The one-frame deferral the pending-drop case uses would still need the pending check for
  a deferred drop and would delay every immediate re-report. `OnInventoryChanged`'s own summary now states
  the caller order, and `IPatchBridge` names it where a caller reads the method.
- **The other inventory-re-report sites were read; none needs the same change.** Most of the audit touched
  the sites that call `CharacterDataSync.ReportInventoryChanged` directly; the patch layer's
  `OnInventoryChanged` callers are exactly six: the three changed postfixes, `DropWearablePatch` (the
  pending-drop case the entry point already defers), and `RecipeTryMakePatch` / `BodyCombinePatch` (the
  craft report is committed by `OnCraftEnd` / `OnCombineEnd` before the snapshot). The receiver-side
  re-reports (`PlayerInteractionApply`'s transfer / heal / use results, `PlayerPushApply`,
  `MedicalOperationApply`, `TraderRecruitCoordinator.ApplyRevive`) apply a host-broadcast fact whose
  kernel commit already reached every peer, so the local snapshot is the convergence copy, not the
  event's first carrier, and those sites send no item fact of their own to order against. `LocalBodyReset`
  sends its heal-reset authority event first by design, and `RemoteIntentApplier` is the intent path's
  model — the item carriers report inside the native call, then the snapshot follows under the same
  pending-drop hold.
- **The craft-scope wear inside `WearWearablePatch` is NOT benign and is not closed here.** That snapshot
  still runs before the craft report, and the craft report retires only WORLD rows from the peers' clone
  fact table, so by code reading a destroyable carried material — and a wearable the auto-pickup
  displaces — leaves the fact table only on the next snapshot and trips the monitor's "left the
  inventory" warning. Filed as `todo/craft-carried-fact-retirement.md` with the code path and its own
  acceptance row.
- **The order is pinned in source.** `InventoryChangeReportOrderPinTests` reads the two patch files,
  removes whole-line, inline and block comments and requires every fact call of each gesture postfix to
  precede exactly one `OnInventoryChanged`; a theory feeds the same checker a correct body, an inverted
  body and a body whose fact call exists only in a comment. The pin failed on the pre-fix tree
  (`WearWearablePatch: … is reported after the snapshot`). The runtime half is
  `CloneFactTableDivergenceMonitorTests` (an event applied before the snapshot leaves the next snapshot
  quiet) plus the acceptance row below — the row ran in batch `20261025-c` and passed on every window
  (`docs/evidence/acceptance/snapshot-overtakes-item-reports-20261025-c.md`).

## Acceptance

Three clients on the deployed artifact, the owner wearing a bag child and swapping two occupied slots
through the native path: every client's window carries **zero** `[CharSync] divergence` for the moved items
over at least one full 1 Hz cycle, and each moved item's `[CarriedSync] applied …` line is present in the
same window (so the row cannot pass by the event never arriving).

## Evidence

`20261025-b/` under the directory named by `acceptance-artifacts-dir` — `ev-a2-host.txt`, `ev-a2-alt.txt`,
`ev-a2-guest-order.txt`, `ev-a2-host-order.txt` (the wear), `ev-a1c-host.txt` (the swap), `a2-guest-wear.json`
and `a1c-guest-switch.json` (the gestures). The cycle's self-check, its mechanism × change × evidence
table and the review's dispositions are `docs/evidence/selfchecks/items/item-report-order-selfcheck.md`.
