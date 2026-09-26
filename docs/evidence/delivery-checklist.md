# Delivery Checklist

Every development cycle runs through this checklist. The gate
(`RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`) runs before
the cycle's final commit as part of `dotnet test` and refuses it while any box
is unchecked. Deployment and manual multiplayer acceptance are user release
actions outside this gate; feature development verification uses
simulation/static evidence. When a release cycle lands, reset the checklist by
manually unchecking every box so the next cycle starts clean.

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
leaves item 8 and FORBIDDEN unchecked, but may write its boxes in one pass. That is the deliberate
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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled
      file:line or runtime log) or is explicitly marked unverified — evidence: selfcheck §1 (9 rows): the settings-row prefab wiring (SettingsMenu.cs child 0 = label, child 1 = control), the hand-placed rows, the two forbidden prefabs, S2a's surface, the intent channel, the polled pointer, the modal guard, the canvas scale, the right-click guard
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: selfcheck §3 (8 rows): all six pages moved with no second renderer, the member card's eligibility answered once for the model and the quick panel's remaining IMGUI card, the four re-anchored pin sets, the untouched S1 probe and port census, the still-IMGUI surfaces, the modal guard, wire/save untouched
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: selfcheck §4 (17 rows): the wrap rule, the game's own prefabs and the template read, the frame-driven visibility, the canvas, the reconcile, the focused-field rule, the id-at-interaction-time rule, the polled rect, the raycast frame, the drag, the intent dispatch and its drop, the close id, the console-page contract, the fade/theme census, the port census, gates/structure
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided — evidence: the wrap rule and the model factories by unit cases; the window by 13 source pins + 15 real-source mutation rows (OnlineUiWindowSurfacePinTests) and the two re-anchored pin sets; the look, the layout and the input need the user's game run (selfcheck §7)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose
      design the user already froze counts as approved (a backlog decision, a recorded
      decision entry, a handoff instruction); re-asking a work-item choice is itself a
      process violation — evidence: the ticket's `## Decision (2026-09-26, user ruling)` froze the uGUI destination and the reuse rule, and the S2b stage is the ticket's own stage list; no work-choice question asked (AGENTS.md rule 9)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0/0 (cuo-s2b-build9.txt); focused 232/232 (cuo-s2b-focus10.txt); format exit 0 with the code in the log (cuo-s2b-format3.txt); gates 288/288 (cuo-s2b-gates-final.txt); full WITH build 4245 + 288, exit 0 (cuo-s2b-full-final.txt)
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round) — evidence: largest touched files 556/530/505 lines (< 600, the window view split when it reached 595); the IMGUI window, the theme's window/title styles, `State.Scroll`, the three dropdown open booleans, the overlay's 21-parameter draw call and the unreachable `Enabled` path all deleted in the same round
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
