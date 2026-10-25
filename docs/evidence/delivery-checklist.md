# Delivery Checklist

Every development cycle runs through this checklist. It is validated by `tools/check-delivery.ps1`, run before the cycle's
commit (2026-10-25: a script, not a test — a test cannot reset the boxes, and a file left green certifies the next cycle
without a record). The script refuses a commit while a required box is unchecked or carries no evidence, archives the
cycle's record under `docs/evidence/checklists/`, and resets every box so the next cycle starts clean. One test stays, the
census: `DeliveryChecklistGateTests.DeliveryChecklist_DeclaresEveryRequiredBox` refuses a checklist that no longer declares
one of the steps below, naming the step that went missing, because a record whose boxes are gone must not pass by having
nothing left to fail on. Deployment and multiplayer acceptance are the agent's actions, run after the commit per
`docs/acceptance/` and outside this script; feature development verification uses simulation/static evidence. The entry
mapping box answers the standard in `../development/native-parity.md`.

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
cycle has none). The focused normative-gate run afterwards is NOT optional: it is the only thing that
proves this checklist complete before the commit.

**Evidence rule (added 2026-09-17)**: a checked box carries a short evidence suffix on the
SAME line — `- [ ] <item> — evidence: <command/file/result>` — because a bare checkmark
records that someone decided the step was done, not what proved it. Keep it to one clause
(a command, a file, or a measured result); the full detail belongs in the cycle's ticket or
evidence file. The suffix goes at the END of the box's own text: the reset cuts from the last
` — evidence:` it finds, so a suffix placed mid-sentence would take the rest of the statement with it.

**Input rule (added 2026-10-22)**: the Entry mapping box also records the exact input sequence the
player performs for a new or changed player-facing gesture — which mouse button is held, which window is
open, what can still be clicked or pressed while it is held — and walks it through the game's own control
flow. A gesture the controls cannot perform is a design defect, not evidence; a call seam a probe can
drive is not evidence that a player can reach it. An "impossible" verdict lists EVERY entrance that opens
the window the gesture needs and the button each one takes (a conflict on one entrance is not the control
scheme), and an entrance behind a hotkey the player cannot be expected to know is not the primary entry:
the gesture opens what it needs from the act itself.

**Cycle-reset rule (added 2026-10-25, the user's finding)**: this file is the CURRENT cycle's record, and
a cycle that never resets it inherits the previous cycle's green boxes — the script only asks whether a
required box is unchecked, so a stale fully-ticked checklist certifies every later commit without a
record of its own. The defect was found on the machine: a whole session of commits ran against the
2026-10-24 drink cycle's eleven checked boxes, and nothing in the tree said so. So: **the first action of
a cycle, before its first commit, is the deliberate reset of every box to `[ ]`** (the one multi-line
edit the operating rule allows, and the reset clears the evidence with the checkmark), and every box then
carries evidence naming THIS cycle — a commit, a path under `.acceptance/`, or a measured reading produced
by it. A box whose evidence names another cycle is not filled.

- [ ] Design comprehension — before the first code edit the ticket states, in its own section, the surface being changed
      (`docs/architecture/surfaces.md` row), what that row says the behaviour is judged against, the record quoted, and what the
      change must NOT alter; a `GAP` row is closed by reading and recording the authority first, never by guessing
      — evidence: the structure ticket's records were read first (audit row 5, `done/architecture-split-pass.md`, and the debt file itself)
- [ ] Native parity — the three lines (player's action and expectation / the native chain the game itself runs / how the
      remote case reaches that same chain, or the named reason it cannot) are written in the ticket BEFORE the first code
      edit, and no forbidden shape is used to fit a mechanism
      — evidence: no native call changed; this cycle is a records correction
- [ ] Native parity probe — for a behaviour that mirrors a native action, the SAME action was run locally and across
      players in one session, and the two effect lists (native call, frame, audio, both clients' state) were compared,
      with every difference either fixed or named as a user decision
      — evidence: no behaviour touched; the cycle corrects a rule's wording and closes a ticket on the record that already answered it
- [ ] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit
      — evidence: no gesture involved
- [ ] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified
      — evidence: the sizes are quoted from the debt file (`RunCoordinator` with its three-extraction note) and from the source (823 lines now)
- [ ] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson)
      — evidence: the checklist box, audit row 5, the ticket index and the census moved together — what every gate that failed on the way checked
- [ ] Self-check table: mechanism x change x evidence, every cell filled
      — evidence: row 1 = the exemption is on record (debt entry + checklist wording); row 2 = no code touched, suites unchanged 802/6519
- [ ] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided
      — evidence: the gates that police the records: `BacklogIntegrityGateTests`, `BacklogReferenceGateTests`, `DocumentCensusGateTests`
- [ ] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation
      — evidence: the goal names the structure tickets as work to advance, and this one closes on its own written criterion
- [ ] Structure review done (touched classes <= 600 lines OR the size is recorded in `docs/architecture-debt.json` with its split plan; state bools; dead mechanisms deleted in the same round)
      — evidence: no class touched; this box's own wording is what got corrected, so it now names the debt-file exemption this cycle relied on
- [ ] Build + dotnet format + dotnet test normative gates pass
      — evidence: `.acceptance/l598-full.log` 802/802 gates and 6519/6519 behaviour green; `dotnet format` not needed for a documentation-only cycle
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
