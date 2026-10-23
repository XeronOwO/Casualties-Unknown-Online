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

**Input rule (added 2026-10-22)**: the Entry mapping box also records the exact input sequence the
player performs for a new or changed player-facing gesture — which mouse button is held, which window is
open, what can still be clicked or pressed while it is held — and walks it through the game's own control
flow. A gesture the controls cannot perform is a design defect, not evidence; a call seam a probe can
drive is not evidence that a player can reach it. An "impossible" verdict lists EVERY entrance that opens
the window the gesture needs and the button each one takes (a conflict on one entrance is not the control
scheme), and an entrance behind a hotkey the player cannot be expected to know is not the primary entry:
the gesture opens what it needs from the act itself.

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the change reworks the give gesture's entry and release; the player's sequence is: open the own backpack (the native `toggleinventory` key, default Tab), hold the right button on an item (the native `HandleStartDragging` → `TryPickupFromUI`), press that same key once to close the own ring while the button is held (the native drag survives; the while-dragging body/limb overlap can force it open only over a real collider, and the remote clone has none), move into a teammate's 1.5-unit range so their backpack opens, then release on a slot (the native `TryPerformInventoryAction` counterpart → give) or on the centre (the native `TryPerformRadialAction` counterpart → use/wear of the displayed player); anywhere else keeps `TryPerformWorldActions`' drop. The centre names the displayed player from the view's focus, so no fallback supplies an identity; the entries are separated by WHERE the release lands, and the centre and slot bands are geometrically disjoint; the design is the user's 2026-10-23 ruling; closing the own bag is the same key that opened it, not a hotkey the player cannot know
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: every new/changed mechanism and its reader: `GiveGestureModePolicy.Decide` (pure, read by `GiveGestureModeTests`) and `.Read` (read by both patches; the four own panels and the remote view are pinned by `GiveGestureGateTests`); `DragUseCenter.IsUnderPointer` (read by `PlayerCameraDragUsePatch` and `DragUseCueTracker`; the native geometry — `inventoryUseLeniency`, `radialCircle` radius — and deliberately NOT the tag, because `AnchorRadialToFocus` disables the tagged Graphic while the view is open); `CrossPlayerDragUse.TryUseOnDisplayed` (read by `GameAdapterBridge.TryHandleDraggedItemUseOnRemote`, called by the release patch) with the displayed player from `RemoteBackpackView.FocusedSteamId` and the view-only refusal; `__state.Center` read by the finaliser to keep the ring open; the end-of-drag bookkeeping (`AutoOpenRemoteBackpackForDrag(null)`) in the own-UI context; `IsTargetInRange` read by `AutoOpenFor`; the native entry points quoted are `HandleDragging`/`HandleStartDragging`/`HandleWhileDragging`/`HandleReleaseDragging`, `TryPerformInventoryAction`, `TryPerformRadialAction` and `TryPerformWorldActions`
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the gesture family walked in one pass: the drag start (unchanged native), the auto-open (gated by the own-UI mode and reset at the drag's end), the release classification (own UI / world / displayed backpack), the centre use/wear with the view-only refusal and the kept-open ring, the cue (the centre; its port no longer carries casts), the proxy/take drag (`RemoteDragIntentWindow`, untouched — the take direction is not a local-item drag), the own-ring local centre (native, untouched), the remote medical family (its focus IS the native wound view, so the mode classifies it OwnUi; that family has its own patch and is untouched), the native drop (kept for the range-edge case), and the live docs re-pointed (architecture page, give ticket, two todo tickets)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `GiveGestureModeTests` (4 matrix cases: own UI, own panel over a displayed backpack, displayed backpack, world); `GiveGestureGateTests` re-based — the own-UI gate order in both patches, the drag-end reset, the centre release consumed and the ring kept open (`__instance.radialOpen = true`), the hidden-tag-free centre hit test, the displayed target and view-only rule, the four own panels, and the cue/release sharing the centre; the existing give/placement pins are unchanged; the pure decision is behaviour-tested, the Unity reads are shape-pinned (no camera in the test host)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the full suite green (6269 behaviour cases, 767 gate cases). L1 is the machine reading: the ten rows in `docs/backlog/todo/give-gesture-own-ui-and-center-use.md` were read by batch `20261023-f` (record `docs/evidence/acceptance/give-gesture-own-ui-and-center-use-20261023-f.md`) — rows 1, 2, 3, 5, 6, 8, 9 and 10 pass; row 4 fails on a pre-existing placement-resolver defect and row 7 could not be staged, so the ticket is rejected back to `todo/` for that fix
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's 2026-10-23 rulings (own UI self-only; the range only selects; the centre uses/wears; a use keeps the bag open; no hint; one press one meaning; the native force-open counts; the range radius unchanged) are the design; no question was re-asked; no new wire member and no new input were added
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — the three new types are small and single-purpose (`GiveGestureMode` 19, `GiveGestureModePolicy` 41, `DragUseCenter` 36); the touched classes stay under 600 (`CrossPlayerDragUse` 278, `PlayerCameraDragUsePatch` 212, `DragUseCueTracker` 61, `PlayerCameraDragGiveOpenPatch` 50); no new mutable state beyond the transient `ReleaseState.Center` (the same shape as `Give`); the dead cue-casts parameter was removed with its reader; the range keeps one consumer (`AutoOpenFor`) and the centre one hit test (release + cue)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors; `dotnet format CasualtiesUnknownOnline.slnx` exit 0; behaviour suite 6269/6269 and normative gates 767/767 green (`full.log` in the local artifact area), the re-based `GiveGestureGateTests` (with the three review-fix pins), the new `GiveGestureModeTests`, the backlog index/reference gates and the document census with the new ticket included
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
