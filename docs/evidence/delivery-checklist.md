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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no CUO entry and no native counterpart exist here: the cycle makes a content declaration's kind an interface and re-points its consumers at it, adds no player-facing gesture and changes nothing a player can see, so there is nothing to map or approve
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the self-check §1 — ten rows, each naming the file or the grep it rests on: the nine kind interfaces, the nine data classes as their implementations, the nine providers (`grep "is not IMod"` returns exactly nine hits and zero concrete-type tests remain), the display-name resolver, six template/behaviour factories, five world-generation distributions, the liquid-tile write-back and its `NormalizedLiquidTileDefinition` view, the three derived-rule containers, the `AllSpawnLayers` sentinel, and the re-reviewed API baseline
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the self-check §2 — the consumer census over all of `src/` and `tests/` (Runtime 5 files, GameAdapter 22, ModExample 1, tests 26; six other projects hold zero references), all nine providers converted with none left on a concrete type, the derived-member family (8 members over 5 data classes) moved whole rather than one at a time, the ONE definition write-back site in the whole tree found and removed, and the interface's collection types matched to the classes' because interface implementation requires an exact return type
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the self-check §1 (ten rows: the nine kind interfaces, the data classes as their implementations, the nine providers, the display-name resolver, six factories, five world-generation distributions, the liquid-tile write-back and its view, the three derived-rule containers, the sentinel, the re-reviewed baseline), §2 (the consumer census and the five things it decided), §3 (what landed against what deliberately did not), §4 (eight verification layers, each with its command and result), §5 (the independent review's ten findings and their dispositions) and §6 (six limits)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the self-check planned four layers, and none needs a game process: the contract SHAPE by the compiler plus `ApiSurfaceGateTests` (which is also the review step the modification policy names — it fails until the baseline is re-reviewed by hand), the SEAM by `ModAuthoredDefinitionBindingTests` driving all nine REAL providers through the real binder with the test project's reflective adapter host (the test project never compile-references GameAdapter, so the roster and loggers are built at runtime), the REFUSAL by `ModContentNullCollectionBindingTests`' nine per-kind assertions, and the null-collection rule by the existing `ModNullCollectionRuleTests` census, which the interfaces are invisible to by construction (it scans public classes)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket's shape was settled in the user's own 2026-10-08 conversation and recorded in `todo/mod-content-attribute-declarations.md` ("The shape (settled)"); the handoff orders this ticket first and states it needs no ruling, and the ticket carries no open user-facing question (its "Open at implementation" items are the agent's to settle, and decision 251 settles them)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build clean (0 warnings, 0 errors) on the frozen tree; `dotnet format CasualtiesUnknownOnline.slnx` exit 0, with the new C# files normalised to the repository's CRLF and no file left wanting a change; behaviour 4854/4854 (was 4850; four new cases this cycle); focused 69/69 and 205/205 with every filter condition written in full; gates 572/573 through the cycle with the only red being this checklist's own required boxes, which are filled above — one further real defect the gates caught mid-cycle (`FullyQualifiedNameGateTests` on a new file's `System.Math` spelling) was fixed before the freeze
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the largest touched class is `CustomItemBehaviorApplier` at 545 lines (unchanged in size — a type rename only), then `GameAdapterItemContentProvider` 457 and `GameAdapterLiquidTileContentProvider` 428 (was 435, it shrank), all far under the gate's 600; the two data classes that carried the derived members shrank with them (`ModBuildingDefinition` 212 → 164, `ModLiquidTileDefinition` 197 → 157); the new types are small and single-purpose (`ModDeclarationCollections` 30, `NormalizedLiquidTileDefinition` 121); no new state bool was added; and the change DELETED a dead mechanism rather than keeping it (the `MaxFloodFill` refusal the normalisation had made unreachable) instead of relocating the write-back that the contract forbade
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
