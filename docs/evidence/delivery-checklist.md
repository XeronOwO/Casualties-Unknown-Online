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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: not applicable — this is a documentation/backlog cycle (a reference sweep and four ticket closures); it adds no gesture, window, wire member or player-visible behaviour, and the only user-facing text changed is the tickets' own records
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: no code mechanism is touched; the artefacts are backlog records — the sweep scan over `todo/`, `review/`, `in-progress/`, `future/`, `watchlist/` (every `docs/...` path extracted and tested), the four moved tickets' inbound links, and the document-census rows; each moved record's own claims were re-read, and the gate-census ticket's claims were re-run (6/6)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the sweep covered every live folder and every `docs/...` path in them; the nine stale references were fixed across the three tickets, the one pre-migration plan table was labelled and recorded as an exclusion, and the four moved tickets' inbound links were re-pointed everywhere (8 census rows, the selfcheck's scope line, 6 document references, both README sections); a re-scan and the doc gates (47/47) confirm no old path remains
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: (1) sweep scan over the five live folders: 24 unresolved `.md` references before, and after the fixes only the 22 pre-migration plan-table names (recorded exclusion, labelled) and the ticket's own two subject quotes remain; (2) `stale-api-doc-paths-in-live-tickets` — nine references rewritten to named headings, decision recorded (periodic audit, no gate, reasons in the ticket); (3) `delivery-checklist-gate-census` — its six gate cases re-ran green (`.acceptance/20261024-c/gate-census-acceptance.log`), the two emptied-file probes named as not re-run; (4) `item-identity-bridge` — closed as resolved with its code facts re-verified (`ItemInstanceId.ById`, `TryFindItem`, self-registration); (5) `bilingual-human-docs` — closed as superseded by the two-block `en/`+`zh/` standard, with the successor named; doc gates 47/47 (`.acceptance/20261024-c/doc-cycle-gates2.log`)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: this cycle has no runtime behaviour to prove; verification is the path scan, the doc gates and the named gate-case run above. Deployment/acceptance is not run for a docs-only cycle and its line stays unchecked by design
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the tickets' own designs froze the work (the sweep's scope and the gate-vs-audit question are the ticket's; closing a superseded/resolved record is a status call the backlog README defines), and the user's standing instruction to clear the todo/review list without asking covers the order; no new user-visible behaviour and no contract change is introduced
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no classes or source files touched this cycle; four backlog records moved (one to `done/`, two to `resolved/`, one sweep record to `done/`), each keeping its content and reason; the records are single-homed — the sweep result lives in its ticket, the closure reasons in each moved ticket, and the index row points at them
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: documentation-only cycle, so build, `dotnet format` and the behaviour suite are skipped by the standard; the focused normative-gate run after this checklist was completed is `.acceptance/20261024-c/doc-cycle-final-gates.log` — 777/777 gate cases green (the doc-only subset was 47/47 before the checklist, `.acceptance/20261024-c/doc-cycle-gates2.log`)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
