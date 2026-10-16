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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: this commit adds a run record, not an entry. The run exercised the entry the way an operator does — a console LINE, through the real console — and every answer quoted in the record is the console's own text, including the refusal that names which properties the verb accepts rather than silently doing something else with a bounded value
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: what this commit records is runtime log evidence for three mechanisms that previously had only reading: the multi-property commit (row 2 and 3 of the record: both properties land and the row follows), the atomicity the single commit buys (rows 4 and 5: the later property is impossible and the earlier one is NOT in the record afterwards — a commit inside the loop would have left it), and the refusal vocabulary (the remove line, quoting remove's own grammar). Explicitly marked UNVERIFIED in the record and left in the ticket: every member-side behaviour, because the member's Steam transport never initialised and the member never entered the world
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the run covered the family as far as the environment allowed, in the order the verbs are reached: read before, the multi-property write, read after, the refused write, read after THAT, and the bounded-property removal. The member half of the same family is named as owed rather than quietly omitted, with the cause attributed to the environment (`steamInit: false`) and the fix proposed where it belongs — a preflight row that reads the member's own transport before the lobby is created, so the next run refuses by name instead of proceeding into a join that does not settle
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the record IS the table this time, one row per command with the console's own answer beside it and a verdict per row. Judged: `/data get` before, `/data merge` (two properties, both landed), `/data get` after (the row followed), `/data merge` refused as a whole, `/data get` after the refusal (nothing changed), `/data remove condition` (refused by name, remove grammar). Not judged, with the reason recorded: every member-side row. The artifact identity the run read (`+b1cc399c`) is the tree's HEAD, checked before the clients started rather than after
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the design was a two-client batch reading the CONSOLE'S OWN answers back — `console` returns the command's `lastLine`, so no verdict here is inferred from a row changing; the item under test was created through the game's own calls (`item-provide mode=create`) and its instance id came from that call rather than from a guess. The member half needs one more thing before it can run: the sandbox's Steam initialised. The record says so, and names the preflight row that would make it a refusal instead of a silent non-settlement
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: acceptance is agent-run in this repository and the user's standing order is that a run happens when the machine is free rather than when asked for. The batch needed no new permission: it starts the clients itself, refuses if a game is already running, and closes what it started. The rows it judges are the ticket's own — the verbs the user asked for, on a live session
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: no production file is touched by this commit at all; it adds a run record under the acceptance evidence area, its census row and the ticket's link to it. The record itself passed the document rules the repository enforces: it has a census row naming what reaches it, and the ticket that sends a reader to it was updated in the same change, so it is not a page nothing points at. The process leftovers were closed too: the MSBuild reuse nodes and the two clients the batch started are gone, and the second client was closed by its own quit action rather than killed
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: no `src/`, `tests/` or `tools/` file changed, so by the repository's own rule this documentation-only change skips build, format and the behaviour suite and is reviewed as a diff. The normative gates were still run because the change adds a tracked document: **667/667 green**, which is what proves the census row resolves, the ticket's inbound link is real and the record carries no machine fingerprint. The deployed artifact the run verified against (`+b1cc399c`) was built and deployed from a clean tree BEFORE the batch started, which is the order the workflow asks for
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
