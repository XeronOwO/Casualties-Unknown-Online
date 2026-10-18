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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle mirrors no new CUO gesture; the native call sites it reuses are the five vital stages and Limb.Update, named in the ticket's suppression table, and the injury event's counterpart is the client's own local damage; the user ruled the three functional forks (injury event, item 4 in-unit, minimal reconciliation) on 2026-10-18
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's design/review sections name every mechanism with its decompiled source; the host pass, seeding, authority round trip, RNG/camera preservation, injury event and quarantine are covered by source pins and pure tests; the actual patch installation and suppression on a live client are explicitly marked unverified (two-client acceptance owed)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the suppression family covers string AND AudioClip sounds, the clone's post-factory LiquidAffect, the Start droppings coroutine, the random/terminal quarantine, the injury channel and the host's authority/report/refusal/advance gates; every capture/apply path for the new TotalBleedSpeed was checked (Mapster capture, restore, kernel rebuild and revive placeholders named in the limits)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's independent-review section lists B1, B2 and M1–M6 with the code they cite and the fix each received, plus the recorded residuals
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: source pins with mutation negative samples (`HostVitalsSuppressionPinTests`), pure rules (`HostVitalsPassTests`, `ContinuousVitalsTests`, `CharacterDataFiniteTests`, `PlayerInjuryRuleTests`, `VitalsReconciliationTests`), real-store rows (`PlayerVitalsOwnershipTests`) and `NetPacketTests` round trips; the live proof is the ticket's acceptance rows (owed)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the session handoff ordered this unit and the user answered the review's three scope questions on 2026-10-18 (injury event, item 4 in-unit, minimal reconciliation)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the retired stage-1 model/tuning files are deleted; `CharacterDataSync` (659) and `CharacterDataStore` (626) are recorded in `docs/architecture-debt.json` with the planned split, every other touched type is under 600, and no new state bool exceeds the ceiling
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` exit 0 (0 warnings); `dotnet format CasualtiesUnknownOnline.slnx` exit 0; the final unfiltered `dotnet test CasualtiesUnknownOnline.slnx` is 5936/5936 behaviour tests and 741/741 normative gates green (`.agent-local/test-final4.log`)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
