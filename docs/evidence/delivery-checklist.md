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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this change adds no gesture and crosses no player boundary; the input sequence is the console's own (`/` opens it, the line is typed, Enter runs it — nothing is held, so no window can be closed mid-input), and the native counterpart it mirrors is Minecraft's Java Edition command template, fetched (`gamerule <rule name> [<value>]`, permission level 2, an omitted value queries, arguments are case-sensitive, snake-case rule names since 1.21.11) and quoted in `docs/backlog/review/gamerule-host-rule-command.md`; the FIVE deviations are recorded there with their reasons (a decimal weight instead of an integer, the host as level 2, refusals naming the accepted set because the console has no completion for these arguments, a surplus argument refused where the console's other verbs ignore it, and two three-way level rules where JE has boolean and integer); the user-visible semantics are the user's 2026-10-23 ruling, and no new key, window or wire member was added
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: every changed mechanism and its reader: `HostRuleCensus.Row.Name`/`Range` + `Find`/`Names` (read by `HostAdminCommands.GameRule`, `HostRuleValueText` and `OnlineUiAdminDrawer.BuildEditableNumberRule`, pinned by `HostRuleCensusTests`); `HostRuleValueText.Choices`/`TryParse`/`Describe` (read by the command and by `HostRulesConfigEditor.TrySet`, pinned by `HostRuleValueTextTests`); `IHostRulesEditor.TrySet(HostRuleId, canonical)` (written by the command, implemented by the plugin editor and by the test stub); the write is the same `ConfigEntry` the Admin page writes, so `BepInExOptionsMonitor` fires and `HostRulesAnnouncer` announces the set (decision 263; read by every member's `AnnouncedHostRules`); `OnlineUiAdminDrawer.BuildEditableNumberRule`'s slider bounds now read the census range; the config metadata (`AcceptableValueRange<double>` and the two `AcceptableValueList<string>` sets) is bound from the same census rows and text forms, so a canonical write from the console cannot be clamped and the accepted sets cannot drift from the verb; the deleted `/hostrules`, `HostRulesJsonParser`/`HostRulesJsonApplier` and the `KeepSkills` writer have no live reader (repo-wide grep)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the host-rule writer family walked in one pass — the Admin page's rows (the weight slider takes its bounds from the census; its label literal stays the localisation census's read), the console verb (the JSON dialect deleted, the new verb in its group), the write seam (keyed by `HostRuleId`; every id has an entry and the default arm names the rule instead of writing a neighbour), the announcement (unchanged; it still fires from the config entry's change), the config text forms (comments re-pointed at `/gamerule`; the `Json` argument kind survives for mod commands with its suggestion provider and its test), the comments that named `/hostrules` re-pointed (`CheatVerbGate`, `CheatAdminCommands`, `AcceptanceCommands`, `HostRulesAnnouncer`, `CommandConsoleService`), and the family's documents re-pointed (configuration en/zh, mod-api en/zh, the console selfcheck, decision 269), including the live tickets that still named the deleted verb (`review/cheat-permission-and-op-commands.md`, `review/mod-native-binding-handshake-parity.md`); the adjacent pre-existing config-clamp defect is filed as `todo/config-clamp-silently-widens-rule-levels.md`, the stale `docs/api/` references in live tickets as `todo/stale-api-doc-paths-in-live-tickets.md`, and the console's green refusal line as `todo/console-command-refusals-render-green.md`
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `HostRuleCensusTests` pins the names (lowercase snake, unique, exact case, sorted list) and that only the numeric rule declares a range; `HostRuleValueTextTests` pins each kind's accepted and refused values (case-sensitivity, the named refusal, the numeric range and its boundaries, the canonical form) and that `Describe` prints a value the parser accepts back; `CommandConsoleCommandTests` pins the verb's answers (query, set by id through the editor, the canonical number, an unknown rule listing the names, an invalid value naming the choices, out of range, usage on a missing or surplus argument, the editor's refusal, the host-only gate); the announcement path is covered by `HostRuleReplicationTests.AHostFlippingARule_ReachesTheMemberAtThatMoment` and `BepInExOptionsMonitorTests` (the entry→monitor edge), plus the acceptance row that reads the member's announced value; the plugin editor's canonical re-parse has no unit test (the plugin assembly is not in the test project) and is named as a limit
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the full suite green (6300 behaviour cases and 777 gate cases in `.acceptance/full-gamerule6.log`). L1 is the machine reading on the deployed artifact: the acceptance batch drives `/gamerule` from the host's console (query, set, an invalid value, an unknown rule, the member's host-only refusal, the numeric range, the restore) and reads the member's announced set with the existing `host-rules-read` recipe; the same batch re-reads `give-gesture-own-ui-and-center-use` row 7 now that a driveable rule switch exists. L2 is the host log's `[Command] /gamerule …` line for each set and refusal invocation (the query path logs at Debug and is read from the console answer instead)
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the design is the user's 2026-10-23 ruling (a host rule is changed by a command shaped like Minecraft's `/gamerule`, host-only, omitting the value queries, lowercase-underscore rule names, an invalid value lists the options by name); this change adds only the five deviations recorded in the ticket; no new input, window or wire member; the work item and its order came from the handoff, not from a question
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `HostRuleValueText` 114 lines, `HostRuleCensus` 107, `HostAdminCommands` 147, `HostRulesConfigEditor` 170, `OnlineUiAdminDrawer` 169, `CommandConsoleService` 589, all under 600; no new mutable state (the census rows, the choice lists and the text maps are immutable; the console group holds only injected interfaces); dead mechanisms deleted in the same change (`/hostrules`, `HostRulesJsonParser` 224 lines, `HostRulesJsonApplier` 35, their eleven tests, and the `KeepSkills` writer only the JSON dialect could reach); one source per fact: the rule names and range in the census, the value spellings in the two config text forms, the announcement in the config entry, and the config's acceptable values bound from the same rows
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors (`.acceptance/build-gamerule-final.log`, including the plugin); `dotnet format CasualtiesUnknownOnline.slnx` exit 0; the final full run after the second review's fixes (`.acceptance/full-gamerule6.log`) read behaviour 6300/6300 and gates 777/777 green
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
