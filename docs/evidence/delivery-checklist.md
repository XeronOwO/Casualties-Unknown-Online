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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: the ticket's mapping table — the four callers of the wear family (`PlayerCamera.cs:1640-1643`, `Body.cs:500-528`, `SaveSystem.cs:338`, CUO's own replay) and the three query sites (`PlayerCamera.cs:1698`, the drop report, `Body.cs:1358`); a limb name the body does not carry is REFUSED and logged, never repaired to a nearest limb
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the self-check §1 — thirteen rows, each with its file, its `reversing/` citation where the game decides, and the case that pins it
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the self-check §2 — the `LimbByName` census that bounds the family at four `Body` sites, the four callers, the multi-worn-sprite sibling (filtered by CUO's own prefix against the live body), the four-closure declaration family and the shape the sibling ruling set, the seven wearable fields all mapped or named out of scope, the three readers of the placement rule, and the limb-name vocabulary reused
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's own table (seven rows) and the self-check §1–§5, every cell filled; §5's ten limits name the native half, the red's reproducibility, the guards' superset semantics, the exact-name comparison, the log-only refusal and the class that crossed the local 550-line measure
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the self-check §4 — the red was observed on the frozen tree first (1 failed / 0 passed, `Assert.False() Failure / Expected: False / Actual: True`), what L0 pins is the constructed `ItemInfo` and the rule the guard asks, and the native half (the guard really stopping a bad wear, the garment landing on the right limb) is named as an acceptance row because it needs a live `Body`
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the handoff instruction named this cycle's ticket; the field list is the ceiling ticket's own Part 3.A entry under the user's 2026-10-07 promotion directive, the non-fatal shape of an unbackable declaration is the user's 2026-10-08 ruling recorded in `review/mod-declared-behaviour-with-no-function.md`, and the contract shape (a behaviour slice replacing the flag) is the family's existing convention — no new user-visible behaviour was introduced beyond fixing the promised one
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build 0 warnings / 0 errors; format exit 0 (every changed file measured byte-wise for CRLF); behaviour 4913/4913 (was 4900); focused 964/964; gates 574/574; the independent review's three majors (the three unguarded `LimbByName` query sites, the postfix reporting a refused wear, the rule with no failing case) and every minor were fixed in this commit
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: new types 28–54 lines, the new patch file 84; largest touched class `GameAdapterBridge` 553 (the reviewed aggregate that forwards every patch port, one line per member — recorded in the self-check §5 because it crossed the local 550 measure); no new state (the guards are stateless, the port carries one method, the wear prefix's `__state` replaces a bool with a three-value outcome); the `bool Wearable` member is replaced rather than kept beside the slice
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
