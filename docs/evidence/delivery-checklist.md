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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the entry is the drag gesture and the label names the SAME target the release resolves - `CrossPlayerDragUse.ResolveTargetSteamId` was extracted so both read one walk rather than two. The native counterpart is the inventory use (`useAction`), whose target is that nearest in-world remote inside `OverlapRadius`
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: every added mechanism names its consumer: the tracker is read by `OnlineUiHost.Draw` through `IDragUseCueState`, the patch port is read by `PlayerCameraDragUseCuePatch`, and the overlay is called from the frame pass. The gates prove the wiring rather than the comment - `AdapterCapabilityPortShapeTests` (port shape, aggregate, single registration) and `AdapterCapabilityCatalogTests` (the patch is claimed by exactly one capability) both went RED before the rows were added
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is what the gesture ANNOUNCES, and the audit produced the three facts the ticket needed: the per-frame hook is `HandleWhileDragging` and the cue gets its own patch class there; the name is resolved on the presentation side; and the target must be the release's own resolution. Still recorded as limits: the sound, the refusal reaching the operator, whether the naming rule spreads to the medical and give gestures, and every acceptance row
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: six decision cases (eligible+target -> named; refused item -> nothing; no target -> nothing; zero id -> nothing; nothing dragged -> nothing; no session -> nothing). The wiring is proven by the shape gates, which is the strongest evidence available without a session: they name the port, the aggregate, the single registration and the capability that claims the patch
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the decision is a pure function; the wiring is pinned by two gate families that both failed before their rows were added, which is a real red rather than a restatement. What still needs the game: rows 1-7, and row 5 (two players standing on each other) is the case the naming rule exists for. The label has no runtime reading yet
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the requirement is the user's ruling of 2026-10-06 - the cue must NAME the player - and this change implements it and nothing beyond it. The items the ticket leaves to the user (which sound, whether the refusal echoes, how far the naming rule spreads) are untouched and recorded
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: four new types (the cue, its policy, the patch port, the tracker), one new patch class and one new overlay; `GameAdapter` grew past the aggregate limit and its record moved from 613 to 617 lines in `docs/architecture-debt.json` in the same change. `dotnet format` clean. The structural decision worth keeping: the cue is its own patch class on a method that already has one, and its own IMGUI surface beside the gate panel
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` 0 warnings / 0 errors; `dotnet format` exit 0; `dotnet test ... --filter "FullyQualifiedName!~DeliveryChecklist"` green at **5556/5556** behaviour cases and **663/663** gates, after three gate families had gone red on the new mechanisms and one renamed test was re-pointed in a page that cited it
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
