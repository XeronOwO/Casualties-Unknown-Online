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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the entry is the owner-to-receiver component state, and the mapping is the native lifetime it mirrors: the game destroys `SleepingPills` and `Painkillers` on their own predicates and `Antidepressants` as soon as its amount clears, so a receiver that removes its copy early reaches the state the sender is already in rather than inventing a change
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the mechanism is the removal direction, and its consumer is named: `CharacterComponentSync.Apply` is called from the 1 Hz snapshot, the heal-result path, the restore path and the remote medical display, all four audited by the review for pairing a message with the right body. Evidence for the defect itself is the capture side writing fields only while the component exists
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is every body component synced through this health message, and all four are aligned in one change: sleeping pills, antidepressants, mindwipe and painkillers. The review confirmed the one hand-built producer (`PrepareRevive`) reaches the new arm and recorded the behaviour change it implies rather than leaving it implicit
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the pure rule (four components, three states, five cases) plus the two apply paths. NOT covered and named by the review: nothing in the suite would fail if the removal arms were deleted, because the two existing test classes for these helpers assert only their reflective shape - so the destructive direction is proven by reading, not by a case
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the rule is proven by cases; the removal is NOT proven by anything yet, and the review said exactly what would: two clients, let a dose decay to zero on one, then read the other scil `GetComponent<Painkillers>() == null` and a happiness reading. Recorded as owed in the ticket
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: no design decision is taken: the change implements the state the sender is already in, and the native lifetime rules are the game's own. The two open questions the heal ticket carries (which surface, how many of KrokMP's extra fields) are untouched
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: three new types (the enum, the policy, and the cases) and two modified helpers; `dotnet format` clean, no class near the line limit. One review finding fixed in the same commit is structural rather than cosmetic: destroying the cached component left the body's own `mindWipe` handle live for up to its half-second refresh, so `Apply` now clears it
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` 0 warnings / 0 errors; `dotnet format` exit 0; `dotnet test ... --filter "FullyQualifiedName!~DeliveryChecklist"` green at **5561/5561** behaviour cases and **663/663** gates
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
