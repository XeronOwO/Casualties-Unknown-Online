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

**Input rule (added 2026-10-22)**: the Entry mapping box also records the exact input sequence the
player performs for a new or changed player-facing gesture — which mouse button is held, which window is
open, what can still be clicked or pressed while it is held — and walks it through the game's own control
flow. A gesture the controls cannot perform is a design defect, not evidence; a call seam a probe can
drive is not evidence that a player can reach it. An "impossible" verdict lists EVERY entrance that opens
the window the gesture needs and the button each one takes (a conflict on one entrance is not the control
scheme), and an entrance behind a hotkey the player cannot be expected to know is not the primary entry:
the gesture opens what it needs from the act itself.

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new entry, control scheme or input sequence: this fix changes only what the existing give release carries over the kernel wire (`PlayerCamera.HandleReleaseDragging` → `PlayerCameraDragUsePatch` → `RemoteDragIntentDispatcher.ReportLocalReleaseOntoProxy` → the placement answer and exact commit), whose real-pointer sequence batch `20261022-f` read and batch `20261023-e` repeated on the real client; no button, window or fallback is added
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the two added wire members and their readers: `WirePlayerInteraction.ExactDestination` (ProtoMember 28) → `KernelPlayerInteractionWireMapper.FromWireInventoryTransfer` → `PlayerInventoryTransferEvent.ExactDestination` → `PlayerInteractionKernelCodec.ToTransferMessage` → `PlayerInteractionApply.OnPlayerInventoryTransfer`'s exact arm → `SendPlacementOutcome`; `WirePlayerInteraction.Returned` (29) → the same hops → the giver's `if (msg.Returned …) DropCreatedItemAtLocalBody` fallback; the outbound half is `KernelPlayerInteractionWireMapper.ToWire(PlayerInventoryTransferEvent)`; the finding is batch `20261023-e`'s two-client log (the give reached `[Take]` and the receiver placed the item, but neither client wrote a `[Placement]` line) and the pre-fix `ToWire`/`FromWire` bodies; the earlier codec-only test is why the suite was green
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: all three result events walked member by member: the transfer had exactly the two flags missing and now maps 7/7, heal maps 8/8, item-use maps 14/14, and the nested payloads (`PlayerInteractionHealth`, `PlayerInteractionLimb`, `PlayerInteractionItem`, `ItemData`, component fields) have no dropped member — so this was a one-off gap in the member added by `e3cdc117`, not a pattern left elsewhere; `WirePlayerInteraction`'s protobuf ids are 1-10 and 13-29 with no duplicate, and 28/29 were unused
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `PlacementAnswerTests` 13 cases (the previous 12 plus `TheGiveTransfer_CarriesTheAnsweredDestinationFlagToTheReceiver`, which reads the give transfer off the serialized frames; the return test now also decodes the second committed batch and asserts `Returned=true`, `ExactDestination=false`); two `KernelWireMapper` round trips (`WireBatchRoundTrip_PreservesExactDestinationOnTransferEvent`, `...ReturnedOnTransferEvent`); `GiveGestureGateTests`' shape pins unchanged; `SyncCoverageGateTests`' row P7 now declares payload fields 28/29 with two anchors (14, evidence count 1130); the member-report census needs no row — the transfer command is HostOnly, so `WirePlayerInteraction` is outside its scope
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the full suite green (6265 behaviour cases, 764 gate cases, `PlacementAnswerTests` included); L1 is the row's machine reading: batch `20261023-e` ran the give on `0.1.0+aaf89655…` for real and found that no outcome reached the host because the kernel wire dropped the two flags (record `docs/evidence/acceptance/placement-failure-report-anchor-20261023-e.md`); the fix is pinned by the wire-level tests and the re-read on the fixed artifact is OWED — not run this round, per the user's 2026-10-23 instruction to land tickets and code only while the machine was needed for a co-op session. Rows 2-3 remain judged by the behaviour tests, because the 60 ms answer→apply window cannot be produced by hand (batches `20261022-i`/`-j`, the user's 2026-10-23 ruling)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff names this row as the next work item and the ticket states the anchor it needed; the user's 2026-10-23 instruction to land tickets and code only, without a machine acceptance run while the machine was needed for a co-op session, scopes this cycle and waives the machine re-read; no user-visible surface changed — the fix is the carrier of the give's existing outcome, and the give gesture itself is untouched
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `WirePlayerInteraction` 29 members at 165 lines, `KernelPlayerInteractionWireMapper` 361, both under 600; one top-level type per file; no state bool added (the two flags are wire fields with one writer and one reader each); no dead mechanism left; the changed tests stay in their existing classes
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors; `dotnet format CasualtiesUnknownOnline.slnx` exit 0; behaviour suite 6265/6265 and normative gates 764/764 green (`full.log` in the local artifact area), `PlacementAnswerTests` (13), the sync-coverage gate's P7 14-anchor check and the document census with the new record included
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
