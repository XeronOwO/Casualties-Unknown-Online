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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the `cursor` target maps to the item `PlayerCamera.TryPickupFromWorld`'s query SELECTS (`PlayerCamera.cs:1423` + `:1437`), not the wider hover mask (`:1997`); the gesture's own gates (paused / conscious / allowUseItem / ten units) are deliberately not mirrored because they judge the pickup action, not the pointer, and that is recorded as a limit rather than a fallback; a pointer that cannot carry an id is refused by name and no nearby item is substituted; the only user-visible addition is the optional `cursor` shape the user asked for — ticket, *The cursor target landed*
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: eight mechanisms, each cited in the ticket's table — the pickup query (`PlayerCamera.cs:1423`/`:1437`), the current id rule (`GeneratedItemAuthority`'s publish, `ItemIdAllocator.EnsureId`) with the stale `ItemInstanceId` comment corrected in this change, another member's item being non-physical and therefore never pointer-returned (`CloneInventoryRenderer`, `StandingItemRecipe.MakeInert`), the port, the shared grammar, the moved one-item verdict, the unchanged report route, and the untouched wire
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the four item-data target shapes now share ONE verdict in `ItemDataTargets` (the read's slot/body resolution and the write's one-item verdict), and the other pointer gesture, `CrossPlayerDragUse`, was checked and resolves PLAYERS by its own world geometry rather than the item layer, so it shares nothing to align — ticket, *Whole-family audit*
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: ten rows in the ticket's table; the eight `CommandConsoleItemDataCursorTests` facts cover the record-only read, five named refusals (read: nothing / no id / not recorded; write: nothing / not recorded), the world-row write's correction route and the merge tap, and each names its assertion; the last two rows name what did not change and what is not covered rather than leaving cells empty
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the scene's answer is staged through `FakeLocalItemAccess.Cursor`, so the wiring — which id the target resolves, which row it reads, what a refusal writes — is proven without a pointer; the real pointer's four rows are named as the acceptance batch's, with the staging trick (place the item under the pointer's own readable world point) recorded, and the pointer-outside-the-window dependency named instead of replaced by a weaker check — ticket, *What the acceptance batch owes*
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff ordered this ticket next after the backlog move, and the ticket's own stage-B text fixes the direction (the cursor target on the write half's port, the one-item rule, the inherited host rule); the id-less question the write ticket left open is answered by the identity rule already recorded (no invented identity — the member-report bound, decision 262), so no design question was opened
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured at this tree — `ItemDataCommands` 460 (549 at HEAD, the verdict moved out), `ItemDataTargets` new at 204, `LocalItemAccess` 140, `ItemDataQueryParser` 133, `ItemDataWriteParser` 324, `ILocalItemAccess` 63, `UnavailableLocalItemAccess` 33, `LocalCursorItemKind` 24; `CommandConsoleService` is 600, exactly the gate ceiling and unchanged in line count by the one construction line, recorded on the architecture watchlist's "at the limit" section with the split due; no new boolean state field (the kind is a classification, not state); the review's dead mechanism is DELETED in this round (the unreachable `AnotherMembers` arm plus its enum value, refusal, test and doc claims), and `ItemInstanceId`'s stale "never carry one" comment is corrected
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` 0 warnings 0 errors; `dotnet format` exit 0; the full suite at this tree is 5613/5613 behaviour cases and 693 gates, whose only red before this line was filled was this checklist's own open box (the other was the structure line above, now filled too); the normative suite re-run after the fill is 693/693
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
