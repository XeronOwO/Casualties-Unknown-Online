# Delivery checklist

Operating rule: a box is checked only after the item is CONFIRMED in this cycle. Bulk checking is forbidden — a box
that cannot be confirmed stays empty and the cycle stops. Documentation-only cycles fill every box in one pass and
leave the release-cycle deployment/acceptance line and the FORBIDDEN line unchecked.
- [ ] Design comprehension — the heartbeat's schedule is a separate concern from the session's role and presence
- [ ] Native parity — no game content; the schedule is ours
- [ ] Native parity probe — the unit facts read the schedule (first ping one interval in, then each interval)
- [ ] Entry mapping — one caller, `SessionService.Update`
- [ ] Mechanism inventory — the warmup call that sat beside the ping block is restored; without it four warmup facts failed
- [ ] Whole-family audit — no other service schedules pings
- [ ] Self-check table — two src files, one test file, three docs
- [ ] Verification design — gates 834/834, unit 6660/0, format ran
- [ ] Plan approved by the user — the goal's main line
- [ ] Structure review done — `SessionService` is 594 lines and the debt entry is deleted
- [ ] Build + dotnet format + dotnet test normative gates pass

- [ ] Release-cycle deployment/acceptance: the live row (role-clearing and the pre-run join) reads after the deploy

- [ ] FORBIDDEN — never check this box; checking it fails the delivery gate
      (a honey-pot: a checked box means a step was skipped on purpose, which is
      exactly what the gate exists to catch)
