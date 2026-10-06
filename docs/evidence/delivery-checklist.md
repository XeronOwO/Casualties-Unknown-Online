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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: `display-body-query-seam-selfcheck.md` §1 — ten entries: R8's own argument order (`PlayerCamera.cs:1614-1618`), `Body.SlotOf`'s walk, `Body.GetItem`'s redirected guard over a LOCAL slot index, the four pre-fix postfix shapes, the measured local-versus-ring slot state at the abort, the total-and-quiet loss, the other three queries, both bracket kinds' call surface, the shared slot array, and the owner-side replay
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: self-check §1 rows 3/6/7/8 and §2 — ALL four `Body` queries the redirect answers now answer by skipping the native body (not only the one that threw), the file's one non-query seam (`PlayerCamera.OpenContainer`) is named with why it keeps its postfix, and every caller inside both bracket kinds was read, including the `UpdateWearables` body swap the `answering == instance` guard leaves native
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/items/display-body-query-seam-selfcheck.md` §3 — five rows: the seam gate (read RED before the fix), the frame that threw and why the prefix answers before it, the runtime re-drive named as the next batch's row, the owner-side applier unchanged with its unit pins, and the review's two adjacent findings with the shapes they follow
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the static half is `RemoteDragQuerySeamGateTests`, read RED on the pre-fix tree (the assertion read `4 of 5 Body query seam(s) do not answer by skipping the native body —` then the four seam classes it backticks) and green after the change, plus the seam file's own census floor; the runtime half is the next three-client batch re-driving `20261006-f`'s F1 recipe and reading the operator's `SwapSlots captured …` with the owner's `replayed native SwapSlots` instead of `UnityException … Transform child out of bounds`
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: this cycle's handoff names the occupied-slot release fix as the first work item ("fix the swap-release exception first") with its reason (it is the Critical ticket's failed row and blocks a second ticket's data), the order is the agent's per `AGENTS.md` rule 9, and the ticket's own record left the fix's shape to the development cycle; no user question was asked this cycle
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` clean through the full filtered run; `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist"` = gate project 390/390 and behaviour 4637/4637 on the frozen tree (this checklist's own gate is the one filtered out and is proven by the single gate run that follows this edit)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: physical lines — `RemoteDragPredicatePatches` 212, `BodyItemPatches` 327 (+6: the guard and its doc), `PlayerCameraUpdateWearablesPatch` 46 (+21: the finalizer and its doc), `RemoteDragQuerySeamGateTests` 148; no new state and no new bool (the added members are the pure `RemoteDragPredicateView.NamesASlotOfTheRing`, the finalizer, and the gate's own constants); the four postfixes were REPLACED rather than kept beside the prefixes, and the round's two review findings were fixed instead of deferred; the one thing this round leaves open is named, not hidden — the report-hook family has no gate yet (self-check §4)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
