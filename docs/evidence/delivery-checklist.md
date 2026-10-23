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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this change adds no gesture, window or key; its input is the console's own typing/Tab and the always-visible suggestion list (nothing is held, so no window can close mid-input), and the behaviour it mirrors is Minecraft's completion, where a caller without cheat permission cannot complete the command's word — the user's 2026-10-24 ruling recorded in `done/console-completion-hides-what-you-cannot-run.md`; the only user-visible change (hidden word + role-aware rule/value candidates) is that ruling, not a limit, and no fallback supplies a command or value the caller cannot run
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: `ConsoleCommandGate` (declared on `[ConsoleCommand]`, carried by `CommandDefinition`/`CommandSpec`, read by `ConsoleCommandAuthority`, pinned by `CommandConsolePermissionCompletionTests`); `ConsoleCommandAuthority` (read by `CommandConsoleCompletion` and by `Help`/`HelpText`); `CheatVerbGate.Judge`/`MayRun`/`TryRefuseQuiet`/`TryRefuse` (one decision; the quiet form is what the filter asks, the refusing form keeps its log line, both pinned — `CommandConsoleMemberGameRuleTests` for the handler half, `CommandConsolePermissionCompletionTests` for the quiet half); `CommandConsoleCompletion` (read by `ICommandCompletionSource`/`ICommandArgumentSuggestions`, driven by `ConsoleInputSession.LiveSuggestions`); `HostRuleCompletion` (reads `HostRuleCensus` + `HostRuleValueText.Choices`, pinned by the value-list and parser round-trip cases); `ItemDataVerbCompletion` + `ItemDataVerbParser.IsWriteVerb` (the `/data` split, pinned by the data case); the committed `console-suggest` recipe (driven by the batch, passed by `AcceptanceDriverGateTests`); nothing touched is unverified
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: every authority command walked in one change — the host-only verbs (`/kick`, `/ban`, `/unban`, `/op`, `/deop`) and the cheat verbs (`/give`, `/summon`, `/heal`, `/gamerule`, `/traderreport`) are classified `Cheat` or filtered by permission at the declaration; `/data` keeps its word with the read verb open and the write verbs + their SNBT state gated by `IsWriteVerb`; `/help`'s list, per-command refusal and its own `CommandName` completion use the same predicate; a hidden command's argument completions are withheld while the typed line still reaches the handler; a mod command applies only its declared permission (recorded limit); the records are re-pointed (decision 269's completion clause, both `/gamerule` tickets' "no completion" limits, the console selfcheck's mechanism/self-check/structure rows, README and the document census)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `CommandConsolePermissionCompletionTests` (17 cases: no authority word for a plain member with positive controls; the host's and granted member's sets; the switch closing and the roster not naming the caller each dropping the member's completions; `/help`'s list, both refusal grounds and its own completion; the 16 rule names in census order with their values in force; each rule's exact value list including the booleans; every offered value accepted by the handler's parser; `/data`'s read/write halves and the write-state SNBT lock; the public command-metadata projection filtered by the same authority; no log from a suggestion; a hidden command typed directly still refused) and the full behaviour suite (`.acceptance/20261024-c/full-behaviour-3.log`); the new committed recipe is covered by the driver gate
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the full suite (behaviour cases in `.acceptance/20261024-c/full-behaviour-3.log`; the gate cases in the final full run after this checklist is complete). L1 is the three-client batch (`20261024-b` prelude, copied into `20261024-c`): per role the batch asks the deployed client's own source through the committed `console-suggest` recipe — `trail=space` for the fresh-argument inputs (`/gamerule `, `/gamerule remote_backpack_access `, `/gamerule native_binding_parity `, `/gamerule piggyback_weight_multiplier `, `/data `) and `trail=none` for `/ga`, `/k`, `/gi` — and reads `/help`, `/help gamerule` and `/help kick` from the console, then ENTERS every offered `/gamerule` value on the host (row 4), flips the switch off and revokes the roster (row 6), and captures one window frame of the open suggestion list (the visual row). L2 is the absence of per-keystroke `[Cheat]` lines in the client logs while the recipe runs, beside the gate's own refusal line when a hidden verb is typed
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the design is the user's 2026-10-24 ruling (a caller without cheat permission cannot complete a cheat command's word; with it, `/gamerule` completes its rule names and values), the ticket and its order came from the handoff, and the implementer's choices (the gate declared on the command, one authority predicate, the completion extracted to its own class, `/data` verb-word completion added with the write half gated) are recorded in `done/console-completion-hides-what-you-cannot-run.md`'s *Implemented* section rather than asked back
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `CommandConsoleService` 507 lines (back under the cap after the split), `CommandConsoleCompletion` 210, `ConsoleCommandAuthority` 51, `HostRuleCompletion` 88, `ItemDataVerbCompletion` 63, `CheatVerbGate` 158, the test class 359, the recipe 37, all ≤600; no new mutable state (the authority and the gate are stateless; the completion holds injected immutably-read sources); the service's completion helpers moved out rather than duplicated, and the moved `ConsoleCommandRegistry.ToSpecs` was DELETED in the same round rather than left dead; one source per fact — the gate classification at the verb, one `Judge` for both gate forms, the census/grammar for the rule vocabulary, `IsWriteVerb` reading the dispatch's own verb constants for the `/data` split
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors including the plugin (`.acceptance/20261024-c/build-final.log`); `dotnet format CasualtiesUnknownOnline.slnx` exit 0 (`.acceptance/20261024-c/format2.log`); the final full run after the review's fixes and after this checklist was completed is `.acceptance/20261024-c/final-full.log` — behaviour 6337/6337 and gates 777/777 green
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
