# Delivery Checklist

Every delivery cycle runs through this checklist. The gate
(`tools/check-delivery.ps1`) refuses the commit while any box is unchecked;
when the cycle completes (deployed + runtime-verified), reset it
(`check-delivery.ps1 -Reset`) so the next cycle starts clean.

- [ ] Mechanism inventory: every touched mechanism has evidence (decompiled
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
