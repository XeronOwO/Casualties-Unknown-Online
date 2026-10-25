# Delivery Checklist

Every development cycle runs through this checklist. It is validated by `tools/check-delivery.ps1`, run before the cycle's
commit (2026-10-25: a script, not a test — a test cannot reset the boxes, and a file left green certifies the next cycle
without a record). The script refuses a commit while a required box is unchecked or carries no evidence, archives the
cycle's record under `docs/evidence/checklists/`, and resets every box so the next cycle starts clean. One test stays, the
census: `DeliveryChecklistGateTests.DeliveryChecklist_DeclaresEveryRequiredBox` refuses a checklist that no longer declares
one of the steps below, naming the step that went missing, because a record whose boxes are gone must not pass by having
nothing left to fail on. Deployment and multiplayer acceptance are the agent's actions, run after the commit per
`docs/acceptance/` and outside this script; feature development verification uses simulation/static evidence. The entry
mapping box answers the standard in `../development/native-parity.md`.

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
cycle has none). The focused normative-gate run afterwards is NOT optional: it is the only thing that
proves this checklist complete before the commit.

**Evidence rule (added 2026-09-17)**: a checked box carries a short evidence suffix on the
SAME line — `- [ ] <item> — evidence: <command/file/result>` — because a bare checkmark
records that someone decided the step was done, not what proved it. Keep it to one clause
(a command, a file, or a measured result); the full detail belongs in the cycle's ticket or
evidence file. The suffix goes at the END of the box's own text: the reset cuts from the last
` — evidence:` it finds, so a suffix placed mid-sentence would take the rest of the statement with it.

**Input rule (added 2026-10-22)**: the Entry mapping box also records the exact input sequence the
player performs for a new or changed player-facing gesture — which mouse button is held, which window is
open, what can still be clicked or pressed while it is held — and walks it through the game's own control
flow. A gesture the controls cannot perform is a design defect, not evidence; a call seam a probe can
drive is not evidence that a player can reach it. An "impossible" verdict lists EVERY entrance that opens
the window the gesture needs and the button each one takes (a conflict on one entrance is not the control
scheme), and an entrance behind a hotkey the player cannot be expected to know is not the primary entry:
the gesture opens what it needs from the act itself.

**Cycle-reset rule (added 2026-10-25, the user's finding)**: this file is the CURRENT cycle's record, and
a cycle that never resets it inherits the previous cycle's green boxes — the script only asks whether a
required box is u