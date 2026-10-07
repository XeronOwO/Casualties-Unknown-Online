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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: self-check §1: ten rows, the native `Drink` body (`WaterContainerItem.cs:198-215`, the one call with no content gate), the `ldc.r4` ml literals, the 36-id drink class, the deleted/added lists
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: self-check §2: nine rows — host admission, host drain, the patient apply with its sound scope, the three operator entries, the untouched sibling chains, the three reviewed censuses
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: self-check §3: ten rows naming their cases — the admission verdicts, both rules' overlap, the cap and the no-dose refusal, the dose on the wire, the rewritten drink cases, the kernel round-trip, the capability claim
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: self-check §4: focused filter 112 cases green; full WITH build behaviour 4774/4774 and gates 480/481, the single red being this checklist's own pending boxes; the native half (measurement, divert, patient apply, clip) named as not L0-reachable, its rows left to a later acceptance batch
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff names `todo/` work by the ticket's own order and this ticket's Part B table fixes the chain order; the native facts were measured from the decompiled tree before any code and no user question was asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format` exit 0, then the final run WITH build: behaviour 4774/4774 (net48) and gates 480/481, the single red being this checklist's own two pending boxes; the standalone gate run after they were ticked read 481/481, with the independent review's blocker and ten smaller findings fixed in the same commit
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the largest touched types measured with `File.ReadAllLines` (the gate's own count): `GameAdapter` 590 (589 at HEAD, +1 ctor parameter), `PlayerInteractionApply` 575, `GameAdapterBridge` 538, all inside the 600 limit and the architecture gate green; dead mechanisms deleted in the same round are the two drink catalogs, their applications and effect records, `RemoteConsumeCatalog`'s liquid half and the WHOLE `TimedBodyEffectMsg` chain (message field, journal record, wire type and apply, which had no producer left); one new static state field (`RemoteDrinkUseHandler._capture`, the topical handler's own window shape) and no new bool
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
