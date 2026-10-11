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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle is the acceptance run of `mod-item-declaration-parity` and adds NO entry and no behaviour; the rows it judged are declared in `docs/evidence/acceptance/20261011-j-scope.md` BEFORE the session, they read a content-declaration surface (which values a mod may write into the game's own static item table) materialized locally on both peers, and no row needed an identity its gesture cannot carry — the one state the rows force is a body field (`body-force intelligence=0`), declared as a substitution with the read that shows it took
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the mechanisms this cycle touches are the acceptance channel's own — the run's fixture placement (`CasualtiesUnknownOnline.ModExample.dll` directly in the `BepInEx\plugins\` tree, the place the in-repo pinyin mod occupies; `deploy.ps1` does not deploy mods), the new read probe `tools/acceptance/recipes/mod-item-read.cs` (the game's own `Item.GlobalItems` and `PlayerCamera.ItemHoverDescription`), the extended `body-force` (`Body.skills.INT` + `Skills.UpdateExpBoundaries`, the game's own mindwipe sequence), and the two clients' logs read with `FileShare.ReadWrite`; each is cited in the record with the file it produced, and the one line that could not be shown pre-existing is named as such rather than assumed
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is the item declaration surface (the game's `ItemInfo` and its decay flags), and this run re-read it in the LIVE game on both peers rather than sampling: the same definition read out of each client's own `Item.GlobalItems`, the same gate answered by each client's own body, the same hover built by the game's own method, and the same item-provider lines censused on each side; the family's other half (all 32 public fields, all 4 decay flags) stays pinned by the behaviour suite the ticket landed, which the batch did not need to repeat
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: this cycle's table is the record's row table (`docs/evidence/acceptance/mod-item-declaration-parity-20261011-j.md`): five rows, each with its class, its verdict and the batch file that carries the reading, plus a *Limits* section naming what the run could not show (the positive-gate half of the tooltip, the unprovable pre-existence of the tooling's own exception, and the forced-write substitution); the ticket's own acceptance table carries the same verdicts and links the record
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: `docs/evidence/acceptance/20261011-j-scope.md` declares the two-client setup, the instruments, the probes, the rows and the substitutions BEFORE the session — including the design that makes the rows discriminating: the reading body is forced BELOW the inherited gate (INT 0) because the host's own INT 7 satisfies the old default too, and a vanilla item whose gate is 1 is read in the same state as a positive control; each row's evidence is a named file in the batch directory (`b1`…`b6`, `c1`…`c3`, `d1`/`d2` for the log censuses), and the record says which mechanism produced each
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the work order is the previous cycle's handoff (run the acceptance batch for the ticket it had just landed and deployed, the only step it left owed), and the standing rule that acceptance is the agent's duty rather than the user's (`docs/acceptance/AGENTS.md`) is the approval for opening it; the batch criteria the rules give — machine and workspace idle — were checked before the first client started (preflight and `session-environment.ps1 -Mode status`), and no work-item choice was re-asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0 after the last C# edit; `dotnet test CasualtiesUnknownOnline.slnx` green at 614 normative gates and 5064 behaviour cases with the checklist gate itself excluded only while its own boxes were still being ticked, and re-run INCLUDING it after this box was checked; this cycle's two reds were its own and were fixed rather than exempted — the new index row was 162 characters against the 160 budget, and extending `body-force` with the `intelligence` argument left `DriverToolTests.Recipe_RunsOneCommittedRecipeAsOneEval` supplying five arguments to a recipe that now declares six (the driver answered exit 64)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: every file this cycle touched was measured after the last edit — `tools/acceptance/recipes/mod-item-read.cs` 48 lines (new), `tools/acceptance/recipes/body-force.cs` 48, `docs/evidence/acceptance/mod-item-declaration-parity-20261011-j.md` 66 (new), `docs/evidence/acceptance/20261011-j-scope.md` 97 (new), `tests/CasualtiesUnknownOnline.Tests/Tooling/DriverToolTests.cs` 608 file lines for a class well inside the 600-line class gate, which the source-shape gate confirms green; no field or state bool was added anywhere (a new read-only probe, one argument on an existing probe, one argument on an existing test invocation), and nothing became dead — the recipe the run wrote is the reusable half and moved into the committed recipes rather than staying in the transcript, and the batch's own one-off probes live in the local artifact area
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
