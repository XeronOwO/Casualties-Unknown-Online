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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new player gesture and no new entry path: the recovery re-drives the host's `WorldJoin`, whose native call site is `PreRunScript.StartRun` (`reversing/Assembly-CSharp/Assembly-CSharp/PreRunScript.cs:273`, reached by `RunCoordinator.OnWorldJoin`), and the exit it recovers from is `PlayerCamera.ToMainMenu` (`PlayerCamera.cs:613`); the ask carries no limb, target or identity (the host's own presence table and committed run baseline decide, and a refusal is named), so no fallback exists; the one user-visible change — a member the host's own exit pulled returns without a cold restart — is this ticket's own acceptance requirement, named as the round's first work item by the handoff
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's `## The recovery (landed 2026-10-20)` and the selfcheck's mechanism table read all seven: the exit path, the two invitation senders plus the receiver, the ask (`ReentryRequest` 156 and the channel's admission), the window, the host's answer (`ReentryPolicy`, `HasCommittedRunBaseline`, `SendWorldJoinTo`), the baseline fact, and the wire/census/matrix bookkeeping; the real-machine recovery is the acceptance row and is not claimed here
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the entry instruction's one-shot edge was walked at every sender (the click's `SendWorldJoin`, the host's InWorld edge's `SendWorldJoin`, `SendWorldJoinTo` for respawn/handshake) and the member's two drop paths (`OnWorldJoin`'s phase gate, `UpdateSceneState`'s exit reset); the window heals the host-exit population, including a `JoinPending` the exit discards, and the ticket's `Limits` name what is out of its reach (a member stuck generating; a host that never returns)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/world/layer-change-member-recovery-selfcheck.md` — nine claim rows, each naming a test, a gate or the batch's artifact, plus the review's findings and the limits
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the window's frame-sequence test (the review's B1 regression), the policy table, the channel's admission/cadence cases and the Runtime answer-seam case; L1 is batch `20261025-a`: the red was read on the pre-fix artifact through the committed recipe `net-drop-message` (`dropped: 1` against the host's one `World join sent`), and the green re-runs the same provocation on the fix artifact and reads `state`, `container-read mode=local`, the host's `[LayerReset]`/`World join sent` lines and both members' scene reports
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff's work order names this ticket as the round's first work item, and the recovery shape is the ticket's own acceptance requirement (a member must return without a cold restart); no new user-visible choice was invented, and decision 266 records the mechanism rather than a new question
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `ReentryCoordinator` 226, `ReentryWindow` 175, `ReentryPolicy` 37, `ReentryDecision` 18, `ReentryRequestChannel` 120; `GameAdapter` 624 and `WorldService` 602 are recorded in `docs/architecture-debt.json` with the split each owes named there; the window's two booleans are one state pair inside a pure type; every added mechanism names its reader (the channel's admitted event → the coordinator; the window → the frame pump) and the old edge-only invite stays the primary path
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet build` 0 warnings 0 errors; normative gates 751/751; behaviour suite 6092/6092 — rerun after the independent review's fixes (batch `20261025-a`'s logs)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
