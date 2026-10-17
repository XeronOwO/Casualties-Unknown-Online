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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this commit adds a run record, not an entry. The run judged the entry a player reads - the overlay line - and its evidence is that line quoted from both clients
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the mechanism is the waiting line, and the run gives it runtime evidence for the first time: `Waiting for the host to start the run…` on a member and `Waiting for the remaining players to load… (0s). The group starts together.` on the host, each read twice 9 s apart with identical text. Marked unverified and named: the NAMED roster form, because the member was already in the world when the host was read
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "every sentence the wait can print", and the run covers three of the four kinds live. The fourth (`OthersLoading`, a member naming a third player still out) has unit cases but no runtime reading, and the record says so rather than implying the family is closed
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the record is the table: three claims, the side that made each, the line it read, and a verdict. Two further rows state what was NOT reached and why - the named roster and the panel width - rather than leaving them implied
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the design was reading the overlay line off both clients with a deployed-artifact check before the clients start, and it earned its cost immediately: the batch's FIRST attempt is kept in the record because it read the OLD wording and its own verification step named the cause (a stale deployment). That is the generalizable half: a batch that reads a deployed artifact must STOP on a failed verification, and this one did not
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: acceptance is agent-run here and the rows judged are the ticket's own step 4. Nothing was decided: the open question about which surface the player reads (the game's own line vs CUO's panel over it) stays recorded as a question for the user
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no production file is touched; the commit adds a run record, its census row and the ticket's link to it. The machine was left clean - both clients closed, reuse nodes shut down - and the deployed artifact now matches HEAD, which is what the first attempt got wrong
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: documentation only, so build, format and the behaviour suite are skipped by the repository's own rule; the normative gates were run because a tracked document was added and carry its census row, and they are **667/667 green**
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
