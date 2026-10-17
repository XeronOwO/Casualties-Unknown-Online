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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this change adds NO gesture and mirrors no native call site: the host registering its own carried inventory is CUO's own record bookkeeping (the ticket's read half found the gap and its stage text froze the direction), and no player-visible gesture or value changed — what changed is that the host's own items have ids and rows at world entry instead of after their first native act — ticket, *The host's own carried inventory is registered*
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: seven mechanisms, each cited in the ticket's table — the shared capture (`CarriedInventoryReporter`), the two arming edges (generation-finished + the host-session rising edge), the write route (`ItemOwnCarriedRegistration` → `SendItemCarriedSync` → `TrySpawnCarried`/`TryUpdateState`, the native act's own), the members' record (the kernel batch a member replay reads), the untouched guest path, the one-shot-not-cadence rule, and the untouched wire
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is every carried-set registration — the member's (unchanged, its cases still green), the host's own (new, same capture and same write), and the crafted-product carried fact (`CraftSyncService.PublishCarriedSyncLocal`, checked: it publishes a single product fact and shares only the publish helper, so nothing to align) — and the role branch lives in ONE place (`SendCarriedInventory`), not in each caller
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: seven rows in the ticket's table; rows 1-4 name the three new facts (row 2 shares row 1's case), row 5 names the class's own run (16/16 in `CarriedInventoryRegistrationTests`, the first thirteen the existing guest/arbitration facts), and rows 6-7 name the line counts and what is not covered — the ID MINTING itself and the live rows are the acceptance batch's, named rather than implied
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the registration is driven through the real `IItemControl` on a host+members simulation (`ItemSimWorld`), so the ROW, its owner and the member's replay are proven without the game; the pump holds the due while a local character restore is queued (the respawn layer's fresh body must not spend the host's one registration — the independent review's M1, fixed here); the live rows (a host's untouched supplies listed, written and read back on both peers, plus the respawn layer) are named as the acceptance batch's, and no weaker check stands in for them — ticket, *What the acceptance batch owes*
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff ordered this ticket, and its stage-B text already fixed this direction ("the host registers its carried inventory at the generation-finished edge the way a member's client does"); the only design detail settled here — one-shot rather than the guest's cadence — follows from the mechanism (a local write cannot be lost, a repeat would re-commit rows), so no user decision was opened
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured at this tree — `ItemService` 578 (548 at HEAD), `ItemOwnCarriedRegistration` 85 (new — the registration lives in its own collaborator, the service grew by the delegation only), `CarriedInventoryReporter` 171, `ItemIdCoordinator` unchanged at 378; no class over the 600 gate; TWO new boolean state fields, one per class (`_pending`, `_hostSessionActive`); the dead `PublishCarriedSync` / `PublishCarriedSyncFor` pair (no caller in src or tests) is DELETED in this round, with the `IItemActionWorldAccess` member it existed for; the watchlist's "near the limit" section carries `ItemService` with the split seam it would take next
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` 0 warnings 0 errors; `dotnet format` exit 0; the full suite at this tree is 5616/5616 behaviour cases (5613 + the three new host-registration facts) and 693 gates — the sync-coverage row's first wording broke its 1,200-character row budget and is trimmed to fit (14/14 in that gate's own run), and this checklist's box was the only other red before it was filled
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
