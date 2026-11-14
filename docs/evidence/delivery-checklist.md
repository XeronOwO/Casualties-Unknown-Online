# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the deployed split keeps the ping outside the world's lifetime
- [ ] Native parity — no game content; the schedule is ours
- [ ] Native parity probe — `role-clearing` reads the ghost-free end and the rejoin on the deployed build
- [ ] Entry mapping — one caller, `SessionService.Update`
- [ ] Mechanism inventory — the heartbeat crossed the split with the same cadence (the unit facts pin it)
- [ ] Whole-family audit — the warmup and presence calls stayed in the service
- [ ] Self-check table — src, tests, docs and the ticket
- [ ] Verification design — build 0 errors, unit 6660/0, gates 834/834, format ran, deploy verified, the live row read
- [ ] Plan approved by the user — the goal's main line
- [ ] Structure review done — the service is 594 lines and the debt entry is deleted
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: deployed `0.1.0+f1981303e`, verify "Deployment matches this tree's build output", `role-clearing` PASS

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
