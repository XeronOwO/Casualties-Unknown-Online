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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the ticket's *Entry mapping* names the game console's own `heal` command (`ConsoleScript.RegisterAllCommands`), the limb restore / hollow clear as CUO's own call sites, and the trader revive's `PrepareRevive` baseline; the revive-location fork was asked with the question tool and answered by the user (in place), and Java Edition's lack of a vanilla `/heal` was fetched from `minecraft.wiki/w/Commands` rather than remembered
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the ticket's *What landed* names each mechanism and its owner (the field table in `HealResetState`, the live-body applier, the request/instruction/result wire, the character-snapshot carrier); the native heal, KrokMP's `ResetHealth`, the medication components and `PrepareRevive` are cited from `reversing/`, and the live-body application is marked acceptance-only (a test host has no game `Body`)
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the cheat family was read end to end — `/data`'s write verbs, `/give`, `/summon` and now `/heal` share `CheatVerbGate` + the host's `IMemberCommandRules` check; `review/cheat-permission-and-op-commands.md`'s `/heal` exclusion was corrected in the same change (its family list, its self-check row 12 and its "What is asked" row 5), `CheatPermissions`' doc and the `NetMsg.CheatPermissions` comment now name `/heal`, and the wire is declared in sync-coverage row I13, the member-report census and both protocol reference blocks
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's fourteen-row *Self-check table* names a live test for thirteen rows and the acceptance row for the adapter half; the field table's 79 reference values, the every-write wiring and the removal direction are `HealResetStateTests`', the limb capture is the completeness gate's + the mapper regression's, and the control-plane rows are the two console test classes
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the reset's control plane is proven by 26 focused cases in the three reset classes (11 host + 9 member + 6 table) plus the capture gate and the mapper regression; the live-body half (the field writes, limb GameObjects, components and overlay) is the acceptance's, with rows 1-6 on the deployed artifact and the adapter's own `[HealReset] local body reset: …` line as the trace — and `health-read`/`limb-read` were extended so row 1 can read the host's saved health and the limb topology the review's blocker corrupted
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket *The design the user confirmed* is the user's 2026-10-18 correction, and the only open fork (where a revived body comes back) was asked with the question tool and answered the same day — in place; no work-item order question was reopened
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `CommandConsoleService` 583 (the old `/heal` method and its selector helper deleted), `PlayerHealResetService` 317, `HealResetState` 311, `LocalBodyReset` 126, `HealResetCommands` 61, `CharacterDataCapture` 132 (the one limb capture); `GameAdapter` 622 is the one over-600 type and is the recorded debt entry, raised 615→622 for the new port rather than split; the one-top-level-type-per-file gate stays green (each new type has its own file) and the new test classes were split under the 40-case gate (`HostToGuestInteractionDirectionTests`)
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build` exit 0 (0 warnings); the behavioural suite 5835/5835 and the normative gates 740/740 (`.agent-local/full-test5.log` and `.agent-local/gates8.log`); `dotnet format` verified no changes in the touched files (the solution-wide verify reports only the generated `obj/.../MyPluginInfo.cs` files, which are untracked build output)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
