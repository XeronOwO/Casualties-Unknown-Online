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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the ticket's *Entry mapping (the native call site)* names the game console's own `spawn` command (`ConsoleScript.RegisterAllCommands`) and its `ParsePosition` words, with `Utils.Create` as the creation call; the three deviations (no `amount`, no 2-unit auto-pickup, no entity state) are recorded as limits with the reason each was not inherited
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's twelve-row inventory under *Landed*; the native command, its position words and `Utils.Create` are cited from `reversing/Assembly-CSharp/ConsoleScript.cs` and `Utils.cs`, the three late-joiner tables from the landed channels, and the adapter's own instantiation is marked unverified-by-test (code review plus the acceptance batch)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the ticket's *Whole-family audit* reads the console's cheat family and the three creation families end to end; the give ladder's drop report moved into the shared `WorldItemCreation` in the same change and its source-shape pin moved with it (`GiveLadderDrop_IsWiredFromTheTransferToTheSpawnReport` now pins the chain through the new owner), so the spawn verb could not become a second item-materialization path
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's twenty rows under *Landed*, each naming a live test (the 59 new cases listed below) or the acceptance row that is its runtime proof; the adapter's sentences and port calls are additionally pinned by `WorldSummonContractGateTests` (16 cases), the review's M1/M2 finding
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: `WorldSummonLateJoinerTests` proves the three late-joiner tables against the real channel/registry/kernel/fan-out with the later joiner's link down during the creation; the adapter's own instantiation, prefab tables and pointer/body reads are the acceptance rows 1-9 (the batch after the deploy), and row 10 is recorded `unproven` if the live window cannot be held
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the umbrella ticket's stage D text froze the direction ("create an entity in the world through the accepted-creation path, with the late-joiner question answered before the verb lands"), and the session handoff orders that stage as the next unit
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `CommandConsoleService` 590, `WorldSummonService` 245, `WorldSummoner` 201, `PlayerInventoryPlacement` 191, `WorldItemCreation` 90, `ItemStateText` 65 (all under 600); the shape gate's own findings were fixed in this round by splitting `IWorldSummon`'s four types into their own files and trimming the matrix row, not by a debt entry; the two `/give`/`/summon` answer printers share one shape instead of a second mechanism; the review's fix cycle added `WorldSummonContractGateTests` as its own file (16 cases) instead of growing `SourceShapeGateTests` (815 lines)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` exit 0 (0 warnings), `dotnet format` exit 0; the landing run recorded the behavioural suite 5798/5798 and the normative gates 693/695, then 695/695 (`.acceptance/test-full-3.log` for the behaviour, `.acceptance/gates-4.log` for the gates); the review-fix run is 5806/5806 and 711/711 (`.acceptance/test-full-review-fixes.log`, `.acceptance/gates-review-fixes.log`)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
