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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: self-check §1, eight rows citing the native dispatch (`PlayerCamera.cs:739`), the one injection implementation (`WaterContainerItem.cs:237-261`), the registry fields that replace the table (`Item.cs:7168`, `LiquidType.injectable`), the dose literals that are NOT data, `CoUtils.DoTimedOp`'s accumulation, and the deleted/added mechanism lists
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: self-check §2, nine rows: host admission, host drain, the one-shot refusal, the operator entry (dispatch + `Inject` divert), the target apply (and the deliberate no-echo rule), the treatment-sound table's 15 removed rows with its gate proved in BOTH directions, the four sibling chains recorded as Part B, and the tests/tools/docs sweep (21 catalog cases replaced, three gate censuses reviewed)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: self-check §3, nine rows naming their cases: the drain arithmetic, the admission verdicts, the start-refusal branch that had NO case before this cycle, the dose on the wire, the host inventing no effect, the cancel/timeout/disconnect guarantees, the whole drained plan, the native half's existence (and the no-double-play rule), and the patch-bridge census
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: self-check §4: the L0 half is the wire dose, the drain arithmetic and the verdict (`InjectionSemanticsTests` + the rewritten session/tool cases), and the native half is named as NOT L0-reachable (adapter needs a game process) with its acceptance rows left to a later batch and its existence pinned by the sound gate instead of claimed
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff names `todo/mod-content-ceiling.md` as the next ticket by priority and its Part 2 stage 2 requires exactly this cut, one chain first, with the delete-the-table acceptance; the new ticket was written before any code and no user question was asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0 warnings / 0 errors after `dotnet format` exit 0 and the independent review's findings were fixed; final run WITH build: behaviour 4781/4781 (net48) and gates 480/481, the single failure being this checklist's own unchecked box; the focused gate run after checking it read 481/481
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the architecture gate forced the one oversized touched class to be split (it reported 680 aggregate lines for `RemoteMedicalOperationHandler`, whose HEAD file itself was 581 lines; the gate counts aggregate, the file line count is what the review re-measured); after the split the handler is 295 file lines and the injection session `RemoteInjectionUseHandler` is 442, both inside the limit; the dead mechanisms deleted in the same round are the whole catalog (`RemoteMedicineCatalog` / `RemoteMedicineApplication` / `RemoteMedicineLiquidEffect`), the terminal `TimedBodyEffects` field, `OperationSession.OriginalLiquids` and eight injection-only `TimedBodyEffectApply` branches; no new state bool (one new dependency field, `RemoteMedicalOperationHandler._injectionOps`, plus `GameAdapterDomains.LimbUseSemantics` so the operator's eligibility uses the DI seam)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
