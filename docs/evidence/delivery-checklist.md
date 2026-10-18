# Delivery Checklist

Every development cycle runs through this checklist. Two gates run before the
cycle's final commit as part of `dotnet test`:
`DeliveryChecklistGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` refuses
the commit while any required box is unchecked, and
`DeliveryChecklistGateTests.DeliveryChecklist_DeclaresEveryRequiredBox` is the
census — it refuses a checklist that no longer declares one of the steps below,
naming the step that went missing, because a record whose boxes are gone must not
pass by having nothing left to fail on. Deployment and multiplayer acceptance are
the agent's actions, run after the commit per `docs/acceptance/` and outside this
gate; feature development verification uses simulation/static evidence. When a
release cycle lands, reset the checklist by manually unchecking every box so the
next cycle starts clean.

**Operating rule (user mandates 2026-08-10 / 2026-08-16)**: boxes are checked ONE LINE AT A
TIME with the Edit tool as each step completes. The checkbox edits do NOT get
their own commit per checkbox — fold the checklist changes into the normal
work commits (implementation/docs/verification steps). The process record is
the line-by-line Edit sequence, not one commit per box. BULK checking (sed / scripts / a single
catch-up pass) is FORBIDDEN: it fabricates the process record and voids the
gate (observed: the cycle was bulk-checked, never committed, then reset —
the user called it out). Only a deliberate cycle reset may touch multiple lines.

**Documentation-only cycles (added 2026-09-17)**: a cycle that changes no runtime or test behaviour
(backlog moves, evidence/citation updates, workflow documentation) still fills EVERY box and still
leaves the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked, but may write its
boxes in one pass. That is the deliberate
multi-line exception the paragraph above already allows for a cycle reset, applied to a cycle with no
implementation sequence to record; the BULK-checking prohibition keeps governing every cycle that
touches `src/`, `tests/` or `tools/`. `dotnet format` may be skipped (it only rewrites C#, and such a
cycle has none). The evidence run may use
`--filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` — but the focused
normative-gate run afterwards is NOT optional: it is the only thing that proves this checklist complete
before the commit.

**Evidence rule (added 2026-09-17)**: a checked box carries a short evidence suffix on the
SAME line — `- [ ] <item> — evidence: <command/file/result>` — because a bare checkmark
records that someone decided the step was done, not what proved it. Keep it to one clause
(a command, a file, or a measured result); the full detail belongs in the cycle's ticket or
evidence file.

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle adds no CUO gesture: it bounds an existing member report, the trade give — the native call site is `PlayerCamera.TryPerformSpecialUIAction`'s `TradeThing` branch (`GiveItem(this.dragItem)`) reaching `TraderScript.GiveItem`, and the report names the item instance the gesture carries; decision 265 rules the shape (data only) and the handoff ordered the row, while the user's 2026-10-18 frame-staleness direction is why a deviation is CLAMPED and answered rather than refused; the only new surface is an acceptance-only console verb, cheat-gated and recorded in its own doc comment
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the native give and its cap (`reversing/.../TraderScript.cs:604-639`, `MAX_VALUE_GIVEN` at `:620-627`), the instance worth (`reversing/.../ItemInfo.cs:29-49`: `condition`, `AmmoScript` `itemType/rounds/maxRounds` — `[Saveable]`, `:6/:64/:71/:75` — and the liquid `stack` at `valuePerLiter` capped at 50) and the kernel row that carries that half (`ItemData`, `ItemState`, the `AmmoScript` component state) are cited in the ticket and the census; the trader state, the transport sender and the receipt set are the receiver's own; the generation naming (`GeneratedItemAuthority.NameContainerContents`, `GeneratedItemRows`) and the carried-set naming (`CarriedInventoryReporter.EnsureContentsAreNamed`) are Unity-typed and marked unverified until the machine reading
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the `TraderAction` kinds were walked one by one (purchase already prices its own stock entry; the meet/haggle/threaten/hug/move numbers are the acting player's own body chain and flags, documented as computed there, and the give is this change); the item-value family was checked too — `BuildingEntityDamaged` closed as unanchorable, the console `/give` and `/summon` state values are the operator's claim by design, `/data` writes are cheat-gated — and the item-identity family behind the give: generated container contents (named at the publish now), carried container contents (named at the statement now), a world row's POSSESSION (the native reach is behaviour, named as out of scope by decision 265) and the native `BoughtItem` guard (the kernel carries no marker, named as unmirrored)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's `NetMsg.TraderAction` section carries the seven-row table (the claimed credit and its clamp, whose item the reporter may speak for, one instance one credit, the clamp/refusal answer, a generated container's contents, a carried container's contents, the acceptance lever), each row naming its tests or its machine-reading status
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: `TradeGiveJudgeTests` (16 facts over real `ItemState` rows) and `TradeStockMachineTests` (the `ItemInfo.GetValue` arithmetic and the clamp) pin the decision; `TradeSimulationTests` drives four give paths through the REAL judge over the wire (`GiveItem_ClaimAbove...`, `UnjudgedInstance`, `SameInstanceTwice`, `AnotherMembersItem`); `ItemSnapshotSimulationTests` pins the generation contents becoming contained rows and `CarriedInventoryRegistrationTests` the carried-statement half; `AcceptanceTraderReportTests` pins the lever's four postures; mutation REDs were run on the freeze: clamp neutralized → 5 failed, ownership arm → 3, repeat arm → 2; the Unity-typed halves are named for the machine reading (the ticket's acceptance-owed paragraph), and `tools/acceptance/preflight.ps1` reports a full two-client run is possible
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's handoff ordered this row as the next work item, decision 265 (the 2026-10-17 data-only ruling) is the standing approval for the bound's shape, and the user's 2026-10-18 direction to handle the frame-staleness-plus-latency deviation is implemented as the clamp-and-answer policy recorded in the census and the ticket
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: after `dotnet format` and the gate's own fix, `ItemService` 586 lines, `ItemKernelAuthority` 591, `CommandConsoleService` 588, `IPatchBridge` 599, `TradeStateSync` 339, `TradeExecutor` 250, `TradeStockMachine` 259, `TradeGiveJudge` 108 and `GeneratedItemRows` 48 — all under the 600 gate (the generation write was extracted into its own type to keep it so, not debt-recorded); no new bool state (the receipt set is a `HashSet`, the verdict records); one dead helper deleted (`SimTraderHost.MarkTerminal`)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` exit 0 (0 warnings), `dotnet format CasualtiesUnknownOnline.slnx` exit 0, and the final unfiltered `dotnet test CasualtiesUnknownOnline.slnx` run after this tree's last edit is 741/741 normative gates and 6010/6010 behaviour tests green (`.agent-local/member-report-bounds-give-full.log`)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
