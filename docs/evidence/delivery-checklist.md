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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: not applicable — the change restores punctuation in comments and documents; no gesture, window, wire member or behaviour changes
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the mechanism is the code-page round trip (UTF-8 read through the system code page, written back, the last byte consumed by `?`); every restored spot was read in its sentence, the two private-use strays were read at the byte level (`U+9225` + the PUA byte where a `?` should be), and the fact that the two `done/` records entered git already corrupted was checked against their history (no uncorrupted revision exists to recover from)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the scan covered ALL tracked `.md/.txt/.cs/.ps1/.json/.cfg` files, not only the four the ticket named; it restored the source file and found a test file the original inventory missed (`HostRuleAnnouncementTests.cs`); the two files it leaves are the ticket's own quotations (by design) and the pinyin data (CJK content, the ticket's own exclusion); the post-pass scan finds no other file
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/decisions/active.md` 39 spots (37 em dash, 2 `–E`, 1 arrow); `docs/standard/document-census.txt` 4 (three dashes, `§6`); `done/member-report-bounds.md` 139 (125 dash, 22 arrow, 2 `×`, 2 private-use strays); `done/item-and-entity-data-commands.md` 62 (58 dash, 4 arrow, 1 stray); `KernelCommandGateway.cs` 36 dash; `HostRuleAnnouncementTests.cs` 4 dash; each spot read before restoration; the post-pass scan reports zero damaged characters and zero PUA bytes in all six; the record's intact `…` (13) untouched
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: not a runtime change; verification is the proofread diff (every changed line read), the whole-tree scan (`git ls-files` restricted to text types; zero hits outside the ticket and the pinyin data), the build (0/0) and the full suite (6343/6343)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket's own method is binding (restore each spot from its meaning, never one global replace; never touch the `zh/` block; re-read every affected sentence), and it is what the pass did; the work is backlog-requested (the user's 2026-10-18 backlog), not a new design
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no class structure changed; the two source/test files are comment-only edits (`KernelCommandGateway.cs` 534 lines, `HostRuleAnnouncementTests.cs` 197), both under the gate; the two records keep their shape; the one non-editor operation (two private-use characters the editor cannot express) is recorded in the ticket with its explicit UTF-8 handling and re-read
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors (`.acceptance/20261024-c/mojibake-build.log`); `dotnet format CasualtiesUnknownOnline.slnx` exit 0 (`.acceptance/20261024-c/mojibake-format.log`); the final full run after this checklist was completed is `.acceptance/20261024-c/mojibake-final-full.log` — behaviour 6343/6343 and gates 777/777 green
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
