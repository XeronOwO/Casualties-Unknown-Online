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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this change adds no gesture and no window; its input is the console's own (`/gamerule <rule name> [<value>]`, typed and run with Enter — nothing is held), and the native counterpart it mirrors is Minecraft Java Edition's command template (permission level 2, an omitted value queries, case-sensitive snake-case names), fetched 2026-10-23 and quoted in `done/gamerule-host-rule-command.md`; the only new user-visible behaviour — a granted member's rule change answered where it typed the verb, with the member's own config never written — is the user's 2026-10-24 ruling ("就接入 op 体系"), recorded in `done/gamerule-joins-the-op-family.md`; no information the verb lacks is substituted by a fallback (the host re-parses the rule id and canonical value it is handed, and refuses what its census does not classify)
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: every touched mechanism and its reader: `HostRuleCommands` (the verb, read by the console registry; its grammar and gate order pinned by `CommandConsoleCommandTests`/`CommandConsoleMemberGameRuleTests`); `IHostRuleChangeControl.Change`/`HandleRequest`/`FireAnswered` (read by the command, the two handlers and `CommandConsoleService.OnGameRuleAnswered`); `HostRuleChangeService.Apply` (the write and the read-back `HostRuleValueText.Describe(HostRuleSet.From(IHostRules), row)`, pinned by the clamp case); `NetMsg.HostRuleChangeRequest`/`Result` + their handlers (read by `NetMessageRegistry`/`PacketDispatcher`, wired through `IHostRuleChangeHandlerContext`, direction rows in `GuestToHostDirectionTests`/`HostToGuestInteractionDirectionTests`); decision 263's change edge (unchanged; read by every member's `AnnouncedHostRules`, pinned by the next-pump case); the plugin `HostRulesConfigEditor.TrySet` (unchanged seam, now reached from the host's request path as well as its console); nothing touched is unverified
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the cheat family walked in one change: the console group (`/gamerule` moved out of `HostAdminCommands` into `HostRuleCommands`, whose gate order — parse, gate, then send/apply — is `/give`'s and `/summon`'s), the host's request path (switch, roster, census, grammar, write, read-back, the order the give/summon/heal services use), the answer shape (`Applied` + `Answer`, printed where the member typed the line), the announcement (the existing change edge, no second path), and the family's records re-pointed (`done/gamerule-host-rule-command.md`'s supersede note, `review/cheat-permission-and-op-commands.md`'s family list — `/gamerule` joined it and `/kick`/`/ban`/`/unban` stayed host-only — decision 269's amendment links, `todo/console-completion-hides-what-you-cannot-run.md`'s hidden-command list); the adjacent pre-existing defects stay their own tickets (`todo/config-clamp-silently-widens-rule-levels.md`, `todo/console-command-refusals-render-green.md` now also naming this verb's refusals)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `CommandConsoleMemberGameRuleTests` (19 cases: the member's write lands on the host and the host's sentence prints, including with the host out of world; the member's own editor is never called; the change edge reaches the member on the next pump; a member's query answers the announced value; the switch/roster/revoked/invalid/unknown refusals each assert NO request was sent; the host's own switch re-check; the swallowed-announcement host refusal and the host service; the editor's refusal; the clamp read-back answers the value in force; a non-canonical value and an unknown rule id sent straight to the host are refused by name; a forged answer from a non-host sender is dropped) and the host matrix in `CommandConsoleCommandTests` through a fake that writes the options monitor; the full behaviour suite is 6315/6315 (`.acceptance/20261024-b/full-behaviour-1.log`)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: L0 is the full suite (6315 behaviour cases, `.acceptance/20261024-b/full-behaviour-1.log`; the 777 gate cases run in the final full run after this checklist is complete). L1 is the three-client batch (`20261024-a` prelude; host, granted member, plain member): rows 1–7 of `done/gamerule-joins-the-op-family.md` are driven through `Invoke-Console` and the two `host-rules-read` recipes and judged from the batch's own answers. L2 is the host log's `[Rules] … set …` and `[Rules] request from … refused` lines plus the member's `[Rules] … asked the host …` line, and both clients' config files read after the run
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the design is the user's 2026-10-24 ruling ("就接入 op 体系": the switch + `/op` roster admit a member's rule change through a host round trip, and the member's own config is never written), the ticket and its order came from the handoff, and the implementer's remaining choices (own command group, census id on the wire, read-back through `IHostRules`, parse before the gate) are recorded in `done/gamerule-joins-the-op-family.md`'s *Implemented* section rather than asked back
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: measured — `HostRuleChangeService` 192 lines, `HostRuleCommands` 85, `CommandConsoleService` 599, `HostAdminCommands` 93, `CheatAdminCommands` 89, the two handlers 18/33, the two messages 38/27, `IHostRuleChangeControl` 44, all ≤600; no new mutable state (the service holds injected interfaces and one subscribed event; the console's field is its existing pattern); dead code removed in the same change — `/gamerule` left `HostAdminCommands`, taking that group's now-unused `IHostRules`/`IHostRulesEditor`/logger dependencies with it; one source per fact (census names/ranges, `HostRuleValueText` grammar, `IHostRules` read-back, the config entry's change edge)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings 0 errors including the plugin (`.acceptance/20261024-b/build-review3.log`); `dotnet format CasualtiesUnknownOnline.slnx` exit 0 (`.acceptance/20261024-b/format2.log`); the final full run after the review's fixes and after this checklist was completed is `.acceptance/20261024-b/final-full.log` — behaviour 6320/6320 and gates 777/777 green
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
