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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no gesture, UI or player-visible behaviour is added, changed or re-scoped — this is a research cycle that touches no `src/`, `tests/` or `tools/` file, and its only output is a backlog ticket plus an evidence record; the independent review's A0 pass confirmed no undecided design is smuggled in (the ticket says "The decision draft below is not ruled yet and no code moves until it is"), and the one thing that IS a CUO entry — the removal effect — is recorded by its native counterpart (`PlayerCamera.WoundSpecialAction` → `SplintLimb.TakeOff`) rather than by a family name
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: every mechanism the record names carries its own path — the four native item→component branches (`Item.cs:400-408`, `:1479-1491`, `:1505-1517`, `:1629-1639`), both `TakeOff` call sets, the `OnDismember` dispatch (`Limb.cs:101-103` via `SendMessage`), CUO's per-entry-point commits (`PlayerItemUseCommit`, `ItemProjection.ApplyDestroy`, `OtherMedicalRemovalApplier`, `CookItemCommand`, `CraftReportJudge`), the empty identity carrier (`ComponentStateMsg` is `{TypeName, Fields}`), the add-only registration (`ItemArbitration.RegisterCarried`) and the three-valued `TerminalKind`; the record's §8 names what it could NOT establish instead of asserting it (the ice pack's spent charge, whether `Limb.cs:135-138` still reaches anything after the drop at `:104-107`, and every runtime claim)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is enumerated in one pass rather than fixed point by point — the four native item→component branches, the two component→item branches, the four similar-looking attaches that are excluded BY NAME because they target `body` rather than a limb (`Painkillers` at `Item.cs:457`/`:840`/`:3650`, `MindwipeScript` at `:1208` and `Liquids.cs:1162`, `SleepingPills` at `:5842`), the ice pack (inside the native family, outside the rule because the game never turns it back into an item), and cooking and crafting (content-changing, hence a new id); and the three existing rules are each ruled on rather than left implicit — `TerminalKind.Consumed` CHANGES, the favourite mark's id+definition guard KEEPS and improves, "an id is issued once and never reused" KEEPS and is what makes the recommendation expressible at all
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the design record's §7 is the mechanism × change × size table (13 rows, each naming the files it touches), §6 answers the ticket's eight questions one by one, and §4 is the boundary table that decides which transforms the rule covers; the review reproduced the load-bearing figures — one production consumer of `NextAwardedItemId`, `RestoredWorldItemSet.Read()` filtering on `World`, `ComponentStateMsg`'s two members, the three-valued `TerminalKind` — and every figure it CORRECTED landed in this same commit (the tourniquet guard at `Item.cs:402` does not carry `isHead`, `ItemPatches` also suppresses the heater-cook source, removal carries the definition as well as `condition`, and the local removal path is a second new-id site)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: this cycle makes no runtime claim and says so — the record's §8 states that every claim is source-level (the decompiled game for §2, our own tree for §3–§7) and names what a later two-client run would have to observe; the cycle's own proof is the independent adversarial review against the frozen tree plus the gate run below, and the ticket deliberately carries no acceptance row because there is nothing to accept yet
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: investigation is the recorded exception, and the work item came from the opening handoff's own order ("先立并执行这份调研") — the ticket it produced is `docs/backlog/todo/item-identity-across-transforms.md`; the design question itself is NOT treated as approved: it is put to the user as a decision draft, and no code moves before the ruling
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` green — gates 614/614 and behaviour 4955/4955; `dotnet format` skipped per the documentation-only cycle rule (no C# changed), and the focused `RepositoryGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` run afterwards is what proves this checklist complete; the independent review found ONE blocker on the frozen tree — the new evidence page carried no `docs/evidence/selfchecks/MANIFEST.md` row, so `SelfcheckManifestGateTests.EverySelfcheckFile_HasExactlyOneManifestRow` was red ("1 self-check file(s) have no MANIFEST row") — and the row landed in this same commit
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no `src/` file is touched, so no class can approach the 600-line gate; the changed artifacts measured on the frozen tree — the ticket 98 lines, the design record 317 lines, the new index row 150 characters against its 160-character budget, one MANIFEST row added; no state bool was added and nothing was left dead, because the record instead NAMES the mechanism the recommended option would delete (`ItemIdCoordinator.NextAwardedItemId` and the whole synthesized namespace) and prices the documentation and test surface that goes with it rather than counting it free
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
