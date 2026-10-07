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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: self-check §1, eight rows: the twelve-constant vocabulary against the nine provider kinds, the three provider-less names with their recorded status, the shape-only kind policy, the Debug-only no-provider branch under an Information-level success line, where the provider map lives versus where registration happens, what a provider-less registration still reaches, how a resource entry's kind is consumed, and the gated baseline
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: self-check §2, seven rows: the twelve constants decided one by one (nine kept with their provider, three removed with reasons), the only other consumer of a removed constant (`BuiltInResourceLocationSource`), `ModContentDisplayName`, the console vocabulary's deliberate discovery-versus-materialization split, the nine providers checked class ↔ registration ↔ constant, and the tests/tools/docs swept for the removed names
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: self-check §3, six rows naming their cases: vocabulary ↔ providers, declaration ↔ composition registration, the load-time warning read from a recording logger, the built-in resource's own kind word, the three baseline tombstones, and each census's own matcher samples
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: self-check §4: the three-census source read with a floor each (the adapter is not in the test host), the production binder driven with a recording logger instead of a "// no throw" comment, four mutation controls (constant without a provider, registration removed, warning reverted to Debug, provider kind repointed) each restored byte-identically, and no row needing a session
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff names the umbrella `todo/mod-content-ceiling.md` as the next ticket by priority, and its own Part 3 B plus promotion rule freeze this entry's design and its two-halves shape; decision 245 was written before implementation; no user question was asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0 warnings / 0 errors; `dotnet format` exit 0 after the independent review's findings were fixed; final run with build: behaviour 4796/4796 and gates 480/481 with the single failure being this checklist's own unchecked box, then 481/481 once it was checked
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the touched classes are 41 / 136 / 31 lines and the new gate is 439 (test project, below the 600-line limit); no new state field or bool anywhere; the dead mechanism removed in the same round is the three-constant vocabulary nothing could bind, with its one remaining consumer rehomed (`BuiltInResourceLocationSource`'s own `PlayerKind`)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
