# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.

- [ ] Design comprehension — before the first code edit the ticket states, in its own section, the surface being changed
- [ ] Native parity — the three lines (player's action and expectation / the native chain the game itself runs / how the clone reproduces it) — `docs/development/native-parity.md`
- [ ] Native parity probe — for a behaviour that mirrors a native action, the SAME action was run locally and across clients
- [ ] Entry mapping (a scope that crosses players or mirrors a native action only): the entry is every control of the CUO window
- [ ] Mechanism inventory: every touched mechanism has evidence (decompiled file:line or runtime log) or is explicitly marked unverified
- [ ] Whole-family audit: fixing one mechanism, the whole family was aligned one by one (no piecemeal fixes — the turret-fire/geyser lesson)
- [ ] Self-check table: mechanism x change x evidence, every cell filled
- [ ] Verification design: how the runtime proves it (diagnostic traces, peer log comparison, hotrepl assertions) is decided
- [ ] Plan approved by the user (before deployment; investigation excepted)
- [ ] Structure review done (touched classes <= 600 lines OR the size is recorded in docs/architecture-debt.json with its split plan; state bools; dead mechanisms deleted in the same round)
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: run by the agent after the commit
      (build → deploy → two-client acceptance per `docs/acceptance/`), outside the
      development commit gate; simulation/static evidence is the feature development
      verification standard.
- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
