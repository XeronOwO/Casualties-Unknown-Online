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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this cycle moves no gesture and no behaviour; it corrects what a DIAGNOSTIC says about a path that already ran. The mapping it does owe is the one it fixes: the line sits on the break path (`NetMsg.BlockDamaged`'s handling) and now names the verdict the judgement reached, so a reader is pointed at the branch that actually ran instead of at the one the old headline guessed — no fallback wording stands in for a shape the branch cannot establish, which is why the damage-only arm now says what it can actually know (the report carried no drop payload)
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: six mechanisms, each read before it changed. (1) `BlockBreakSync.OnRemoteDamageBrokeBlock` — the line, its doc and its two call sites; (2) `BlockBreakArbitration.TryAccept` — where `Fresh` (the `_recentBroken` record an applied air write creates), `Repeat` (the cell's `_accepted` record) and `LostAirWrite` (verified generation, standing cell, no air-write record) come from, which is what makes each new sentence checkable; (3) the damage-only branch — reached by a plain hit AND by a break whose roll was empty, because `BlockReportChannel.BuildBlockDamaged` sends `Drops = drops is { Count: > 0 } ? [.. drops] : null`, so the two are indistinguishable on the wire; (4) `AcceptBreak`'s own verdict wording, which had the same defect latent in its catch-all; (5) `WorldBuildingEntitySync.MarkSupportLossRemote`'s two lines — reached by seven call sites, several with no air write, and logged on guests too while the method's doc says a guest never judges; (6) `WorldStateMessageService.ReportBlockState`'s 65,536-cell cap, which the unchanged tail's "recorded" does not account for. Marked UNVERIFIED: the new sentence on the machine — no run has seen it
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the family is "a log line that names one shape for a method that runs in several", and the sweep found three more members, all fixed in this commit rather than filed. (1) `AcceptBreak`'s `_ => "re-accepted (repeat report)"` — an explicit `Repeat` arm now answers for it and the catch-all is neutral; (2) `[BuildingSupport] marked remote death … after remote air-write` — true on the two `WorldEventSync` write branches, false on the four that reach it without an air write, so it now points at the caller's own line; (3) `[BuildingSupport] captured … the table a break report for that cell is judged against` — logged on guests too, where the same method's doc says a guest never judges, so it now says whose table it is. One member is deliberately NOT fixed and is named instead: the shared tail's "the block-state difference was recorded" is false when the 65,536-cell table is full (`ReportBlockState` returns without writing), which needs that call to answer whether it recorded — a Runtime change, not a wording one
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: this cycle's cells are verdict x sentence x what the machine already recorded, and the review proved the middle column from `TryAccept` and the route. `Fresh` (the air write IS what accepted the report) — the cited window's own host lines: the air write applied at 20:21:59.317 and the drops-carrying report accepted at .362, while the old line said "without an air-write report" at .364, directly under the applier's accurate Debug line; `Repeat` — the same pair repeated at 20:22:03.749/.751 in `c16`, so the false headline reproduced there too; `LostAirWrite` — keeps the sentence that was true for it; the damage-only arm — re-worded after the review showed the branch cannot tell a plain hit from an empty-roll break. No automated test can reach this path (the method is private, its class is Unity-bound and the test project loads the adapter reflectively), so the red is the recorded run window and the fix's own reading is the next batch — which is why the ticket is in `review/` and not `done/`
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: a two-client run whose MEMBER breaks a block with a drop payload, judging the host's own `[BlockBreak]` window: the new sentence must appear beside the applier's Debug line and must not contradict it, which is exactly the pair the red window shows failing. The batch scripts already filter on the `[BlockBreak]`/`[BuildingSupport]` prefixes, so the same capture shape works unchanged. What this design cannot do is pin the wording automatically — the review asked whether a source-text gate would be better and the answer is no (a gate that greps for a sentence teaches the next reader to change the sentence and the gate together); the wording is judged by the run, and the ticket says so
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the ticket is the user's own acceptance batch's finding (batch `20261016-c` run 2, read while judging the S2b captured-rows half), and its "fix direction" paragraph already names both acceptable shapes: carry the verdict into the message, or log the lost-air-write sentence only on that verdict. This cycle takes the first, which the ticket lists first, and extends it to the sibling lines the same rule demands. No design question was open — the judgement was in hand at the call site all along
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `BlockBreakSync` 580 → **594 lines**, six under the hard gate, and the ticket now says the next change that is not a wording tweak splits it first rather than tripping `SourceShapeGateTests`. No state, no bool, no new member was added: the parameter is the judgement the caller already held, and the switch selects interned literals. Dead wording deleted in the same round, in four places rather than one: the false headline, the doc's parenthetical that taught the same reading, the sibling support-loss line that named one write from seven call sites, and `AcceptBreak`'s catch-all that answered for `Repeat` in the same breath as everything else
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors after the last edit; `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist"` green at **5484/5484** behaviour cases and **661/661** gates; the ticket move then made both `DocumentCensusGateTests` rows red (a document may not change folder without its census row) and the run also caught the index row and the ticket's own census path — all repointed before this line's check. The independent adversarial review ran against the FROZEN tree BEFORE this line: **2 major, 4 minor and 4 nits, all fixed here** — the two majors were that the method's own doc still taught the old reading forty lines above the fixed line, and that the damage-only arm asserted a shape its branch cannot establish (a drops-free report is a plain hit OR a break whose roll was empty, `BlockReportChannel` sends both the same way); the minors were the ticket not being moved, the three sibling lines, and the unchanged tail's "recorded" against the 65,536-cell cap
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
