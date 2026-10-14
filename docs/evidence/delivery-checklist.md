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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the cycle adds NO gesture and no wire: it changes how CUO's own window lays out the controls it already had (`OnlineUiControlSizing.EffectiveWidth`, `OnlineUiRowGeometry.LayOutRow`), so the entry mapping is the one the layout ticket already records and nothing needs information its gesture cannot carry; the two user-visible behaviour changes it does make — the colour picker's blocks now 88 units wide with the colour as a fill, and a row no longer drawing the game's grey frame — are both the execution of the user's own words on 2026-10-14 ("only the button border colour, the middle transparent") and 2026-10-15 ("the light grey box in the background is still there, can it go"), not limits standing in for a decision
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: no mechanism is ADDED; three are changed and each was read live before it was touched (`.acceptance/20261015-a/b1-admin-dump.txt`, `b3-prefs-dump.txt`): the width rule (its old reading of the element root produced 7282→30537 units of growth), the element's own declared width (never written: 88 declared, 18 rendered), and the row's own prefab art (the grey frame, `Image[sprite=uiBlockNano color=0.37,0.37,0.37]`, read on the row root). The new `OnlineUiControlSizing.AuthoredChildWidth` names its consumer (`OnlineUiControlView` passes it into the rule), `OnlineUiSurfaceParts` names its four consumers (the window, the two panels, the overlay), and the one thing the cycle could NOT verify from the tree — whether the authored checkbox size reads as the game's own checkbox at the player's scale — is named as a Limit on the ticket rather than assumed
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "every element whose width the layout gives it, and every surface that builds a part", and each member was walked: Toggle and Slider (the runaway branch) take the control's authored width; Button and ColorSwatch (which had no width write at all) take the model's floor, and the tab row was routed through the same rule once the review showed its declared `TabWidth` floor was unread; TextField and Dropdown keep the rule they had; the placeholder of a kind whose game prefab is missing takes the declared width too (recorded in `OnlineUiRowGeometry.LayOutRow`'s doc); `PrefabWidth(_rect)` — the same reading spelled through a helper — is now rejected by `WidthsFollowTheContent`; and the duplicated construction helpers the window and the two panels each carried were moved into `OnlineUiSurfaceParts` with a pin that refuses a private copy growing back, which is the sibling defect the same sweep found
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket carries it as its own section — *What landed — the colour block's fill and width* and *Fourth pass (2026-10-15)* walk the defect, the mechanism, the fix and the pins, one claim per bullet, each with the reading or the capture that backs it; the pins themselves are the executable table (7 new or re-anchored facts with 14 mutation rows, all green)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the runtime proof is a live reading of CUO's own object tree through the in-process evaluator (`.acceptance/20261015-a/ui-dump.cs`), taken before and after: the row's `LU` preferred width, the label's rect, the control's `LayoutElement`, the block's `Swatch Fill` child with its colour and its refused raycast, and the row root's `Image` alpha — the same numbers the tickets quote; the frames the run captured are the visual half (`b6-host-admin-en-top.png`, `a3-host-admin-zh.png`, `a6-host-prefs-zh.png` plus the `a7-colour-zoom.png` zoom), and the family check is the four other pages' captures. Out of reach and named as the acceptance batch's work: the panels' and the launcher's rendering after the width change, which no row of these tickets covers
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the work order is the handoff's own first next step ("fix the two UI defects first: user-visible, confirmed on the real machine, small change surface"), the two tickets' rows were written by the acceptance run of batch `20261014-a`, and the grey row frame is the user's direct request of 2026-10-15; the design calls inside the fix — where the width rule lives, that the control's authored size is read once at build, and how the fill is laid inside the sprite — are architecture and mechanism, which the rules put on the agent
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` run after the last C# edit (its only output is the workspace-load warning, the tree carries no formatting diff); `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors; `dotnet test CasualtiesUnknownOnline.slnx` green at **5272/5272** behaviour cases (net48) and **660/660** gates at the commit `5279880b`. Focused runs along the way: the pin classes red first (8 failures, `.acceptance/20261015-a/red-pins.txt`), then 428/428 on `FullyQualifiedName~OnlineUi`; the gates themselves caught three real defects mid-cycle (the 600-line ceiling twice and the two stale-reference gates), each fixed before the commit rather than waived
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured as whole-file lines (the way `SourceShapeGateTests` measures a single-type file): `OnlineUiControlView.cs` 600 → **576** (the width rule, the row-geometry entry point and the shared typography moved out), `OnlineUiWindowView.cs` 598 → **564** (the construction helpers and the tab row's new line), `OnlineUiPanelView.cs` 558 → **518**, `OnlineUiRowGeometry.cs` 488 → **536**, `OnlineUiControlSizing.cs` 106 → **149**; new single-type files `OnlineUiControlTypography.cs` **32**, `OnlineUiSurfaceParts.cs` **62**. No new boolean state field (the flags the change adds are none; `_controlAuthoredWidth` is a readonly float read once). Deleted in the same round: `OnlineUiControlSizing.EffectiveWidth`'s dead two-argument overload and `OnlineUiControlView.CaptionWidth`, the window's and the panel's three duplicated private helpers, and `OnlineUiControlFactory.SwatchImageOn` — nothing was left beside its replacement
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
