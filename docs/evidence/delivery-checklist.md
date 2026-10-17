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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: `/data` and `/give` are CUO's own console surfaces with no native call site; the change implements the user's decision 264 and the handoff order (`docs/decisions/active.md` 264), and the give destination ladder remains the next unit, not a limit recorded as done
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the seven-row inventory in `docs/backlog/review/snbt-state-language.md` (parser/writer, paths, language port, local write, grant state, completion, wire state), with the adapter's game facts cited from `WaterContainerItem` (capacity, UpdateCondition) and `Item` (`Stats` reads `Item.GetItem(id)`, so the prefab path needs no Awake)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: both verbs moved onto the one language, `Contents` is refused by name rather than half-addressed, the old token grammar and its tests are deleted from `src/` and `tests/`, and the protocol references, member-report census, sync-coverage I10/I11 and both consumer tickets were updated in the same change
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/backlog/review/snbt-state-language.md`'s twelve rows, whose named tests exist and pass (`SnbtParserTests`, `SnbtNumberTests`, `NbtPathTests`, `ItemDataVerbParserTests`, `ItemStateCompletionTests`, `ItemDataRendererTests`, the migrated console suites, and the new `CommandConsoleItemDataPathTests`)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the text/paths are unit-tested, the command wiring runs through the real console against the fake scene, and the adapter's own `ItemStateDocument` (prefab capacity, component legality, condition-from-fill) is the acceptance batch's evidence: give rows 2/3 and `/data modify … Liquids[0].Amount` are the run's proof
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: decision 264 (`docs/decisions/active.md`) rules the syntax and orders this ticket, and the handoff names the SNBT subsystem as the next unit
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the Runtime/Data and GameAdapter/schema types were split to one top-level type per file at the shape gate; `ItemDataWriteCommands`/`ItemDataCommands`/`ItemDataVerbParser`/`ItemStateCompletion` are each under 600; dead `NumberValue` and the request's tree field are deleted; the independent review's findings were fixed in this same commit
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` exit 0; `dotnet format` exit 0; `.acceptance/test-snbt.log` records the behavioural suite 5735/5735 and the normative gates 693/693 on the frozen tree
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
