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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: not applicable — documentation-only cycle (decision points recorded on six backlog tickets); no gesture, window, wire member or player-visible behaviour changes
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the decision points cite the mechanisms they would change — the console handler signature (`ModConsoleCommand.Handler`), the blast/death ownership path (`TrapEffectApplier`, `BuildingEntity`'s death branch), the native `WorldGeneration.loadingText` vs CUO's panel, the restore-claim's negative fact, `SettingsMenu.WaitForKeyPress`, and the intro skip's `_skipIntro` static; no mechanism is touched, so the evidence is the existing ticket text and its cited decompiled sites
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the annotation pass covered the open tickets with a genuine decision, and the rest stay as they were; no code fix was made, so no family alignment was owed — the one family touched by this cycle is the backlog itself, and each ticket's own options/related list was read before the decision point was written
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: six tickets annotated with a `## Decision point (2026-10-24)` section naming the options, a recommendation and what unblocks each (`console-command-refusals-render-green`, `host-blast-rolls-a-second-drop-set`, `loading-screen-progress-detail`, `placement-failure-report-anchor`, `launch-intro-cover-policy`, `enter-opener-keybind-capture`); no behaviour changed; the gate suite (`enter-acceptance-gates3.log` 777/777) and the full run stay green after the edits
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: not applicable to a decision-point cycle (no runtime behaviour); the gate for the cycle is the documentation gates, and the final run is cited on the build line
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: this cycle is the opposite of an unapproved plan: it records that six tickets NEED the user's decision (contract shape, death ownership, native surface, the unanchorable claim, the intro default and its friend-join residual, the key-capture fix shape), with the recommendation each; no user-visible change was made without that approval
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no classes or source files touched; six backlog tickets gained one `## Decision point` section each, in their existing shape; no new document was created, so the census is unchanged
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: documentation-only cycle, so build/format/behaviour are skipped by the standard; the focused normative-gate run after this checklist was completed is `.acceptance/20261024-c/decision-points-gates.log` — 777/777 gate cases green
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
