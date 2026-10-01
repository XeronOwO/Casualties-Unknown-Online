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

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified — evidence: Run E's own log/ticket plus the code reads in the ticket's What happened: `SteamTransport.SendTo` printed one full diagnostics line per attempt with no backoff and no gate, `SteamSendFailureClassifier` mapped `k_EResultLimitExceeded` to `Other`, `PacketSender` recorded the failures into the traffic window, no session watchdog existed
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson) — evidence: the congestion family is covered in both directions (the host drops the stalled member and keeps playing; the guest ends its own session on a stall towards the host, or on 15 s of host silence), non-congestion failure kinds stay un-gated by design (a send is how a broken session is re-driven), and the IP-direct transport was audited and recorded out of scope (its write blocks and a failure closes the peer, so the flood shape is Steam-only)
- [x] Self-check table: mechanism x change x evidence, every cell filled — evidence: the ticket's acceptance matrix (rows 1–7, frozen before implementation) plus the independent adversarial review's claim-by-claim report (`.acceptance/batch-r-review-report.md`: all 7 claims verified at the mechanism level, the 4 named falsifications attempted and failed, 1 evidence gap and 6 minor findings fixed or recorded)
- [x] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided — evidence: rows 1–3 are pinned by a deterministic unit drive (pre-fix red: 10 000 attempts → 10 000 channel calls and one log line each; post-fix green: ≤20 calls, ≤20 lines, native session/relay queries ≤2), and rows 4–7 by a live two-client run whose refusal window is produced by freezing the guest's main thread through the HotRepl eval surface (`.acceptance/batch-r/guest-freeze.cs`) with an `explode` burst against the resulting queue (`.acceptance/batch-r/host-burst.cs`); the host log size and eval latency are the per-step card points
- [x] Plan approved by the user (before deployment; investigation excepted) — a ticket whose design the user already froze counts as approved (a backlog decision, a recorded decision entry, a handoff instruction); re-asking a work-item choice is itself a process violation — evidence: the user settled the two user-visible forks before implementation (a bounded stall drops that guest and the host keeps playing; the guest ends its own session on a stall or on host silence), and the ticket's acceptance matrix was frozen before any code was written; no work-item choice was re-asked
- [x] Build + dotnet format + dotnet test normative gates pass — evidence: `dotnet format CasualtiesUnknownOnline.slnx` exit 0; `dotnet build CasualtiesUnknownOnline.slnx` 0 warnings / 0 errors; with the checklist itself still unchecked, the evidence run was `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"` → gates 299/299 and main suite 4573/4573 (build included), then the gate project re-run alone to confirm all 300 with the boxes checked
- [x] Structure review done (touched classes <= 600 lines, state bools, dead mechanisms deleted in the same round) — evidence: `PeerSendRefusalPolicy` 310 lines, `SteamTransport` 222, `PeerSendStallWatchdog` 130, `GuestHostSilenceWatchdog` 91, `SteamSendChannel` 50 — all far below the 600-line gate; one new state bool (`_armed`, the guest watchdog's arm flag); dead mechanisms handled in the same round (`IsCongested` deleted as test-only, the never-called `Reset` now wired through `ISessionReset.ResetSessionState` on the session edge)
- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
