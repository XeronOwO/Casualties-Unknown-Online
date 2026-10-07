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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: self-check §1, fourteen rows — the per-client native panel and its two scene-wired entries, the host-captured layer baseline, why a member's own regeneration has nothing new to apply (`EnsureGuestApplied` is idempotent per params instance), the existing generation identity, the transition a member actually takes, the native entry's fourth (local-body) clause, the family's second producer (the drill pod) and the panel's other entry (`SaveAndExit`), the port pattern, the large-service rule that put this domain on its own interface, the three member-side gates the review's blocker named, and the sink's own producer census
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: self-check §1 rows 9-12 — the sink family was enumerated by grep over the decompiled tree: `RegenerateWorld` has exactly three callers (the panel's `ContinueRun`, the drill pod's name-based call, and the console's `skiplayer`), so the pod's own semantics (a two-layer step plus its `doPod` arrival effects) and the panel's other entry (`SaveAndExit` writing the native save) were each read and FILED with their evidence instead of being silently reduced to the panel's shape; the two sibling tickets carry the readings that are still missing
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: `docs/evidence/selfchecks/world/layer-complete-choice-for-members-selfcheck.md` §3, seventeen rows — the Runtime arbitration (five cases), the adapter's own coordinator through its own constructor (the delegation rule and the request on the surface), the two pure decisions, the sink's and the marker's shape, the wire round-trip, the direction row, the port census on source and on the compiled artifact, the sync-coverage anchors, the doc-pair hashes, the mutation controls, and the architecture gates; §1 carries fourteen mechanism rows including the review's blocker (the three member-side gates that key on the member's own state)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: self-check §3 row 16 and §4 — the machine half is the simulated host + two members (the new request's admission and refusals), the adapter's own coordinator built reflectively (the delegation rule), and four mutation controls whose reds were observed and whose files were restored byte-identically (SHA-256 re-checked); the runtime half is the ticket's row on three clients with a dead host at the boundary (not run here, §4 item 1), and every refusal the host can produce is logged with the sender and BOTH generations (`[LayerChoice]`), which is what that batch reads
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff names `layer-complete-choice-for-members` as the development-side next work item (after the previous cycle's commit), the ticket's own `Source` line is the user's 2026-10-07 request, and its authority answer was written into the ticket before implementation (its *Design* section, written in this cycle's first edit); no user question was asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet build CasualtiesUnknownOnline.slnx` clean (0 warnings, 0 errors); `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet test CasualtiesUnknownOnline.slnx` (with a build, the checklist's own gate included) = gate project 451/451 and behaviour 4699/4699, 0 failures
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: physical lines — `LayerAdvanceRequestChannel` 101, `ILayerAdvanceControl` 30, `LayerAdvanceCoordinator` 140, `LayerAdvancePolicy` 47, `LayerAdvanceDecision` 17, the two patch files 27 + 54, `HandlerContext` 79, `GameAdapter` 588, test files 115 / 145 / 86 — every file under the 600-line gate, and `WorldService` (545), `WorldStateMessageService` (538) and `IWorldControl` (565) are back to their pre-cycle contents because the domain got its own control interface instead of three forwarders; one new state field (`_progressionGrantedAtLayer`, an int latch, no new bool) and the marker patch's `InContinueRun` (the `WorldGenerationUpdatePatch.InUpdate` pattern); no dead mechanism is left (the first draft's void-prefix seam and its `OnLocalLayerChoice` member were REPLACED by the sink, not kept beside it); the independent adversarial review (fresh context, frozen tree) reported 1 blocker, 2 major, 4 minor and 4 nits — ALL fixed in this round: the blocker was mechanism-level (leaving the member's own regeneration in place left the member in a world of its own, because the pull, the world join and the join follow-up all key on the member's own state — the local descent is now suppressed at the sink, `WorldGenerationRegenerateWorldPatch`); the majors were the "a lost request costs only a click" claim (now true, and the divergence it named is gone) and two doc cells asserting `savePanel` appears nowhere under `src/` (it appears in the host drive; both reworded); the minors were the missing id-141 row in both protocol-message blocks (added, pair hashes re-recorded), the request firing before the native guard (the sink now runs only after the entry's guard passed), this checklist row carrying the previous cycle's evidence, and a duplicated `## Non-goals` in the ticket; the nits were the "no compile-time coupling" wording, the MANIFEST's "two pure-decision suites", a doubled blank line and the unlogged member-side no-op branch (now a Debug line). The reviewer's FIX-ROUND re-verification (same fresh context, tree frozen again) confirmed the blocker fixed in code and reported seven further minor/nit items, ALL fixed in the same round: the sink's load-bearing line now has a named pure decision with four behavioural rows (`LayerAdvancePolicy.ShouldSuppressLocalDescent`, exercised by `ShouldSuppressLocalDescent_NeedsBothThePanelEntryAndAnAcceptedDelegation`); the marker is consumed (read-and-clear) at the sink so a throwing entry body cannot leak it into the pod's or the console's next descent; a choice that cannot be reported keeps the game's own descent (`ILayerAdvanceControl.TrySendLayerAdvanceRequest` answers false, the coordinator logs and stands down, pinned by `TryDelegateLocalAdvance_WhenNothingWasReported_KeepsTheGameDescend` and `AChoiceWithNothingToStamp_IsNotSent`); four stale numbers and one stale method name in these records were corrected; the doubled blank line in the matrix was actually removed; one proof cell was re-pointed at the test that really covers it; and the stale-stamp member's dead end is now named in the ticket's and self-check's limits for the runtime row to read
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
