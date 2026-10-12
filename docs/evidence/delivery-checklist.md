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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: RECORD-ONLY cycle (the previous cycle's stage-1 fix `22ae381c` is committed and deployed, and nothing in this change touches `src/` or `tests/`): it moves that ticket to `review/`, splits its amplifier half into `docs/backlog/todo/call-context-scope-leak.md`, and re-points the six references and the index row the move invalidated — no CUO entry, no gesture and no player-visible behaviour is added, changed or proposed here
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the only mechanisms this cycle touches are the backlog's own bookkeeping rules, and each is read from its gate rather than assumed: `BacklogIntegrityGateTests` requires every ticket to appear exactly once in the index and every index link to resolve; `BacklogReferenceGateTests` requires every backward reference in the tree to resolve to an existing ticket; `EveryTicketStatusFieldAgreesWithItsFolder` requires the `Status` line to name the folder the file sits in. The new ticket's own subject mechanism (`CallContext`'s depth latch) is carried there with its measured part and its explicitly UNVERIFIED part separated, so nothing this cycle writes claims an artifact it does not have
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family here is "everything that names a ticket by path", and it was swept to zero rather than fixed where the gate happened to complain: `git grep -l 'backlog/todo/mod-item-with-no-declared-tags-throws.md'` answered six files (`docs/acceptance/lessons.md`, `docs/backlog/done/mod-item-authored-actions.md`, three batch acceptance records, the delivery checklist, plus the gitignored local archive) and the same command now answers **0**; the sweep also caught what the gate reported in the same breath — an index row the move's edit had dropped, restored in the same commit
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: one row, and it has an OBSERVED failure rather than an inference: the move → `git mv` + index row + six reference rewrites → `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` was run BEFORE the rewrites and failed exactly as predicted (three gates: the dropped index row, and `BacklogReferenceGateTests` naming the same six stale paths one by one), then ran green after them; `git grep` for the old path answers 0, and no test-case count moved (5067 behaviour cases untouched — no test file changed)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: this cycle has no runtime surface to prove — it changes records only, so the proof is the normative-gate project itself (backlog integrity, reference resolution, status-vs-folder) plus a repeatable `git grep` for the retired path; the runtime half of the ticket it moves belongs to the acceptance batch's rows, which the ticket's own *Limits* assigns
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: no new work item was chosen and no design question was asked: the move and the split are this ticket's own recorded state (`Status` + the *Required work* item that assigns the amplifier its own cycle) being made true in the index and the references, and the split's scope comes from that ticket's own text rather than a fresh decision
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: documentation-only cycle, so `dotnet format` is skipped per the checklist's own rule (no C# was edited) and no build was needed; `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests --nologo -v q` green at 615/615 after the rewrites, the same project having been red at 612/615 before them. The previous cycle's own numbers, kept for continuity: `dotnet format` exit 0, build 0 warnings / 0 errors, and the full suite green at 5067 behaviour cases + 615 gates with the stage-1 fix in place (`22ae381c`, deployed and verified as `0.1.0+22ae381c86cf63a7b3da90d61378c48c629b0be6`)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no class is touched at all this cycle (no `src/` or `tests/` file appears in the diff): one ticket moved `todo/` → `review/` with its `Status` line re-stated, one new ticket added, one index row added and one replaced, six references re-pointed; no state bool, no dead mechanism and no line-count question arises, and the moved ticket's own stages are now split so neither half has to carry the other's unverified claims. The previous cycle's measurements stand for the code it landed: `ModItemInfoFactory.cs` 257 lines, `ItemDeclarationParityTests.cs` 516, `IModItemDefinition.cs` 241, `ExampleMod.cs` 454
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
