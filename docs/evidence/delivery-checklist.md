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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no new gesture; the change fixes an existing one (`/` and Enter, one opener) by asking the GAME's own keyboard ownership — its in-world console's `active` flag (`ConsoleScript.Update` executes Enter whenever active, no focus test) and an active selected text field — instead of the stale event-system selection; the input sequence is unchanged (press the key with no CUO surface up) and the ticket's 2026-10-07 design is the one implemented
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the console's Enter path and its `active` gate are read from `reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs` (`Update`: `if (!this.active) return;` then `Input.GetKeyDown(13)`; `ToggleActiveState` selects the field on open); the stale selection is read from `TMP_InputField`'s `OnDisable` → `DeactivateInputField` → `ReleaseSelection` (no `SetSelectedGameObject(null)`); the defect and the fixed reading are read from the running clients (`.acceptance/20261024-c/enter-red2.log` red, the green log on the fixed artifact); the settings key-bind capture is read from `SettingsMenu.cs` (button-driven `WaitForKeyPress`) and filed as `todo/enter-opener-keybind-capture.md`
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: `IsTheGamesOwnTextInputFocused` has exactly one consumer (the shared `/`+Enter gate in `OnlineUiHost.Update`), and `currentSelectedGameObject` is read only in `OnlineUiKeyboardFacts`; the other input seams (`SetOnlineUiModal`, `SetOnlineUiEscapeSurfaceVisible`) write booleans and never read the selection; the game console, the crafting filter, the settings number fields and the recipe filter are the text-field family the reading covers, and the ONE surface it cannot see (the settings key-bind capture) is named in the guard's doc and filed as `todo/enter-opener-keybind-capture.md` rather than left implied
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: (1) the console fact — `ConsoleScript.instance.active` makes the guard true while the console is open regardless of selection (probe `game-console-focus`: open → `guard=true`); (2) the lifetime fact — the closed console's field is inactive in hierarchy and the guard is false (red log `guard=true` with `selectedActive=false`; green log `guard=false`); (3) the opener — Return after the game console opened/closed opens the CUO console in plain-chat mode (green frame) and opens nothing before the fix (red frame); (4) the capture gap — decompiled and filed, with the guard's doc naming it; (5) the recipes — `game-console-focus` / `key-tap` carried by the driver gate and observed in the client (`guardFound=true`, `vk=13 scanCode=28 posted=true`); full suite 6343/6343
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the full suite (no unit host can build the game console or event system, so the condition has no unit case; `dotnet test` passes 6343/6343); L1 is a two-client world with the host driven through the committed `window-key` declaration (`key-hold bind=console` toggles the game console, `key-tap vk=13` posts Return) and `game-console-focus` reading the guard; the RED is the same probe on the pre-fix artifact (`enter-red2`), the GREEN the same probe on the fixed artifact, each with a frame of the host's screen
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the 2026-10-07 user request froze the two-opener design (Enter opens plain chat, `/` command mode) and the batch `20261018-t` record froze the failing row's verdict; this cycle implements the ticket's OWN suggested fix ("check the selected object's active state") plus the reviewer's console-flag correction, and introduces no new user-visible choice — the one surface it cannot cover is filed, not silently limited
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `OnlineUiKeyboardFacts` 45 lines (was 22; one method renamed and one fact added, no state), `GameAdapter` 631 (unchanged in size, already recorded in `docs/architecture-debt.json`), the two recipes 47 and 33 lines; the old single-fact method is replaced rather than left beside the new one, so no dead reading remains; one source per fact — the console's own `active` flag, the event system's selection + active state, and the input components the reading names
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors (`.acceptance/20261024-c/enter-build2.log`); `dotnet format CasualtiesUnknownOnline.slnx` exit 0 (`.acceptance/20261024-c/enter-format2.log`); the final full run after this checklist was completed is `.acceptance/20261024-c/enter-final-full.log` — behaviour 6343/6343 and gates 777/777 green
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
