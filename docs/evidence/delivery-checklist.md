# Delivery Checklist

Every development cycle runs through this checklist. The gate
(`tools/check-delivery.ps1`) runs before the cycle's final commit and refuses
it while any box is unchecked. Deployment and manual multiplayer acceptance are
user release actions outside this gate; feature development verification uses
simulation/static evidence. When a release cycle lands, reset the checklist
(`check-delivery.ps1 -Reset`) so the next cycle starts clean.

**Operating rule (user mandates 2026-08-10 / 2026-08-16)**: boxes are checked ONE LINE AT A
TIME with the Edit tool as each step completes. The checkbox edits do NOT get
their own commit per checkbox — fold the checklist changes into the normal
work commits (implementation/docs/verification steps). The process record is
the line-by-line Edit sequence, not one commit per box. BULK checking (sed / scripts / a single
catch-up pass) is FORBIDDEN: it fabricates the process record and voids the
gate (observed: the cycle was bulk-checked, never committed, then reset —
the user called it out). Only the -Reset switch may touch multiple lines.

- [x] Mechanism inventory: every touched mechanism has evidence (decompiled
      file:line or runtime log) or is explicitly marked unverified
- [x] Whole-family audit: fixing one mechanism, the whole family was aligned
      one by one (no piecemeal fixes — the turret-fire/geyser lesson)
- [x] Self-check table: mechanism x change x evidence, every cell filled
- [x] Verification design: how the runtime proves it (diagnostic traces,
      peer log comparison, hotrepl assertions) is decided
- [x] Plan approved by the user (before deployment; investigation excepted)
- [x] Build + dotnet format + check-architecture + check-event-replay pass
- [x] Structure review done (touched classes <= 600 lines, state bools,
      dead mechanisms deleted in the same round)
- [ ] Release-cycle deployment/acceptance: performed by the user outside the
      development commit gate; simulation/static evidence is the feature
      development verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked forbidden box means a step was skipped on
      purpose, which is exactly what the gate exists to catch)
