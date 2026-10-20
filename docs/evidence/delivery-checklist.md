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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no gesture, no entry and no wire member; the change mirrors ONE native presentation call site — `WoundView.SetCharDetails` (`WoundView.cs:54-68`), whose live caller is `PlayerCamera.Start`'s per-character roll (`PlayerCamera.cs:726-729`; the other native caller is `SaveSystem.TryLoadGame`, which CUO blocks under decision 165) — and makes it name the character the panel already displays. The user-visible change (the header names the viewed player) is the user's frozen ticket design (2026-10-19); the unknown-identity case is blanked, never a fallback to the viewer's own values
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: seven mechanisms read — the native writer and its two call sites (`WoundView.cs:54-68`, `PlayerCamera.cs:726-729`, `SaveSystem.cs:438-441`); `WoundView.UpdateView` (the two labels appear only in `SetCharDetails`, so nothing rewrites them per frame); the carrier (`CharacterDataMsg.NativeFields`, decision 171, filled by `CharacterDataCapture.Capture` and relayed unchanged by `CharacterDataStore.RelayCharacterData`); the local identity read and restore write (`CharacterNativeFields.LiveSystem`); the focus lifecycle (`RemoteMedicalView.Close`, the native toggle patch, the coordinator's per-frame pump); the TWO native readers that assume the header is the local identity — the snapshot capture and the game's own save (`SaveSystem.SaveGame`, `SaveSystem.cs:172-175`, which writes `save.sv`), the latter guarded by the capture window; the death-stats reader of `specimenId` (`PlayerCamera.cs:2315`). The runtime half is the acceptance batch; the parts it cannot prove are the ticket's `Limits`
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the identity label family walked — one native writer fills five fields (`bodyStatText`, `versionText`, `cInfo`, `firmwareVer`, `specimenId`); `firmwareVer` has no reader, `specimenId` is read only by the death-stats line (`PlayerCamera.cs:2315`), and `cInfo` has THREE readers besides the writer: the adapter's local identity read (`CharacterNativeFields.LiveSystem`, guarded), the game's own save (`SaveSystem.SaveGame`, `SaveSystem.cs:172-175`, guarded by the capture window added this cycle) and CUO's header itself. The independent review found the save reader unguarded and the first checklist draft claiming otherwise; both are corrected here. The sibling label families already had their display sources (limb readouts via the `WoundView.body` patch, ECG via `ECGVisualizer.get_body`, speech via `RemoteMedicalTalkTextPatch`), and `nameText` has no writer at all (ticket non-goal). The read side's whole family is enumerated above — every reader of the panel's identity is either served the remembered copy or runs inside the capture window
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's `What landed (2026-10-20)` table carries one row per mechanism (takeover on open and per refresh, close restore, local identity read and adopt, unknown identity, stale borrow, the native save's capture window, wire unchanged), each naming the test or the source fact that proves it
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is 23 focused cases in `RemoteMedicalHeaderTests` (the borrow rule including the show→blank orderings that the review found unpinned, the capture window, and source-anchored wiring), with a red→green record for the blank defect (`.acceptance/20261024-a/red-header-blank-order.log`: 3 failures on the pre-fix rule; `focused-header-tests.log`: 23/23 after). At runtime the acceptance batch reads the live header through `wound-read` (now returning `cInfo`, `bodyStatText`, `versionText`) and `panel-header.cs`, each owner's own panel for the expected values, and the coordinator's takeover/restore/adopt log lines; row 5 forces the owner's `cInfo` and reads the viewer's panel after one snapshot, row 6 reads a third client's panel of the VIEWER. What the batch cannot produce (a live capture failure, a scene reload, a native save with the panel open) is named in the ticket's `Limits`
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's handoff (2026-10-19, carried into this session's prompt on 2026-10-20) names this ticket as the round's first work item; the ticket's `Design (decided by the user)` freezes the behaviour (the header shows the viewed player's own data, the viewer's values return on close, the native `WoundView` stays the only surface), and the carrier correction recorded in the ticket is a fact about the existing snapshot, not a new design
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured after the final edit — `RemoteMedicalCoordinator` 332, `RemoteMedicalView` 129, `CharacterNativeFields` 169, `RemoteMedicalPatches` 409, the new `RemoteMedicalHeader` 203, the new `RemoteMedicalHeaderState` 195, the new test file 353, `wound-read` recipe 36. The state class holds three genuine fields (the remembered own identity, the values this focus wrote, the blank latch) and no dead member: each is read by a case in `RemoteMedicalHeaderTests`. No mechanism was deleted — the carrier already existed and gained a reader, not a sibling
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0, the exit code recorded on the log's last line (`.acceptance/20261024-a/format-2.log`); the normative gate project 750/750 (`.acceptance/20261024-a/gates-4.log`); the full solution 6056 behaviour tests + 750 gates, exit 0 (`.acceptance/20261024-a/full-3.log`) — the run includes the checklist gate itself, which is what proved these boxes complete
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
