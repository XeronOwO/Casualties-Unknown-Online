# Delivery Checklist

Every delivery cycle runs through this checklist. The gate
(`tools/check-delivery.ps1`) runs before the cycle's FINAL commit (the
accepted cycle — deployed + runtime-verified, all boxes checked) and refuses
it while any box is unchecked. Intermediate commits (fix → commit → deploy →
acceptance → re-fix loop) run only the code gates. When the final commit
lands, reset the checklist (`check-delivery.ps1 -Reset`) so the next cycle
starts clean.

**Operating rule (user mandate 2026-08-10)**: boxes are checked ONE LINE AT A
TIME with the Edit tool as each step completes — the checkmarks ride their
step's intermediate commit into git history, which is the audit trail
(which commit checked which box). BULK checking (sed / scripts / a single
catch-up pass) is FORBIDDEN: it fabricates the process record and voids the
gate (observed: the cycle was bulk-checked, never committed, then reset —
the user called it out). Only the -Reset switch may touch multiple lines.

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled
      file:line or runtime log) or is explicitly marked unverified
- [ ] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson)
- [ ] Self-check table: mechanism x change x evidence, every cell filled
- [ ] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided
- [ ] Plan approved by the user (before deployment; investigation excepted)
- [ ] Build + dotnet format + check-architecture + check-event-replay pass
- [ ] Deployed (real game dir only — deploy.ps1 hard-rejects sandbox paths)
- [ ] Runtime verification done (post-deploy evidence: logs / acceptance)
- [ ] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round)
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked forbidden box means a step was skipped on
      purpose, which is exactly what the gate exists to catch)
