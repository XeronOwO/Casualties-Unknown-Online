# Delivery Checklist

Every development cycle runs through this checklist. The gate
(`RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes`) runs before
the cycle's final commit as part of `dotnet test` and refuses it while any box
is unchecked. Deployment and multiplayer acceptance are the agent's actions,
run after the commit per `docs/acceptance/` and outside this gate; feature
development verification uses simulation/static evidence. When a release cycle
lands, reset the checklist by manually unchecking every box so the next cycle
starts clean.

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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no entry added and no gesture touched — this cycle changes which operation KIND one existing entry's state message names (the shared shrapnel session the wound-view release gesture starts, the call site decision 246 already maps), so the limb, the target and the intent all still come from the entry's own request; the authority still derives and writes its own values and the operator's numbers stay evidence, so no user-visible behaviour is ADDED — the one the run showed broken (the patient's limb taking the pull's damage) is the behaviour the entry always claimed
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's self-check table (six rows) plus its Limits — each row names the file and the case that pins it; the receiving half is explicitly marked unverified because the behavioural test project excludes the Game Adapter from compile, and acceptance rows 1-3 are its judgement; the ticket's decay reading cites the game's own lines (`reversing/…/Limb.cs:56`, `:524`, `:507-508`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the five operation messages that carry the field were enumerated with all sixteen of their constructions in `src/`, the only two builders that had been leaning on the old default (both injection) now name it, and every reader of the field was walked — the adapter's state/end routing, the Stage-3 start guard (the one silent shape, now rejected by name), the target's body validator (`default: "Unsupported operation."`) and the start ack's own log sentence; the sibling with the same plausible-default shape (`RuntimeEntityRejectedMsg.Reason`) is recorded in the ticket's Limits with the reason it is out of scope rather than silently skipped
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's self-check table (six rows) plus its Limits and its review section; two mutations were observed red — the behavioural case before the fix (`Expected: Shrapnel, Actual: Injection`, 2 of the 2 state messages the session published) and the new gate with its assignment deleted (it named the file and the message)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: two offline layers plus the runtime rows — the L0 case pins the kind the authority PUBLISHES (`ShrapnelStateAndTerminal_CarryTheShrapnelKind`) and the source gate pins every builder of the five messages; what L0 cannot reach (the patient's own body taking the write, and the authority's copy surviving the next report) is acceptance rows 1-3, read on both clients, with row 4 as the Stage-3 regression the default change could have caused
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff that opened this session names the work item itself ("file a ticket for the patient's local values not syncing; the parent ticket's row 1 cannot close until it is cleared up"), and the fix follows the recorded rules rather than a new choice — root cause over patch stacking, the family aligned in one pass, rules carried by a fail-able gate, and judgment ownership left where it was
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet test CasualtiesUnknownOnline.slnx` green — behaviour 4932/4932 (was 4931), gates 595/595 (was 593, the new gate's two cases); the one red the change caused was this cycle's own new gate file using fully qualified names, fixed in this change
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no touched class approaches the gate — the largest are `OtherMedicalOperationSessionService` 575 and `MedicalOperationApply` 233, and no new type was added to `src/`; no state was added anywhere (the change is one field's default per message, one assignment, one warning and one guard that now answers); nothing became dead — the opposite, the Stage-3 guard's silent return was the one branch with no observable outcome and it now logs and answers; the receiving warning reuses the adapter's existing logger rather than adding a channel
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
