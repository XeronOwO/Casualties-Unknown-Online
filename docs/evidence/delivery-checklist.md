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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new gesture or UI: the change repairs two existing native-driven facts — the host's bind against the game's generation window (`WorldGeneration.generatingWorld`, `reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:1537` set true, `:3630` false) and the runtime-spawn classification at `BuildingEntity.Start` (`BuildingEntity.cs:35`) — plus the layer-reset couple; no CUO action is re-routed between entries, and each fix has exactly one correct behaviour (a row bound mid-generation is the row the entry reset must drop; a generation animal is never a runtime spawn), so no user-visible choice was introduced
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's `## Fix cycle after batch 20261021-f` and the selfcheck §5 name each touched mechanism with its reader: `WorldIsSettled`/`CanPublishRoster` → `UpdateHost` → `CaptureHostEnemies`; `EnemyRosterPublisher.IsReady` → `BroadcastEnemyState`; `EnemyAnimalScan.Union` → capture/freeze/adoption; `IsRuntimeSpawn(sessionActive, edgeObserved, existedAtEdge)` → `OnAnimalInstantiated` → `EntitySpawnSync`; `LayerScopedTableReset` per-table gate → the members' `EnemyRosterReset`; `DestroyCopy` → every staged destroy; the batch's own log lines are the runtime evidence (`host enemy baseline established over 0 animals`, `…83 enemy row(s)`, `291/873 runtime spawns`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family of "the generating flag read at Start" was walked to both its members — the enemy classification AND the entity-creation channel (`EntitySpawnSync.OnEntityInstantiated`, which now takes the enemy domain's verdict; its non-animal half is named as a limit, not silently skipped); the family of "the tracked animal set" was walked to every consumer (host capture, freeze pass, adoption candidates, live pass) and the union is one implementation; every staged destroy (stale binding, surplus, host-ordered removal) goes through the one deactivating `DestroyCopy`; the reset couple is now per-table; the independent review's three majors are the family audit's own findings, all fixed here
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/enemies/enemy-roster-adoption-selfcheck.md` §5 (the three fixes, the four REDs, the review dispositions) and §6 (the limits, each with its reader)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is `EnemySyncServiceTests.RosterUnstable_HoldsThePeriodicStream_NotJustTheSnapshots`, `LayerBoundaryKernelResetTests.LayerReset_AnEmptyEnemyTable_CommitsNoEnemyReset` (plus its repaired tombstone case), `EnemyRosterAdoptionTests.IsRuntimeSpawn_OnlyAfterTheGenerationEdgeInASession` and the source-shape gate; L1 is the batch rerun: the dense entry reads `host enemy baseline established over N animals`, no member `[Enemy] host reset the enemy table for a new layer`, `snapshot sent … N enemies, 0 runtime spawns`, no `[EntitySpawn] reporting … (animal`, and both members `aligned=True` with 0 built; the descent, drift, mid-run, runtime and bite rows re-drive as before
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff names this ticket and its `### Next cycle (in order)` as the round's first work item, in that order; the three fixes and the review's findings are defects with one correct behaviour each (no design trade-off), and the 2026-10-21 rebuild decisions still stand
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `EnemySyncCoordinator` 472, `EnemyRosterBinder` 386, `EnemyAnimalScan` 52 (new, the one union both roles read), `GameAdapterBridge` 597 (the first draft crossed the 600 cap at 605 and was compacted rather than recorded as debt), `EntitySpawnSync` 525, `LayerScopedTableReset` 98, `EnemyRosterPublisher` 134, `EnemySyncService` 575; the coordinator holds four bool state fields (edge observed, mapping established, guest frozen, adoption pending ≤ 5); `IsGenerationAnimal` was folded into `OnAnimalInstantiated`'s return instead of kept as a second accessor, and nothing dead was left
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet build CasualtiesUnknownOnline.slnx` 0 errors 0 warnings; behaviour suite 6142/6142 (`full-20261021-g2.log`); normative gates 754/754 (`gates-20261021-g.log`), the checklist gate included; the final run on this frozen tree re-confirms both counts
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
