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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: self-check §1, nine rows citing the one topical implementation (`WaterContainerItem.cs:218-234` `ApplyToLimb` with its `healthUsable` gate), the four per-item ml literals that are NOT data (Item.cs:652/676/2101/2125), both native clip sites, the two registry fields, and the deleted/added lists
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: self-check §2, nine rows: host admission, host drain, the patient apply (with the deliberate scope difference from Part A and its reason), the three operator entries, the four removed sound rows with the gate proved in BOTH directions, the seven untouched sibling chains, and the tests/gates sweep
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: self-check §3, ten rows naming their cases: the admission verdicts, the two chains' disjointness, the measured-dose arithmetic and its cap, the dose on the wire with the host inventing no body state, the limb selection carried instead of resolved, the no-dose refusal, the registry refusal that replaced the allowlist, the journal round-trip, the native half's existence (no double-play), and the patch-bridge census
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: self-check §4: the L0 half is the wire dose, the drain arithmetic and the verdict (`TopicalSemanticsTests` + the rewritten `ItemUseTests` topical cases + the extended kernel round-trip), and the native half — the measurement, the divert and the clip relay — is named as NOT L0-reachable (needs a game process) with its acceptance rows left to a later batch and its existence pinned by the sound gate instead of claimed
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff names `todo/` as the next work by priority; this ticket is the highest-priority item with pending development, its Part B table fixes the chain order (topical first) and the native predicate, and the migration was planned from the game's own decompiled data before any code; no user question was asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0 warnings / 0 errors after `dotnet format` exit 0 with the independent review's findings fixed; final run WITH build: behaviour 4786/4786 (net48) and gates 480/481, the single failure being this checklist's own unchecked box; the standalone gate run after checking it read 481/481
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the architecture gate forced the one oversized touched class to be split (it reported `PlayerItemUseService` at 619 lines mid-change — an in-session intermediate reading, not reproducible from this tree, where HEAD is 580 and the final file is 489); the recursive carried-item tree and the drain arithmetic moved to `CarriedItemUseTree` (169) in the same change, the largest other touched `src/` files being `PlayerInteractionApply` 557 and `GameAdapterBridge` 529, all inside the limit; dead mechanisms deleted in the same round are the three catalog types and the four topical sound rows plus `PlayTreatmentSound`'s liquid-clip half; one new state field (`RemoteTopicalUseHandler._capture`), matching the injection session's own static-session shape, with no new bool
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
