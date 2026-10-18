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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle is presentation geometry, and its native counterpart is named — the game's own settings screen instantiates the same `Special/GameSettingDropdown` row and lets `TMP_Dropdown` build and place the list from the template inside it (`reversing/Assembly-CSharp/Assembly-CSharp/SettingsMenu.cs`, the `SettingDropdown` branch, `Utils.Create("Special/GameSettingDropdown", this.content)`), which is why CUO reuses the control instead of a parallel UI; no gesture needs information it cannot carry (no limb, identity or target is involved), no two entries can claim one item, and the one user-visible choice the review raised — a list with no room below opening above instead of off the canvas — is row 4's own expected behaviour ("every option can be clicked"), not a new choice
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: every touched mechanism is named with its owner and its evidence — the template's rect (`OnlineUiBoxGeometry.LayOutInterior`), its adoption, placement and lifetime (`OnlineUiDropdownPopup.Adopt`/`NormaliseAdopted`), the list's construction, clamp and flip (`reversing/Unity.TextMeshPro/Unity.TextMeshPro/TMP_Dropdown.cs`, `Show`/`SetupTemplate`), the side rule (`OnlineUiDropdownPlacement`), the drag rule (`OnlineUiWindowDrag`) and its application (`OnlineUiWindowDragHandler`); the runtime half is `w-popup2.json`, `w-popup-loglevel.json`, `w-drag.json` and the native probe `native-dropdown.json`, and the rendering half is the acceptance batch's rows 4 and 9
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: both shipped dropdowns were read live (language, 2 options, and log level, 7 options, both on Preferences), all four drag edges were driven (right/left/up/down), the two panels were checked for a popup layer and have none (kept as a recorded limit), and the game's own list internals — mask, `ScrollRect`, content, items — were re-read in the same probes; the old symmetric-Y clamp was deleted rather than kept beside the new rule
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's eight-row *Self-check: mechanism × change × evidence* table names a live case or a structural pin for every mechanism
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the reds were observed (the extracted first-cut drag rule failed 3 of 5 cases in `.acceptance/20261018-w/red-drag.log`, and the popup's defect is re-derived from the native probe `native-dropdown.json` against the batch's `a94` reading), the pins carry mutation proof (the popup's 8 rows, the drag's exact 190/16 values, and `OnlineUiWindowSurfacePinTests`' 2 rows), and the live proof is `w-popup2.json` (list under the field at 150×193.4), `w-popup-loglevel.json`, `w-after-select.png` (the option applied at once), `w-popup-frame.png` and `w-drag.json` (the title bar on the canvas at the top clamp)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the session handoff orders this ticket as the round's first work item; the design is the ticket's own frozen decisions (the popup layer, the engine-stacked shell, the one compact height) and the clamp's recorded intent ("a strip of it always stays on the canvas so it can be grabbed again"); the one behaviour the review raised as a trade-off — a list with no room below opening above instead of off the canvas — was resolved in the direction the row's own expected behaviour states ("every option can be clicked"), not as a new choice, and no open fork was reopened
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the touched types stay under 600 lines — `OnlineUiDropdownPopup` 354, `OnlineUiBoxGeometry` 316, `OnlineUiWindowView` 594, `OnlineUiWindowDragHandler` 53, `OnlineUiWindowDrag` 58, `OnlineUiDropdownPlacement` 41; no new state bool (the popup's logger is constructor-injected, the adoption table is the layer's own state), and the old symmetric-Y clamp was deleted rather than left beside the new rule
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` exit 0 (0 warnings); `dotnet format` exit 0; `dotnet test CasualtiesUnknownOnline.slnx` is 5861/5861 behavioural and 741/741 normative gates, all green (`.acceptance/20261018-w/full2.log`), after the focused 94-test run (`.acceptance/20261018-w/focused3.log`) and the red in `.acceptance/20261018-w/red-drag.log`
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
