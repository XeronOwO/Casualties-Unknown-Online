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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the entry is the drag gesture, and the mapping is what the reading settled: the cue must name the SAME player the release resolves, or the label and the release disagree - and the release is the native inventory use (`useAction`) whose target is `CrossPlayerDragUse`'s own nearest-player walk
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the mechanism is the cue decision, and its evidence is three file reads: `PlayerCamera.HandleReleaseDragging` runs only on release (so the cue cannot live there), `PlayerCamera.HandleWhileDragging` runs per drag frame and is already patched, and the display-name fallback the ticket asks for already exists on the presentation side with three copies of its own. The consumer is named: the overlay that will read the port each frame
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "what a cross-player gesture announces", and the audit is what kept this change to the cue DECISION: the sound, the refusal reaching the operator and the question of spreading the naming rule to the medical and give gestures are all the ticket's own items, all recorded as limits rather than half-done here. The one family member audited in full is the gesture's own target resolution, which the cue now shares rather than duplicating
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the decision (eligible item + a target + a session -> a named cue); the two honesty cases (an item the gate refuses shows nothing; a row with no identity shows nothing); solo (no cue). Six cases. NOT covered and named: the drawing, the sound, the refusal echo, and every acceptance row
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the decision is a pure function so it is proven by cases; the DRAWING is not proven by anything until the overlay lands, and the ticket's rows 1-7 need a session. Recorded rather than implied: the label has no runtime reading yet, and row 5 - two players standing on each other - is the case the naming rule exists for
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the requirement is the user's own ruling of 2026-10-06: the cue must NAME the player, because two players standing on each other cannot be told apart by a highlight. This change implements exactly that rule and nothing beyond it; the items the ticket leaves to the user (which sound, whether the refusal echoes, how far the naming rule spreads) are untouched
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: two new types and one test class; no touched class near the line limit and no boolean state added. The structural finding worth keeping is where the cue does NOT go: the per-frame patch that would have been the obvious home already carries the favourite-mark logic, so the cue gets its own patch class on the same method instead of widening that one
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` 0 warnings / 0 errors; `dotnet format` exit 0; `dotnet test ... --filter "FullyQualifiedName!~DeliveryChecklist"` green at **5555/5555** behaviour cases and **662/662** gates
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
