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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle adds no gesture, no entry and no wire member; it mirrors ONE native presentation call site — `Body.HandleCirculation`'s thump block (Body.cs:888-924), whose panel condition the existing remote focus genuinely satisfies (`RemoteMedicalCoordinator.Open` → `PlayerCamera.WoundViewButton`) — and redirects that one sound to the body the panel already displays. The one user-visible change (the alarm follows the displayed patient; silent while it is not critically dying) is the user's own frozen ticket design (2026-10-19), not a new scope: no fallback, no identity the gesture cannot carry, and no order-arbitrated entry
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: five mechanisms, all read: the native thump block (Body.cs:888-924); both `Sound.Play` overloads and the pre-existing `SoundPlayPatch`/`SoundPlayAudioClipPatch` (the `HostVitalsPass` guard and the string→AudioClip routing, Sound.cs:52-54); the display body's `isCriticallyDying` inputs, including the wire `TotalBleedSpeed` the review found misprojected and this cycle fixed; the display `heartProg` advance (`AdvanceMedicalDisplay`); and the focus close/reset path. The audible result itself is unverified without a listener and is named as the acceptance residual; no new game API was touched
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the heartbeat clip family walked — only `Body.cs:910/914/917` names the three clips, the local body's thump is refused by the new prefix, the host-simulated clone's was already refused inside `HostVitalsPass`, and the panel's own `WoundView.Update` critical audio loop already read the display body, so it needed no change; the ECG sibling (`ECGVisualizer.get_body`) already followed the display body. The walk also produced the display projection's `totalBleedSpeed` fix (the one family member that misstated the patient), pinned by `DisplayBody_BleedSpeedRidesTheWire_NotALimbSum`
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's `What landed (2026-10-19)` table carries one row per mechanism (native thump, panel alarm, focus lifecycle, display state, game clips only), each naming the test or batch-record evidence that proves it
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: 12 focused reflection/source tests (the beat rule, the three native clip names, the patch target/overload, the close reset, the coordinator's call order, the prefix guard, the wire-bleed pin) green in `.acceptance/20261023-c/focused-heartbeat-tests-3.log`; at runtime the next acceptance batch (`20261023-c`) reads the Debug cue/suppression lines, the display-vs-local critical states, the `Sound.Play` branch and the live heartthump AudioSource census on the deployed artifact. Audibility stays a user residual: the run captures no audio
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's 2026-10-19 handoff names this fix as the round's first work item and restates the ticket's order (this ticket first, the identity header after); the ticket's own `Design (decided by the user)` freezes the behaviour ("a healthy displayed patient is silent and a critical one alarms"), and the review's added row 6 rides that frozen scope as a review finding, not a new design
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured after the final edit — `RemoteMedicalCoordinator` 324, `RemoteMedicalPatches` 388, `RemoteMedicalView` 124, the new `RemoteMedicalHeartbeat` 124, `GameAdapterBridge` 598 (under the 600 gate; its watchlist entry's measured count was refreshed in this cycle and the named port-forwarding split remains due), the new test file 230. The two bools in the helper are genuine state (beat latch, once-per-focus missing-clip report); nothing left dead — `ComputeTotalBleedSpeed` was deleted with its last caller, and each clip constant keeps a consumer (`IsNativeHeartbeatClip` or `Beat`)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0 (`.acceptance/20261023-c/format.log`); focused `RemoteMedicalHeartbeatTests` 12/12 (`.acceptance/20261023-c/focused-heartbeat-tests-3.log`); the normative gate project 750/750 (`.acceptance/20261023-c/gates-2.log`); the full solution 6033 behaviour tests + 750 gates, exit 0 (`.acceptance/20261023-c/full-2.log`) — the run includes the checklist gate itself, which is what proved these boxes complete
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
