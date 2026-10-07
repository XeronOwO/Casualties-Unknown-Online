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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: self-check §1, eight rows: the serializer's TWO null shapes (net48 probe: no constructor, no initializer, nil reaches the setter, an omitted element does not), the per-consumer guards that were the only cover, the unguarded members' real failure mode, where the rule lives now (member + decode seam), why the seam is generic, the required-member refusals, the scan surface, the five code-built members
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: self-check §2: 27 payload members decided one by one, the five code-built declarations, `ModContentDefinition.Data` as the accounted-for exception, the decode-seam row, and every payload consumer guard deleted with the one non-payload coalesce named
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: self-check §3, ten rows naming their cases: both decode shapes (nil and omitted), the repaired-not-hidden re-encode, the assignment path, the no-nil payload, the census floor, the code-built declarations, the binder-level binding, the four required-member refusals, the DTO-level rule
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: self-check §4: an assembly-discovered census (rows ARE the members), BOTH payload shapes rewritten out of a real payload (nil and omitted, neither depending on the serializer producing it), the binder half read from recording loggers on both sides, and four mutation controls (member setter, decode seam, scan surface, required refusal)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff names this ticket as the next one by priority and dependency; the seam decision and decision 244 were written before implementation; no user question was asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0 warnings / 0 errors; `dotnet format` exit 0; final run with build: gates 451/451 and behaviour 4794/4794, both after the independent review's findings were fixed
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: largest touched class 579 lines and it SHRANK (`GameAdapterItemContentProvider`); no new state fields (the codec is stateless); dead mechanisms deleted with the fix: twelve duplicated `FromPayload` serializer bodies collapsed into `ModPayloadCodec.Decode<T>`, fifteen consumer-side null guards, and two dead null branches in `CustomBuildingTemplateFactory`; review: 1 blocker + 1 major + 6 minor + 4 nit, all fixed
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
