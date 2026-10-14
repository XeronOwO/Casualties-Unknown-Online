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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the change reaches other players only through the report it REUSES, and the ticket's §Entry mapping names each counterpart one line at a time: the gesture is CUO's own (the user's 2026-10-07 request, not a native mirror), the state write is the game's own save restore (`condition`/`favourited` assigned, then the codec's `RestoreLiquids`/`RestoreComponentStates`; `SetCondition` deliberately unused because it drains water by ratio), the report is `ItemUseSync.OnItemUsed`'s host branch, and the target is the read verb's grammar. No entry needs information its gesture cannot carry and none resolves a target by a fallback (a target naming several items is refused at execution, the user's 2026-10-14 ruling); the one behaviour question the umbrella ticket reserved — editing another player's carried item — was put to the user, who left the stage's scope to the agent and said the rest waits for later rounds, and the ticket's Limits carry it
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: four mechanisms, each with its reader named in the ticket's §What landed — (1) the local-scene port `ILocalItemAccess`, read by `ItemDataCommands` and implemented by the adapter's `LocalItemAccess`, replaced into the container and faked in the console tests; (2) the write grammar `ItemDataWriteParser` over the read verb's own target prefix, pinned by `ItemDataWriteParserTests`; (3) the value half `ItemDataWriteValues`, pinned by `ItemDataWriteValuesTests`; (4) the report route, two EXISTING `IItemControl` calls chosen by the kernel's world-rooted walk, pinned by route cases that assert the events each route raises. The adapter's own half (the id index, the proxy/standing refusals, the three writes) has NO unit coverage, and the ticket names it as the acceptance batch's half rather than claiming a test
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "a console command group that expands a player selector and then addresses ONE item", swept rather than patched: the target grammar now has exactly ONE implementation serving two verbs (`ItemDataQueryParser.TryParseTarget`, with `TryParse` the whole-line form the read verb keeps), the write's values and refusals live in one pure class, and both report routes are the two calls the native half already makes instead of a third report shape. The independent review then found the route was chosen by the item's own row kind — a child of a WORLD container would have gone out as a carried fact its receivers cannot place — and the family was realigned to the kernel's own world-rooted walk in the same cycle, with the regression case that reads it (`Set_OnAContainedChildOfAWorldContainer_TakesTheWorldCorrectionRoute`)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's §Self-check, five rows with no empty cell, each naming the run behind it: the grammar's 33 cases (6 accepted shapes, 7 flag words, 2 keyword-casing cases, 17 refused shapes, the finite-number and the named unknown-property refusal), the value half's 14 cases over every field kind, the route cases that assert WHICH event each route raises (the row changes on both, so the row is not the discriminator), and the local-vs-record case that pins where the value is computed
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the console's answer line plus one Information log per write are the runtime trace (the refusals log at Warn with the reason), the pure tests pin the grammar and the values without a session, the console tests drive the REAL console and the REAL kernel over a fake scene (the kernel row changing is the report landing), and the adapter's own half is the ticket's seven acceptance rows — decided rather than assumed, with the unverifiable half named in the ticket's Limits
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the work item is the handoff's own next step (stage B of the user's 2026-10-07 request), and the design the user had already frozen is inherited rather than re-asked: the write is the holder's own report, the one-item verdict is taken at execution (2026-10-14), and `merge`/`remove` are named as not-yet. The single question the umbrella ticket reserved for the user (can a write reach another player's carried item) was asked ONCE, in plain words with the effort of each option; the user answered that the agent sets the pace and that whatever is left waits for later rounds, so the narrower path was taken and recorded in the ticket's Limits
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors; `dotnet format CasualtiesUnknownOnline.slnx` exit 0 after the last C# edit and again after the independent review's fixes; `dotnet test CasualtiesUnknownOnline.slnx` green at **5198/5198** behaviour cases (net48) and **657/658** gates — the one red being this checklist's own open boxes mid-cycle, which is the gate working; the focused re-run after every box was checked is the commit's own gate run
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured the way `SourceShapeGateTests` measures (every line, not the non-blank ones): `ItemDataCommands.cs` 248 → **475**, `CommandConsoleService.cs` 571 → **575** (the watchlist entry refreshed in this cycle), `ItemDataQueryParser.cs` 103 → **125**, `ItemDataRenderer.cs` 160 → **202**, and the new files 47 / 24 / 19 / 86 / 236 / 153 / 32 / 22 — every touched class far inside 600, `GameAdapter.cs` untouched at its 600-line ceiling (the port is a standalone registration for exactly that reason); no new state field or boolean anywhere (the command group holds injected services only, the parser and the value half are static and pure). A mechanism was DELETED rather than added: the write verb has no report shape of its own, it calls the two the native half already makes, and the review's dead `NoLiquidContainer` result was removed the moment a test showed the value check refuses that case first
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
