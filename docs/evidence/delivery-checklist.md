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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this change adds NO entry and mirrors no gesture — it decides which values a mod may write into the game's own static item table (`ModItemInfoFactory.Build`, the only `new ItemInfo` under `src/`), and a content definition is materialized locally on every peer from the same declaration and never serialized, so nothing crosses a wire and no arbitration exists to place; the one user-visible behaviour change is the defect the ticket exists for (`RecognitionMinimum` defaults to 0, where the data class's leftover 2 hid the name its author wrote and printed an unchosen INT requirement), the scope is the user's own 2026-10-07 directive that open-ended content customisation is the point of the platform, and every OTHER new member defaults to the game's own value, pinned by `Item_DeclaringNothing_KeepsTheGamesOwnValuesForEverythingElse`
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's inventory table names each touched mechanism and its evidence — the build site (`ModItemInfoFactory.cs:34`, the only `new ItemInfo` under `src/`), the field initialiser the defect comes from (`ItemInfo.cs:181`), the nine undeclarable fields with their native readers (`PlayerCamera.cs:1043/:2126/:2139/:745/:1172/:1831`, `Body.cs:1390/:1297/:1403/:2120`, `MinigameBase.cs:223`, `CarriedEncumbranceCalculator.cs:40`, `InvButton.cs:85/:137`), the decay mask (`Item.cs:186-201`) and the refusal site (`CustomItemBehaviorValidator`); the game's own item table was counted rather than assumed (260 `rec = new Recognition(` assignments against 218 item definitions)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is the game's own item type, and it was walked field by field rather than sampled: of `ItemInfo`'s 32 public instance fields the declaration reached 23, and the 9 it did not (plus the 3 `DecayType` condition bits behind a byte the framework only half wrote) are all declarable after this change — proven by `Item_DeclaringEveryField_ReachesEveryPublicFieldTheGameReads` and `TheDecayVocabulary_ReachesEveryFlagTheGameReads`, which drive the production provider and read the built item; the two sibling records that carried the wrong claim about this list are corrected in the same change (`review/mod-item-wearable-declaration.md`'s non-goal, and the umbrella's Part 3.A entry, which was also incomplete)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's table carries seven mechanism × change × evidence rows, each naming the case that pins it (the defect with its red's own text, the 32-field census with its floor, the decay census with the by-name pin, the three refusals, the battery-plus-condition shape, the inert-declaration report, the unchanged defaults) plus the contract row for the 24 baseline lines; the ticket's *Limits* section names what the table does NOT cover (a list-valued field compared by length, "written" versus "read", the game's own 24 recognition-less definitions, and the two rows only a live session can judge)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: three classes of evidence, decided before the run — (a) the build site itself, driven reflectively through the real provider by `ItemDeclarationParityTests` (12 cases: the recognition default, the 32-field census, the decay-flag census, the three refusals, the inert-declaration report, and the unchanged-defaults case); (b) the live game's own item table, read in the acceptance batch by an eval over `Item.GlobalItems` (`example.dressing`'s `rec.min` on the host AND on the guest, which is what proves the declaration materializes identically on both peers) plus the game's own hover text for that item with the reading body's INT recorded beside it; (c) the batch's own log windows, which must carry no `[ERR]` for the item provider. The rows are declared in the batch's scope page before the session, and the ticket's acceptance table names which mechanism produced each row's evidence
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the work order is the previous cycle's handoff (`todo/mod-content-ceiling.md`, the only High in `todo/`, whose Part 3.A remainder this ticket cuts), the SCOPE was frozen by the user on 2026-10-07 ("open-ended content customisation and a mod-extensible platform" is the promotion directive the umbrella records), and the item-level design rule this change extends was ruled in this family twice before (`review/mod-item-wearable-declaration.md` and `review/mod-declared-behaviour-with-no-function.md`: a declaration the framework cannot materialize is answered and inert, never fatal); no design-shaped choice was taken here beyond the two refusals and the one default, each argued from the game's own code in the ticket
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` green (0 warnings, 0 errors); `dotnet format CasualtiesUnknownOnline.slnx` exit 0 after the last C# edit (the review's two test additions included); `dotnet test CasualtiesUnknownOnline.slnx` green at 614 normative gates and 5064 behaviour cases, taken after the boxes above were filled, with the checklist gate itself excluded only while its own boxes were still being ticked — and re-run INCLUDING it after this box was checked, which is what proves the checklist complete
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: every touched class was measured after the last edit — `ModItemInfoFactory` 220, `CustomItemBehaviorValidator` 317, `GameAdapterItemContentProvider` 463, `IModItemDefinition` 195, `ModItemDecayCondition` 32, `ItemDeclarationParityTests` 347, all under the 600-line gate; no field or state bool was added anywhere (a new `[Flags]` vocabulary and properties on an existing data contract, plus one private static validator and two test helpers), and nothing became dead — the members the change supersedes are the game's own field initialiser's effect on mod items, which is what the change removes rather than leaves beside a second path
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
