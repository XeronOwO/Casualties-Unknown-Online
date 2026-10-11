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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no entry added — this cycle changes where an EXISTING entry's random values come from, the native `ShrapnelMinigame.BreakGrasp` call site the family's mapping table already names, reached from its own `Update`; the limb stays the one that call site named, the authority still judges, and the operator's screen now showing the value the authority commits is the user's own 2026-10-09 target rather than a new behaviour
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's self-check table — seven mechanism rows, each naming its file and the case that pins it; the native half (the window really wrapping the live call, the two extra draws inside it) is marked unverified, with acceptance rows 6-8 as its judgement
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the census in the ticket's §2 — all 195 of the game's own item delegates parsed and brace-matched (counted twice by two independent methods), the six that read `Random` named, the three operator-side measurement sites named, and the solid-food chain's 2026-10-08 migration confirmed; the family holds ONE double-roll site, so the window is scoped to it rather than generalised, and the two stale claims the ticket carried (a second site, and the sequence field that was never assigned) were corrected in the same change
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's self-check table (seven rows) plus its Limits; two mutations were observed red on the new cases — dropping the nonce from the seed mix, and `Differs => false`
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: three layers — the seed derivation as pure-function cases, the roller's cases over a recording fake engine stream, and the integration cases driving the real `SendShrapnelUpdate` path; the native half is judged at runtime by `ShrapnelGripSlipRoller`'s comparison line, which acceptance rows 6-8 read
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user set this cycle's target on 2026-10-09 — random-state control instead of waiting for the authority — which the ticket's *The target the user set* records; the ticket's design points (the code-registered seam, the derived seed, the authority-side comparison) follow from that target and the architecture rules, so no new user ruling was needed
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` 0 warnings / 0 errors; `dotnet format` exit 0, every changed file measured byte-wise (CRLF where the tree is CRLF, LF where it is LF, no BOM); behaviour 4931/4931 (was 4922); gates 593/593 (two new gate cases); the one red the change caused was the index row's own 160-character budget, fixed in this change
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the largest touched classes are `GameAdapter` 589 and `ShrapnelOperationSessionService` 555, both over the local 550 measure and both under the 600-line gate; that is recorded rather than papered over, and the session service owes a wire-assembly extraction the next time it is touched; the new types are 12-97 lines; no new state booleans beyond the two idempotent-close guards (the slip counter lives on the session that owns it, and the window is a disposable); and the dead mechanism `ShrapnelSessionStateWriter.RandomInRange` is DELETED rather than kept beside the derived draw
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
