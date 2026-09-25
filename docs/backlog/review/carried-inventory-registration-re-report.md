# Carried-inventory registration has no re-report

- Status: Review
- Priority: Medium
- Category: Network / sync coverage / items / arbitration
- Source: Sync coverage audit 2026-09-09 (`docs/evidence/sync-coverage-matrix.md` row I8)
- Related: `resolved/remote-backpack-native-interaction-parity.md`, `resolved/remote-backpack-item-projection-acceptance-issues.md`, `review/guest-command-loss-reconciliation.md` (the sibling swallowed-guest-report landing)

## Problem (evidence)

`CarriedInventory` (NetMsg 65) is the guest's one-shot registration of the item
ids it self-assigned during generation. The host uses it to build the
per-guest transfer table used by cross-player take/arbitration.

- Only send site: `src/CasualtiesUnknownOnline.GameAdapter/Items/CarriedInventoryReporter.cs:133`
  (`_items.SendCarriedInventory(items);`), emitted on the generation-finished
  edge (`:51`, `if (_generating)`).
- Channel: `src/CasualtiesUnknownOnline.Runtime/Session/Items/ItemIdCoordinator.cs:104`
  (`SendCarriedInventory`), reliable by `PacketSender` default.
- No periodic re-report, and the 1 Hz character snapshot does not feed the host's
  arbitration table (`CharacterDataStore.SaveCharacterData` stores the snapshot
  only), so a swallowed registration leaves the host's transfer table empty for
  those ids.
- Consequence at the arbitration seam: `ItemArbitration.AdoptEvidence` logs
  "no transfer-table entry, not arbitrated" and returns null; the kernel state is
  partially healed by the accepted-first carried update path
  (`src/CasualtiesUnknownOnline.Application/Kernel/KernelProtocolCommandHandler.cs`, the
  `HandleMissingCarriedUpdate` path),
  but the arbitration table is not.
- `ItemIdWatermark` (NetMsg 64) is the better-behaved sibling: it is monotonic and
  self-heals on the next allocation
  (`src/CasualtiesUnknownOnline.GameAdapter/Items/ItemIdAllocator.cs:33`), and the
  host re-grants it on member add (`ItemIdCoordinator.cs:42-49`).

## Goal

The host's per-guest carried-id registration converges after a swallowed
`CarriedInventory`, so cross-player take/drop arbitration works for the guest's
starting inventory and craft products without requiring a reconnect.

## Design direction (decide at implementation)

1. **Low-frequency absolute re-report** — re-send the guest's current carried-id
   set periodically (e.g. 5–10 s) and on member (re)entry; the host replaces its
   table for that guest idempotently.
2. **Host request on miss** — when arbitration misses an id, ask the owner for a
   registration refresh.
3. **Fold into the character snapshot** — teach the 1 Hz snapshot consumer to
   register unknown carried ids (largest change; only if it keeps the snapshot a
   read model).

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | Guest starting inventory; registration dropped | Host converges; take arbitration works |
| 2 | Craft product ids added after the first report | Converge on the next re-report |
| 3 | Reconnect while in world | Table rebuilt exactly once |
| 4 | Guest picks up a world item (id already host-known) | No duplicate registration |
| 5 | Two guests with overlapping local counters | Host keeps per-guest tables separate |
| 6 | Item destroyed after registration | Terminal fact wins; no resurrection |
| 7 | Registration arrives before the item fact | Accepted-first path still works |

## Non-goals

- Item id allocation strategy changes.
- Cross-player take protocol changes (the arbitration seam stays).

## Landed (2026-09-19)

**Mechanism (design option 1, refined).** The registration is an ABSOLUTE report of the guest's
CURRENT carried set, re-captured at every report by the reporter that already owns the body
enumeration (`src/CasualtiesUnknownOnline.GameAdapter/Items/CarriedInventoryReporter.cs`), and
repeated on the cadence of a new Runtime policy object
(`src/CasualtiesUnknownOnline.Runtime/Session/Items/CarriedInventoryReportSchedule.cs`, held by
`ItemIdCoordinator`):

- a registration WINDOW opens on the two edges that make a registration meaningful — the
  generation-finished falling edge, and the host's `ItemIdWatermark` grant, which is the join AND
  reconnect signal because a rejoined guest restores a world instead of generating one
  (`GameAdapterSessionBinding.OnItemIdWatermark`);
- while it is open the guest reports every 5 s for 12 reports (≈ 60 s, the documented ≤ 30 s entry
  swallow window at this family's order of magnitude), then keeps re-reporting once a minute for
  the rest of the world's life;
- the steady half is what makes the report durable rather than one-shot: an id the guest
  self-assigns later (a crafted product, an item unloaded from a container) converges on the next
  report, whether or not the frame that introduced it survived;
- the capture states EVERY authoritative item of the body, including one that already carries an
  instance id — a snapshot id the host knows, an id an earlier report of this reporter stamped, or a
  product `CraftingSync`/`ContainerItemSync` stamped. This is load-bearing, not a detail: the first
  capture stamps an id on everything it reports (`ItemIdAllocator.Allocate`), so a capture that
  skipped bound items would come back EMPTY on every later report and send nothing at all. The first
  version of this change did exactly that — the 2026-09-19 independent review caught it as a blocker
  — which is why the rule is now pinned by
  `SourceShapeGateTests.CarriedInventoryCapture_DoesNotSkipItemsThatAlreadyCarryAnInstanceId` and
  named at runtime by the coordinator's inert-capture warning;
- re-capturing instead of replaying a frozen frame is what keeps a repeat honest — an item the
  guest destroyed, dropped or handed over is simply absent from the next capture;
- an empty capture still spends a step, so a guest carrying nothing does not re-enumerate its body
  every frame; an empty capture AFTER this session registered a non-empty set is named in a warning
  (the inert-capture failure mode above); the window is dropped on session end
  (`ItemIdCoordinator.ResetForSessionEnd`).

**The host half is registration-only** (`ItemArbitration.RegisterCarried`), which is what makes a
repeat safe in both directions: an id whose transfer-table entry already exists keeps that entry
untouched — it is the arbitrated record, and a cross-player heal may have consumed part of its
condition — and an id the kernel does not accept as THIS guest's carried item (another guest's,
still in the world, or Terminal) is refused with a log, because the kernel is the carried-ownership
authority. An id the kernel does not know is spawned as this guest's carried fact, the same
first-registration path as before. A repeat never pushes KERNEL state either: the deleted
`EnsureCarried` used to call `TryUpdateState` for an id the kernel already knew, and the registration
path deliberately no longer does — a registration states ids, while the guest's state channels
(use/slot/container reports) are the ones that carry state, so a repeat cannot roll the kernel back.
No production caller depended on that update: `CraftSyncService`'s fallback runs exactly when the
table has no entry and spawns its products itself, and `CharacterDataStore` only READS the table.

Option 2 (host request on miss) was rejected because it only heals AFTER the first take/use has
already been arbitrated without the entry — the user-visible failure it would leave behind. Option 3
(fold into the 1 Hz character snapshot) was rejected because it turns a read model into an authority
input. Neither judgement rests on the wire: compatibility is never a design input — the handshake's
protocol check is the boundary (decision 188), so a mechanism that needed a new message would take
one and bump the number (decision 137). As it happens, the landing adds no wire member and changes no
message shape, and the host's `CarriedInventory` handler is untouched — a fact about the mechanism,
never the reason for it.

**Acceptance matrix** (as landed — every row names the test that covers it, and the half it runs in):

| # | Scenario | Covered by | Half |
|---|---|---|---|
| 1 | Guest starting inventory; registration dropped | `SwallowedRegistration_ConvergesOnTheDenseReReport` — the link drops every `CarriedInventory` frame, then the next dense step heals it | Runtime; the capture is the test's, the production capture is pinned by the gate row below |
| 2 | An id that appears after the first report (a craft product, a container unload) | `AnIdThatAppearsAfterTheFirstReport_ConvergesOnTheNextReport` — the captured set GROWS between reports and the next report registers the new id | Runtime |
| 3 | Reconnect while in world | `Rejoin_ReRegistersOnce_WithoutDuplicatingTheEntry` — the host's table is cleared, the guest's join edge re-registers it, exactly one entry per id | Runtime (the real grant is forwarded by the adapter binding) |
| 4 | Guest picks up a world item (id already host-known) | `APickedUpItem_IsPartOfTheCurrentSet_WithoutADuplicateEntry` — a real spawn + pickup, then a repeat of the current set keeps exactly one entry | Runtime |
| 5 | Two guests with overlapping local counters | `TwoGuests_KeepSeparateTables` | Runtime (a regression guard — it also passes on the pre-change tree) |
| 6 | Item destroyed after registration | `RegistrationOfADestroyedId_DoesNotResurrectTheEntry` — the kernel holds the id as Terminal and the repeat is refused | Runtime (red before the change) |
| 7 | Registration arrives before the item fact | `RegistrationBeforeTheItemFact_ArbitratesTheLaterReport` | Runtime (a regression guard — it also passes on the pre-change tree) |
| — | A repeat must not roll back the host's record | `RepeatedRegistration_DoesNotRollBackTheHostsArbitratedEntry` — the transfer-table entry AND the kernel state keep the arbitrated condition | Runtime (red before the change) |
| — | Another guest's item claimed by a registration | `RegistrationOfAnotherGuestsItem_IsNotAdopted` — the kernel gate refuses it | Runtime (red before the change) |
| — | An inert capture (the review blocker's shape) | `AnEmptyCaptureAfterANonEmptyRegistration_IsNamedInAWarning` plus `SourceShapeGateTests.CarriedInventoryCapture_DoesNotSkipItemsThatAlreadyCarryAnInstanceId` | Runtime + source gate |
| — | An empty capture spends a window step | `EmptyCapture_SpendsTheWindowStep_SoThePumpDoesNotReCaptureEveryFrame` | Runtime |
| — | Cadence arithmetic (dense window → steady minute) | the seven `CarriedInventoryReportScheduleTests` | pure policy |

Red first: three tests failed on the pre-change tree with the quoted messages (`the host's
arbitrated condition must survive a re-report, got 1`; `a terminal item id must never re-enter the
transfer table`; `another guest's carried item must not be adopted twice`), reproduced independently
by the reviewer against a pristine `git archive HEAD` tree. The two capture guards were added by the
fix round: the blocker they guard was NOT reachable by the first suite, because the production
capture needs Unity and every Runtime test supplies its own capture.

**Declared limits (what this automation cannot prove).** The automated half is the Runtime half: the
wire behavior, the transfer-table semantics and the cadence policy run on the simulation world with
a fake network (including a targeted `DropMessageId = NetMsg.CarriedInventory` swallow). The Game
Adapter's Unity half — the live body/limb enumeration, the real generation-finished edge, the real
`ItemIdWatermark` grant arriving on a reconnect, and the real 5 s/60 s rhythm — is not reachable from
the test host: its CAPTURE RULE is pinned by the source gate and its failure mode is named in a
runtime warning, while the real enumeration still needs the user's dual-client acceptance. Two
reachable limits: (a) a long world load can spend the dense budget on empty captures before the body
exists (the watermark grant rides handshake completion) — the window stays armed, so the steady
minute still registers the set, i.e. the worst case after a rejoin is one minute, not a lost
registration; (b) the steady cost is at most one carried-inventory frame per guest per minute, and
only when the capture has something to state. One behavioural widening is named rather than implied:
the host's clone fact table (`CloneFactTable.ApplyCarriedInventory`) now receives the FULL set on
every report instead of only the newly bound items. It cannot corrupt state — the same table is
written the same way by the carried-sync event path, the owner's 1 Hz snapshot replaces it wholesale
the next second, and a report can only state items the body currently holds, so it cannot resurrect
a dropped one — but a repeat now touches that table every time, which the one-shot report did not. A
registration that survives no report at all for a whole session is the same accepted loss any
uncommitted local state has at a session boundary.

**Evidence.** Matrix row I8 moved from `Event-only gap` to `OK` (verdict summary `OK` 54 → 55,
`Event-only gap` 1 → 0), the row's trigger/fallback/recovery/loss cells and the gap list now record
the mechanism, and the evidence JSON anchors for the row went 8 → 20 (the reporter's stale
`_items.SendCarriedInventory(items);` anchor re-pointed to the capture call, plus the two fix-round
anchors: the capture source gate and the inert-capture warning; the JSON's declared total went
947 → 959). The K1-family statement that no periodic item-command re-send exists was already
corrected by the I5 landing; nothing in this row's old text claimed the registration needed no
re-report.

**Independent adversarial review (2026-09-19, fresh context, FULL tier, read-only, frozen tree).**
First-revision verdict: DO NOT SHIP — one BLOCKER: the capture skipped every item that already
carried an instance id, and because the first capture stamps an id on everything it reports, every
later capture was empty and `SendCarriedInventory` sent nothing — the re-report was inert in
production while this section declared the gap closed. It also found a MAJOR (the acceptance matrix
named a cadence unit test plus prose for a scenario no test drove, and row 4's test actually
simulated a cross-player heal) and minors: the unstated `TryUpdateState` removal, two dangling
`todo/…` citations in `KernelProtocolCommandHandler.cs`, stale "row I8 is the remaining gap" claims in
`review/guest-command-loss-reconciliation.md`, the second registration edge missing from the matrix
row's trigger cell, the dense window being spendable by a long load, and the "one frame per minute"
cost claim. Everything else held: every number reproduced, no wire change (protocol 31), the host
half's two rules, and the red-first record — the reviewer rebuilt a pristine HEAD tree with
`git archive` and reproduced the three failures with the quoted messages.

**Fix round (2026-09-19).** Every finding was fixed in the same working revision:

- the capture states EVERY authoritative item of the body, bound or not (`CarriedInventoryReporter`,
  with the why in the code: a capture that skips bound items goes empty after its own first report);
- the rule is pinned by
  `SourceShapeGateTests.CarriedInventoryCapture_DoesNotSkipItemsThatAlreadyCarryAnInstanceId` (its
  matcher carries a positive/negative contract test; run against HEAD it flags the two old skip
  lines, in the working tree it flags none);
- the failure mode is named at runtime: an empty capture after this session registered a non-empty set
  logs a warning instead of silently spending the window (`ItemIdCoordinator`), pinned by
  `AnEmptyCaptureAfterANonEmptyRegistration_IsNamedInAWarning`; the wording states the inference
  ("if the guest still carries them") instead of asserting a fault, and the branch BEFORE any
  registration — a legitimately empty body, e.g. while loading — is pinned by
  `AnEmptyCaptureBeforeAnyRegistration_IsNotWarned`;
- the source gate declares its exact reach: it pins the null-test SHAPE of the removed skip, and an
  equivalent skip written through a local variable is caught by the runtime warning's EFFECT instead
  (stated in the gate's own doc, so the declaration matches the scan);
- two new Runtime tests cover the scenarios the matrix claimed but no test drove —
  `AnIdThatAppearsAfterTheFirstReport_ConvergesOnTheNextReport` (the captured set grows between
  reports) and `APickedUpItem_IsPartOfTheCurrentSet_WithoutADuplicateEntry` (a real spawn + pickup,
  then a repeat) — and the matrix now names, per row, the test AND the half it runs in, labelling the
  two rows that are regression guards rather than change detectors;
- the kernel-state delta is stated and pinned
  (`RepeatedRegistration_DoesNotRollBackTheHostsArbitratedEntry` now asserts the kernel condition too);
- the dangling source citations are repointed, the stale "remaining gap" claims corrected, the audit
  ticket's gap list annotates I8 as closed, the matrix trigger cell names the second edge and the
  due-gate, the cost claim is corrected and the long-load limit declared.

**Verification (2026-09-19, final).** `dotnet build` 0 warnings / 0 errors; `dotnet format` exit 0;
focused `dotnet test … --filter "FullyQualifiedName~CarriedInventory"` 20/20; neighbours
`--filter "FullyQualifiedName~Item|FullyQualifiedName~Craft"` 472/472; normative gates 62/62; full
suite 3420 green with build. Lines (all under the 600 gate):
`CarriedInventoryReportSchedule` 110, `CarriedInventoryReporter` 129, `ItemIdCoordinator` 202,
`ItemArbitration` 477, `ItemService` 590, `CuoBootstrap` 599 (untouched).

