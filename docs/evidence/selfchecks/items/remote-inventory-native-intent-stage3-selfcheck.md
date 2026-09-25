# Remote inventory native parity — stage 3 item-interaction self-check (2026-09-25)

Ticket: `docs/backlog/todo/remote-inventory-native-parity-rework.md` (stage 3). Design record:
`docs/architecture/remote-inventory-native-parity.md` (amended by stage 3). Decisions: 217 (stage 0),
218 (the window's shape, stage 1), 219 (the container family and the while-dragging body), 220 (the
item interactions). Cycle scope: R10's radial-centre use/wear, R6's combine, R2/R3's battery
load/unload pair, the while-dragging favourite store, R12's trader hand-in and the two audits the
design left to this stage (the item sounds and the non-drag UI paths) — with
`ProtocolVersion.Current` bumped in the same change (37 → 38, the handshake unchanged as the
compatibility boundary).

## What landed (anchors are our own paths plus quoted text)

| # | Mechanism | Where it lives | What it does |
|---|---|---|---|
| 1 | Item-interaction captures | `Runtime/Session/PlayerInteraction/RemoteDragIntentCapture.ItemInteractions.cs`, "The item-interaction half of the drag-window state machine" | R10's use/wear, the two-item combine and battery load, the hit-item battery unload, the favourite store and the trader hand-in become intents; a second operand must be the bracket owner's own item with an authoritative id, and a dragged operand must be the dragged proxy |
| 2 | Wire operands | `Runtime/Protocol/Messages/RemoteInventoryIntentMsg.cs`, "The second item operand of the two-item kinds" and "The trader's world position" | the message carries `TargetItemInstanceId` and a nullable `TargetTraderPosition` (nullable so an absent operand cannot look like a trader at the origin) |
| 3 | Favourite store seam | `GameAdapter/Patches/PlayerCameraHandleWhileDraggingPatch.cs`, "The frame's favourite candidates" and "the frame's favourite stores" | the frame's own candidate buttons are read before the native body and compared after; an observed proxy store becomes a `ToggleFavourite` intent and the proxy's field goes back to the value the frame started with |
| 4 | R10 restored | `GameAdapter/Patches/RemoteDragMutationPatches.cs`, `RemoteDragBodyUseItemPatch` / `RemoteDragBodyWearPatch` (the suppression patch is deleted) | the native radial branch runs again and its two calls are captured like every other mutation |
| 5 | Radial no-op probe | `GameAdapter/Patches/PlayerCameraRadialActionProbePatch.cs` | records the native method's own `true` answer for a release that consumed nothing, so the window does not report it as an unclassified gesture (`RemoteDragNoOp.RadialCentreWithoutAnAction`) |
| 6 | Container pairing split out | `Runtime/Session/PlayerInteraction/RemoteContainerMoveCapture.cs`, "The container-call coalescing of one drag bracket" | the move-pair / take-out / expansion-batch rules became their own state machine after the aggregate-line gate caught the window at 625 lines; the window keeps its capture API and delegates |
| 7 | Owner replay | `GameAdapter/RemoteIntentApplier.cs`, the seven new switch arms | each kind runs the native call on the owner's real items, with the slot/battery credit and the trader's credit total as the verified writes |
| 8 | Host operands | `Runtime/Session/PlayerInteraction/PlayerRemoteInventoryIntentService.cs`, "The operands each intent kind needs" | the second item must be the owner's own instance and differ from the item; the trader operand must be present and finite |
| 9 | Trader identity | `GameAdapter/World/TraderLocator.cs` + `TradeStateSync` delegating to it | one position-keyed trader lookup with the trade domain's tolerance, shared by the applier and the trade state sync |
| 10 | Report-hook guards | `GameAdapter/Patches/UseItemPatches.cs`, `BodyPatches.cs` (`WearWearablePatch`), `TraderPatches.cs` (`GiveItemPatch`) | a display proxy never reports a local use or a wear re-report, and a trader hand-in reports only when the credit really moved |

## Findings that amended the frozen design (recorded, not silently absorbed)

| # | Finding | Evidence | Amendment |
|---|---|---|---|
| 1 | The favourite store needs no IL rewriting: the frame bracket can observe it | `PlayerCamera.cs:1736-1747` (the store is a field write on the first overlapping button's item, behind `GetKeyDown(favourite)`), `RemoteItemPresentation` (`favourited` is already a projected source value) | design §3.2.8: the value of the frame's candidate buttons is compared across the bracket, the store becomes an intent, the proxy's field is put back — and the stage-2 frame-skip cost disappears |
| 2 | R10 running again creates a native outcome with no call and no CUO name for it | `PlayerCamera.cs:1638-1648` (the branch consumes the release when the item is neither wearable nor usable) | design §3.2.5 and `RemoteDragNoOp.RadialCentreWithoutAnAction`: the probe reports the native answer instead of the window re-deriving the branch's geometry |
| 3 | The frozen vocabulary's `GiveToTrader` row is unimplementable as written | `PlayerCamera.cs:1663` reads `this.currentTrader`, which the OWNER's client does not have; the trade domain keys its messages by position (`TraderSwingMsg.Position`, `TradeStateSync.FindTraderAt`) | design §3.3: the trader is an operand carried as its world position, resolved by `TraderLocator` |
| 4 | `UnloadBattery` has one operand, not two | `PlayerCamera.cs:1545` (`item.battery.UnloadBattery(false)`) — the dragged item only told the dispatch the direction | design §3.3: the hit item takes `ItemInstanceId`; no second operand |
| 5 | A captured proxy call would have been reported as a local write by three existing hooks | `UseItemPatches.UseItemPatch`'s postfix calls `OnItemUsed` regardless of which prefix skipped the body; `WearWearablePatch`'s postfix calls `OnInventoryChanged` unconditionally; `TraderPatches.GiveItemPatch` reported every call, including the credit-cap refusal | design §3.6: the two proxy guards and the verified-credit rule are part of this change (rule 6, "patch hooks report only verified writes") |
| 6 | The item-interaction family does not fit in one file under the aggregate gate | the gate reported `RemoteDragIntentCapture : 625 aggregate lines (max 600)` | the partial split alone was not enough (the gate sums the type's files); the container pairing became `RemoteContainerMoveCapture`, leaving the window at 511 aggregate lines |

## Decisions taken in this stage

| Decision | Statement | Where it lands |
|---|---|---|
| Favourite seam | observed across the frame bracket, never an IL rewrite; the native condition still decides | design §3.2.8, decision 220 |
| Radial restore | the suppression patch is deleted; the native branch's own no-op answer is probed, not re-derived | design §3.2.5/§3.6, decision 220 |
| Two-item operands | `TargetItemInstanceId` carries the native call's FIRST argument for combine (the receiver) and the receiving item for the battery load | design §3.3, decision 220 |
| Trader operand | the trader rides the intent as its world position, resolved by the shared `TraderLocator` | design §3.3/§6, decision 220 |
| Item sounds | no existing path carries an item's own sound; recorded as a limit rather than answered with a second feedback path | design §3.4/§6, decision 220 |
| Non-drag UI paths | audited: the touch auto-wear cannot reach a proxy, the craft screen keeps its own report path, the medical panel reaches the captured `ApplyWoundItem` and the landed medical-operation session, and the design's context-menu note has no caller in the build | design §6 |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings, 0 errors.
- Test-expectation red observed before the green was accepted: with the item-interaction family
  reverted to its pre-stage-3 shape (every one of the seven kinds refused, the frame's release-only
  predicate back to `IsContinuousGesture`, the native no-op answer not recorded, and the host refusing
  the kinds as unknown) the focused run failed **11/87** — the fifteen new capture cases except the
  four refusal cases, plus `TwoItemIntent_WithBothItemsOfTheOwner_IsForwarded` and
  `TraderHandIn_WithAPosition_IsForwardedToTheOwner` — output `%TEMP%\cuo-red-stage3.txt`. The probe
  was a working-tree-only edit; after restoring it the whole-tree fingerprint was byte-identical
  (`sha256 90387dff…` before and after, `%TEMP%\cuo-stage3-fingerprint-{before,after}.txt`).
- Focused after the review-freeze edits: `RemoteItemInteractionCaptureTests` (16 cases: the radial
  use and the wear/use pair, the two-item operands with both cross-owner and id-less refusals, the
  battery load/unload operand shapes, the favourite store beside the frame's drain, the
  other-owner/other-bracket refusals, the radial-centre classified no-op, the frame's refusal to claim
  a release no-op, and the trader position), `RemoteInventoryIntentWireTests` (17: the five new operand
  cases on top of the slot/limb/amount ones), `RemoteIntentRequestTests` (23: the five host cases on
  top of the stage 1-2 rows), `RemoteItemInteractionOperandsTests` (3: the two-item receiver order),
  plus the stage 1-2 `RemoteDragIntentCaptureTests` passing unchanged through the container split —
  **93/93 in one run**.
- Full suite **with build**: 3906/3906 (`dotnet test CasualtiesUnknownOnline.slnx --filter
  "FullyQualifiedName!~DeliveryChecklist"`), 148/148 for the gate project in the same run while the
  checklist was open; after the boxes were closed the gate project reports 149/149.
- Structure: the window's aggregate is 511 lines (`RemoteDragIntentCapture.cs` 330 +
  `RemoteDragIntentCapture.ItemInteractions.cs` 181), `RemoteContainerMoveCapture.cs` 176,
  `RemoteIntentApplier.cs` ~430, `RemoteDragMutationPatches.cs` ~400 — all inside the 600-line gate.
  The gate is what forced the container-pairing split (finding 6).
- One gate failure was fixed rather than recorded as debt: `Architecture_OneTopLevelTypePerFileAndAggregateLimits`
  is the only red this cycle produced, and the response was the extraction in finding 6.

## Independent adversarial review of this stage (same commit)

A fresh-context reviewer audited the frozen working tree against the design record, the game's own
code and the suites, and independently reproduced the build (clean `--no-incremental` too), the
focused families, the normative gates and the full suite. Report:
`%TEMP%\cuo-review-remote-inventory-stage3.md`. First-pass verdict: **0 blockers / 1 major / 4
minors**, and no claim of this self-check was falsified — every number the reviewer could re-measure
reproduced. Confirmed sound: the favourite candidate set really is the native store's write set (with
the destroyed item, the closed view, the requester's own item, the id-less proxy and a throwing native
body all handled and the proxy restored first), the R10 restore does not widen beyond the radial
centre, the probe coexists with `RemoteMedicalBlockRadialActionPatch`, both use and wear are captured
in native order, the wire operands and field numbers are consistent, the seven replays match the
natives (argument order checked against the decompiled calls), the claimed verified writes are real
re-reads, `ToggleFavourite` is discrete and re-reports, the host checks cover rogue-peer items,
self-targeting, a missing trader and non-finite coordinates without bypassing permission, membership,
ownership or arbitration, the three report hooks are correct for proxy and non-proxy items, and the
container-pairing extraction is behaviour-identical to HEAD (diffed field by field). What came back,
and what happened to each:

| Finding | Outcome |
|---|---|
| M1 — `CraftingSync` carries no `CallContext.Origin.RemoteApply` guard, so the OWNER's replayed combine was committed as a LOCAL craft operation (an operation-trace line plus a `CraftReportMsg` the host judged and relayed); the reviewer explicitly did not claim item-state corruption | **Fixed where the reviewer located it**: `CraftingPatches.BodyCombinePatch`'s prefix now leaves `__state` null inside a RemoteApply scope — the begin call pushes its own `Craft` scope, so a guard inside the sync class could never fire. The window-taken case was already correct (Harmony's null `__state`) |
| m1 — the trader "verified write" could not see the cap: the wire carried the item's raw value while the native credit clamps to what is left of the lifetime cap | **Fixed**: the reported value IS the verified credit (`totalValueGiven` after minus before), so the host credits exactly what this client did, and a hand-in that credited nothing reports nothing |
| m2 — the owner-side executor has no test face at all, so a swapped argument order (the combine receiver, the battery direction) would stay green | **Fixed in substance**: the two order decisions moved into the pure `RemoteItemInteractionOperands`, pinned by `RemoteItemInteractionOperandsTests`; the executor itself stays scene-bound, which is the limit stage 2 recorded rather than a new gap |
| m3 — no wire case pinned "a trader standing at (0,0)" against "no trader operand", and `LoadBattery` had no case of its own | **Fixed**: both cases added |
| m4 — the probe "fires on any `true`", so a release whose pointer is outside the circle would be classified and hide its "produced no intent" line | **Refuted, and the invariant is now pinned.** `PlayerCamera.cs:1636-1648` returns `true` only INSIDE its geometry block (the tag, the menu scale and the pointer's distance to the circle); a pointer outside the circle returns `false`, so the probe records nothing and that release keeps its unclassified line. `AWhileDraggingFrame_DoesNotTakeAReleaseNoOp` pins the other half of the guard — a frame never claims a release no-op |

The reviewer's "not verified" list is recorded rather than disputed: the credit-cap divergence's
visible symptom, whether the host's craft path could ever double-apply, the radial geometry in the
field, the frame-level favourite presentation and the item-sound limit all need a live scene or two
clients — and the reviewer deliberately did not run `dotnet format`, because it rewrites the tree
under review.

## Limit

- No runtime evidence: no two-client session, no frame-level verification of animation, sound or input
  interception. The item sounds the operator does not hear (design §3.4), the radial centre's feel,
  the battery and trader gestures and the held-remote-item medical chain are the user's release-cycle
  acceptance.
- The item's own sound is the one requirement §3.4's audit could not deliver: `combine`, `waterpour`,
  `batteryinsert` and the `useAction` clips play on the client that runs the mutation and no existing
  path carries them. Carrying them would change local-versus-remote for every player, so it is a
  recorded limit with the design's own "no second feedback path" rule behind it.
- The craft screen's own consumption path keeps the scope it had (ticket row 15) and is a stage 4
  re-check, not a claim of this stage.
- The trader hand-in needs the owner's scene to hold the same-position trader; an owner without one
  refuses with a line, which no automated test can exercise (the lookup needs a scene).
