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

- [x] Entry mapping (a scope that crosses players or mirrors a native action only): every CUO entry names the native CALL SITE it mirrors; an action needing information its gesture cannot carry is refused or answered by the user, never given a fallback; the entries stay isolated instead of being arbitrated by order; and every user-visible behaviour change in the list is user-approved rather than recorded as a limit — evidence: no entry and no native counterpart exist here: the cycle retypes the mod-visible contract (messages, packets, status values) and changes no player-facing gesture, so there is no behaviour to approve
- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: the self-check's table — the model, the encoder, the tunnel, declared packets, the status frame, the status value, the two projections, the retired payload path, the null-collection census, the registry and docs, each with its case
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: every surface a mod's own bytes crossed moved together — the anonymous tunnel, the declared packets, CUO's own status frame, the status value, the two projections, the example mods, the baseline, the terminology registry and both document blocks; no surface keeps a second spelling
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the self-check — §1 mechanism x change x evidence, §2 the family audit, §5 the limits (no game run, stage B open, the wire is CUO's own)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: the encode/decode path under test IS the production one (`ModValueCodecTests` 20 cases, `ModValueTests` 12, the whole mod family 460 through the real composition root); the rail's inclusive end and a full-size frame that is not a value are both cases; a live session is the acceptance batch's row, and the refusals are observable log lines naming the path
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user's 2026-10-08 ruling is the ticket's source; the handoff orders this ticket first and states that the concrete data-model shape is the agent's to fix and record in the ticket rather than re-ask, which is the ticket's *Frozen shape (2026-10-09)* section, including the two-stage split
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: build clean (0 warnings, 0 errors); `dotnet format CasualtiesUnknownOnline.slnx` exit 0; behaviour 4848/4848 (net48); gates 573/573 once this box was filled (572/573 mid-cycle, the only red being this checklist's own check)
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: the biggest added class is `ModValueCodec` (591 lines including its docs; the architecture gate's limit is 600, so the file is at 98% of it and a split is the next structural move if it grows) and `ModValue` (413, stateless or immutable and holding no mutable state); the payload path is DELETED rather than bypassed (`ModPayloadCodec`, the twelve `FromPayload` bodies it served, `[DataContract]` on the three travelling types, `ModPacketPolicy.IsValidPayload`, and every defensive-copy site on the surfaces that moved)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
