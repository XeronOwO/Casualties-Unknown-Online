# Delivery Checklist

Every development cycle runs through this checklist. Two gates run before the
cycle's final commit as part of `dotnet test`:
`DeliveryChecklistGateTests.DeliveryChecklist_NoIncompleteRequiredBoxes` refuses
the commit while any required box is unchecked, and
`DeliveryChecklistGateTests.DeliveryChecklist_DeclaresEveryRequiredBox` is the
census — it refuses a checklist that no longer declares one of the steps below,
naming the step that went missing, because a record whose boxes are gone must not
pass by having nothing left to fail on. Deployment and multiplayer acceptance are
the agent's actions, run after the commit per `docs/acceptance/` and outside this
gate; feature development verification uses simulation/static evidence. When a
release cycle lands, reset the checklist by manually unchecking every box so the
next cycle starts clean.

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
leaves the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked, but may write its
boxes in one pass. That is the deliberate
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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: not applicable, and the reason is checkable — nothing here crosses players or mirrors a native call site: the change adds a READ-ONLY console verb over the kernel's own item row, with no new wire message, no patch and no game-side behaviour, so `git status --short -- src` names the command group with its grammar/renderer, the read model and its test fake, and nothing else
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: four mechanisms, each with its reader named — (1) the target grammar `ItemDataQueryParser`, read by the verb's handler and pinned by `ItemDataQueryParserTests`; (2) the kernel item read model, `IKernelItemFacts.QueryItems()` added and already implemented by the authority its two existing readers use, read by the verb; (3) the selector expansion `CommandPlayerTargets`, read by the verb and by `/heal`; (4) the renderer `ItemDataRenderer`, read by the verb and pinned by `ItemDataRendererTests`
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "a console command group that expands a player selector", swept rather than patched where the ticket pointed: `CommandSelectorResolver.Resolve` now has exactly ONE call site (`CommandPlayerTargets`), the console's own private copy is DELETED by this change rather than left beside it, and `HostAdminCommands` was checked and left alone because it resolves a member by SteamId or display name and never a selector; the item read model's enumerating consumers were swept too (the concrete-authority callers keep reading the same kernel method the interface now fronts, so no second enumeration path exists)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's table (`review/item-data-read-commands.md` §Self-check), five rows with no empty cell, each naming the run behind it: the grammar's 5 accepted and 15 refused shapes, the renderer's per-kind/per-location/contents cases, the 15 integration cases through the real console and the real kernel, and the model/selector rows naming their implementors and their single call site
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the console lines ARE the runtime trace — the verb answers into the console's bounded buffer and logs one Information line per invocation naming the target and what it resolved to — the pure tests pin the grammar and the wording without a session, the integration tests drive the real console and the real kernel, and the two-client half is the ticket's six acceptance rows (rows 3-4 read the SAME ids from both peers), decided rather than assumed
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the work item is the user's own 2026-10-07 request (`todo/item-and-entity-data-commands.md`, which fixes the family and asks for the attribute first), and this session's reply settled the staging explicitly — asking which stage to take first was itself called out ("the rules already say not to ask this kind of question; go at your own pace") — while the target vocabulary the user did specify (a player selector, a per-player slot, the item nearest the cursor, expanded Minecraft-style) is recorded in the ticket's grammar and its limits
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` succeeded, 0 warnings / 0 errors; `dotnet format CasualtiesUnknownOnline.slnx` exit 0 after the last C# edit, and again after the user-ruled uniqueness guard (also 0); `dotnet test CasualtiesUnknownOnline.slnx` green at **5126/5126** behaviour cases (net48) and **658/658** gates — the cycle's earlier run of 5124/5124 and 657/658 carried exactly one red, this checklist's own two then-open boxes, which is the gate working rather than a defect — and the focused re-run once both were checked is green: the checklist, backlog, census, documentation-tree, source-shape and selfcheck-manifest gates together, **129/129**
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured the way `SourceShapeGateTests` measures (every line, not the non-blank ones) — `CommandConsoleService.cs` 584 at HEAD → **571**, `IKernelItemFacts.cs` 20 → 33, and the five new files 262 / 160 / 103 / 45 / 12 (`ItemDataCommands` the largest), every touched class far inside 600; no new state bool and no new state field anywhere (the group holds injected services only, and the parser/renderer are static and pure). The change DELETED a mechanism rather than adding one: the console's own selector expansion and its display-name lookup are gone, `CommandSelectorResolver.Resolve` now has exactly ONE call site (`CommandPlayerTargets`), and `HostAdminCommands` was checked and left alone because it resolves a member by SteamId or name and never a selector
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
