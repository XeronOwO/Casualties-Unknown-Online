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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no entry, gesture or limb source changed — the removal half lands on the body of the target the entry already names (the native WoundView special branch decision 246 maps), the removed item still goes to the operator, and the design line the receiving half implements is the family's own frozen wording ("Target's own local body must be instructed to remove the native component and does not auto-pick the item", `review/remote-medical-stage-3-other-actions.md`); the one behaviour this cycle ADDS on a user-reachable surface — the panel drawing a teammate's splint/tourniquet icon from the state — was ruled by the user in this session as a defect to fix rather than a trade-off to choose
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the two tickets' own sections name, per mechanism, the code and the reading — the two codec halves, the `RemoteMedicalDisplayBody` marker, the icon projection and the driver rework each carry their consumer (`CharacterDataSync.ApplyHealState`/`CharacterRestoreApplier`, `RemoteMedicalCoordinator`, the native `WoundView.AddImageToLimb`, the four recipes); the halves' BEHAVIOUR is explicitly marked as runtime-only, because the behavioural project excludes the Game Adapter from compile, and the game code cited is stable-line `reversing/` (`SplintLimb.cs:15`, `TourniquetScript.cs:21`, `WoundView.cs:396`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: all THREE component types are covered on both halves (capture, removal, inert creation), every limb-row builder that can reach the authoritative half was audited for a complete component list (`CharacterDataCapture`, `PlayerCharacterAccess.CloneLimb`, `PlayerInteractionKernelCodec`, `KernelLimbWireMapper`, `EnemyCombatKernelCodec`, `TraderRecruitPolicy`), the panel's own `TryGetComponent<ChilledLimb>` read and the tourniquet's lower-limb block were walked, and the driver family was audited as a family: the hand-built focus, the member probe and both open recipes were fixed together, and the sweep found the probe bug (a FIELD read as a property) that had hidden the real cause behind a workaround
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: both tickets' own tables and the new gate's row in `docs/evidence/normative-gates.md`; the defect was seen RED before the fix on the unfixed build — the one-client probe applied the authority's own removal shape through `CharacterDataSync.ApplyHealState` and read `componentsAfter=SplintLimb` beside `splintedAfter=false` with `messageComponents=0` (`.acceptance/20261011-c/red-message-clear-limb3.json`) — and two mutations of the new gate were observed red afterwards: `DestroyImmediate` → `Destroy` fails `...TheAuthoritativeApply_RemovesExactlyWhatItCaptures_AndNeverRunsTheGamesTeardown` ("Sub-string not found: DestroyImmediate"), and one `enabled = false;` flipped to `true` fails `...TheDisplayApply_RefusesABodyCuoDoesNotOwn_AndKeepsEveryInstanceInert` ("Expected: 3, Actual: 2")
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: three layers with the reach of each named — the one-client probe `tools/acceptance/recipes/limb-component-apply.cs` reads the RECEIVING half through the production call and separates the flag from the component; the source gate plus the reflective surface case pin what L0 cannot reach (the adapter is excluded from the behavioural project's compile, and the code under it is Unity's `AddComponent`/`Destroy`); the three-client rows judge what neither can — the race against the target's own 1 Hz report (removal rows 1-2), the item count (row 3), the second-removal refusal (row 4), the display panel's zero exceptions and the icon it must still draw (display rows 1-5, the icon row read from a captured frame per the run's visual-row rule), and the drive's own shape (display row 6)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff that opened this session names the work item AND its order ("first thing next session: the Critical removal ticket … it has to be designed together with the display ticket … then re-run row 4"), both tickets carry the frozen family design they implement, and the single behaviour this cycle adds was ruled by the user in-session (a missing teammate splint icon in the panel is a defect, not a trade-off) instead of being parked in a Limits section
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet test CasualtiesUnknownOnline.slnx` green — behaviour 4931/4931, gates 600/600 (was 595; the new `LimbComponentApplyGateTests` adds five cases, and the red the independent review found in this change — a backlog index row over the 160-character budget — is fixed here: a row is a pointer, not a summary)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no touched class approaches the gate — the largest touched are `CharacterDataSync` 553 (one call site changed in it), `RemoteMedicalCoordinator` 332, `LimbComponentStateCodec` 287, and the two new files are 138 (`RemoteMedicalDisplayIcons`) and 34 (`RemoteMedicalDisplayBody`); one top-level type per file, each new type single-purpose; the only new state is the icon registry and it is bounded by the focus's own close; nothing became dead — the ambiguous `Apply` was REMOVED rather than kept as an overload beside the two named halves, and the gate pins that no unnamed entry point returns
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
