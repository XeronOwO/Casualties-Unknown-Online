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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: `local-item-into-remote-display-selfcheck.md` §1 — nine entries: the one-sided bracket, the native target reads (`PlayerCamera.cs:1532`/`:1540`/`:1666`), the ring's clone resolution, `Container.LoadItem`'s re-parent with `MarkRemoteCloneTree`, the R5 half-state argument, the clone-owned body slot, the collider argument's own boundary, the early-out cancel, and the while-dragging favourite gate
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: self-check §1 rows 2/5/6 and §4 — every proxy-naming branch of the release (container, R5's loop, battery, combine, body slot, the container window) is refused by the one target read, and the two it does not read are named with their evidence and their boundary (world fallback: the creating paths disable the collider, the reuse path is not covered — §4; use-by-drag: runs before the guard and mutates no proxy)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/items/local-item-into-remote-display-selfcheck.md` §3 — four rows: the seam census pin, the L0 port contract, the implementation census, and the runtime row named as planned with its pre-fix red
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the static half is the seam census pin (red on the pre-fix tree: the pinned member is not declared) plus the L0 port contract; the runtime half is batch `20261006-d`'s own gesture re-driven in batch `20261006-e` and read from the release probe (`castItemProxy`, `castContentsAfter`, `childInCast`) together with the owner's log (no `[ItemDropped]`, no `[ItemDestroyed]`, one refusal line)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: this cycle's handoff names `local-item-into-remote-display` as the first work item and the order is the agent's per `AGENTS.md` rule 9; the ticket's own "What it needs" carries the fail-closed rule the fix implements; no user question was asked this cycle
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` clean; `dotnet format` exit 0; `dotnet test CasualtiesUnknownOnline.slnx` behaviour 4637/4637 and gate project 382/382 on the frozen tree (the filtered pass that ran while these two boxes were still open was 381/381, the one filtered gate being this checklist's own)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: physical lines — `PlayerCameraDragUsePatch` 231, `PlayerCameraHandleWhileDraggingPatch` 209, `RemoteDragIntentDispatcher` 167, `IRemoteBackpackPatchBridge` 53, `GameAdapterBridge` 514 (unchanged in shape, +2 lines); no new state and no new bool (the guard returns a nested `readonly record struct` and holds nothing); the one gate this round found dead-in-effect (`SnapshotFavourites`' view-open early return) was deleted rather than kept, and no seam or intent member was added to the vocabulary
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
