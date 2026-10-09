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

- [ ] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no entry and no native counterpart exist here: the cycle changes the mod content registry and its providers, and no player-visible behaviour
- [ ] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's inventory table — contract, nine DTOs, registry, policy, display-name seam, binder, catalog, owner query, console completion, nine providers, codec, baseline, four doc pairs, example mod, acceptance recipe
- [ ] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: all nine content kinds moved together — nine DTOs, nine providers, the display-name seam, the catalog, the ownership query, the console resource-id source, the baseline and four doc pairs; no kind left on the payload path
- [ ] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's table — DTO identity, the provider cast refusal driven by `StubContentDefinition`, registry instance identity, the policy rails, the nine provider suites, the census counts, the baseline tombstones
- [ ] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: build + the full suite, `ModContentTests` driving the real mod stack and the nine provider suites the seam; the runtime proof is the typed `building-template-inject` recipe binding into the live provider in the acceptance batch
- [ ] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's 2026-10-08 ruling is the ticket's source and its shape section; the handoff orders this ticket first, and the concrete interface shape is recorded in the ticket rather than re-asked
- [ ] Build + dotnet format + dotnet test normative gates pass — evidence: build clean (0 warnings, 0 errors); `dotnet format CasualtiesUnknownOnline.slnx` exit 0; behaviour 4819/4819 (net48); the gate project's only red before this box was this checklist's own check
- [ ] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: biggest touched class `GameAdapterItemContentProvider` 455 lines, registry 94, interface 40, policy 34; no new state; the payload path, `ModContentDefinition`, `MaxDefinitionBytes`/`IsValidData` and the content DTOs' serialization attributes deleted in the same round
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
